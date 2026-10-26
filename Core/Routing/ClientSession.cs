using System.Collections.Concurrent;
using System.Text.Json;
using ModelContextGateway.Core.CodeMode;

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
        private readonly IEmbeddingService? _embeddingService;
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

        public ClientSession(string sessionId, HttpResponse? clientResponse, List<McpServer> servers, HttpClient httpClient, IEmbeddingService? embeddingService, SessionManager? sessionManager, Microsoft.Extensions.Logging.ILogger logger, IServiceProvider? rootServices = null)
        {
            _sessionId = sessionId;
            _clientResponse = clientResponse;
            _servers = servers;
            _httpClient = httpClient;
            _embeddingService = embeddingService;
            _sessionManager = sessionManager;
            _logger = logger;
            _rootServices = rootServices;

            if (_embeddingService != null)
            {
                var provider = _embeddingService as ModelContextGateway.Core.VectorSearch.IEmbeddingProvider
                    ?? new ModelContextGateway.Core.VectorSearch.EmbeddingServiceAdapter(_embeddingService);
                _toolRoutingManager.EmbeddingProvider = provider;
            }
            if (_rootServices != null)
            {
                var store = _rootServices.GetService(typeof(ModelContextGateway.Core.VectorSearch.IToolVectorStore)) as ModelContextGateway.Core.VectorSearch.IToolVectorStore;
                if (store != null)
                {
                    _toolRoutingManager.VectorStore = store;
                }

                var codeMode = _rootServices.GetService(typeof(ModelContextGateway.Core.CodeMode.ICodeModeExecutionEngine)) as ModelContextGateway.Core.CodeMode.ICodeModeExecutionEngine;
                if (codeMode != null)
                {
                    _toolRoutingManager.CodeModeEngine = codeMode;
                }
            }
        }

        public ClientSession(string sessionId, HttpResponse? clientResponse, List<McpServer> servers, HttpClient httpClient, IEmbeddingService? embeddingService, Microsoft.Extensions.Logging.ILogger logger, IServiceProvider? rootServices = null)
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
            var settings = (_embeddingService as DynamicEmbeddingService)?.GetSettings();
            bool enableCodeMode = settings?.EnableCodeMode ?? false;
            var tools = await _toolRoutingManager.ListToolsAsync(body, IsMetaMode, _backendConnections, _logger, EnsureBackendsInitializedAsync, _servers, _sessionManager, enableCodeMode);
            return await FilterAuthorizedAsync(tools, "tools/list", "name", httpContext);
        }

        public async Task<List<object>> FilterAuthorizedToolsAsync(List<object> tools, HttpContext? httpContext = null)
        {
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
                var (normalizedToolName, ambiguityErr) = _toolRoutingManager.NormalizeTargetToolName(toolName, _servers, _logger);
                if (!string.IsNullOrEmpty(ambiguityErr))
                {
                    statusCode = 400;
                    errorMessage = ambiguityErr;
                    var errResult = new
                    {
                        isError = true,
                        content = new[] {
                            new {
                                type = "text",
                                text = ambiguityErr
                            }
                        }
                    };
                    responsePayload = JsonSerializer.Serialize(errResult);
                    return errResult;
                }
                toolName = normalizedToolName;

                // Namespace validation
                var activeServerIds = _servers.Where(s => s.Enabled)
                    .SelectMany(s => string.IsNullOrWhiteSpace(s.Alias) ? new[] { s.Id } : new[] { s.Id, s.Alias })
                    .Distinct()
                    .ToList();
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
                        var identity = await ResolveUserIdentityAsync(contextToUse);
                        var config = contextToUse?.RequestServices?.GetService<IConfiguration>();
                        bool isCallerAdmin = SecurityValidationHelper.IsAdmin(identity, config, contextToUse);

                        var sandboxCallbacks = BuildSandboxCallbacks(httpContext);

                        var res = await _toolRoutingManager.CallToolAsync(toolName, body, dbFactory, _backendConnections, _servers, _logger, _httpClient, _embeddingService, EnsureBackendsInitializedAsync, RewriteRequestJson, cts.Token, _sessionManager, _sessionId,
                            filterAuthorizedToolsAsync: async (tools) => await FilterAuthorizedAsync(tools, "tools/list", "name", httpContext),
                            callerGroups: identity.GroupNames,
                            isCallerAdmin: isCallerAdmin,
                            sandboxCallbacks: sandboxCallbacks);
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

        private CodeModeSandboxCallbacks BuildSandboxCallbacks(HttpContext? httpContext)
        {
            return new CodeModeSandboxCallbacks
            {
                ListServersAsync = async () =>
                {
                    var result = new List<object>();
                    foreach (var s in _servers.Where(s => s.Enabled))
                    {
                        if (await IsUserAuthorizedAsync("tools/list", $"server:{s.Id}", httpContext))
                        {
                            result.Add(new
                            {
                                id = s.Id,
                                alias = s.Alias,
                                name = s.DisplayName,
                                enabled = s.Enabled
                            });
                        }
                    }
                    return result;
                },
                ListResourcesAsync = async (serverIdFilter) =>
                {
                    var allResources = await ListAllAuthorizedResourcesAsync("{}", httpContext);
                    if (string.IsNullOrWhiteSpace(serverIdFilter))
                    {
                        return allResources;
                    }
                    var matchedServer = _servers.FirstOrDefault(s =>
                        string.Equals(s.Id, serverIdFilter, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(s.Alias) && string.Equals(s.Alias, serverIdFilter, StringComparison.OrdinalIgnoreCase)));
                    var canonicalId = matchedServer?.Id ?? serverIdFilter;
                    var prefix1 = $"mcp://{canonicalId}/";
                    var prefix2 = $"mcp://{serverIdFilter}/";
                    return allResources.Where(r =>
                    {
                        if (r is IDictionary<string, object> dict && dict.TryGetValue("uri", out var uObj) && uObj is string uStr)
                        {
                            return uStr.StartsWith(prefix1, StringComparison.OrdinalIgnoreCase) ||
                                   uStr.StartsWith(prefix2, StringComparison.OrdinalIgnoreCase);
                        }
                        return false;
                    }).ToList();
                },
                SearchResourcesAsync = async (query) =>
                {
                    var allResources = await ListAllAuthorizedResourcesAsync("{}", httpContext);
                    return await _resourceRoutingManager.SearchResourcesAsync(query, allResources);
                },
                ReadResourceAsync = async (uri) =>
                {
                    var reqBody = JsonSerializer.Serialize(new
                    {
                        jsonrpc = "2.0",
                        id = Guid.NewGuid().ToString("N"),
                        method = "resources/read",
                        @params = new { uri = uri }
                    });
                    var res = await ReadResourceAsync(uri, reqBody, httpContext);
                    return res ?? new { };
                },
                ListPromptsAsync = async (serverIdFilter) =>
                {
                    var allPrompts = await ListPromptsAsync("{}", httpContext);
                    if (string.IsNullOrWhiteSpace(serverIdFilter))
                    {
                        return allPrompts;
                    }
                    var matchedServer = _servers.FirstOrDefault(s =>
                        string.Equals(s.Id, serverIdFilter, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(s.Alias) && string.Equals(s.Alias, serverIdFilter, StringComparison.OrdinalIgnoreCase)));
                    var canonicalId = matchedServer?.Id ?? serverIdFilter;
                    var prefix1 = $"{canonicalId}__";
                    var prefix2 = $"{serverIdFilter}__";
                    return allPrompts.Where(p =>
                    {
                        if (p is IDictionary<string, object> dict && dict.TryGetValue("name", out var nObj) && nObj is string nStr)
                        {
                            return nStr.StartsWith(prefix1, StringComparison.OrdinalIgnoreCase) ||
                                   nStr.StartsWith(prefix2, StringComparison.OrdinalIgnoreCase);
                        }
                        return false;
                    }).ToList();
                },
                GetPromptAsync = async (promptName, promptArgs) =>
                {
                    var reqBody = JsonSerializer.Serialize(new
                    {
                        jsonrpc = "2.0",
                        id = Guid.NewGuid().ToString("N"),
                        method = "prompts/get",
                        @params = new
                        {
                            name = promptName,
                            arguments = promptArgs.HasValue && promptArgs.Value.ValueKind == JsonValueKind.Object ? (object)promptArgs.Value : new Dictionary<string, string>()
                        }
                    });
                    var res = await GetPromptAsync(promptName, reqBody, httpContext);
                    return res ?? new { };
                }
            };
        }

        public async Task<List<object>> ListAllAuthorizedDownstreamToolsAsync(HttpContext? httpContext = null)
        {
            var rawTools = await _toolRoutingManager.ListToolsAsync(
                "{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\",\"id\":\"dts-list\"}",
                false,
                _backendConnections,
                _logger,
                EnsureBackendsInitializedAsync,
                _servers,
                _sessionManager,
                false);
            return await FilterAuthorizedAsync(rawTools, "tools/list", "name", httpContext);
        }

        public async Task<List<object>> ListAllAuthorizedResourcesAsync(string body = "{}", HttpContext? httpContext = null)
        {
            var resources = await _resourceRoutingManager.ListResourcesAsync(
                body,
                _backendConnections,
                _logger,
                EnsureBackendsInitializedAsync,
                _sessionManager,
                isMetaMode: false,
                servers: _servers);
            return await FilterAuthorizedAsync(resources, "resources/list", "uri", httpContext);
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
            var resources = await _resourceRoutingManager.ListResourcesAsync(
                body,
                _backendConnections,
                _logger,
                EnsureBackendsInitializedAsync,
                _sessionManager,
                isMetaMode: IsMetaMode,
                servers: _servers);
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
                var activeServerIds = _servers.Where(s => s.Enabled)
                    .SelectMany(s => string.IsNullOrWhiteSpace(s.Alias) ? new[] { s.Id } : new[] { s.Id, s.Alias })
                    .Distinct()
                    .ToList();
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

                var res = await _resourceRoutingManager.ReadResourceAsync(
                    resourceUri,
                    body,
                    _backendConnections,
                    EnsureBackendsInitializedAsync,
                    RewriteRequestJson,
                    _sessionManager,
                    getAuthorizedToolsAsync: () => ListAllAuthorizedDownstreamToolsAsync(httpContext),
                    getAuthorizedResourcesAsync: () => ListAllAuthorizedResourcesAsync("{}", httpContext),
                    getAuthorizedPromptsAsync: () => ListPromptsAsync("{}", httpContext),
                    servers: _servers);
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
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("params", out var paramsProp) || !root.GetProperty("params").TryGetProperty("ref", out var refProp))
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

                    var activeServerIds = _servers.Where(s => s.Enabled)
                        .SelectMany(s => string.IsNullOrWhiteSpace(s.Alias) ? new[] { s.Id } : new[] { s.Id, s.Alias })
                        .Distinct()
                        .ToList();
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

                    if (string.Equals(serverId, "router", StringComparison.OrdinalIgnoreCase))
                    {
                        var argName = string.Empty;
                        var argVal = string.Empty;
                        if (paramsProp.TryGetProperty("argument", out var argObj))
                        {
                            if (argObj.TryGetProperty("name", out var nProp))
                            {
                                argName = nProp.GetString() ?? string.Empty;
                            }
                            if (argObj.TryGetProperty("value", out var vProp))
                            {
                                argVal = vProp.GetString() ?? string.Empty;
                            }
                        }

                        var values = new List<string>();
                        if (string.Equals(argName, "server_name", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(argName, "servers", StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var s in _servers.Where(s => s.Enabled))
                            {
                                if (s.Id.StartsWith(argVal, StringComparison.OrdinalIgnoreCase) &&
                                    await IsUserAuthorizedAsync("completion/complete", $"server:{s.Id}", httpContext))
                                {
                                    values.Add(s.Id);
                                }
                                if (!string.IsNullOrWhiteSpace(s.Alias) &&
                                    s.Alias.StartsWith(argVal, StringComparison.OrdinalIgnoreCase) &&
                                    await IsUserAuthorizedAsync("completion/complete", $"server:{s.Id}", httpContext))
                                {
                                    values.Add(s.Alias);
                                }
                            }
                        }
                        else if (string.Equals(argName, "tool_name", StringComparison.OrdinalIgnoreCase))
                        {
                            var tools = await ListAllAuthorizedDownstreamToolsAsync(httpContext);
                            foreach (var t in tools)
                            {
                                if (t is IDictionary<string, object> dict && dict.TryGetValue("name", out var tnObj) && tnObj is string tn &&
                                    tn.StartsWith(argVal, StringComparison.OrdinalIgnoreCase))
                                {
                                    values.Add(tn);
                                }
                            }
                        }

                        var localPromptRes = new { completion = new { values = values.Distinct().Take(10).ToList(), hasMore = false } };
                        responsePayload = JsonSerializer.Serialize(localPromptRes);
                        return localPromptRes;
                    }

                    var resolvedServerId = _servers.FirstOrDefault(s =>
                        string.Equals(s.Id, serverId, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(s.Alias) && string.Equals(s.Alias, serverId, StringComparison.OrdinalIgnoreCase)))?.Id ?? serverId;

                    await EnsureBackendsInitializedAsync();

                    if (!_backendConnections.TryGetValue(resolvedServerId, out var conn))
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

                    if (uriString == "logs://{server_name}/today" ||
                        string.Equals(uriString, "router://code-mode/types/{server_name}", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(uriString, "router://resources/server/{server_name}", StringComparison.OrdinalIgnoreCase))
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
                            var checkUri = uriString.Replace("{server_name}", s, StringComparison.OrdinalIgnoreCase);
                            if (await IsUserAuthorizedAsync("resources/read", checkUri, httpContext) ||
                                await IsUserAuthorizedAsync("completion/complete", s, httpContext))
                            {
                                matching.Add(s);
                            }
                        }
                        var res = new { completion = new { values = matching.Take(10).ToList(), hasMore = false } };
                        responsePayload = JsonSerializer.Serialize(res);
                        return res;
                    }

                    var activeServerIds = _servers.Where(s => s.Enabled)
                        .SelectMany(s => string.IsNullOrWhiteSpace(s.Alias) ? new[] { s.Id } : new[] { s.Id, s.Alias })
                        .Distinct()
                        .ToList();
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
                    var backendTemplate = Uri.UnescapeDataString(parts[1]);

                    var resolvedServerId = _servers.FirstOrDefault(s =>
                        string.Equals(s.Id, serverId, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(s.Alias) && string.Equals(s.Alias, serverId, StringComparison.OrdinalIgnoreCase)))?.Id ?? serverId;

                    await EnsureBackendsInitializedAsync();

                    if (!_backendConnections.TryGetValue(resolvedServerId, out var conn))
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
            var settings = (_embeddingService as DynamicEmbeddingService)?.GetSettings();
            bool enableCodeMode = settings?.EnableCodeMode ?? IsMetaMode;
            var prompts = await _promptRoutingManager.ListPromptsAsync(body, _backendConnections, _logger, EnsureBackendsInitializedAsync, _sessionManager, enableCodeMode);
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
                var activeServerIds = _servers.Where(s => s.Enabled)
                    .SelectMany(s => string.IsNullOrWhiteSpace(s.Alias) ? new[] { s.Id } : new[] { s.Id, s.Alias })
                    .Distinct()
                    .ToList();
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

                var res = await _promptRoutingManager.GetPromptAsync(
                    promptName,
                    body,
                    _backendConnections,
                    EnsureBackendsInitializedAsync,
                    RewriteRequestJson,
                    getAuthorizedToolsAsync: () => ListAllAuthorizedDownstreamToolsAsync(httpContext),
                    servers: _servers);
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
