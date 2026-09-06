using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ModelContextGateway.Infrastructure.Transports
{
    public class SseTransport : ITransport
    {
        private readonly string? _passThroughToken;
        private readonly string? _forwardedUser;

        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(15);
        private readonly McpServer _server;
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly JsonRpcStateManager _stateManager;
        private readonly ISecretRetriever? _secretRetriever;
        private readonly CancellationTokenSource _cts = new();

        private string? _messageUrl;
        private TaskCompletionSource<string> _endpointTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private string _sessionId = Guid.NewGuid().ToString("N");

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            Converters = { new JsonRpcMessageConverter() }
        };

        public SseTransport(McpServer server, HttpClient httpClient, ILogger logger, JsonRpcStateManager stateManager, ISecretRetriever? secretRetriever = null, string? passThroughToken = null, string? forwardedUser = null)
        {
            _passThroughToken = passThroughToken;
            _forwardedUser = forwardedUser;

            _server = server;
            _httpClient = httpClient;
            _logger = logger;
            _stateManager = stateManager;
            _secretRetriever = secretRetriever;
        }

        public async Task<string?> ResolveTokenAsync(ISecretRetriever? secretRetriever = null)
        {
            if (!string.IsNullOrEmpty(_passThroughToken) && (_server.AllowPassThroughAuth || _server.SecretProvider == "UserProvided"))
            {
                return _passThroughToken;
            }



            var provider = _server.SecretProvider ?? "None";
            if (provider.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrEmpty(_server.ApiKey) ? _server.ApiKey : null;
            }

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

            return secret;
        }

        private async Task ApplyAuthAndCustomHeadersAsync(HttpRequestMessage request)
        {
            var token = await ResolveTokenAsync();
            var authShape = (_server.AuthShape ?? "bearer").ToLowerInvariant();

            if (!string.IsNullOrEmpty(token))
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

            if (!string.IsNullOrEmpty(_forwardedUser))
            {
                request.Headers.TryAddWithoutValidation("X-Forwarded-User", _forwardedUser);
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

        public async Task ConnectAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, _server.Url);
            request.Headers.Host = "localhost";
            request.Headers.Add("Mcp-Session-Id", _sessionId);
            await ApplyAuthAndCustomHeadersAsync(request);

            _logger.LogInformation("Connecting to backend {ServerId} SSE stream at {Url}...", _server.Id, _server.Url);
        }

        private async Task ProcessEventDataAsync(string? currentEvent, string data, Func<JsonRpcMessage, Task> onMessageReceived)
        {
            if (currentEvent == "endpoint")
            {
                string url;
                if (Uri.IsWellFormedUriString(data, UriKind.Absolute))
                {
                    url = data;
                }
                else
                {
                    var baseUri = new Uri(_server.Url);
                    url = new Uri(baseUri, data).ToString();
                }

                _messageUrl = url;
                _endpointTcs.TrySetResult(url);
                _stateManager.MarkConnected();
                return;
            }

            if (currentEvent == "message")
            {
                await ProcessMessageEventAsync(data, onMessageReceived);
            }
        }

        private async Task ProcessMessageEventAsync(string data, Func<JsonRpcMessage, Task> onMessageReceived)
        {
            try
            {
                var responseObj = JsonSerializer.Deserialize<JsonRpcMessage>(data, _jsonOptions);
                if (responseObj == null)
                {
                    return;
                }

                if (responseObj is not JsonRpcResponse)
                {
                    _logger.LogDebug("[JSON-RPC Backend {ServerId} -> Gateway] {Payload}", _server.Id, PiiSanitizer.SanitizePayload(data));
                }

                await onMessageReceived(responseObj);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse SSE message data: {Data}", data);
            }
        }

        public void StartReader(Func<JsonRpcMessage, Task> onMessageReceived)
        {
            _ = Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (_endpointTcs.Task.IsCompleted)
                        {
                            _endpointTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                            _messageUrl = null;
                        }

                        using var request = new HttpRequestMessage(HttpMethod.Get, _server.Url);
                        request.Headers.Host = "localhost";
                        request.Headers.Accept.Clear();
                        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
                        request.Headers.Add("Mcp-Session-Id", _sessionId);

                        await ApplyAuthAndCustomHeadersAsync(request);

                        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
                        response.EnsureSuccessStatusCode();

                        IEnumerable<string>? sessionValues = null;
                        if (response.Headers.TryGetValues("Mcp-Session-Id", out var hVals))
                        {
                            sessionValues = hVals;
                        }
                        else if (response.Content.Headers.TryGetValues("Mcp-Session-Id", out var cVals))
                        {
                            sessionValues = cVals;
                        }

                        if (sessionValues != null)
                        {
                            _sessionId = sessionValues.FirstOrDefault() ?? string.Empty;
                            _ = Task.Delay(1500, _cts.Token).ContinueWith(t =>
                            {
                                if (!t.IsCanceled && _messageUrl == null)
                                {
                                    _messageUrl = _server.Url;
                                    _endpointTcs.TrySetResult(_server.Url);
                                }
                            });
                        }
                        else if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
                        {
                            _ = Task.Delay(1500, _cts.Token).ContinueWith(t =>
                            {
                                if (!t.IsCanceled && _messageUrl == null)
                                {
                                    _messageUrl = _server.Url;
                                    _endpointTcs.TrySetResult(_server.Url);
                                }
                            });
                        }

                        using var stream = await response.Content.ReadAsStreamAsync(_cts.Token);
                        using var reader = new StreamReader(stream);

                        string? currentEvent = null;
                        while (!_cts.Token.IsCancellationRequested)
                        {
                            var line = await reader.ReadLineAsync(_cts.Token);
                            if (line == null)
                            {
                                break;
                            }

                            if (line.StartsWith("event:"))
                            {
                                currentEvent = line.Substring(6).Trim();
                                continue;
                            }

                            if (line.StartsWith("data:"))
                            {
                                var data = line.Substring(5).Trim();
                                await ProcessEventDataAsync(currentEvent, data, onMessageReceived);
                                continue;
                            }

                            if (string.IsNullOrEmpty(line))
                            {
                                currentEvent = null;
                            }
                        }
                        _logger.LogWarning("Disconnected from backend {ServerId} (clean EOF). Reconnecting in 5s...", _server.Id);
                        _messageUrl = null;
                        _stateManager.MarkDisconnected();
                        await Task.Delay(5000, _cts.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Disconnected from backend {ServerId}. Reconnecting in 5s... Error: {Msg}", _server.Id, ex.Message);
                        _messageUrl = null;
                        _stateManager.MarkDisconnected();
                        await Task.Delay(5000, _cts.Token);
                    }
                }
            });

            _ = Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), _cts.Token);
                    try
                    {
                        var resp = await CallMethodAsync("ping", new { });
                        if (resp.Error != null)
                        {
                            _logger.LogWarning("Ping failed for backend {ServerId}: {Code} {Message}", _server.Id, resp.Error.Code, resp.Error.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Ping exception for backend {ServerId}", _server.Id);
                    }
                }
            });
        }

        private static object? GetJsonElementValue(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out long l))
                    {
                        return l;
                    }

                    if (element.TryGetDouble(out double d))
                    {
                        return d;
                    }

                    return element.GetRawText();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                default:
                    return element.GetRawText();
            }
        }

        public async Task<JsonRpcResponse> SendRequestAsync(string method, string bodyJson, string? targetAuthToken = null)
        {
            if (_stateManager.IsDisconnected)
            {
                return new JsonRpcResponse { Error = new JsonRpcError { Code = -32001, Message = "Not connected" } };
            }

            if (_messageUrl == null)
            {
                using var ctsTimeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                ctsTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    await _endpointTcs.Task.WaitAsync(ctsTimeout.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("Timeout waiting for SSE endpoint URL.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Exception waiting for SSE endpoint URL.");
                }
            }

            if (_messageUrl == null)
            {
                return new JsonRpcResponse { Error = new JsonRpcError { Code = -32001, Message = "Not connected" } };
            }

            string upstreamRequestId = Guid.NewGuid().ToString("N");
            object? originalId = null;
            string modifiedBody = bodyJson;
            bool isNotification = true;

            try
            {
                var node = System.Text.Json.Nodes.JsonNode.Parse(bodyJson);
                if (node is System.Text.Json.Nodes.JsonObject obj)
                {
                    if (obj.ContainsKey("id"))
                    {
                        isNotification = false;
                        var idNode = obj["id"];
                        if (idNode != null)
                        {
                            using var doc = JsonDocument.Parse(idNode.ToJsonString());
                            originalId = GetJsonElementValue(doc.RootElement);
                        }
                        else
                        {
                            originalId = null;
                        }

                        obj["id"] = upstreamRequestId;
                        modifiedBody = node.ToJsonString();
                    }
                    else if (obj.ContainsKey("method") && !method.StartsWith("notifications/", StringComparison.OrdinalIgnoreCase))
                    {
                        isNotification = false;
                        originalId = null;
                        obj["id"] = upstreamRequestId;
                        modifiedBody = node.ToJsonString();
                    }
                    else
                    {
                        isNotification = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse/rewrite JSON ID in SendRequestAsync");
            }

            if (isNotification)
            {
                var content = new StringContent(modifiedBody, Encoding.UTF8, "application/json");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                using var req = new HttpRequestMessage(HttpMethod.Post, _messageUrl) { Content = content };
                req.Headers.Host = "localhost";
                req.Headers.Accept.Clear();
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (!string.IsNullOrEmpty(_sessionId))
                {
                    req.Headers.TryAddWithoutValidation("Mcp-Session-Id", _sessionId);
                }

                await ApplyAuthAndCustomHeadersAsync(req);
                if (!string.IsNullOrEmpty(targetAuthToken))
                {
                    req.Headers.Add("X-Target-Auth", targetAuthToken);
                }

                using var res = await _httpClient.SendAsync(req, _cts.Token);
                res.EnsureSuccessStatusCode();

                return new JsonRpcResponse();
            }

            var tcs = _stateManager.CreateTrackedRequest(upstreamRequestId, originalId, _sessionId, _cts.Token, RequestTimeout);

            try
            {
                var content = new StringContent(modifiedBody, Encoding.UTF8, "application/json");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                using var req = new HttpRequestMessage(HttpMethod.Post, _messageUrl) { Content = content };
                req.Headers.Host = "localhost";
                req.Headers.Accept.Clear();
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (!string.IsNullOrEmpty(_sessionId))
                {
                    req.Headers.TryAddWithoutValidation("Mcp-Session-Id", _sessionId);
                }

                await ApplyAuthAndCustomHeadersAsync(req);
                if (!string.IsNullOrEmpty(targetAuthToken))
                {
                    req.Headers.Add("X-Target-Auth", targetAuthToken);
                }

                _logger.LogDebug("[JSON-RPC Gateway -> Backend {ServerId}] {Payload}", _server.Id, PiiSanitizer.SanitizePayload(modifiedBody));

                using var res = await _httpClient.SendAsync(req, _cts.Token);
                res.EnsureSuccessStatusCode();

                var response = await tcs.Task.WaitAsync(RequestTimeout, _cts.Token);
                var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                _logger.LogDebug("[JSON-RPC Backend {ServerId} -> Gateway] {Payload}", _server.Id, PiiSanitizer.SanitizePayload(responseJson));
                return response;
            }
            finally
            {
                _stateManager.TryRemoveRequest(upstreamRequestId);
            }
        }

        public async Task<JsonRpcResponse> CallMethodAsync(string method, object parameters, string? overrideId = null)
        {
            string upstreamRequestId = Guid.NewGuid().ToString("N");
            object? originalId = overrideId ?? Guid.NewGuid().ToString("N");

            var bodyObj = new { jsonrpc = "2.0", method = method, @params = parameters, id = upstreamRequestId };
            var bodyJson = JsonSerializer.Serialize(bodyObj);

            if (_stateManager.IsDisconnected)
            {
                throw new InvalidOperationException($"Backend {_server.Id} is disconnected.");
            }

            if (_messageUrl == null)
            {
                using var ctsTimeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                ctsTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    await _endpointTcs.Task.WaitAsync(ctsTimeout.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("Timeout waiting for SSE endpoint URL.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Exception waiting for SSE endpoint URL.");
                }
            }

            if (_messageUrl == null)
            {
                throw new InvalidOperationException($"Backend {_server.Id} has not sent its endpoint event yet.");
            }

            var tcs = _stateManager.CreateTrackedRequest(upstreamRequestId, originalId, _sessionId, _cts.Token, RequestTimeout);

            try
            {
                var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                using var postReq = new HttpRequestMessage(HttpMethod.Post, _messageUrl) { Content = content };
                postReq.Headers.Host = "localhost";
                postReq.Headers.Accept.Clear();
                postReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                postReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
                if (!string.IsNullOrEmpty(_sessionId))
                {
                    postReq.Headers.Add("Mcp-Session-Id", _sessionId);
                }

                await ApplyAuthAndCustomHeadersAsync(postReq);

                using var res = await _httpClient.SendAsync(postReq, _cts.Token);
                res.EnsureSuccessStatusCode();

                return await tcs.Task.WaitAsync(RequestTimeout, _cts.Token);
            }
            finally
            {
                _stateManager.TryRemoveRequest(upstreamRequestId);
            }
        }

        public async Task SendNotificationAsync(string method, string bodyJson)
        {
            if (_messageUrl == null)
            {
                return;
            }

            var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var req = new HttpRequestMessage(HttpMethod.Post, _messageUrl) { Content = content };
            req.Headers.Host = "localhost";
            await ApplyAuthAndCustomHeadersAsync(req);
            if (!string.IsNullOrEmpty(_sessionId))
            {
                req.Headers.TryAddWithoutValidation("Mcp-Session-Id", _sessionId);
            }

            using var res = await _httpClient.SendAsync(req, _cts.Token);
            res.EnsureSuccessStatusCode();
        }

        public async Task SendResponseAsync(string responseJson)
        {
            if (_messageUrl == null)
            {
                return;
            }

            var content = new StringContent(responseJson, Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var req = new HttpRequestMessage(HttpMethod.Post, _messageUrl) { Content = content };
            req.Headers.Host = "localhost";
            await ApplyAuthAndCustomHeadersAsync(req);
            if (!string.IsNullOrEmpty(_sessionId))
            {
                req.Headers.TryAddWithoutValidation("Mcp-Session-Id", _sessionId);
            }

            using var res = await _httpClient.SendAsync(req, _cts.Token);
            res.EnsureSuccessStatusCode();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _stateManager.MarkDisconnected();
            _cts.Dispose();
        }
    }
}


