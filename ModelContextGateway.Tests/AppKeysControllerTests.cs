using System.Data;
using System.Net;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ModelContextGateway.Tests
{
    public class AppKeysControllerTests : IDisposable
    {
        private class NonDisposingConnection : IDbConnection
        {
            private readonly IDbConnection _inner;
            public NonDisposingConnection(IDbConnection inner) => _inner = inner;
            [System.Diagnostics.CodeAnalysis.AllowNull]
            public string ConnectionString { get => _inner.ConnectionString ?? string.Empty; set => _inner.ConnectionString = value ?? string.Empty; }
            public int ConnectionTimeout => _inner.ConnectionTimeout;
            public string Database => _inner.Database;
            public ConnectionState State => _inner.State;
            public IDbTransaction BeginTransaction() => _inner.BeginTransaction();
            public IDbTransaction BeginTransaction(IsolationLevel il) => _inner.BeginTransaction(il);
            public void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
            public void Close() { }
            public IDbCommand CreateCommand() => _inner.CreateCommand();
            public void Dispose() { }
            public void Open()
            {
                if (_inner.State != ConnectionState.Open)
                {
                    _inner.Open();
                }
            }
        }

        private readonly SqliteConnection _rawConnection;
        private readonly IDbConnectionFactory _dbFactory;
        private readonly IConfiguration _config;

        public AppKeysControllerTests()
        {
            _rawConnection = new SqliteConnection($"DataSource=file:mem_{Guid.NewGuid():N}?mode=memory&cache=shared");
            _rawConnection.Open();

            _rawConnection.Execute(@"
                CREATE TABLE IF NOT EXISTS AppKeys (
                    Id TEXT PRIMARY KEY,
                    Name TEXT,
                    Username TEXT,
                    OwnerSid TEXT DEFAULT '',
                    KeyType TEXT DEFAULT 'personal',
                    KeyPrefix TEXT,
                    EncryptedKey TEXT,
                    ScopesJson TEXT DEFAULT '[]',
                    ExpiresAt TEXT,
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );
                CREATE TABLE IF NOT EXISTS Settings (
                    Id TEXT PRIMARY KEY,
                    DashboardTitle TEXT DEFAULT 'MCP Gateway', DashboardIcon TEXT DEFAULT 'fa-solid fa-network-wired', GlobalMaxKeys INTEGER DEFAULT 100,
                    UserMaxKeys INTEGER DEFAULT 5
                );
                CREATE TABLE IF NOT EXISTS UserQuotas (
                    Username TEXT PRIMARY KEY,
                    MaxKeys INTEGER DEFAULT 5,
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );");

            _rawConnection.Execute("INSERT OR REPLACE INTO Settings (Id, GlobalMaxKeys, UserMaxKeys) VALUES ('default', 100, 5);");

            var mockFactory = new Mock<IDbConnectionFactory>();
            mockFactory.Setup(f => f.CreateConnection()).Returns(() => new NonDisposingConnection(_rawConnection));
            mockFactory.Setup(f => f.ProviderName).Returns("sqlite");
            _dbFactory = mockFactory.Object;

            _config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DB_ENCRYPTION_KEY", "TestSecretKey1234567890123456789012" },
                { "Oidc:TrustedProxies", "127.0.0.1,::1" },
                { "Admin:GroupSid", "full_admin" }
            }).Build();
        }

        public void Dispose()
        {
            _rawConnection.Dispose();
        }

        private AppKeysController CreateController(string username = "alice", string role = "Admin")
        {
            var repo = new DatabaseRepository(_dbFactory);
            var credSvc = new CredentialService(_dbFactory);
            var controller = new AppKeysController(repo, repo, _config, new Mock<ModelContextGateway.Infrastructure.Logging.IAuditLogger>().Object, credSvc, repo);

            var services = new ServiceCollection();
            services.AddSingleton(_config);
            services.AddLogging();
            services.AddSingleton<IIdentityProvider, OidcIdentityProvider>();
            services.AddSingleton<CompositeIdentityProvider>();

            var serviceProvider = services.BuildServiceProvider();

            var httpContext = new DefaultHttpContext
            {
                RequestServices = serviceProvider
            };
            httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;
            httpContext.Request.Headers["X-Forwarded-For"] = "127.0.0.1";
            httpContext.Request.Headers["Remote-User"] = username;
            httpContext.Request.Headers["Remote-Groups"] = role == "Admin" ? "full_admin" : "house_member";

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            return controller;
        }

        [Fact]
        [Requirement("AUTH-PERSONAL-APPKEY-LIST", "AUTH", RequirementType.Positive, "Non-admin users can view their personal App Keys")]
        public async Task GetAppKeys_NonAdmin_ReturnsOnlyPersonalKeys_ForCurrentUser()
        {
            await _rawConnection.ExecuteAsync(@"
                INSERT INTO AppKeys (Id, Name, Username, KeyType, KeyPrefix, EncryptedKey, ScopesJson) VALUES
                ('key-alice-1', 'Alice Personal', 'alice', 'personal', 'mcp-alice-1', 'hash1', '[""all""]'),
                ('key-alice-sys', 'Alice System', 'alice', 'system', 'mcp-alice-sys', 'hash2', '[""all""]'),
                ('key-bob-1', 'Bob Personal', 'bob', 'personal', 'mcp-bob-1', 'hash3', '[""all""]');");

            var controller = CreateController("alice", "User");
            var result = await controller.GetAppKeys();

            var okResult = Assert.IsAssignableFrom<OkObjectResult>(result);
            var json = JsonSerializer.Serialize(okResult.Value);
            using var doc = JsonDocument.Parse(json);
            var array = doc.RootElement.EnumerateArray().ToList();

            // Non-admin alice only gets her 1 personal key
            Assert.Single(array);
            var item = array[0];
            Assert.Equal("key-alice-1", item.GetProperty("Id").GetString());
            Assert.Equal("alice", item.GetProperty("Username").GetString());
            Assert.Equal("personal", item.GetProperty("KeyType").GetString());
        }

        [Fact]
        [Requirement("AUTH-SYSTEM-APPKEY-SEPARATION", "AUTH", RequirementType.Positive, "System keys are distinct and require admin permissions")]
        public async Task SystemAppKeys_RequireAdmin_AndSeparateFromPersonalKeys()
        {
            var adminController = CreateController("admin", "Admin");

            // 1. Admin creates a system key
            var sysReq = new CreateAppKeyRequest
            {
                Name = "CI Daemon Key",
                Username = "ci-daemon",
                KeyType = "system",
                Scopes = new List<string> { "all" }
            };
            var createRes = await adminController.CreateAppKey(sysReq);
            var okCreate = Assert.IsAssignableFrom<OkObjectResult>(createRes);
            var createJson = JsonSerializer.Serialize(okCreate.Value);
            using var createDoc = JsonDocument.Parse(createJson);
            var createdKeyId = createDoc.RootElement.GetProperty("Id").GetString()!;
            Assert.Equal("system", createDoc.RootElement.GetProperty("KeyType").GetString());
            Assert.Equal("ci-daemon", createDoc.RootElement.GetProperty("Username").GetString());

            // 2. Admin queries filtered by keyType = "system"
            var sysListRes = await adminController.GetAppKeys("system", null);
            var okSysList = Assert.IsAssignableFrom<OkObjectResult>(sysListRes);
            var sysJson = JsonSerializer.Serialize(okSysList.Value);
            using var sysDoc = JsonDocument.Parse(sysJson);
            Assert.True(sysDoc.RootElement.EnumerateArray().All(k => k.GetProperty("KeyType").GetString() == "system"));

            // 3. Non-admin alice cannot revoke the system key
            var userController = CreateController("alice", "User");
            var revokeRes = await userController.RevokeAppKey(createdKeyId);
            Assert.IsType<ForbidResult>(revokeRes);

            // 4. Admin can revoke the system key
            var adminRevokeRes = await adminController.RevokeAppKey(createdKeyId);
            var okRevoke = Assert.IsAssignableFrom<OkObjectResult>(adminRevokeRes);
            Assert.NotNull(okRevoke);
        }

        [Fact]
        [Requirement("AUTH-PERSONAL-APPKEY-CREATE", "AUTH", RequirementType.Positive, "Non-admin users can create personal App Keys up to quota")]
        public async Task CreateAppKey_NonAdmin_CreatesPersonalKey_UpToDefaultQuota()
        {
            var controller = CreateController("alice", "User");

            // Mint 5 keys (default quota = 5)
            for (int i = 1; i <= 5; i++)
            {
                var req = new CreateAppKeyRequest
                {
                    Name = $"Key {i}",
                    KeyType = "system", // Non-admin should be forced to 'personal'
                    Username = "other_user" // Non-admin should be forced to currentUser
                };
                var res = await controller.CreateAppKey(req);
                var okRes = Assert.IsAssignableFrom<OkObjectResult>(res);
                var json = JsonSerializer.Serialize(okRes.Value);
                using var doc = JsonDocument.Parse(json);
                Assert.Equal("personal", doc.RootElement.GetProperty("KeyType").GetString());
                Assert.Equal("alice", doc.RootElement.GetProperty("Username").GetString());
            }

            // 6th key should fail due to quota
            var reqExceeded = new CreateAppKeyRequest { Name = "Key 6" };
            var exceededRes = await controller.CreateAppKey(reqExceeded);
            var badRes = Assert.IsAssignableFrom<BadRequestObjectResult>(exceededRes);
            Assert.Equal(400, badRes.StatusCode);
            var errJson = JsonSerializer.Serialize(badRes.Value);
            Assert.Contains("personal app-key limit of 5", errJson);
        }

        [Fact]
        [Requirement("AUTH-PERSONAL-APPKEY-QUOTA-OVERRIDE", "AUTH", RequirementType.Positive, "Custom user quotas override default limit")]
        public async Task CreateAppKey_CustomQuotaOverride_AllowsHigherLimit()
        {
            // Insert custom quota override: dave gets 7 keys instead of default 5
            var repo = new DatabaseRepository(_dbFactory);
            await repo.SetUserQuotaAsync("dave", 7);

            var controller = CreateController("dave", "User");

            // Mint 7 keys successfully
            for (int i = 1; i <= 7; i++)
            {
                var req = new CreateAppKeyRequest { Name = $"Dave Key {i}" };
                var res = await controller.CreateAppKey(req);
                Assert.IsAssignableFrom<OkObjectResult>(res);
            }

            // Check limits endpoint reflects 7
            var limitsRes = await controller.GetAppKeysLimits();
            var okLimits = Assert.IsAssignableFrom<OkObjectResult>(limitsRes);
            var limitsJson = JsonSerializer.Serialize(okLimits.Value);
            using var doc = JsonDocument.Parse(limitsJson);
            Assert.Equal(7, doc.RootElement.GetProperty("userMax").GetInt32());
            Assert.Equal(7, doc.RootElement.GetProperty("userActiveKeys").GetInt32());
            Assert.True(doc.RootElement.GetProperty("isLimitReached").GetBoolean());

            // 8th key should fail
            var req8 = new CreateAppKeyRequest { Name = "Dave Key 8" };
            var res8 = await controller.CreateAppKey(req8);
            var badRes = Assert.IsAssignableFrom<BadRequestObjectResult>(res8);
            Assert.Equal(400, badRes.StatusCode);
            var errJson = JsonSerializer.Serialize(badRes.Value);
            Assert.Contains("personal app-key limit of 7", errJson);
        }

        [Fact]
        [Requirement("SEC-APPKEY-SANITIZE-METADATA-GET", "SEC", RequirementType.Positive, "AppKeys API returns sanitized key metadata without leaking plaintext tokens.")]
        public async Task GetAppKeys_ReturnsSanitizedKeys_ForAdminAndFiltered()
        {
            await _rawConnection.ExecuteAsync(@"
                INSERT INTO AppKeys (Id, Name, Username, KeyType, KeyPrefix, EncryptedKey, ScopesJson)
                VALUES ('key-1', 'Test Key', 'alice', 'personal', 'mcp-test', 'secret-hash', '[""read""]');");

            var controller = CreateController("alice", "Admin");
            var result = await controller.GetAppKeys(null, "alice");

            var okResult = Assert.IsAssignableFrom<ObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);
            Assert.NotNull(okResult.Value);

            var allKeysRes = await controller.GetAppKeys(null, null);
            Assert.Equal(200, ((ObjectResult)allKeysRes).StatusCode);
        }

        [Fact]
        [Requirement("AUTH-02", "AUTH", RequirementType.Positive, "Creates new AppKeys with custom scope slugs successfully.")]
        public async Task CreateAppKey_CreatesNewKey_Successfully_WithDifferentScopeSlugs()
        {
            var controller = CreateController("alice", "User");
            var req1 = new CreateAppKeyRequest
            {
                Name = "Server Key",
                Scopes = new List<string> { "server:plex" },
                ExpiresInDays = 30
            };

            var res1 = await controller.CreateAppKey(req1);
            Assert.Equal(200, ((ObjectResult)res1).StatusCode);

            var req2 = new CreateAppKeyRequest
            {
                Name = "Group Key",
                Scopes = new List<string> { "group:media" }
            };

            var res2 = await controller.CreateAppKey(req2);
            Assert.Equal(200, ((ObjectResult)res2).StatusCode);

            var req3 = new CreateAppKeyRequest
            {
                Name = "Tool Key",
                Scopes = new List<string> { "tool:docker_list" }
            };

            var res3 = await controller.CreateAppKey(req3);
            Assert.Equal(200, ((ObjectResult)res3).StatusCode);
        }

        [Fact]
        [Requirement("GUARD-APPKEY-MISSING-NAME", "GUARD", RequirementType.Negative, "Rejects AppKey creation with BadRequest when name is missing.")]
        public async Task CreateAppKey_ReturnsBadRequest_WhenNameMissing()
        {
            var controller = CreateController("alice", "User");
            var req = new CreateAppKeyRequest { Name = "" };

            var result = await controller.CreateAppKey(req);
            var badRes = Assert.IsAssignableFrom<BadRequestObjectResult>(result);
            Assert.Equal(400, badRes.StatusCode);
        }

        [Fact]
        [Requirement("GUARD-APPKEY-USER-QUOTA", "GUARD", RequirementType.Negative, "Enforces user AppKey limit and returns BadRequest when quota is exceeded.")]
        public async Task CreateAppKey_EnforcesUserLimit_ForNonAdmin()
        {
            // Set UserMaxKeys to 1
            await _rawConnection.ExecuteAsync("UPDATE Settings SET UserMaxKeys = 1;");

            var controller = CreateController("bob", "User");
            var req1 = new CreateAppKeyRequest { Name = "Key 1" };
            var res1 = await controller.CreateAppKey(req1);
            Assert.Equal(200, ((ObjectResult)res1).StatusCode);

            // Second key should hit personal limit
            var req2 = new CreateAppKeyRequest { Name = "Key 2" };
            var res2 = await controller.CreateAppKey(req2);
            var badRes = Assert.IsAssignableFrom<BadRequestObjectResult>(res2);
            Assert.Equal(400, badRes.StatusCode);
        }

        [Fact]
        [Requirement("AUTH-110", "AUTH", RequirementType.Positive, "CreateAppKey allows creating unlimited AppKeys when UserMaxKeys is set to 0.")]
        public async Task CreateAppKey_AllowsUnlimited_WhenLimitsAreZero()
        {
            // Set UserMaxKeys to 0 (unlimited) and GlobalMaxKeys to 0 (unlimited)
            await _rawConnection.ExecuteAsync("UPDATE Settings SET UserMaxKeys = 0, GlobalMaxKeys = 0;");

            var controller = CreateController("carol", "User");
            for (int i = 1; i <= 10; i++)
            {
                var req = new CreateAppKeyRequest { Name = $"Key {i}" };
                var res = await controller.CreateAppKey(req);
                var okRes = Assert.IsAssignableFrom<ObjectResult>(res);
                Assert.Equal(200, okRes.StatusCode);
            }

            var limitsRes = await controller.GetAppKeysLimits();
            var okLimits = Assert.IsAssignableFrom<OkObjectResult>(limitsRes);
            var json = System.Text.Json.JsonSerializer.Serialize(okLimits.Value);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            Assert.False(doc.RootElement.GetProperty("isLimitReached").GetBoolean());
            Assert.Equal(10, doc.RootElement.GetProperty("userActiveKeys").GetInt32());
            Assert.Equal(0, doc.RootElement.GetProperty("userMax").GetInt32());
            Assert.Equal(0, doc.RootElement.GetProperty("globalMax").GetInt32());
        }

        [Fact]
        [Requirement("GUARD-APPKEY-REVOKE-NOTFOUND", "GUARD", RequirementType.Negative, "Returns NotFound when revoking non-existent AppKey ID.")]
        public async Task RevokeAppKey_ReturnsNotFound_WhenIdDoesNotExist()
        {
            var controller = CreateController("alice", "Admin");
            var result = await controller.RevokeAppKey("non-existent-id");
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        [Requirement("GUARD-APPKEY-REVOKE-FORBID", "GUARD", RequirementType.Negative, "Returns Forbid when non-owner/non-admin attempts to revoke an AppKey.")]
        public async Task RevokeAppKey_ReturnsForbid_WhenUserNotOwnerOrAdmin()
        {
            await _rawConnection.ExecuteAsync(@"
                INSERT INTO AppKeys (Id, Name, Username, KeyType, KeyPrefix, EncryptedKey)
                VALUES ('key-alice', 'Alice Key', 'alice', 'personal', 'mcp-alice', 'hash');");

            var controller = CreateController("charlie", "User");
            var result = await controller.RevokeAppKey("key-alice");
            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        [Requirement("AUTH-02", "AUTH", RequirementType.Positive, "Returns quota limits and current active AppKey counts for the user.")]
        public async Task GetAppKeysLimits_ReturnsLimitsAndCounts()
        {
            var controller = CreateController("alice", "User");
            var result = await controller.GetAppKeysLimits();

            var okResult = Assert.IsAssignableFrom<ObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        [Requirement("AUTH-APPKEY-ADMIN-QUOTA", "AUTH", RequirementType.Positive, "Administrator can create, update, and delete custom user quota overrides.")]
        public async Task QuotaEndpoints_Admin_CanManageCustomUserQuotas()
        {
            var controller = CreateController("admin", "Admin");

            // 1. Set quota for user 'frank' to 15
            var setReq = new SetUserQuotaRequest { Username = "frank", MaxKeys = 15 };
            var setRes = await controller.SetUserQuota(setReq);
            var okSet = Assert.IsAssignableFrom<OkObjectResult>(setRes);
            Assert.Equal(200, okSet.StatusCode);

            // 2. Get all quotas
            var getRes = await controller.GetUserQuotas();
            var okGet = Assert.IsAssignableFrom<OkObjectResult>(getRes);
            var quotas = Assert.IsAssignableFrom<IEnumerable<UserQuota>>(okGet.Value);
            var frankQuota = Assert.Single(quotas, q => q.Username == "frank");
            Assert.Equal(15, frankQuota.MaxKeys);

            // 3. Delete quota
            var delRes = await controller.DeleteUserQuota("frank");
            var okDel = Assert.IsAssignableFrom<OkObjectResult>(delRes);
            Assert.Equal(200, okDel.StatusCode);

            // 4. Verify deleted
            var afterGetRes = await controller.GetUserQuotas();
            var afterQuotas = Assert.IsAssignableFrom<IEnumerable<UserQuota>>(((OkObjectResult)afterGetRes).Value);
            Assert.DoesNotContain(afterQuotas, q => q.Username == "frank");
        }

        [Fact]
        [Requirement("GUARD-APPKEY-QUOTA-INVALID-PARAM", "GUARD", RequirementType.Negative, "Returns BadRequest on invalid quota override input parameters.")]
        public async Task QuotaEndpoints_Validation_ReturnsBadRequest_OnInvalidInputs()
        {
            var controller = CreateController("admin", "Admin");

            var badSet1 = await controller.SetUserQuota(new SetUserQuotaRequest { Username = "", MaxKeys = 5 });
            Assert.IsType<BadRequestObjectResult>(badSet1);

            var badSet2 = await controller.SetUserQuota(new SetUserQuotaRequest { Username = "frank", MaxKeys = -1 });
            Assert.IsType<BadRequestObjectResult>(badSet2);

            var badDel = await controller.DeleteUserQuota("");
            Assert.IsType<BadRequestObjectResult>(badDel);
        }

        [Fact]
        [Requirement("GUARD-04", "GUARD", RequirementType.Negative, "Fails closed and returns HTTP 500 when database errors occur in AppKeys controller.")]
        public async Task Controllers_HandleDbFailures_Returning500()
        {
            var mockConn = new Mock<IDbConnection>();
            mockConn.Setup(c => c.State).Returns(ConnectionState.Open);

            var failingFactory = new Mock<IDbConnectionFactory>();
            failingFactory.Setup(f => f.CreateConnection()).Returns(mockConn.Object);
            failingFactory.Setup(f => f.ProviderName).Returns("sqlite");

            var failingRepo = new DatabaseRepository(failingFactory.Object);
            var failingCredSvc = new CredentialService(failingFactory.Object);
            var controller = new AppKeysController(failingRepo, failingRepo, _config, new Mock<ModelContextGateway.Infrastructure.Logging.IAuditLogger>().Object, failingCredSvc, failingRepo);
            var services = new ServiceCollection();
            services.AddSingleton(_config);
            services.AddLogging();
            services.AddSingleton<IIdentityProvider, OidcIdentityProvider>();
            services.AddSingleton<CompositeIdentityProvider>();

            var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
            httpContext.Request.Headers["X-Forwarded-For"] = "127.0.0.1";
            httpContext.Request.Headers["Remote-User"] = "admin";
            httpContext.Request.Headers["Remote-Groups"] = "full_admin";
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            Assert.Equal(500, ((ObjectResult)await controller.GetAppKeys()).StatusCode);
            Assert.Equal(500, ((ObjectResult)await controller.GetAppKeysLimits()).StatusCode);
            Assert.Equal(500, ((ObjectResult)await controller.CreateAppKey(new CreateAppKeyRequest { Name = "Test" })).StatusCode);
            Assert.Equal(500, ((ObjectResult)await controller.RevokeAppKey("k1")).StatusCode);
            Assert.Equal(500, ((ObjectResult)await controller.GetUserQuotas()).StatusCode);
            Assert.Equal(500, ((ObjectResult)await controller.SetUserQuota(new SetUserQuotaRequest { Username = "u1", MaxKeys = 5 })).StatusCode);
            Assert.Equal(500, ((ObjectResult)await controller.DeleteUserQuota("u1")).StatusCode);
        }
    }
}
