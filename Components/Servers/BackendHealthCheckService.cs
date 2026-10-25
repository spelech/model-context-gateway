using Dapper;

namespace ModelContextGateway.Components.Servers
{
    /// <summary>
    /// Background hosted service that periodically probes downstream backend MCP servers and updates their health status.
    /// </summary>
    public class BackendHealthCheckService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly SessionManager _sessionManager;
        private readonly ILogger<BackendHealthCheckService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackendHealthCheckService"/> class.
        /// </summary>
        /// <param name="serviceProvider">The application service provider scope factory.</param>
        /// <param name="httpClientFactory">The HTTP client factory for sending health probes.</param>
        /// <param name="sessionManager">The session manager tracking active backend statuses.</param>
        /// <param name="logger">The logger instance.</param>
        public BackendHealthCheckService(
            IServiceProvider serviceProvider,
            IHttpClientFactory httpClientFactory,
            SessionManager sessionManager,
            ILogger<BackendHealthCheckService> logger)
        {
            _serviceProvider = serviceProvider;
            _httpClientFactory = httpClientFactory;
            _sessionManager = sessionManager;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting Backend MCP Server Health Check background service...");

            // Immediate probe on startup
            await ProbeAllServersAsync();

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProbeAllServersAsync();
            }
        }

        public async Task ProbeAllServersAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                List<McpServer> servers = new();
                try
                {
                    var dbFactory = scope.ServiceProvider.GetService<IDbConnectionFactory>();
                    if (dbFactory != null)
                    {
                        using var conn = dbFactory.CreateConnection();
                        var rawServers = (await conn.QueryAsync(@"SELECT * FROM Servers")).ToList();

                        servers = rawServers.Select(s => new McpServer
                        {
                            Id = Convert.ToString(s.Id) ?? string.Empty,
                            DisplayName = Convert.ToString(s.DisplayName) ?? string.Empty,
                            Url = Convert.ToString(s.Url) ?? string.Empty,
                            Enabled = s.Enabled is long l ? l != 0L : Convert.ToBoolean(s.Enabled),
                            Hidden = s.Hidden is long lh ? lh != 0L : Convert.ToBoolean(s.Hidden),
                            Type = Convert.ToString(s.Type) ?? "sse",
                            ApiKey = Convert.ToString(s.ApiKey),
                            HealthCheckMode = (s as IDictionary<string, object>)?.ContainsKey("HealthCheckMode") == true
                                ? (Convert.ToString((s as dynamic).HealthCheckMode) ?? "ping")
                                : "ping"
                        }).Where(s => s.Enabled).ToList();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to query servers for health check probe");
                }

                var tasks = servers.Select(s => ProbeServerAsync(s));
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occurred during backend servers health check sweep");
            }
        }

        public async Task ProbeServerAsync(McpServer server)
        {
            if (!server.Enabled)
            {
                _sessionManager.UpdateBackendStatus(server.Id, "Disabled", 0, "");
                return;
            }

            if (server.Type == "custom")
            {
                _sessionManager.UpdateBackendStatus(server.Id, "Connected", 1, "");
                return;
            }

            if (server.Type == "stdio")
            {
                if (!ServerValidationHelper.IsValidStdioCommand(server.Url, out var stdioErr))
                {
                    _sessionManager.UpdateBackendStatus(server.Id, "Failed", 1, stdioErr ?? "Invalid STDIO command");
                    return;
                }

                _sessionManager.UpdateBackendStatus(server.Id, "Connected", 1, "");
                return;
            }

            if (string.Equals(server.HealthCheckMode, "none", StringComparison.OrdinalIgnoreCase))
            {
                _sessionManager.UpdateBackendStatus(server.Id, "Connected", 1, "");
                return;
            }

            try
            {
                var client = _httpClientFactory.CreateClient("McpClient");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                bool useRootProbe = string.Equals(server.HealthCheckMode, "root", StringComparison.OrdinalIgnoreCase) ||
                                    server.Id.StartsWith("postgres-", StringComparison.OrdinalIgnoreCase);

                if (!useRootProbe && Uri.TryCreate(server.Url, UriKind.Absolute, out var targetUri))
                {
                    var pingPayload = "{\"jsonrpc\":\"2.0\",\"id\":\"health-ping\",\"method\":\"ping\"}";
                    using var pingRequest = new HttpRequestMessage(HttpMethod.Post, server.Url)
                    {
                        Content = new StringContent(pingPayload, System.Text.Encoding.UTF8, "application/json")
                    };
                    pingRequest.Headers.Accept.Clear();
                    pingRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    if (!string.IsNullOrEmpty(server.ApiKey))
                    {
                        pingRequest.Headers.Add("Authorization", $"Bearer {server.ApiKey}");
                        pingRequest.Headers.Add("X-API-Key", server.ApiKey);
                    }

                    using var pingResponse = await client.SendAsync(pingRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                    if (pingResponse.IsSuccessStatusCode)
                    {
                        _sessionManager.UpdateBackendStatus(server.Id, "Connected", 1, "");
                        return;
                    }

                    _logger.LogDebug("Server {ServerId} returned {StatusCode} on JSON-RPC ping; falling back to root probe.", server.Id, pingResponse.StatusCode);
                }

                var probeUrl = server.Url;
                if (Uri.TryCreate(server.Url, UriKind.Absolute, out var uri))
                {
                    // Probe the base authority root (e.g. http://frigate-mcp:8086/) rather than /mcp or /sse
                    // to avoid initiating unbounded client streaming sessions on downstream Python MCP SDK transports.
                    probeUrl = $"{uri.Scheme}://{uri.Authority}/";
                }

                var request = new HttpRequestMessage(HttpMethod.Get, probeUrl);
                if (!string.IsNullOrEmpty(server.ApiKey))
                {
                    request.Headers.Add("Authorization", $"Bearer {server.ApiKey}");
                    request.Headers.Add("X-API-Key", server.ApiKey);
                }

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                // Any response means the server container / network process is listening and healthy
                _sessionManager.UpdateBackendStatus(server.Id, "Connected", 1, "");
            }
            catch (Exception ex)
            {
                _sessionManager.UpdateBackendStatus(server.Id, "Failed", 1, ex.Message);
            }
        }
    }
}


