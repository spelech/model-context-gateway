using System.Collections.Concurrent;
using System.Text.Json;

namespace ModelContextGateway.Core.Routing
{
    /// <summary>
    /// Represents an active client session and handles routing.
    /// </summary>
    public partial class ClientSession
    {
        private readonly string _sessionId;
        private HttpResponse? _clientResponse;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly List<McpServer> _servers;
        private readonly ConcurrentDictionary<string, BackendConnection> _backendConnections = new();
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly IEmbeddingService _embeddingService;
        private readonly IServiceProvider? _rootServices;

        private readonly Core.Routing.ToolRoutingManager _toolRoutingManager = new();
        private readonly Core.Routing.ResourceRoutingManager _resourceRoutingManager = new();
        private readonly Core.Routing.PromptRoutingManager _promptRoutingManager = new();

        public bool IsMetaMode { get; set; } = false;
        private Task? _initializeTask = null;
        public readonly object _initLock = new();
        private readonly CancellationTokenSource _cts = new();
        private string _lastInitializeRequest = string.Empty;
        private readonly List<Task> _backendInitTasks = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeRequestCancellationTokens = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonRpcResponse>> _clientPendingRequests = new();

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            Converters = { new JsonRpcMessageConverter() }
        };

        private readonly SessionManager? _sessionManager;

        public ClientSession(string sessionId, HttpResponse? clientResponse, List<McpServer> servers, HttpClient httpClient, IEmbeddingService embeddingService, SessionManager? sessionManager, Microsoft.Extensions.Logging.ILogger logger, IServiceProvider? rootServices = null)
        {
            _sessionId = sessionId;
            _clientResponse = clientResponse;
            _servers = servers;
            _httpClient = httpClient;
            _embeddingService = embeddingService;
            _sessionManager = sessionManager;
            _logger = logger;
            _rootServices = rootServices;
        }

        public ClientSession(string sessionId, HttpResponse? clientResponse, List<McpServer> servers, HttpClient httpClient, IEmbeddingService embeddingService, Microsoft.Extensions.Logging.ILogger logger, IServiceProvider? rootServices = null)
            : this(sessionId, clientResponse, servers, httpClient, embeddingService, sessionManager: null, logger, rootServices)
        {
        }

        public HttpResponse? GetClientResponse() => _clientResponse;

        public void UpdateClientResponse(HttpResponse? clientResponse)
        {
            _clientResponse = clientResponse;
        }

        public void DecoupleClientResponse()
        {
            _clientResponse = null;
        }


        public SessionManager? GetSessionManager() => _sessionManager;

        public async Task<List<object>> ListToolsAsync(string body, HttpContext? httpContext = null)
        {
            var tools = await _toolRoutingManager.ListToolsAsync(body, IsMetaMode, _backendConnections, _logger, EnsureBackendsInitializedAsync, _servers, _sessionManager);
            return await FilterAuthorizedAsync(tools, "tools/list", "name", httpContext);
        }

        public async Task<object?> CallToolAsync(string toolName, string body, IDbConnectionFactory dbFactory, HttpContext? httpContext = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int statusCode = 200;
            string? errorMessage = null;
            string? responsePayload = null;

            try
            {
                // Namespace validation
                var activeServerIds = _servers.Where(s => s.Enabled).Select(s => s.Id).ToList();
                if (!SecurityValidationHelper.ValidateToolOrPromptName(toolName, activeServerIds))
                {
                    statusCode = 403;
                    errorMessage = $"Security Error: Invalid or spoofed namespaced identifier: '{toolName}'.";
                    var errResult = new
                    {
                        isError = true,
                        content = new[] {
                            new {
                                type = "text",
                                text = errorMessage
                            }
                        }
                    };
                    responsePayload = JsonSerializer.Serialize(errResult);
                    return errResult;
                }

                // RBAC Check — use the live per-request HttpContext when provided
                var isAuth = await IsUserAuthorizedAsync("tools/call", toolName, httpContext);
                if (!isAuth)
                {
                    var identity = await ResolveUserIdentityAsync(httpContext);
                    statusCode = 403;
                    errorMessage = $"Security Error: User '{identity.Username}' does not have permission to execute tool '{toolName}'.";
                    var errResult = new
                    {
                        isError = true,
                        content = new[] {
                            new {
                                type = "text",
                                text = errorMessage
                            }
                        }
                    };
                    responsePayload = JsonSerializer.Serialize(errResult);
                    return errResult;
                }

                if (toolName == "execute_tool")
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("params", out var paramsProp) &&
                            paramsProp.TryGetProperty("arguments", out var argsProp) &&
                            argsProp.TryGetProperty("name", out var targetNameProp))
                        {
                            var targetName = targetNameProp.GetString();
                            if (!string.IsNullOrEmpty(targetName))
                            {
                                var (normalizedTargetName, _) = _toolRoutingManager.NormalizeTargetToolName(targetName, _servers, _logger);
                                targetName = normalizedTargetName;
                                var isTargetAuth = await IsUserAuthorizedAsync("tools/call", targetName, httpContext);
                                if (!isTargetAuth)
                                {
                                    var identity = await ResolveUserIdentityAsync(httpContext);
                                    statusCode = 403;
                                    errorMessage = $"Security Error: User '{identity.Username}' does not have permission to execute tool '{targetName}'.";
                                    var errResult = new
                                    {
                                        isError = true,
                                        content = new[] {
                                            new {
                                                type = "text",
                                                text = errorMessage
                                            }
                                        }
                                    };
                                    responsePayload = JsonSerializer.Serialize(errResult);
                                    return errResult;
                                }
                            }
                        }
                    }
                    catch (Exception exExecAuth)
                    {
                        _logger.LogDebug(exExecAuth, "Failed to parse inner target tool name from execute_tool body");
                    }
                }

                var contextToUse = httpContext ?? _clientResponse?.HttpContext;
                var abortToken = contextToUse?.RequestAborted ?? CancellationToken.None;
                string? cancellationKey = null;
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(abortToken, _cts.Token))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("id", out var idProp))
                        {
                            var rawId = idProp.ValueKind switch
                            {
                                JsonValueKind.String => idProp.GetString(),
                                JsonValueKind.Number => idProp.GetRawText(),
                                JsonValueKind.Null => null,
                                _ => idProp.GetRawText()
                            };

                            if (rawId != null)
                            {
                                var scopeId = _sessionId;
                                if (_sessionId == "global-stateless-session" && httpContext != null)
                                {
                                    scopeId = $"{_sessionId}:{httpContext.TraceIdentifier}";
                                }
                                cancellationKey = $"{scopeId}:{rawId}";

                                if (!_activeRequestCancellationTokens.TryAdd(cancellationKey, cts))
                                {
                                    throw new InvalidOperationException($"Duplicate request ID '{rawId}' detected in session '{scopeId}'. Silent overwrite prevented.");
                                }
                            }
                        }
                    }
                    catch (Exception exVal) when (exVal is InvalidOperationException && exVal.Message.Contains("Duplicate request ID"))
                    {
                        throw;
                    }
                    catch { }

                    try
                    {
                        var res = await _toolRoutingManager.CallToolAsync(toolName, body, dbFactory, _backendConnections, _servers, _logger, _httpClient, _embeddingService, EnsureBackendsInitializedAsync, RewriteRequestJson, cts.Token, _sessionManager, _sessionId,
                            filterAuthorizedToolsAsync: async (tools) => await FilterAuthorizedAsync(tools, "tools/list", "name", httpContext));
                        responsePayload = res != null ? JsonSerializer.Serialize(res) : null;
                        return res;
                    }
                    catch (Exception exCall)
                    {
                        statusCode = 500;
                        errorMessage = exCall.Message;
                        throw;
                    }
                    finally
                    {
                        if (cancellationKey != null)
                        {
                            _activeRequestCancellationTokens.TryRemove(cancellationKey, out _);
                        }
                    }
                }
            }
            catch (Exception exOuter)
            {
                if (statusCode == 200)
                {
                    statusCode = 500;
                    errorMessage = exOuter.Message;
                }
                throw;
            }
            finally
            {
                stopwatch.Stop();
                await AuditInvocationAsync("tools/call", toolName, body, statusCode, stopwatch.ElapsedMilliseconds, responsePayload, errorMessage, httpContext);
            }
        }




        public void Close()
        {
            try
            {
                _cts.Cancel();
            }
            catch { }
            foreach (var conn in _backendConnections.Values)
            {
                conn.Dispose();
            }
            _backendConnections.Clear();
        }


        public async Task<List<object>> ListResourcesAsync(string body, HttpContext? httpContext = null)
        {
            var resources = await _resourceRoutingManager.ListResourcesAsync(body, _backendConnections, _logger, EnsureBackendsInitializedAsync, _sessionManager);
            return await FilterAuthorizedAsync(resources, "resources/list", "uri", httpContext);
        }

        public async Task<object?> ReadResourceAsync(string resourceUri, string body, HttpContext? httpContext = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int statusCode = 200;
            string? errorMessage = null;
            string? responsePayload = null;

            try
            {
                // Namespace validation
                var activeServerIds = _servers.Where(s => s.Enabled).Select(s => s.Id).ToList();
                if (!SecurityValidationHelper.ValidateResourceUri(resourceUri, activeServerIds))
                {
                    statusCode = 403;
                    errorMessage = $"Security Error: Invalid or spoofed resource URI namespace: '{resourceUri}'.";
                    throw new UnauthorizedAccessException(errorMessage);
                }

                // RBAC Check — use the live per-request HttpContext when provided
                var isAuth = await IsUserAuthorizedAsync("resources/read", resourceUri, httpContext);
                if (!isAuth)
                {
                    var identity = await ResolveUserIdentityAsync(httpContext);
                    statusCode = 403;
                    errorMessage = $"Security Error: User '{identity.Username}' does not have permission to read resource '{resourceUri}'.";
                    throw new UnauthorizedAccessException(errorMessage);
                }

                var res = await _resourceRoutingManager.ReadResourceAsync(resourceUri, body, _backendConnections, EnsureBackendsInitializedAsync, RewriteRequestJson, _sessionManager);
                responsePayload = res != null ? JsonSerializer.Serialize(res) : null;
                return res;
            }
            catch (Exception ex)
            {
                if (statusCode == 200)
                {
                    statusCode = 500;
                    errorMessage = ex.Message;
                }
                throw;
            }
            finally
            {
                stopwatch.Stop();
                await AuditInvocationAsync("resources/read", resourceUri, body, statusCode, stopwatch.ElapsedMilliseconds, responsePayload, errorMessage, httpContext);
            }
        }

        public async Task<List<object>> ListResourceTemplatesAsync(string body, HttpContext? httpContext = null)
        {
            var templates = await _resourceRoutingManager.ListResourceTemplatesAsync(body, _backendConnections, _logger, EnsureBackendsInitializedAsync, _sessionManager);
            return await FilterAuthorizedAsync(templates, "resources/templates/list", "uriTemplate", httpContext);
        }

        public async Task<object> CompleteAsync(string body, HttpContext? httpContext = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int statusCode = 200;
            string? errorMessage = null;
            string? responsePayload = null;
            string targetItem = "unknown";

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (!root.TryGetProperty("params", out var paramsProp) || !paramsProp.TryGetProperty("ref", out var refProp))
                {
                    statusCode = 403;
                    errorMessage = "Security Error: Missing required completion parameters or reference.";
                    throw new UnauthorizedAccessException(errorMessage);
                }

                if (!refProp.TryGetProperty("type", out var typeProp))
                {
                    statusCode = 403;
                    errorMessage = "Security Error: Missing completion reference type.";
                    throw new UnauthorizedAccessException(errorMessage);
                }

                var refType = typeProp.GetString();
                if (refType == "ref/prompt")
                {
                    if (!refProp.TryGetProperty("name", out var nameProp))
                    {
                        statusCode = 403;
                        errorMessage = "Security Error: Missing prompt name in completion reference.";
                        throw new UnauthorizedAccessException(errorMessage);
                    }

                    var promptName = nameProp.GetString() ?? string.Empty;
                    targetItem = promptName;

                    var activeServerIds = _servers.Where(s => s.Enabled).Select(s => s.Id).ToList();
                    if (!SecurityValidationHelper.ValidateToolOrPromptName(promptName, activeServerIds))
                    {
                        statusCode = 403;
                        errorMessage = $"Security Error: Invalid or unknown prompt identifier: '{promptName}'.";
                        throw new UnauthorizedAccessException(errorMessage);
                    }

                    var isAuth = await IsUserAuthorizedAsync("completion/complete", promptName, httpContext);
                    if (!isAuth)
                    {
                        var identity = await ResolveUserIdentityAsync(httpContext);
                        statusCode = 403;
                        errorMessage = $"Security Error: User '{identity.Username}' does not have permission to complete prompt '{promptName}'.";
                        throw new UnauthorizedAccessException(errorMessage);
                    }

                    var parts = promptName.Split("__", 2);
                    if (parts.Length != 2)
                    {
                        statusCode = 403;
                        errorMessage = $"Security Error: Unresolved target backend for prompt '{promptName}'.";
                        throw new UnauthorizedAccessException(errorMessage);
                    }

                    var serverId = parts[0];
                    var rawName = parts[1];

                    await EnsureBackendsInitializedAsync();

                    if (!_backendConnections.TryGetValue(serverId, out var conn))
                    {
                        statusCode = 403;
                        errorMessage = $"Security Error: Target backend server '{serverId}' is not available for prompt '{promptName}'.";
                        throw new UnauthorizedAccessException(errorMessage);
                    }

                    var rewrittenBody = RewriteRequestJson(body, "name", rawName);
                    var resp = await conn.SendRequestAsync("completion/complete", rewrittenBody);
                    object result = resp.Result != null ? resp.Result.Value : new { completion = new { values = Array.Empty<string>(), hasMore = false } };
                    responsePayload = JsonSerializer.Serialize(result);
                    return result;
                }
                else if (refType == "ref/resource")
                {
                    string uriString = string.Empty;
                    string propName = "uriTemplate";
                    if (refProp.TryGetProperty("uriTemplate", out var templateProp))
                    {
                        uriString = templateProp.GetString() ?? string.Empty;
                        propName = "uriTemplate";
                    }
                    else if (refProp.TryGetProperty("uri", out var uriProp))
                    {
                        uriString = uriProp.GetString() ?? string.Empty;
                        propName = "uri";
                    }

                    if (string.IsNullOrWhiteSpace(uriString))
                    {
                        statusCode = 403;
                        errorMessage = "Security Error: Missing uri or uriTemplate in resource completion reference.";
                        throw new UnauthorizedAccessException(errorMessage);
                    }

                    targetItem = uriString;

                    if (uriString == "logs://{server_name}/today")
                    {
                        var argVal = string.Empty;
                        if (paramsProp.TryGetProperty("argument", out var argObj) && argObj.TryGetProperty("value", out var vProp))
                        {
                            argVal = vProp.GetString() ?? string.Empty;
                        }
                        else if (paramsProp.TryGetProperty("value", out var valProp))
                        {
                            argVal = valProp.GetString() ?? string.Empty;
                        }

                        var serverIds = _servers.Where(s => s.Enabled).Select(s => s.Id).ToList();
                        var matching = new List<string>();
                        foreach (var s in serverIds.Where(id => id.StartsWith(argVal, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (await IsUserAuthorizedAsync("resources/read", $"logs://{s}/today", httpContext) ||
                                await IsUserAuthorizedAsync("completion/complete", s, httpContext))
                            {
                                matching.Add(s);
                            }
                        }
                        var res = new { completion = new { values = matching.Take(10).ToList(), hasMore = false } };
                        responsePayload = JsonSerializer.Serialize(res);
                        return res;
                    }

                    var activeServerIds = _servers.Where(s => s.Enabled).Select(s => s.Id).ToList();
                    if (!SecurityValidationHelper.ValidateResourceUri(uriString, activeServerIds))
                    {
                        statusCode = 403;
                        errorMessage = $"Security Error: Invalid or spoofed resource URI namespace: '{uriString}'.";
                        throw new UnauthorizedAccessException(errorMessage);
                    }

                    var isResourceAuth = await IsUserAuthorizedAsync("completion/complete", uriString, httpContext);
                    if (!isResourceAuth)
                    {
                        var identity = await ResolveUserIdentityAsync(httpContext);
                        statusCode = 403;
                        errorMessage = $"Security Error: User '{identity.Username}' does not have permission to complete resource '{uriString}'.";
                        throw new UnauthorizedAccessException(errorMessage);
                    }

                    var parts = uriString.Substring("mcp://".Length).Split('/', 2);
                    if (parts.Length != 2)
                    {
                        statusCode = 403;
                        errorMessage = $"Security Error: Unresolved target backend for resource '{uriString}'.";
                        throw new UnauthorizedAccessException(errorMessage);
                    }

                    var serverId = parts[0];
                    var backendTemplate = parts[1];

                    await EnsureBackendsInitializedAsync();

                    if (!_backendConnections.TryGetValue(serverId, out var conn))
                    {
                        statusCode = 403;
                        errorMessage = $"Security Error: Target backend server '{serverId}' is not available for resource '{uriString}'.";
                        throw new UnauthorizedAccessException(errorMessage);
                    }

                    var rewrittenBody = RewriteRequestJson(body, propName, backendTemplate);
                    var resp = await conn.SendRequestAsync("completion/complete", rewrittenBody);
                    object result = resp.Result != null ? resp.Result.Value : new { completion = new { values = Array.Empty<string>(), hasMore = false } };
                    responsePayload = JsonSerializer.Serialize(result);
                    return result;
                }
                else
                {
                    statusCode = 403;
                    errorMessage = $"Security Error: Unsupported or unknown completion reference type: '{refType}'.";
                    throw new UnauthorizedAccessException(errorMessage);
                }
            }
            catch (Exception ex)
            {
                if (statusCode == 200)
                {
                    statusCode = 500;
                    errorMessage = ex.Message;
                }
                throw;
            }
            finally
            {
                stopwatch.Stop();
                await AuditInvocationAsync("completion/complete", targetItem, body, statusCode, stopwatch.ElapsedMilliseconds, responsePayload, errorMessage, httpContext);
            }
        }

        public async Task<List<object>> ListPromptsAsync(string body, HttpContext? httpContext = null)
        {
            var prompts = await _promptRoutingManager.ListPromptsAsync(body, _backendConnections, _logger, EnsureBackendsInitializedAsync, _sessionManager);
            return await FilterAuthorizedAsync(prompts, "prompts/list", "name", httpContext);
        }

        public async Task<object?> GetPromptAsync(string promptName, string body, HttpContext? httpContext = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int statusCode = 200;
            string? errorMessage = null;
            string? responsePayload = null;

            try
            {
                // Namespace validation
                var activeServerIds = _servers.Where(s => s.Enabled).Select(s => s.Id).ToList();
                if (!SecurityValidationHelper.ValidateToolOrPromptName(promptName, activeServerIds))
                {
                    statusCode = 403;
                    errorMessage = $"Security Error: Invalid or spoofed prompt namespace: '{promptName}'.";
                    throw new UnauthorizedAccessException(errorMessage);
                }

                // RBAC Check — use the live per-request HttpContext when provided
                var isAuth = await IsUserAuthorizedAsync("prompts/get", promptName, httpContext);
                if (!isAuth)
                {
                    var identity = await ResolveUserIdentityAsync(httpContext);
                    statusCode = 403;
                    errorMessage = $"Security Error: User '{identity.Username}' does not have permission to access prompt '{promptName}'.";
                    throw new UnauthorizedAccessException(errorMessage);
                }

                var res = await _promptRoutingManager.GetPromptAsync(promptName, body, _backendConnections, EnsureBackendsInitializedAsync, RewriteRequestJson);
                responsePayload = res != null ? JsonSerializer.Serialize(res) : null;
                return res;
            }
            catch (Exception ex)
            {
                if (statusCode == 200)
                {
                    statusCode = 500;
                    errorMessage = ex.Message;
                }
                throw;
            }
            finally
            {
                stopwatch.Stop();
                await AuditInvocationAsync("prompts/get", promptName, body, statusCode, stopwatch.ElapsedMilliseconds, responsePayload, errorMessage, httpContext);
            }
        }

    }
}
