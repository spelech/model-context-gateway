using System.Collections.Concurrent;
using System.Text.Json;

namespace ModelContextGateway.Core.Routing
{
    public class BackendConnection : IDisposable
    {
        private readonly ITransport _transport;
        private readonly JsonRpcStateManager _stateManager;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            Converters = { new JsonRpcMessageConverter() }
        };

        public ConcurrentDictionary<string, TaskCompletionSource<JsonRpcResponse>> PendingRequests => _stateManager.PendingRequests;
        public TimeSpan RequestTimeout { get => _transport.RequestTimeout; set => _transport.RequestTimeout = value; }

        public BackendConnection(McpServer server, HttpClient httpClient, ILogger logger, ISecretRetriever? secretRetriever = null, string? passThroughToken = null, string? forwardedUser = null, IUserSecretStore? userSecretStore = null)
        {
            _stateManager = new JsonRpcStateManager();

            if (server.Type == "http" || server.Type == "custom" || server.Type == "streamable")
            {
                _transport = new HttpTransport(server, httpClient, logger, secretRetriever, passThroughToken, null, forwardedUser, userSecretStore);
            }
            else if (server.Type == "sse")
            {
                _transport = new SseTransport(server, httpClient, logger, _stateManager, secretRetriever, passThroughToken, forwardedUser, userSecretStore);
            }
            else if (server.Type == "stdio")
            {
                _transport = new StdioTransport(server, logger, _stateManager, secretRetriever, passThroughToken);
            }
            else
            {
                throw new NotSupportedException($"Transport type '{server.Type}' is not supported by the gateway.");
            }
        }

        public async Task ConnectAsync()
        {
            await _transport.ConnectAsync();
        }

        public bool TryCompleteRequest(string id, JsonRpcResponse? response)
        {
            return _stateManager.TryCompleteRequest(id, response);
        }

        public void StartReader(Func<JsonRpcMessage, Task> onMessageReceived)
        {
            _transport.StartReader(async (message) =>
            {
                if (message is JsonRpcResponse response && response.Id != null)
                {
                    var idStr = response.Id is JsonElement je
                        ? (je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText())
                        : response.Id?.ToString();
                    if (idStr != null)
                    {
                        if (_stateManager.TryCompleteRequest(idStr, response))
                        {
                            return;
                        }
                    }
                }

                await onMessageReceived(message);
            });
        }

        public async Task<JsonRpcResponse> SendRequestAsync(string method, string bodyJson, string? targetAuthToken = null)
        {
            return await _transport.SendRequestAsync(method, bodyJson, targetAuthToken);
        }

        public async Task<JsonRpcResponse> CallMethodAsync(string method, object parameters, string? overrideId = null)
        {
            if (_transport is SseTransport sse)
            {
                return await sse.CallMethodAsync(method, parameters, overrideId);
            }
            else if (_transport is HttpTransport http)
            {
                return await http.CallMethodAsync(method, parameters, overrideId);
            }
            else if (_transport is StdioTransport stdio)
            {
                return await stdio.CallMethodAsync(method, parameters, overrideId);
            }

            throw new NotSupportedException();
        }

        public async Task SendNotificationAsync(string method, string bodyJson)
        {
            await _transport.SendNotificationAsync(method, bodyJson);
        }

        public async Task SendResponseAsync(JsonRpcResponse response)
        {
            var json = JsonSerializer.Serialize(response, _jsonOptions);
            await _transport.SendResponseAsync(json);
        }

        public void Dispose()
        {
            _transport.Dispose();
        }
    }
}
