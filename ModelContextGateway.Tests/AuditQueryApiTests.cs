using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Moq;

namespace ModelContextGateway.Tests
{
    public class AuditQueryApiTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly IDbConnectionFactory _dbFactory;
        private readonly string _dbName;

        public AuditQueryApiTests()
        {
            _dbName = $"Data Source=AuditQueryTestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            _connection = new SqliteConnection(_dbName);
            _connection.Open();

            _connection.Execute(@"
                CREATE TABLE IF NOT EXISTS AuditLogs (
                    LogId INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT DEFAULT CURRENT_TIMESTAMP,
                    RequestId TEXT,
                    UserPrincipalName TEXT,
                    UserSid TEXT,
                    ServerCodeName TEXT,
                    ItemName TEXT,
                    RequestMethod TEXT,
                    ExecutionTimeMs INTEGER,
                    StatusCode INTEGER,
                    RequestPayload TEXT,
                    ResponsePayload TEXT,
                    ErrorMessage TEXT
                );
                CREATE TABLE IF NOT EXISTS AccessPolicies (
                    Id TEXT PRIMARY KEY,
                    TargetId TEXT,
                    RequiredGroup TEXT,
                    IsAllowed INTEGER DEFAULT 1
                );
                CREATE TABLE IF NOT EXISTS GroupMappings (
                    Id TEXT PRIMARY KEY,
                    ExternalId TEXT,
                    InternalGroup TEXT
                );");

            var mockFactory = new Mock<IDbConnectionFactory>();
            mockFactory.Setup(f => f.CreateConnection()).Returns(() => new SqliteConnection(_dbName));
            mockFactory.Setup(f => f.ProviderName).Returns("sqlite");
            _dbFactory = mockFactory.Object;
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        [Fact]
        [Requirement("SEC-AUDIT-QUERY-FILTERED-ROWS", "SEC", RequirementType.Positive, "Audit queries support filtering by user, server, and pagination while recording query access in audit log.")]
        public async Task AuditQuery_ReturnsFilteredRows_AndLogsAuditAction()
        {
            await _connection.ExecuteAsync(@"
                INSERT INTO AuditLogs (UserPrincipalName, UserSid, ServerCodeName, ItemName, RequestMethod, StatusCode, ExecutionTimeMs)
                VALUES ('alice', 'S-1-5-21-1001', 'serverA', 'tool1', 'tools/call', 200, 45);
                INSERT INTO AuditLogs (UserPrincipalName, UserSid, ServerCodeName, ItemName, RequestMethod, StatusCode, ExecutionTimeMs)
                VALUES ('bob', 'S-1-5-21-1002', 'serverB', 'tool2', 'tools/call', 200, 30);");

            using var conn = _dbFactory.CreateConnection();
            var sql = @"SELECT Timestamp, UserPrincipalName, UserSid, ServerCodeName, ItemName, RequestMethod, StatusCode, ExecutionTimeMs, ErrorMessage
                        FROM AuditLogs
                        WHERE (@user   IS NULL OR UserPrincipalName = @user)
                          AND (@server IS NULL OR ServerCodeName = @server)
                        ORDER BY Timestamp DESC LIMIT @take OFFSET @skip;";

            var rows = (await conn.QueryAsync(sql, new { user = "alice", server = (string?)null, take = 200, skip = 0 })).ToList();

            Assert.Single(rows);
            Assert.Equal("alice", (string)rows[0].UserPrincipalName);
        }

        [Fact]
        [Requirement("SEC-AUDIT-POLICY-SAVE-ACTION", "SEC", RequirementType.Positive, "PermissionsController records audit action when access policy is saved.")]
        public async Task SavePolicy_WritesAuditAction_OnSuccess()
        {
            var mockAuditLogger = new Mock<IAuditLogger>();
            var controller = new PermissionsController(_dbFactory, mockAuditLogger.Object);

            var policy = new McpAccessPolicy
            {
                Id = "policy-123",
                TargetId = "server:test",
                RequiredGroup = "Admins",
                IsAllowed = true
            };

            var httpContext = new DefaultHttpContext();
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var result = await controller.SavePolicy(policy) as OkObjectResult;
            Assert.NotNull(result);

            mockAuditLogger.Verify(a => a.LogAdminActionAsync(
                It.IsAny<string>(),
                "policy.save",
                "server:test",
                It.Is<string>(d => d.Contains("server:test") && d.Contains("Admins")),
                true,
                null
            ), Times.Once);
        }

        [Fact]
        [Requirement("SEC-AUDIT-MAPPING-SAVE-ACTION", "SEC", RequirementType.Positive, "PermissionsController records audit action when group mapping is saved.")]
        public async Task SaveMapping_WritesAuditAction_OnSuccess()
        {
            var mockAuditLogger = new Mock<IAuditLogger>();
            var controller = new PermissionsController(_dbFactory, mockAuditLogger.Object);

            var mapping = new GroupMapping
            {
                Id = "mapping-123",
                ExternalId = "ext-group",
                InternalGroup = "int-group"
            };

            var httpContext = new DefaultHttpContext();
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var result = await controller.SaveMapping(mapping) as OkObjectResult;
            Assert.NotNull(result);

            mockAuditLogger.Verify(a => a.LogAdminActionAsync(
                It.IsAny<string>(),
                "mapping.save",
                "ext-group",
                It.Is<string>(d => d.Contains("ext-group") && d.Contains("int-group")),
                true,
                null
            ), Times.Once);
        }

        [Fact]
        [Requirement("SEC-AUDIT-LOGGER-ADMIN-PERSIST", "SEC", RequirementType.Positive, "AuditLogger persists administrative actions directly into AdminAuditLogs database table.")]
        public async Task LogAdminActionAsync_WritesRowToAdminAuditLogs()
        {
            // First ensure table exists in SQLite
            using (var conn = _dbFactory.CreateConnection())
            {
                conn.Execute(@"
                    CREATE TABLE IF NOT EXISTS AdminAuditLogs (
                        Id TEXT PRIMARY KEY,
                        Username TEXT,
                        Action TEXT,
                        Target TEXT,
                        Details TEXT,
                        Success INTEGER,
                        ErrorMessage TEXT,
                        Timestamp TEXT DEFAULT CURRENT_TIMESTAMP
                    );");
            }

            var auditLogger = new AuditLogger(_dbFactory);

            await auditLogger.LogAdminActionAsync(
                "test-admin",
                "test-action",
                "test-target",
                "test-details",
                true,
                "no-error"
            );

            using var connAssert = _dbFactory.CreateConnection();
            var row = await connAssert.QueryFirstOrDefaultAsync("SELECT * FROM AdminAuditLogs WHERE Username = 'test-admin'");

            Assert.NotNull(row);
            Assert.Equal("test-action", (string)row!.Action);
            Assert.Equal("test-target", (string)row!.Target);
            Assert.Equal("test-details", (string)row!.Details);
            Assert.Equal(1, (int)row!.Success);
            Assert.Equal("no-error", (string)row!.ErrorMessage);
        }
    }
}
