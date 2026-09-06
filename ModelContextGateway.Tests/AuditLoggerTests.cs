using Dapper;
using Microsoft.Data.Sqlite;
using Moq;

namespace ModelContextGateway.Tests
{
    public class AuditLoggerTests : IDisposable
    {
        private const string ConnectionString = "Data Source=InMemoryAuditDb;Mode=Memory;Cache=Shared";
        private readonly SqliteConnection _masterConnection;
        private readonly IDbConnectionFactory _dbFactory;

        public AuditLoggerTests()
        {
            _masterConnection = new SqliteConnection(ConnectionString);
            _masterConnection.Open();

            _masterConnection.Execute(@"
                CREATE TABLE IF NOT EXISTS AdminAuditLogs (
                    Id TEXT PRIMARY KEY,
                    Username TEXT,
                    Action TEXT,
                    Target TEXT,
                    Details TEXT,
                    Success INTEGER,
                    ErrorMessage TEXT,
                    Timestamp TEXT DEFAULT CURRENT_TIMESTAMP
                );
                CREATE TABLE IF NOT EXISTS AuditLogs (
                    RequestId TEXT PRIMARY KEY,
                    UserPrincipalName TEXT,
                    UserSid TEXT,
                    ServerCodeName TEXT,
                    ItemName TEXT,
                    RequestMethod TEXT,
                    ExecutionTimeMs INTEGER,
                    StatusCode INTEGER,
                    RequestPayload TEXT,
                    ResponsePayload TEXT,
                    ErrorMessage TEXT,
                    Timestamp TEXT DEFAULT CURRENT_TIMESTAMP
                );");

            var mockFactory = new Mock<IDbConnectionFactory>();
            mockFactory.Setup(f => f.CreateConnection()).Returns(() =>
            {
                var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                return conn;
            });
            mockFactory.Setup(f => f.ProviderName).Returns("sqlite");
            _dbFactory = mockFactory.Object;
        }

        public void Dispose()
        {
            _masterConnection.Dispose();
        }

        [Fact]
        [Requirement("SEC-AUDIT-LOG-INVOCATION-RECORD", "SEC", RequirementType.Positive, "AuditLogger writes tool invocation audit records with actor attribution and duration to AuditLogs table.")]
        public async Task LogInvocationAsync_WritesEntryToDatabase()
        {
            var auditLogger = new AuditLogger(_dbFactory);
            await auditLogger.LogInvocationAsync("req-1", "admin@local", "S-1-5-21", "docker", "list_containers", "tools/call", 15, 200, "{}", "{}");

            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            var entry = await conn.QueryFirstOrDefaultAsync<dynamic>("SELECT * FROM AuditLogs WHERE RequestId = 'req-1';");
            Assert.NotNull(entry);
            Assert.Equal("admin@local", (string)entry!.UserPrincipalName);
            Assert.Equal("list_containers", (string)entry!.ItemName);
        }

        [Fact]
        [Requirement("SEC-AUDIT-LOG-ADMIN-ACTION-RECORD", "SEC", RequirementType.Positive, "AuditLogger records administrative configuration changes and security events to AdminAuditLogs table.")]
        public async Task LogAdminActionAsync_WritesEntryToDatabase()
        {
            var auditLogger = new AuditLogger(_dbFactory);
            await auditLogger.LogAdminActionAsync("admin", "CreateAppKey", "key-123", "{\"scopes\":[\"read\"]}", true);

            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            var entry = await conn.QueryFirstOrDefaultAsync<dynamic>("SELECT * FROM AdminAuditLogs WHERE Username = 'admin';");
            Assert.NotNull(entry);
            Assert.Equal("CreateAppKey", (string)entry!.Action);
            Assert.Equal("key-123", (string)entry!.Target);
            Assert.Equal(1L, (long)entry!.Success);
        }

        [Fact]
        [Requirement("GUARD-04", "GUARD", RequirementType.Negative, "AuditLogger fails closed and throws InvalidOperationException when audit log database is unreachable.")]
        public async Task LogInvocationAsync_ThrowsInvalidOperationException_OnConnectionFailure()
        {
            var failingFactory = new Mock<IDbConnectionFactory>();
            failingFactory.Setup(f => f.CreateConnection()).Throws(new Exception("DB connection failed"));
            failingFactory.Setup(f => f.ProviderName).Returns("sqlite");

            var logger = new AuditLogger(failingFactory.Object);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                logger.LogInvocationAsync("req-2", "admin@local", "S-1", "s1", "item", "method", 10, 200));
        }

        [Fact]
        [Requirement("GUARD-04", "GUARD", RequirementType.Negative, "AuditLogger fails closed and throws InvalidOperationException when admin audit log persistence fails.")]
        public async Task LogAdminActionAsync_ThrowsInvalidOperationException_OnConnectionFailure()
        {
            var failingFactory = new Mock<IDbConnectionFactory>();
            failingFactory.Setup(f => f.CreateConnection()).Throws(new Exception("DB connection failed"));
            failingFactory.Setup(f => f.ProviderName).Returns("sqlite");

            var logger = new AuditLogger(failingFactory.Object);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                logger.LogAdminActionAsync("admin", "DeleteKey", "k1", "{}", false));
        }
    }
}
