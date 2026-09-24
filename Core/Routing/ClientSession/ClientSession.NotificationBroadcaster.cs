using System.Text.Json;

namespace ModelContextGateway.Core.Routing
{
    public partial class ClientSession
    {
        public async Task WriteMessageAsync(object message)
        {
            if (_sessionId == "global-stateless-session" || _clientResponse == null)
            {
                return;
            }

            await _writeLock.WaitAsync();
            try
            {
                if (_clientResponse.HttpContext?.RequestAborted.IsCancellationRequested == true)
                {
                    return;
                }
                var json = JsonSerializer.Serialize(message, _jsonOptions);
                _logger.LogDebug("[JSON-RPC Gateway -> Client] {Payload}", PiiSanitizer.SanitizePayload(json));
                _sessionManager?.AddPerformanceMetrics(0, json.Length / 4, 0);
                await _clientResponse.WriteAsync($"event: message\ndata: {json}\n\n");
                await _clientResponse.Body.FlushAsync();
            }
            catch (ObjectDisposedException)
            {
                // Client connection closed cleanly
            }
            catch (OperationCanceledException)
            {
                // Request cancelled
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Notice writing to client SSE stream: {Message}", ex.Message);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task BroadcastNotificationAsync(string method, string body)
        {
            var tasks = _backendConnections.Select(async entry =>
            {
                try
                {
                    await entry.Value.SendNotificationAsync(method, body);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Upstream server '{ServerId}' failed to receive notification '{Method}': {Message}", entry.Key, method, ex.Message);
                }
            });
            await Task.WhenAll(tasks);
        }
    }
}


