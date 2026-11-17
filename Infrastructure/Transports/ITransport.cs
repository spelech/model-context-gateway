namespace ModelContextGateway.Infrastructure.Transports
{
    public interface ITransport : IDisposable
    {
        Task ConnectAsync();
        void StartReader(Func<JsonRpcMessage, Task> onMessageReceived);
        Task<JsonRpcResponse> SendRequestAsync(string method, string body, string? targetAuthToken = null, CancellationToken cancellationToken = default, TimeSpan? timeoutOverride = null);
        Task SendNotificationAsync(string method, string body);
        Task SendResponseAsync(string responseJson);
        TimeSpan RequestTimeout { get; set; }
    }
}
