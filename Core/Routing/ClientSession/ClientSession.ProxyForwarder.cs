using System.Text.Json;

namespace ModelContextGateway.Core.Routing
{
    /// <summary>
    /// Partial class implementation providing request cancellation, client sampling request forwarding, and backend broadcast capabilities.
    /// </summary>
    public partial class ClientSession
    {
        /// <summary>
        /// Registers a cancellation token source for an active request, scoped by session and optional trace identifier.
        /// </summary>
        public bool RegisterRequestCancellation(string requestId, CancellationTokenSource cts, string? traceIdentifier = null)
        {
            var scopeId = _sessionId;
            if (_sessionId == "global-stateless-session" && !string.IsNullOrEmpty(traceIdentifier))
            {
                scopeId = $"{_sessionId}:{traceIdentifier}";
            }
            var cancellationKey = $"{scopeId}:{requestId}";
            return _activeRequestCancellationTokens.TryAdd(cancellationKey, cts);
        }

        /// <summary>
        /// Cancels an active request by triggering its registered cancellation token source.
        /// </summary>
        /// <param name="requestId">The unique request ID to cancel.</param>
        /// <param name="traceIdentifier">Optional HTTP trace identifier for scoping stateless requests.</param>
        public void CancelRequest(string requestId, string? traceIdentifier = null)
        {
            if (!string.IsNullOrEmpty(traceIdentifier))
            {
                var targetKey = $"{_sessionId}:{traceIdentifier}:{requestId}";
                if (_activeRequestCancellationTokens.TryRemove(targetKey, out var ctsTrace))
                {
                    try
                    {
                        ctsTrace.Cancel();
                        _logger.LogInformation("Cancelled active request: {Key}", targetKey);
                    }
                    catch (ObjectDisposedException) { }
                    return;
                }
            }

            var keysToCancel = new List<string>();
            foreach (var key in _activeRequestCancellationTokens.Keys)
            {
                if (key == requestId || key == $"{_sessionId}:{requestId}" || key.EndsWith($":{requestId}"))
                {
                    keysToCancel.Add(key);
                }
            }

            foreach (var key in keysToCancel)
            {
                if (_activeRequestCancellationTokens.TryRemove(key, out var cts))
                {
                    try
                    {
                        cts.Cancel();
                        _logger.LogInformation("Cancelled active request: {Key}", key);
                    }
                    catch (ObjectDisposedException) { }
                }
            }
        }

        public bool TryHandleClientResponse(string requestId, string responseJson)
        {
            if (_clientPendingRequests.TryRemove(requestId, out var tcs))
            {
                try
                {
                    var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson, _jsonOptions);
                    if (response != null)
                    {
                        tcs.SetResult(response);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse client response for ID {RequestId}", requestId);
                }
                tcs.TrySetResult(new JsonRpcResponse { Id = requestId, Error = new JsonRpcError { Code = -32603, Message = "Failed to parse response payload." } });
                return true;
            }
            return false;
        }

        public async Task<JsonRpcResponse> ForwardRequestToClientAsync(JsonRpcRequest request)
        {
            var tcs = new TaskCompletionSource<JsonRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            var requestId = request.Id?.ToString() ?? Guid.NewGuid().ToString("N");

            if (!_clientPendingRequests.TryAdd(requestId, tcs))
            {
                throw new InvalidOperationException($"A client pending request with ID '{requestId}' already exists. Cannot overwrite silently.");
            }

            var clientRequest = new
            {
                jsonrpc = "2.0",
                method = request.Method,
                id = requestId,
                @params = request.Params
            };

            await WriteMessageAsync(clientRequest);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using var reg = cts.Token.Register(() => tcs.TrySetCanceled());
            try
            {
                return await tcs.Task;
            }
            catch (TaskCanceledException)
            {
                return new JsonRpcResponse
                {
                    Id = requestId,
                    Error = new JsonRpcError { Code = -32000, Message = "Sampling request timed out or cancelled by client." }
                };
            }
            finally
            {
                _clientPendingRequests.TryRemove(requestId, out _);
            }
        }

        public async Task<Dictionary<string, JsonElement>> BroadcastRequestAsync(string body)
        {
            var results = new Dictionary<string, JsonElement>();
            var tasks = new List<Task<(string ServerId, JsonElement Result)>>();

            foreach (var entry in _backendConnections)
            {
                var conn = entry.Value;
                var serverId = entry.Key;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var response = await conn.SendRequestAsync("unknown", body);
                        return (serverId, response.Result ?? default(JsonElement));
                    }
                    catch
                    {
                        return (serverId, default(JsonElement));
                    }
                }));
            }

            var completed = await Task.WhenAll(tasks);
            foreach (var item in completed)
            {
                if (item.Result.ValueKind != JsonValueKind.Undefined)
                {
                    results[item.ServerId] = item.Result;
                }
            }
            return results;
        }
    }
}
