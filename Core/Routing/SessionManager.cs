using System.Collections.Concurrent;
using Dapper;

namespace ModelContextGateway.Core.Routing
{
    public partial class SessionManager
    {
        private readonly ConcurrentDictionary<string, ClientSession> _sessions = new();
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SessionManager> _logger;

        public int ActiveSessionsCount => _sessions.Count;

        public ConcurrentDictionary<string, BackendStatus> BackendStatuses { get; } = new();

        public SessionManager(IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory, ILogger<SessionManager> logger)
        {
            _serviceProvider = serviceProvider;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public void UpdateBackendStatus(string serverId, string status, int attempts, string error)
        {
            var bStatus = BackendStatuses.GetOrAdd(serverId, id => new BackendStatus { ServerId = id });
            bStatus.Status = status;
            bStatus.Attempts = attempts;
            bStatus.Error = error;
        }

        public async Task<ClientSession> CreateSessionAsync(string sessionId, HttpResponse clientResponse, string? targetServerId = null, bool metaMode = false)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var conn = dbFactory.CreateConnection();
            var rawServers = await conn.QueryAsync<McpServer>("SELECT * FROM Servers WHERE Enabled = 1");
            var servers = rawServers.ToList();

            if (!string.IsNullOrWhiteSpace(targetServerId))
            {
                servers = servers.Where(s =>
                    string.Equals(s.Id, targetServerId, StringComparison.OrdinalIgnoreCase) ||
                    (s.Categories != null && s.Categories.Any(c => string.Equals(c, targetServerId, StringComparison.OrdinalIgnoreCase)))
                ).ToList();
            }

            var sessionLogger = _serviceProvider.GetRequiredService<ILogger<ClientSession>>();
            var client = _httpClientFactory.CreateClient("McpClient");
            var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();

            var session = new ClientSession(sessionId, clientResponse, servers, client, embeddingService, this, sessionLogger, _serviceProvider);
            session.IsMetaMode = metaMode;

            if (_sessions.TryRemove(sessionId, out var oldSession))
            {
                try
                {
                    oldSession.Close();
                }
                catch (Exception exClose)
                {
                    _logger.LogWarning(exClose, "Error closing overwritten session for ID {SessionId}", sessionId);
                }
            }

            _sessions[sessionId] = session;
            return session;
        }

        public void RegisterSession(string sessionId, ClientSession session)
        {
            _sessions[sessionId] = session;
        }

        public ClientSession? GetSession(string sessionId)
        {
            _sessions.TryGetValue(sessionId, out var session);
            return session;
        }

        public void CloseSession(string sessionId)
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                session.Close();
            }
        }

        public System.Collections.Generic.List<ClientSession> GetActiveSessions()
        {
            return _sessions.Values.ToList();
        }

        public void ResetAll()
        {
            _logger.LogInformation("Resetting all active MCP client sessions due to configuration change.");
            var keys = _sessions.Keys.ToList();
            foreach (var key in keys)
            {
                CloseSession(key);
            }
        }
    }
}
