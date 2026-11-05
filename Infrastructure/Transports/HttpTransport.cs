using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ModelContextGateway.Infrastructure.Transports
{
    public class HttpTransport : ITransport
    {
        private string? _passThroughToken;
        private readonly System.Security.Principal.WindowsIdentity? _callerWindowsIdentity;
        private readonly string? _forwardedUser;
        private readonly IUserSecretStore? _userSecretStore;
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(15);
        private readonly McpServer _server;
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly ISecretRetriever? _secretRetriever;
        private readonly CancellationTokenSource _cts = new();
        private readonly SemaphoreSlim _sessionLock = new(1, 1);
        private string _sessionId = string.Empty;
        private string? _lastInitializePayload;
        private bool _disposed = false;

        public HttpTransport(McpServer server, HttpClient httpClient, ILogger logger, ISecretRetriever? secretRetriever = null, string? passThroughToken = null, System.Security.Principal.WindowsIdentity? callerWindowsIdentity = null, string? forwardedUser = null, IUserSecretStore? userSecretStore = null)
        {
            _passThroughToken = passThroughToken;
            _callerWindowsIdentity = callerWindowsIdentity;
            _forwardedUser = forwardedUser;
            _userSecretStore = userSecretStore;
            _server = server;
            _httpClient = httpClient;
            _logger = logger;
            _secretRetriever = secretRetriever;
        }

        private static HttpRequestMessage CloneHttpRequestMessage(HttpRequestMessage req)
        {
            var clone = new HttpRequestMessage(req.Method, req.RequestUri);
            if (req.Content != null)
            {
                var ms = new MemoryStream();
                req.Content.CopyToAsync(ms).GetAwaiter().GetResult();
                ms.Position = 0;
                var streamContent = new StreamContent(ms);
                foreach (var header in req.Content.Headers)
                {
                    streamContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                clone.Content = streamContent;
            }
            foreach (var header in req.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            return clone;
        }

        public async Task<string?> ResolveTokenAsync(ISecretRetriever? secretRetriever = null)
        {
            string? rawCandidate = null;
            if (!string.IsNullOrEmpty(_passThroughToken) && (_server.AllowPassThroughAuth || _server.SecretProvider == "UserProvided" || _server.EnableOAuth3Lo))
            {
                rawCandidate = _passThroughToken;
            }
            else if (_server.EnableOAuth3Lo && _userSecretStore != null && !string.IsNullOrEmpty(_forwardedUser))
            {
                rawCandidate = await _userSecretStore.GetSecretAsync(_forwardedUser, _server.Id);
            }

            if (string.IsNullOrEmpty(rawCandidate))
            {
                var provider = _server.SecretProvider ?? "None";
                if (provider.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    rawCandidate = !string.IsNullOrEmpty(_server.ApiKey) ? _server.ApiKey : null;
                }
                else
                {
                    var retriever = secretRetriever ?? _secretRetriever;
                    if (retriever == null)
                    {
                        throw new InvalidOperationException($"SecretProvider is configured to '{provider}' for server '{_server.Id}', but no secret retriever is registered.");
                    }

                    string path = !string.IsNullOrWhiteSpace(_server.SecretPath) ? _server.SecretPath : _server.Url;
                    string field = !string.IsNullOrWhiteSpace(_server.SecretField)
                        ? _server.SecretField
                        : (!string.IsNullOrWhiteSpace(_server.SecretItemKey) ? _server.SecretItemKey : "ApiKey");

                    if (!string.IsNullOrWhiteSpace(_server.SecretMount))
                    {
                        path = $"{_server.SecretMount}:{path}";
                    }
                    else if (provider.Equals("Vault", StringComparison.OrdinalIgnoreCase) &&
                             string.IsNullOrWhiteSpace(_server.SecretPath) &&
                             !string.IsNullOrWhiteSpace(_server.SecretItemKey))
                    {
                        // Frontend passes 'mount:path:field' inside SecretItemKey
                        var parts = _server.SecretItemKey.Split(':', 3);
                        if (parts.Length == 3)
                        {
                            path = $"{parts[0]}:{parts[1]}";
                            field = parts[2];
                        }
                    }
                    else if (provider.Equals("WindowsRegistry", StringComparison.OrdinalIgnoreCase) &&
                             (string.IsNullOrWhiteSpace(_server.SecretPath) || path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                    {
                        path = @"SOFTWARE\McpRouter\Secrets";
                    }

                    string? secret;
                    if (retriever is CompositeSecretRetriever composite)
                    {
                        secret = await composite.GetSecretForProviderAsync(provider, path, field);
                    }
                    else
                    {
                        secret = await retriever.GetSecretAsync(path, field);
                    }

                    if (string.IsNullOrEmpty(secret))
                    {
                        throw new System.Security.SecurityException($"Failed to resolve secret from provider '{provider}' for server '{_server.Id}' (path: '{path}', field: '{field}'). Plaintext ApiKey fallback is disabled.");
                    }

                    rawCandidate = secret;
                }
            }

            return await OAuthEgressTokenManager.ExtractOrRefreshTokenAsync(
                _server,
                rawCandidate,
                _forwardedUser,
                _userSecretStore,
                _httpClient,
                _logger,
                updatedToken => { _passThroughToken = updatedToken; });
        }

        private async Task ApplyAuthAndCustomHeadersAsync(HttpRequestMessage request)
        {
            var token = await ResolveTokenAsync();
            var authShape = (_server.AuthShape ?? "bearer").ToLowerInvariant();

            if (!string.IsNullOrEmpty(token))
            {
                if (_server.EnableOAuth3Lo)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
                else
                {
                    switch (authShape)
                    {
                        case "bearer":
                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                            break;
                        case "basic":
                            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
                            break;
                        case "raw":
                            request.Headers.TryAddWithoutValidation("Authorization", token);
                            break;
                        case "x-api-key":
                            request.Headers.TryAddWithoutValidation("X-API-Key", token);
                            break;
                        case "custom-header":
                            var headerName = !string.IsNullOrWhiteSpace(_server.CustomHeaderName) ? _server.CustomHeaderName : "X-Auth-Token";
                            request.Headers.TryAddWithoutValidation(headerName, token);
                            break;
                        case "query":
                            var paramName = !string.IsNullOrWhiteSpace(_server.CustomHeaderName) ? _server.CustomHeaderName : "token";
                            var uriBuilder = new UriBuilder(request.RequestUri!);
                            var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
                            query[paramName] = token;
                            uriBuilder.Query = query.ToString();
                            request.RequestUri = uriBuilder.Uri;
                            break;
                        default:
                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                            break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(_server.HeadersJson))
            {
                try
                {
                    var customHeaders = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(_server.HeadersJson);
                    if (customHeaders != null)
                    {
                        foreach (var kvp in customHeaders)
                        {
                            request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse custom headers for server {ServerId}", _server.Id);
                }
            }

            if (!string.IsNullOrEmpty(_forwardedUser))
            {
                request.Headers.TryAddWithoutValidation("X-Forwarded-User", _forwardedUser);
            }

            // OpenTelemetry / W3C Trace Context Propagation
            var currentActivity = System.Diagnostics.Activity.Current;
            if (currentActivity != null)
            {
                if (!string.IsNullOrEmpty(currentActivity.Id) && !request.Headers.Contains("traceparent"))
                {
                    request.Headers.TryAddWithoutValidation("traceparent", currentActivity.Id);
                }
                if (!string.IsNullOrEmpty(currentActivity.TraceStateString) && !request.Headers.Contains("tracestate"))
                {
                    request.Headers.TryAddWithoutValidation("tracestate", currentActivity.TraceStateString);
                }
                if (currentActivity.Baggage.Any() && !request.Headers.Contains("baggage"))
                {
                    var baggageHeader = string.Join(",", currentActivity.Baggage.Select(b => $"{Uri.EscapeDataString(b.Key)}={Uri.EscapeDataString(b.Value ?? "")}"));
                    request.Headers.TryAddWithoutValidation("baggage", baggageHeader);
                }
            }
        }

        private async Task<HttpRequestMessage> CreateHttpRequestMessageAsync(string bodyJson, string? targetAuthToken, string? sessionId)
        {
            var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var req = new HttpRequestMessage(HttpMethod.Post, _server.Url) { Content = content };

            if (!string.IsNullOrEmpty(targetAuthToken))
            {
                req.Headers.Add("X-Target-Auth", targetAuthToken);
            }
            if (req.RequestUri != null && !string.IsNullOrEmpty(req.RequestUri.Host) && !req.RequestUri.IsLoopback)
            {
                req.Headers.Host = req.RequestUri.Authority;
            }
            else
            {
                req.Headers.Host = "localhost";
            }
            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            if (!string.IsNullOrEmpty(sessionId))
            {
                req.Headers.TryAddWithoutValidation("Mcp-Session-Id", sessionId);
            }

            await ApplyAuthAndCustomHeadersAsync(req);
            return req;
        }

        private async Task<HttpResponseMessage> ExecuteHttpRequestAsync(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            var authShape = (_server.AuthShape ?? "bearer").ToLowerInvariant();
            bool isImpersonation = string.Equals(authShape, "impersonation", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(authShape, "kerberos-impersonate", StringComparison.OrdinalIgnoreCase);

            if (isImpersonation)
            {
                if (!OperatingSystem.IsWindows())
                {
                    var msg = $"Kerberos impersonation failed for server '{_server.Id}': Impersonation requires running on a Windows host with Active Directory integration.";
                    _logger.LogError("{ErrorMessage}", msg);
                    throw new InvalidOperationException(msg);
                }

                if (_callerWindowsIdentity == null)
                {
                    var msg = $"Kerberos impersonation failed for server '{_server.Id}': Inbound caller is not authenticated via Active Directory / Windows Authentication.";
                    _logger.LogError("{ErrorMessage}", msg);
                    throw new InvalidOperationException(msg);
                }

#pragma warning disable CA1416
                try
                {
                    return await System.Security.Principal.WindowsIdentity.RunImpersonatedAsync(
                        _callerWindowsIdentity.AccessToken,
                        async () =>
                        {
                            using var handler = new SocketsHttpHandler
                            {
                                AllowAutoRedirect = false,
                                Credentials = System.Net.CredentialCache.DefaultNetworkCredentials
                            };
                            using var impClient = new HttpClient(handler);
                            using var impReq = CloneHttpRequestMessage(req);
                            return await impClient.SendAsync(impReq, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                        });
                }
                catch (Exception ex)
                {
                    var msg = $"Kerberos delegation failed for server '{_server.Id}': Ensure Service Principal Names (SPNs) are registered and AD Constrained Delegation (S4U2Proxy) is configured for the service account. Detail: {ex.Message}";
                    _logger.LogError(ex, "{ErrorMessage}", msg);
                    throw new InvalidOperationException(msg, ex);
                }
#pragma warning restore CA1416
            }
            else
            {
                return await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
        }

        private async Task<bool> TryRecoverSessionAsync(string failedSessionId, CancellationToken cancellationToken)
        {
            await _sessionLock.WaitAsync(cancellationToken);
            try
            {
                // If another concurrent request already rotated the session, we consider it recovered
                if (!string.IsNullOrEmpty(_sessionId) && _sessionId != failedSessionId)
                {
                    _logger.LogDebug("Session for backend '{ServerId}' was already refreshed by another concurrent call (Current: '{SessionId}').", _server.Id, _sessionId);
                    return true;
                }

                _sessionId = string.Empty;

                var initPayload = !string.IsNullOrEmpty(_lastInitializePayload)
                    ? _lastInitializePayload
                    : GatewayMetadata.BuildInitializeRequest();

                _logger.LogInformation("Performing downstream handshake re-initialization for backend '{ServerId}'...", _server.Id);

                using var initReq = await CreateHttpRequestMessageAsync(initPayload, targetAuthToken: null, sessionId: null);
                using var initResp = await ExecuteHttpRequestAsync(initReq, cancellationToken);

                if (!initResp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Downstream re-initialization handshake failed for backend '{ServerId}' with HTTP {StatusCode}", _server.Id, initResp.StatusCode);
                    return false;
                }

                if (initResp.Headers.TryGetValues("Mcp-Session-Id", out var sVals))
                {
                    _sessionId = sVals.FirstOrDefault() ?? string.Empty;
                }
                else if (initResp.Content.Headers.TryGetValues("Mcp-Session-Id", out var scVals))
                {
                    _sessionId = scVals.FirstOrDefault() ?? string.Empty;
                }

                // Send notifications/initialized to complete the handshake if supported
                try
                {
                    var notifJson = "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}";
                    using var notifReq = await CreateHttpRequestMessageAsync(notifJson, targetAuthToken: null, _sessionId);
                    using var notifResp = await ExecuteHttpRequestAsync(notifReq, cancellationToken);
                }
                catch (Exception notifEx)
                {
                    _logger.LogDebug(notifEx, "Best-effort notifications/initialized during recovery returned error for server '{ServerId}'", _server.Id);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to recover session for backend '{ServerId}'", _server.Id);
                return false;
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        public Task ConnectAsync()
        {
            // HTTP transport does not need persistent connection
            return Task.CompletedTask;
        }

        public void StartReader(Func<JsonRpcMessage, Task> onMessageReceived)
        {
            // HTTP transport has no background reader
        }

        public async Task<JsonRpcResponse> SendRequestAsync(string method, string bodyJson, string? targetAuthToken = null)
        {
            return await SendRequestInternalAsync(method, bodyJson, targetAuthToken, isRetry: false);
        }

        private async Task<JsonRpcResponse> SendRequestInternalAsync(string method, string bodyJson, string? targetAuthToken, bool isRetry)
        {
            if (_disposed)
            {
                return new JsonRpcResponse { Error = new JsonRpcError { Code = -32001, Message = "Not connected" } };
            }

            if (string.Equals(method, "initialize", StringComparison.OrdinalIgnoreCase))
            {
                _lastInitializePayload = bodyJson;
            }

            _logger.LogDebug("[JSON-RPC Gateway -> Backend {ServerId}] {Payload}", _server.Id, PiiSanitizer.SanitizePayload(bodyJson));

            using var ctsTimeout = new CancellationTokenSource(RequestTimeout > TimeSpan.Zero ? RequestTimeout : TimeSpan.FromSeconds(15));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ctsTimeout.Token);

            string initialSessionSnapshot = _sessionId;
            using var req = await CreateHttpRequestMessageAsync(bodyJson, targetAuthToken, initialSessionSnapshot);

            var resp = await ExecuteHttpRequestAsync(req, linked.Token);

            // Handle session eviction / expiration (HTTP 404 or HTTP 400 when sent with a session id)
            bool isSessionError = (resp.StatusCode == HttpStatusCode.NotFound || resp.StatusCode == HttpStatusCode.BadRequest);
            bool isInitializeMethod = string.Equals(method, "initialize", StringComparison.OrdinalIgnoreCase);

            if (isSessionError && !string.IsNullOrEmpty(initialSessionSnapshot) && !isRetry && !isInitializeMethod)
            {
                _logger.LogWarning("Streamable HTTP backend '{ServerId}' rejected request '{Method}' with HTTP {StatusCode} (Session '{SessionId}' may have expired). Initiating transparent session re-initialization.",
                    _server.Id, method, resp.StatusCode, initialSessionSnapshot);

                resp.Dispose();

                bool reinitialized = await TryRecoverSessionAsync(initialSessionSnapshot, linked.Token);
                if (reinitialized)
                {
                    _logger.LogInformation("Successfully recovered session for backend '{ServerId}' (New Session: '{SessionId}'). Retrying request '{Method}'.",
                        _server.Id, _sessionId, method);
                    return await SendRequestInternalAsync(method, bodyJson, targetAuthToken, isRetry: true);
                }
            }

            resp.EnsureSuccessStatusCode();

            // Strictly capture Mcp-Session-Id only on successful (2xx) responses
            if (resp.IsSuccessStatusCode)
            {
                if (resp.Headers.TryGetValues("Mcp-Session-Id", out var sVals))
                {
                    _sessionId = sVals.FirstOrDefault() ?? _sessionId;
                }
                else if (resp.Content.Headers.TryGetValues("Mcp-Session-Id", out var scVals))
                {
                    _sessionId = scVals.FirstOrDefault() ?? _sessionId;
                }
            }

            string responseBody = string.Empty;
            var mediaType = resp.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
            if (mediaType == "text/event-stream")
            {
                using var stream = await resp.Content.ReadAsStreamAsync(linked.Token);
                using var reader = new StreamReader(stream);
                var currentDataLines = new List<string>();
                string? line;
                while ((line = await reader.ReadLineAsync(linked.Token)) != null)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("data:"))
                    {
                        currentDataLines.Add(trimmed.Substring(5).Trim());
                    }
                    else if (string.IsNullOrEmpty(trimmed) && currentDataLines.Count > 0)
                    {
                        var eventJson = string.Join("\n", currentDataLines);
                        currentDataLines.Clear();

                        try
                        {
                            using var doc = JsonDocument.Parse(eventJson);
                            var root = doc.RootElement;
                            // Check if this event is a JSON-RPC response (has "result", "error", or an "id" without a notification "method")
                            if (root.TryGetProperty("result", out _) ||
                                root.TryGetProperty("error", out _) ||
                                (root.TryGetProperty("id", out _) && !root.TryGetProperty("method", out _)))
                            {
                                responseBody = eventJson;
                                break;
                            }
                        }
                        catch
                        {
                            // Incomplete or non-JSON event payload; keep reading
                        }
                    }
                }

                // If EOF reached without a trailing blank line, check buffered lines
                if (string.IsNullOrEmpty(responseBody) && currentDataLines.Count > 0)
                {
                    var eventJson = string.Join("\n", currentDataLines);
                    try
                    {
                        using var doc = JsonDocument.Parse(eventJson);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("result", out _) ||
                            root.TryGetProperty("error", out _) ||
                            (root.TryGetProperty("id", out _) && !root.TryGetProperty("method", out _)))
                        {
                            responseBody = eventJson;
                        }
                    }
                    catch
                    {
                        responseBody = eventJson;
                    }
                }
            }
            else
            {
                responseBody = await resp.Content.ReadAsStringAsync(linked.Token);
            }

            _logger.LogDebug("[JSON-RPC Backend {ServerId} -> Gateway] {Payload}", _server.Id, PiiSanitizer.SanitizePayload(responseBody));
            _logger.LogDebug("[HttpTransport DEBUG] Server {ServerId} responded with status {StatusCode}. Body: '{Body}'", _server.Id, resp.StatusCode, PiiSanitizer.SanitizePayload(responseBody));

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return new JsonRpcResponse();
            }

            var responseObj = JsonSerializer.Deserialize<JsonRpcResponse>(responseBody);
            return responseObj ?? new JsonRpcResponse { Error = new JsonRpcError { Code = -32603, Message = "Failed to deserialize POST response" } };
        }

        public async Task<JsonRpcResponse> CallMethodAsync(string method, object parameters, string? overrideId = null)
        {
            var bodyObj = new { jsonrpc = "2.0", method = method, @params = parameters, id = overrideId ?? Guid.NewGuid().ToString("N") };
            var bodyJson = JsonSerializer.Serialize(bodyObj);
            return await SendRequestAsync(method, bodyJson);
        }

        public async Task SendNotificationAsync(string method, string bodyJson)
        {
            try
            {
                await SendRequestAsync(method, bodyJson);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.MethodNotAllowed)
            {
                _logger.LogWarning("Upstream backend '{ServerId}' rejected notification '{Method}' with HTTP {StatusCode}", _server.Id, method, ex.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send notification '{Method}' to upstream backend '{ServerId}': {Message}", method, _server.Id, ex.Message);
            }
        }

        public Task SendResponseAsync(string responseJson)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _disposed = true;
            try { _cts.Cancel(); } catch { }
            try { _cts.Dispose(); } catch { }
        }
    }
}
