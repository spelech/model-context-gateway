using System.Data;
using System.Net;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ModelContextGateway.Tests
{
    public class AdminToolsParityTests : IDisposable
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
        private readonly Mock<IAuditLogger> _mockAuditLogger;
        private readonly DatabaseRepository _dbRepo;
        private readonly ICredentialService _credentialService;
        private readonly SessionManager _sessionManager;
        private readonly DynamicEmbeddingService _dynamicEmbeddingService;
        private readonly BackendHealthCheckService _healthCheckService;
        private readonly AdminMcpServer _adminMcpServer;
        private readonly MockHttpMessageHandler _mockHttpHandler;

        public AdminToolsParityTests()
        {
            Environment.SetEnvironmentVariable("MCG_ALLOW_PRIVATE_IPS", "true");
            _rawConnection = new SqliteConnection($"DataSource=file:mem_parity_{Guid.NewGuid():N}?mode=memory&cache=shared");
            _rawConnection.Open();

            _rawConnection.Execute(@"
                CREATE TABLE IF NOT EXISTS Servers (
                    Id TEXT PRIMARY KEY, DisplayName TEXT, Url TEXT, Enabled INTEGER DEFAULT 1, Hidden INTEGER DEFAULT 0,
                    Type TEXT DEFAULT 'sse', SecretProvider TEXT, SecretItemKey TEXT, SecretMount TEXT, SecretPath TEXT,
                    SecretField TEXT, AuthShape TEXT, CustomHeaderName TEXT, Categories TEXT DEFAULT '[]', ApiKey TEXT,
                    HeadersJson TEXT, AutoDiscovered INTEGER DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS AppKeys (
                    Id TEXT PRIMARY KEY, Name TEXT, Username TEXT, OwnerSid TEXT DEFAULT '', KeyType TEXT DEFAULT 'personal', KeyPrefix TEXT,
                    EncryptedKey TEXT, ScopesJson TEXT DEFAULT '[]', ExpiresAt TEXT, CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );
                CREATE TABLE IF NOT EXISTS Settings (
                    Id TEXT PRIMARY KEY, DashboardTitle TEXT DEFAULT 'MCP Gateway', DashboardIcon TEXT DEFAULT 'fa-solid fa-network-wired',
                    EmbeddingProvider TEXT DEFAULT 'onnx', EmbeddingApiUrl TEXT, EmbeddingApiKey TEXT, EmbeddingApiModel TEXT,
                    EmbeddingModelDir TEXT, GlobalMaxKeys INTEGER DEFAULT 100, UserMaxKeys INTEGER DEFAULT 5,
                    ToolSearchDefaultLimit INTEGER DEFAULT 5, ToolSearchMinScore REAL DEFAULT 0.35, ToolSearchScoreDropoffRatio REAL DEFAULT 0.60
                );
                CREATE TABLE IF NOT EXISTS AccessPolicies (
                    Id TEXT PRIMARY KEY, TargetId TEXT, RequiredGroup TEXT, IsAllowed INTEGER DEFAULT 1
                );
                CREATE TABLE IF NOT EXISTS GroupMappings (
                    Id TEXT PRIMARY KEY, ExternalId TEXT, InternalGroup TEXT
                );
                CREATE TABLE IF NOT EXISTS SecretProviders (
                    ProviderName TEXT PRIMARY KEY, DisplayName TEXT, EncryptedConfigJson TEXT, IsEnabled INTEGER DEFAULT 1
                );
                CREATE TABLE IF NOT EXISTS AuthProviderConfigs (
                    ProviderName TEXT PRIMARY KEY, DisplayName TEXT, UserHeader TEXT, GroupsHeader TEXT, EncryptedConfigJson TEXT, IsEnabled INTEGER DEFAULT 1
                );
                CREATE TABLE IF NOT EXISTS AuditLogs (
                    RequestId TEXT, UserPrincipalName TEXT, UserSid TEXT, ServerCodeName TEXT, ItemName TEXT,
                    RequestMethod TEXT, ExecutionTimeMs INTEGER, StatusCode INTEGER, RequestPayload TEXT, ResponsePayload TEXT,
                    ErrorMessage TEXT, Timestamp TEXT DEFAULT CURRENT_TIMESTAMP
                );
                CREATE TABLE IF NOT EXISTS AdminAuditLogs (
                    Id TEXT PRIMARY KEY, Username TEXT, Action TEXT, Target TEXT, Details TEXT, Success INTEGER,
                    ErrorMessage TEXT, Timestamp TEXT DEFAULT CURRENT_TIMESTAMP
                );");

            _rawConnection.Execute("INSERT OR REPLACE INTO Settings (Id, GlobalMaxKeys, UserMaxKeys) VALUES ('default', 100, 5);");
            _rawConnection.Execute(@"INSERT OR REPLACE INTO Servers (Id, DisplayName, Url, Type, Enabled)
                VALUES ('mock-calc-server', 'Mock Calculation Server', 'http://127.0.0.1:9099/mcp', 'http', 1);");

            var mockFactory = new Mock<IDbConnectionFactory>();
            mockFactory.Setup(f => f.CreateConnection()).Returns(() => new NonDisposingConnection(_rawConnection));
            mockFactory.Setup(f => f.ProviderName).Returns("sqlite");
            _dbFactory = mockFactory.Object;

            _config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DB_ENCRYPTION_KEY", "TestSecretKey1234567890123456789012" },
                { "Admin:GroupSid", "full_admin" },
                { "Security:AllowedIpRanges:0", "127.0.0.0/8" },
                { "Security:AllowedIpRanges:1", "::1" }
            }).Build();

            _mockAuditLogger = new Mock<IAuditLogger>();
            _dbRepo = new DatabaseRepository(_dbFactory, _config);
            _credentialService = new CredentialService(_dbFactory);

            _mockHttpHandler = new MockHttpMessageHandler
            {
                Handler = async (req) =>
                {
                    var body = req.Content != null ? await req.Content.ReadAsStringAsync() : "";
                    if (body.Contains("\"initialize\""))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                                "{\"jsonrpc\":\"2.0\",\"id\":\"test-init\",\"result\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"serverInfo\":{\"name\":\"mock-backend\",\"version\":\"1.0\"}}}",
                                Encoding.UTF8, "application/json")
                        };
                    }
                    if (body.Contains("\"notifications/initialized\""))
                    {
                        return new HttpResponseMessage(HttpStatusCode.Accepted)
                        {
                            Content = new StringContent("{}", Encoding.UTF8, "application/json")
                        };
                    }
                    if (body.Contains("\"tools/call\""))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                                "{\"jsonrpc\":\"2.0\",\"id\":\"admin-test-call-id\",\"result\":{\"content\":[{\"type\":\"text\",\"text\":\"Calculation result: 2\"}]}}",
                                Encoding.UTF8, "application/json")
                        };
                    }
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{}", Encoding.UTF8, "application/json")
                    };
                }
            };

            var services = new ServiceCollection();
            services.AddSingleton(_config);
            services.AddSingleton(_dbFactory);
            services.AddLogging();
            services.AddHttpClient();
            var serviceProvider = services.BuildServiceProvider();
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

            _dynamicEmbeddingService = new DynamicEmbeddingService(httpClientFactory.CreateClient(), serviceProvider.GetRequiredService<ILoggerFactory>(), serviceProvider);
            _sessionManager = new SessionManager(serviceProvider, httpClientFactory, serviceProvider.GetRequiredService<ILogger<SessionManager>>());
            _healthCheckService = new BackendHealthCheckService(serviceProvider, httpClientFactory, _sessionManager, serviceProvider.GetRequiredService<ILogger<BackendHealthCheckService>>());

            var httpClient = new HttpClient(_mockHttpHandler);

            _adminMcpServer = new AdminMcpServer(
                _dbRepo,
                _dbRepo,
                _dbRepo,
                _dbRepo,
                _dbRepo,
                _dbFactory,
                _mockAuditLogger.Object,
                _credentialService,
                _healthCheckService,
                _dynamicEmbeddingService,
                _sessionManager,
                ldapService: null,
                httpClient: httpClient,
                configuration: _config,
                logger: serviceProvider.GetRequiredService<ILogger<AdminMcpServer>>()
            );
        }

        public void Dispose()
        {
            _rawConnection.Dispose();
            Environment.SetEnvironmentVariable("MCG_ALLOW_PRIVATE_IPS", null);
        }

        [Theory]
        [InlineData("manage_servers")]
        [InlineData("manage_appkeys")]
        [InlineData("manage_clients")]
        [InlineData("manage_policies")]
        [InlineData("manage_group_mappings")]
        [InlineData("manage_providers")]
        [InlineData("manage_settings")]
        [InlineData("manage_custom_files")]
        [InlineData("manage_system")]
        [InlineData("test_tool_call")]
        [Requirement("MCP-ADMIN-PARITY-TOOLS-COVERAGE", "MCP", RequirementType.Positive, "Ensures every UI management workflow is backed by a verified, equivalent action within the consolidated Admin MCP tools.")]
        public async Task AdminTools_ExecuteSuccessfully(string toolName)
        {
            JsonElement args = toolName switch
            {
                "manage_servers" => JsonDocument.Parse("{\"action\":\"list\"}").RootElement,
                "manage_appkeys" => JsonDocument.Parse("{\"action\":\"list\"}").RootElement,
                "manage_clients" => JsonDocument.Parse("{\"action\":\"list\"}").RootElement,
                "manage_policies" => JsonDocument.Parse("{\"action\":\"list\"}").RootElement,
                "manage_group_mappings" => JsonDocument.Parse("{\"action\":\"list\"}").RootElement,
                "manage_providers" => JsonDocument.Parse("{\"action\":\"list\"}").RootElement,
                "manage_settings" => JsonDocument.Parse("{\"action\":\"get\"}").RootElement,
                "manage_custom_files" => JsonDocument.Parse("{\"action\":\"list\"}").RootElement,
                "manage_system" => JsonDocument.Parse("{\"action\":\"diagnostics\"}").RootElement,
                "test_tool_call" => JsonDocument.Parse("{\"serverId\":\"mock-calc-server\",\"toolName\":\"calculate\",\"arguments\":{}}").RootElement,
                _ => throw new ArgumentException($"Unknown tool {toolName}")
            };

            var res = await _adminMcpServer.CallToolAsync(toolName, args, "parity_admin");
            Assert.NotNull(res);

            var json = JsonSerializer.Serialize(res);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("isError", out var isErrorProp));
            Assert.False(isErrorProp.GetBoolean(), $"Tool '{toolName}' returned error: {json}");
            Assert.True(root.TryGetProperty("content", out var contentProp));
            Assert.Equal(JsonValueKind.Array, contentProp.ValueKind);
            Assert.True(contentProp.GetArrayLength() > 0);
        }

        [Fact]
        [Requirement("MCP-ADMIN-PARITY-SERVERS", "MCP", RequirementType.Positive, "Validates that the manage_servers tool provides comprehensive administrative capabilities including listing, retrieving, creating, updating, toggling, deleting, and reconnecting servers.")]
        public async Task ManageServers_Parity_AllActions()
        {
            var serverId = "srv-parity-01";

            // 1. Create Server with rich metadata
            var createArgs = JsonDocument.Parse($@"{{
                ""action"": ""create"",
                ""id"": ""{serverId}"",
                ""displayName"": ""Parity Test Server"",
                ""url"": ""http://127.0.0.1:8080/sse"",
                ""type"": ""sse"",
                ""enabled"": true,
                ""hidden"": false,
                ""secretProvider"": ""Vault"",
                ""secretItemKey"": ""keys/backend1"",
                ""authShape"": ""bearer"",
                ""apiKey"": ""sec-bearer-token-123"",
                ""categories"": [""core"", ""diagnostics""]
            }}").RootElement;

            var createRes = await _adminMcpServer.CallToolAsync("manage_servers", createArgs, "admin_user");
            AssertIsSuccess(createRes);

            // 2. Get Server
            var getArgs = JsonDocument.Parse($@"{{ ""action"": ""get"", ""id"": ""{serverId}"" }}").RootElement;
            var getRes = await _adminMcpServer.CallToolAsync("manage_servers", getArgs, "admin_user");
            var getDoc = AssertIsSuccess(getRes);
            var getText = getDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.Contains("Parity Test Server", getText);
            Assert.Contains("Vault", getText);
            Assert.Contains("keys/backend1", getText);

            // 3. Update Server
            var updateArgs = JsonDocument.Parse($@"{{
                ""action"": ""update"",
                ""id"": ""{serverId}"",
                ""displayName"": ""Updated Parity Server"",
                ""authShape"": ""customHeader"",
                ""customHeaderName"": ""X-Custom-Auth""
            }}").RootElement;

            var updateRes = await _adminMcpServer.CallToolAsync("manage_servers", updateArgs, "admin_user");
            AssertIsSuccess(updateRes);

            var getDocUpdated = AssertIsSuccess(await _adminMcpServer.CallToolAsync("manage_servers", getArgs, "admin_user"));
            var getUpdatedText = getDocUpdated.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.Contains("Updated Parity Server", getUpdatedText);
            Assert.Contains("X-Custom-Auth", getUpdatedText);

            // 4. Toggle Server (Explicit false)
            var toggleArgs = JsonDocument.Parse($@"{{ ""action"": ""toggle"", ""id"": ""{serverId}"", ""enabled"": false }}").RootElement;
            var toggleRes = await _adminMcpServer.CallToolAsync("manage_servers", toggleArgs, "admin_user");
            var toggleDoc = AssertIsSuccess(toggleRes);
            var toggleText = toggleDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.Contains("\"enabled\": false", toggleText);

            // Toggle Server (Flip back)
            var toggleFlipArgs = JsonDocument.Parse($@"{{ ""action"": ""toggle"", ""id"": ""{serverId}"" }}").RootElement;
            var toggleFlipRes = await _adminMcpServer.CallToolAsync("manage_servers", toggleFlipArgs, "admin_user");
            var toggleFlipDoc = AssertIsSuccess(toggleFlipRes);
            Assert.Contains("\"enabled\": true", toggleFlipDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!);

            // 5. Reconnect Server
            var reconnectArgs = JsonDocument.Parse($@"{{ ""action"": ""reconnect"", ""id"": ""{serverId}"" }}").RootElement;
            var reconnectRes = await _adminMcpServer.CallToolAsync("manage_servers", reconnectArgs, "admin_user");
            AssertIsSuccess(reconnectRes);

            // 6. Reconnect All Servers
            var reconnectAllArgs = JsonDocument.Parse(@"{ ""action"": ""reconnect_all"" }").RootElement;
            var reconnectAllRes = await _adminMcpServer.CallToolAsync("manage_servers", reconnectAllArgs, "admin_user");
            AssertIsSuccess(reconnectAllRes);

            // 7. List Servers
            var listArgs = JsonDocument.Parse(@"{ ""action"": ""list"" }").RootElement;
            var listRes = await _adminMcpServer.CallToolAsync("manage_servers", listArgs, "admin_user");
            var listDoc = AssertIsSuccess(listRes);
            Assert.Contains(serverId, listDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!);

            // 8. Delete Server
            var deleteArgs = JsonDocument.Parse($@"{{ ""action"": ""delete"", ""id"": ""{serverId}"" }}").RootElement;
            var deleteRes = await _adminMcpServer.CallToolAsync("manage_servers", deleteArgs, "admin_user");
            AssertIsSuccess(deleteRes);

            // 9. Get Deleted Server -> Returns Error
            var getDeletedRes = await _adminMcpServer.CallToolAsync("manage_servers", getArgs, "admin_user");
            AssertIsError(getDeletedRes);
        }

        [Fact]
        [Requirement("GUARD-ADMIN-SERVERS-VALIDATION", "GUARD", RequirementType.Negative, "Verifies that the manage_servers tool accurately enforces validation by rejecting malformed transport types, missing required parameters, and requests for non-existent servers.")]
        public async Task ManageServers_ValidationGuardrails()
        {
            // Invalid transport type
            var badTypeArgs = JsonDocument.Parse(@"{
                ""action"": ""create"",
                ""id"": ""srv-bad"",
                ""displayName"": ""Bad Protocol"",
                ""url"": ""http://localhost:8080"",
                ""type"": ""websockets_invalid""
            }").RootElement;
            var badTypeRes = await _adminMcpServer.CallToolAsync("manage_servers", badTypeArgs, "admin_user");
            AssertIsError(badTypeRes);

            // Missing required action
            var missingActionArgs = JsonDocument.Parse(@"{ ""id"": ""srv-1"" }").RootElement;
            var missingActionRes = await _adminMcpServer.CallToolAsync("manage_servers", missingActionArgs, "admin_user");
            AssertIsError(missingActionRes);

            // Non-existent server get
            var getNonExistentArgs = JsonDocument.Parse(@"{ ""action"": ""get"", ""id"": ""srv-ghost-999"" }").RootElement;
            var getGhostRes = await _adminMcpServer.CallToolAsync("manage_servers", getNonExistentArgs, "admin_user");
            AssertIsError(getGhostRes);

            // Non-existent server update
            var updateGhostArgs = JsonDocument.Parse(@"{ ""action"": ""update"", ""id"": ""srv-ghost-999"", ""displayName"": ""Ghost"" }").RootElement;
            var updateGhostRes = await _adminMcpServer.CallToolAsync("manage_servers", updateGhostArgs, "admin_user");
            AssertIsError(updateGhostRes);

            // Non-existent server toggle
            var toggleGhostArgs = JsonDocument.Parse(@"{ ""action"": ""toggle"", ""id"": ""srv-ghost-999"" }").RootElement;
            var toggleGhostRes = await _adminMcpServer.CallToolAsync("manage_servers", toggleGhostArgs, "admin_user");
            AssertIsError(toggleGhostRes);

            // Non-existent server delete
            var deleteGhostArgs = JsonDocument.Parse(@"{ ""action"": ""delete"", ""id"": ""srv-ghost-999"" }").RootElement;
            var deleteGhostRes = await _adminMcpServer.CallToolAsync("manage_servers", deleteGhostArgs, "admin_user");
            AssertIsError(deleteGhostRes);

            // Non-existent server reconnect
            var reconnectGhostArgs = JsonDocument.Parse(@"{ ""action"": ""reconnect"", ""id"": ""srv-ghost-999"" }").RootElement;
            var reconnectGhostRes = await _adminMcpServer.CallToolAsync("manage_servers", reconnectGhostArgs, "admin_user");
            AssertIsError(reconnectGhostRes);
        }

        [Fact]
        [Requirement("MCP-ADMIN-PARITY-APPKEYS", "MCP", RequirementType.Positive, "manage_appkeys supports full parity for list, get_limits, create, and revoke actions.")]
        public async Task ManageAppKeys_Parity_AllActions()
        {
            // 1. Get Limits
            var limitsArgs = JsonDocument.Parse(@"{ ""action"": ""get_limits"", ""username"": ""carol"" }").RootElement;
            var limitsRes = await _adminMcpServer.CallToolAsync("manage_appkeys", limitsArgs, "admin_user");
            var limitsDoc = AssertIsSuccess(limitsRes);
            var limitsText = limitsDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.Contains("globalMax", limitsText);
            Assert.Contains("userMax", limitsText);

            // 2. Create Key with granular scopes
            var createArgs = JsonDocument.Parse(@"{
                ""action"": ""create"",
                ""name"": ""Carol Data Key"",
                ""username"": ""carol"",
                ""scopes"": [""category:database"", ""admin""],
                ""expiresInDays"": 45
            }").RootElement;

            var createRes = await _adminMcpServer.CallToolAsync("manage_appkeys", createArgs, "admin_user");
            var createDoc = AssertIsSuccess(createRes);
            var createText = createDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.Contains("plaintextKey", createText, StringComparison.OrdinalIgnoreCase);

            using var payloadDoc = JsonDocument.Parse(createText);
            var keyId = payloadDoc.RootElement.GetProperty("id").GetString()!;

            // 3. List Keys filtered by username
            var listFilterArgs = JsonDocument.Parse(@"{ ""action"": ""list"", ""username"": ""carol"" }").RootElement;
            var listFilterRes = await _adminMcpServer.CallToolAsync("manage_appkeys", listFilterArgs, "admin_user");
            var listFilterDoc = AssertIsSuccess(listFilterRes);
            var listFilterText = listFilterDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.Contains(keyId, listFilterText);
            Assert.Contains("Carol Data Key", listFilterText);

            // 4. List Keys unfiltered
            var listAllArgs = JsonDocument.Parse(@"{ ""action"": ""list"" }").RootElement;
            var listAllRes = await _adminMcpServer.CallToolAsync("manage_appkeys", listAllArgs, "admin_user");
            var listAllDoc = AssertIsSuccess(listAllRes);
            Assert.Contains(keyId, listAllDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!);

            // 5. Revoke Key
            var revokeArgs = JsonDocument.Parse($@"{{ ""action"": ""revoke"", ""id"": ""{keyId}"" }}").RootElement;
            var revokeRes = await _adminMcpServer.CallToolAsync("manage_appkeys", revokeArgs, "admin_user");
            AssertIsSuccess(revokeRes);

            // 6. Revoke non-existent Key -> Returns Error
            var revokeGhostRes = await _adminMcpServer.CallToolAsync("manage_appkeys", revokeArgs, "admin_user");
            AssertIsError(revokeGhostRes);
        }

        [Fact]
        [Requirement("MCP-ADMIN-PARITY-CLIENTS", "MCP", RequirementType.Positive, "manage_clients supports full parity for register, list, and delete actions.")]
        public async Task ManageClients_Parity_AllActions()
        {
            // 1. Register dynamic client
            var regArgs = JsonDocument.Parse(@"{
                ""action"": ""register"",
                ""displayName"": ""Analytics Agent Service"",
                ""scopes"": [""tools:call"", ""resources:list""],
                ""expiresInDays"": 90
            }").RootElement;

            var regRes = await _adminMcpServer.CallToolAsync("manage_clients", regArgs, "admin_user");
            var regDoc = AssertIsSuccess(regRes);
            var regText = regDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.Contains("clientId", regText);
            Assert.Contains("clientSecret", regText);

            using var payloadDoc = JsonDocument.Parse(regText);
            var clientId = payloadDoc.RootElement.GetProperty("clientId").GetString()!;
            var id = payloadDoc.RootElement.GetProperty("id").GetString()!;

            // 2. List clients
            var listArgs = JsonDocument.Parse(@"{ ""action"": ""list"" }").RootElement;
            var listRes = await _adminMcpServer.CallToolAsync("manage_clients", listArgs, "admin_user");
            var listDoc = AssertIsSuccess(listRes);
            var listText = listDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.Contains("Analytics Agent Service", listText);
            Assert.Contains(clientId, listText);

            // 3. Delete client
            var delArgs = JsonDocument.Parse($@"{{ ""action"": ""delete"", ""id"": ""{id}"" }}").RootElement;
            var delRes = await _adminMcpServer.CallToolAsync("manage_clients", delArgs, "admin_user");
            AssertIsSuccess(delRes);

            // 4. Delete non-existent client -> Returns Error
            var delGhostArgs = JsonDocument.Parse(@"{ ""action"": ""delete"", ""id"": ""ghost-client-999"" }").RootElement;
            var delGhostRes = await _adminMcpServer.CallToolAsync("manage_clients", delGhostArgs, "admin_user");
            AssertIsError(delGhostRes);
        }

        [Fact]
        [Requirement("MCP-ADMIN-PARITY-POLICIES", "MCP", RequirementType.Positive, "manage_policies supports full parity for list, save, and delete access control policies.")]
        public async Task ManagePolicies_Parity_AllActions()
        {
            var policyId = "pol-parity-01";

            // 1. Save Policy (Allow)
            var saveArgs = JsonDocument.Parse($@"{{
                ""action"": ""save"",
                ""id"": ""{policyId}"",
                ""targetId"": ""database-cluster"",
                ""requiredGroup"": ""data_engineers"",
                ""isAllowed"": true
            }}").RootElement;

            var saveRes = await _adminMcpServer.CallToolAsync("manage_policies", saveArgs, "admin_user");
            AssertIsSuccess(saveRes);

            // 2. List Policies
            var listArgs = JsonDocument.Parse(@"{ ""action"": ""list"" }").RootElement;
            var listRes = await _adminMcpServer.CallToolAsync("manage_policies", listArgs, "admin_user");
            var listDoc = AssertIsSuccess(listRes);
            var listText = listDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.Contains("database-cluster", listText);
            Assert.Contains("data_engineers", listText);

            // 3. Update Policy (Deny)
            var updateArgs = JsonDocument.Parse($@"{{
                ""action"": ""save"",
                ""id"": ""{policyId}"",
                ""targetId"": ""database-cluster"",
                ""requiredGroup"": ""data_engineers"",
                ""isAllowed"": false
            }}").RootElement;

            var updateRes = await _adminMcpServer.CallToolAsync("manage_policies", updateArgs, "admin_user");
            AssertIsSuccess(updateRes);

            // 4. Delete Policy
            var delArgs = JsonDocument.Parse($@"{{ ""action"": ""delete"", ""id"": ""{policyId}"" }}").RootElement;
            var delRes = await _adminMcpServer.CallToolAsync("manage_policies", delArgs, "admin_user");
            AssertIsSuccess(delRes);

            // 5. Verify Policy Removed from list
            var listAfterRes = await _adminMcpServer.CallToolAsync("manage_policies", listArgs, "admin_user");
            var listAfterDoc = AssertIsSuccess(listAfterRes);
            Assert.DoesNotContain("data_engineers", listAfterDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!);
        }

        [Fact]
        [Requirement("GUARD-ADMIN-POLICIES-WILDCARD-DENY", "GUARD", RequirementType.Negative, "manage_policies rejects wildcard deny policies to prevent global lockout.")]
        public async Task ManagePolicies_WildcardDenyGuardrail()
        {
            var wildcardDenyArgs = JsonDocument.Parse(@"{
                ""action"": ""save"",
                ""targetId"": ""*"",
                ""requiredGroup"": ""contractors"",
                ""isAllowed"": false
            }").RootElement;

            var res = await _adminMcpServer.CallToolAsync("manage_policies", wildcardDenyArgs, "admin_user");
            var doc = AssertIsError(res);
            var errorText = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.Contains("Cannot save a wildcard deny policy", errorText);
        }

        [Fact]
        [Requirement("MCP-ADMIN-PARITY-GROUP-MAPPINGS", "MCP", RequirementType.Positive, "manage_group_mappings supports full parity for list, save, and delete external-to-internal group mappings.")]
        public async Task ManageGroupMappings_Parity_AllActions()
        {
            var mappingId = "map-parity-01";

            // 1. Save Group Mapping
            var saveArgs = JsonDocument.Parse($@"{{
                ""action"": ""save"",
                ""id"": ""{mappingId}"",
                ""externalId"": ""S-1-5-21-PARITY-ENGINEERING"",
                ""internalGroup"": ""engineering_leads""
            }}").RootElement;

            var saveRes = await _adminMcpServer.CallToolAsync("manage_group_mappings", saveArgs, "admin_user");
            AssertIsSuccess(saveRes);

            // 2. List Group Mappings
            var listArgs = JsonDocument.Parse(@"{ ""action"": ""list"" }").RootElement;
            var listRes = await _adminMcpServer.CallToolAsync("manage_group_mappings", listArgs, "admin_user");
            var listDoc = AssertIsSuccess(listRes);
            var listText = listDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.Contains("S-1-5-21-PARITY-ENGINEERING", listText);
            Assert.Contains("engineering_leads", listText);

            // 3. Update Group Mapping
            var updateArgs = JsonDocument.Parse($@"{{
                ""action"": ""save"",
                ""id"": ""{mappingId}"",
                ""externalId"": ""S-1-5-21-PARITY-ENGINEERING"",
                ""internalGroup"": ""senior_architects""
            }}").RootElement;

            var updateRes = await _adminMcpServer.CallToolAsync("manage_group_mappings", updateArgs, "admin_user");
            AssertIsSuccess(updateRes);

            // 4. Delete Group Mapping
            var delArgs = JsonDocument.Parse($@"{{ ""action"": ""delete"", ""id"": ""{mappingId}"" }}").RootElement;
            var delRes = await _adminMcpServer.CallToolAsync("manage_group_mappings", delArgs, "admin_user");
            AssertIsSuccess(delRes);

            // 5. Verify Mapping Removed
            var listAfterRes = await _adminMcpServer.CallToolAsync("manage_group_mappings", listArgs, "admin_user");
            var listAfterDoc = AssertIsSuccess(listAfterRes);
            Assert.DoesNotContain("senior_architects", listAfterDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!);
        }

        [Fact]
        [Requirement("MCP-ADMIN-PARITY-PROVIDERS", "MCP", RequirementType.Positive, "manage_providers supports full parity for list, save_secret, test_vault, save_auth, and test_ldap actions.")]
        public async Task ManageProviders_Parity_AllActions()
        {
            // 1. Save Secret Provider
            var saveSecArgs = JsonDocument.Parse(@"{
                ""action"": ""save_secret"",
                ""providerName"": ""vault"",
                ""displayName"": ""Enterprise Vault"",
                ""configJson"": ""{\""Address\"":\""https://vault.internal:8200\"",\""Token\"":\""s.parityToken123\""}"",
                ""isEnabled"": true
            }").RootElement;

            var saveSecRes = await _adminMcpServer.CallToolAsync("manage_providers", saveSecArgs, "admin_user");
            AssertIsSuccess(saveSecRes);

            // 2. Save Auth Provider
            var saveAuthArgs = JsonDocument.Parse(@"{
                ""action"": ""save_auth"",
                ""providerName"": ""ldap"",
                ""displayName"": ""Corp Active Directory"",
                ""userHeader"": ""X-Corp-User"",
                ""groupsHeader"": ""X-Corp-Groups"",
                ""configJson"": ""{\""Server\"":\""dc.corp.local\"",\""Port\"":636}"",
                ""isEnabled"": true
            }").RootElement;

            var saveAuthRes = await _adminMcpServer.CallToolAsync("manage_providers", saveAuthArgs, "admin_user");
            AssertIsSuccess(saveAuthRes);

            // 3. List All Providers (Assert Redaction)
            var listAllArgs = JsonDocument.Parse(@"{ ""action"": ""list"", ""type"": ""all"" }").RootElement;
            var listAllRes = await _adminMcpServer.CallToolAsync("manage_providers", listAllArgs, "admin_user");
            var listAllDoc = AssertIsSuccess(listAllRes);
            var listAllText = listAllDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.Contains("secretProviders", listAllText);
            Assert.Contains("authProviders", listAllText);
            Assert.DoesNotContain("s.parityToken123", listAllText);

            // 4. List Secret Providers Only
            var listSecArgs = JsonDocument.Parse(@"{ ""action"": ""list"", ""type"": ""secrets"" }").RootElement;
            var listSecRes = await _adminMcpServer.CallToolAsync("manage_providers", listSecArgs, "admin_user");
            var listSecDoc = AssertIsSuccess(listSecRes);
            Assert.Contains("Enterprise Vault", listSecDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!);

            // 5. List Auth Providers Only
            var listAuthArgs = JsonDocument.Parse(@"{ ""action"": ""list"", ""type"": ""auth"" }").RootElement;
            var listAuthRes = await _adminMcpServer.CallToolAsync("manage_providers", listAuthArgs, "admin_user");
            var listAuthDoc = AssertIsSuccess(listAuthRes);
            Assert.Contains("Corp Active Directory", listAuthDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!);

            // 6. Test Vault Connectivity (Graceful result without crash)
            var testVaultArgs = JsonDocument.Parse(@"{
                ""action"": ""test_vault"",
                ""address"": ""http://127.0.0.1:9876"",
                ""token"": ""dummy_token""
            }").RootElement;

            var testVaultRes = await _adminMcpServer.CallToolAsync("manage_providers", testVaultArgs, "admin_user");
            AssertIsSuccess(testVaultRes);

            // 7. Test LDAP Connectivity over LDAPS (Graceful result without crash)
            var testLdapArgs = JsonDocument.Parse(@"{
                ""action"": ""test_ldap"",
                ""server"": ""127.0.0.1"",
                ""port"": 636,
                ""useSsl"": true
            }").RootElement;

            var testLdapRes = await _adminMcpServer.CallToolAsync("manage_providers", testLdapArgs, "admin_user");
            AssertIsSuccess(testLdapRes);
        }

        [Fact]
        [Requirement("GUARD-ADMIN-PROVIDERS-LDAP-PLAINTEXT", "GUARD", RequirementType.Negative, "manage_providers rejects unencrypted LDAP connections on port 389.")]
        public async Task ManageProviders_LdapPlaintextGuardrail()
        {
            var plainLdapArgs = JsonDocument.Parse(@"{
                ""action"": ""test_ldap"",
                ""server"": ""127.0.0.1"",
                ""port"": 389,
                ""useSsl"": false
            }").RootElement;

            var res = await _adminMcpServer.CallToolAsync("manage_providers", plainLdapArgs, "admin_user");
            var doc = AssertIsError(res);
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.Contains("LDAP over plaintext", text);
        }

        [Fact]
        [Requirement("MCP-ADMIN-PARITY-SETTINGS", "MCP", RequirementType.Positive, "manage_settings supports full parity for get and update global router configurations.")]
        public async Task ManageSettings_Parity_AllActions()
        {
            // 1. Get Settings
            var getArgs = JsonDocument.Parse(@"{ ""action"": ""get"" }").RootElement;
            var getRes = await _adminMcpServer.CallToolAsync("manage_settings", getArgs, "admin_user");
            var getDoc = AssertIsSuccess(getRes);
            Assert.NotNull(getDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString());

            // 2. Update Settings via direct properties
            var updateArgs = JsonDocument.Parse(@"{
                ""action"": ""update"",
                ""dashboardTitle"": ""Enterprise Model Context Gateway Hub"",
                ""dashboardIcon"": ""fa-solid fa-server"",
                ""embeddingProvider"": ""api"",
                ""embeddingApiUrl"": ""https://embeddings.corp.internal/v1"",
                ""embeddingApiKey"": ""embed-key-999"",
                ""globalMaxKeys"": 500,
                ""userMaxKeys"": 25
            }").RootElement;

            var updateRes = await _adminMcpServer.CallToolAsync("manage_settings", updateArgs, "admin_user");
            var updateDoc = AssertIsSuccess(updateRes);
            var updateText = updateDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.Contains("Enterprise Model Context Gateway Hub", updateText);
            Assert.Contains("500", updateText);

            // 3. Verify Get reflects updated values
            var getAfterRes = await _adminMcpServer.CallToolAsync("manage_settings", getArgs, "admin_user");
            var getAfterDoc = AssertIsSuccess(getAfterRes);
            var getAfterText = getAfterDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.Contains("Enterprise Model Context Gateway Hub", getAfterText);

            // 4. Update Settings via nested object
            var updateNestedArgs = JsonDocument.Parse(@"{
                ""action"": ""update"",
                ""settings"": {
                    ""DashboardTitle"": ""Nested MCP Gateway"",
                    ""GlobalMaxKeys"": 250,
                    ""UserMaxKeys"": 12
                }
            }").RootElement;

            var updateNestedRes = await _adminMcpServer.CallToolAsync("manage_settings", updateNestedArgs, "admin_user");
            var updateNestedDoc = AssertIsSuccess(updateNestedRes);
            Assert.Contains("Nested MCP Gateway", updateNestedDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!);
        }

        [Fact]
        [Requirement("MCP-ADMIN-PARITY-CUSTOM-FILES", "MCP", RequirementType.Positive, "manage_custom_files supports full parity for list, get, save, and delete prompt and resource files.")]
        public async Task ManageCustomFiles_Parity_AllActions()
        {
            var promptName = $"prompt-parity-{Guid.NewGuid():N}.json";
            var resourceName = $"resource-parity-{Guid.NewGuid():N}.txt";
            var promptContent = "{\"name\":\"system_analysis\",\"description\":\"Analyzes architecture\"}";
            var resourceContent = "# Architecture Resource Guide\n- High availability\n- Low latency";

            // 1. Save Prompt File
            var savePromptArgs = JsonDocument.Parse($@"{{
                ""action"": ""save"",
                ""type"": ""prompts"",
                ""name"": ""{promptName}"",
                ""content"": {JsonSerializer.Serialize(promptContent)}
            }}").RootElement;
            AssertIsSuccess(await _adminMcpServer.CallToolAsync("manage_custom_files", savePromptArgs, "admin_user"));

            // 2. Save Resource File
            var saveResourceArgs = JsonDocument.Parse($@"{{
                ""action"": ""save"",
                ""type"": ""resources"",
                ""name"": ""{resourceName}"",
                ""content"": {JsonSerializer.Serialize(resourceContent)}
            }}").RootElement;
            AssertIsSuccess(await _adminMcpServer.CallToolAsync("manage_custom_files", saveResourceArgs, "admin_user"));

            // 3. Get Prompt File
            var getPromptArgs = JsonDocument.Parse($@"{{ ""action"": ""get"", ""type"": ""prompts"", ""name"": ""{promptName}"" }}").RootElement;
            var getPromptDoc = AssertIsSuccess(await _adminMcpServer.CallToolAsync("manage_custom_files", getPromptArgs, "admin_user"));
            Assert.Contains("system_analysis", getPromptDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!);

            // 4. Get Resource File
            var getResourceArgs = JsonDocument.Parse($@"{{ ""action"": ""get"", ""type"": ""resources"", ""name"": ""{resourceName}"" }}").RootElement;
            var getResourceDoc = AssertIsSuccess(await _adminMcpServer.CallToolAsync("manage_custom_files", getResourceArgs, "admin_user"));
            Assert.Contains("Architecture Resource Guide", getResourceDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!);

            // 5. List Custom Files
            var listPromptsArgs = JsonDocument.Parse(@"{ ""action"": ""list"", ""type"": ""prompts"" }").RootElement;
            var listPromptsDoc = AssertIsSuccess(await _adminMcpServer.CallToolAsync("manage_custom_files", listPromptsArgs, "admin_user"));
            Assert.Contains(promptName, listPromptsDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!);

            var listResourcesArgs = JsonDocument.Parse(@"{ ""action"": ""list"", ""type"": ""resources"" }").RootElement;
            var listResourcesDoc = AssertIsSuccess(await _adminMcpServer.CallToolAsync("manage_custom_files", listResourcesArgs, "admin_user"));
            Assert.Contains(resourceName, listResourcesDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!);

            var listAllArgs = JsonDocument.Parse(@"{ ""action"": ""list"", ""type"": ""all"" }").RootElement;
            var listAllDoc = AssertIsSuccess(await _adminMcpServer.CallToolAsync("manage_custom_files", listAllArgs, "admin_user"));
            var allText = listAllDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.Contains(promptName, allText);
            Assert.Contains(resourceName, allText);

            // 6. Delete Files
            var delPromptArgs = JsonDocument.Parse($@"{{ ""action"": ""delete"", ""type"": ""prompts"", ""name"": ""{promptName}"" }}").RootElement;
            AssertIsSuccess(await _adminMcpServer.CallToolAsync("manage_custom_files", delPromptArgs, "admin_user"));

            var delResourceArgs = JsonDocument.Parse($@"{{ ""action"": ""delete"", ""type"": ""resources"", ""name"": ""{resourceName}"" }}").RootElement;
            AssertIsSuccess(await _adminMcpServer.CallToolAsync("manage_custom_files", delResourceArgs, "admin_user"));

            // 7. Get Deleted File -> Returns Error
            AssertIsError(await _adminMcpServer.CallToolAsync("manage_custom_files", getPromptArgs, "admin_user"));
        }

        [Fact]
        [Requirement("GUARD-ADMIN-CUSTOM-FILES-VALIDATION", "GUARD", RequirementType.Negative, "manage_custom_files rejects invalid prompt JSON syntax and unsupported file categories.")]
        public async Task ManageCustomFiles_ValidationGuardrails()
        {
            // Invalid JSON in prompt template
            var badJsonArgs = JsonDocument.Parse(@"{
                ""action"": ""save"",
                ""type"": ""prompts"",
                ""name"": ""malformed-prompt.json"",
                ""content"": ""{ unquoted_key: 'broken' ""
            }").RootElement;

            var badJsonRes = await _adminMcpServer.CallToolAsync("manage_custom_files", badJsonArgs, "admin_user");
            var badDoc = AssertIsError(badJsonRes);
            Assert.Contains("Invalid JSON format", badDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!);

            // Invalid file type
            var badTypeArgs = JsonDocument.Parse(@"{
                ""action"": ""get"",
                ""type"": ""executables"",
                ""name"": ""malicious.sh""
            }").RootElement;

            var badTypeRes = await _adminMcpServer.CallToolAsync("manage_custom_files", badTypeArgs, "admin_user");
            AssertIsError(badTypeRes);

            // Delete non-existent file
            var delGhostArgs = JsonDocument.Parse(@"{
                ""action"": ""delete"",
                ""type"": ""prompts"",
                ""name"": ""ghost-file-999.json""
            }").RootElement;

            var delGhostRes = await _adminMcpServer.CallToolAsync("manage_custom_files", delGhostArgs, "admin_user");
            AssertIsError(delGhostRes);
        }

        [Fact]
        [Requirement("MCP-ADMIN-PARITY-SYSTEM", "MCP", RequirementType.Positive, "manage_system supports full parity for diagnostics, get_logs, clear_logs, and query_audit actions.")]
        public async Task ManageSystem_Parity_AllActions()
        {
            // 1. Diagnostics
            var diagArgs = JsonDocument.Parse(@"{ ""action"": ""diagnostics"" }").RootElement;
            var diagDoc = AssertIsSuccess(await _adminMcpServer.CallToolAsync("manage_system", diagArgs, "admin_user"));
            var diagText = diagDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.Contains("activeSessions", diagText);
            Assert.Contains("workingSet64", diagText);
            Assert.Contains("machineName", diagText);
            Assert.Contains("osVersion", diagText);
            Assert.Contains("processUptime", diagText);

            // 2. Get Logs
            var logsArgs = JsonDocument.Parse(@"{ ""action"": ""get_logs"", ""limit"": 20 }").RootElement;
            var logsDoc = AssertIsSuccess(await _adminMcpServer.CallToolAsync("manage_system", logsArgs, "admin_user"));
            Assert.Equal(JsonValueKind.Array, logsDoc.RootElement.GetProperty("content").ValueKind);

            // 3. Clear Logs
            var clearArgs = JsonDocument.Parse(@"{ ""action"": ""clear_logs"" }").RootElement;
            var clearDoc = AssertIsSuccess(await _adminMcpServer.CallToolAsync("manage_system", clearArgs, "admin_user"));
            Assert.Contains("cleared", clearDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!);

            // 4. Query Audit Logs
            var auditArgs = JsonDocument.Parse(@"{ ""action"": ""query_audit"", ""take"": 10, ""skip"": 0 }").RootElement;
            var auditDoc = AssertIsSuccess(await _adminMcpServer.CallToolAsync("manage_system", auditArgs, "admin_user"));
            Assert.Equal(JsonValueKind.Array, auditDoc.RootElement.GetProperty("content").ValueKind);
        }

        [Fact]
        [Requirement("MCP-ADMIN-PARITY-TEST-TOOL-CALL", "MCP", RequirementType.Positive, "test_tool_call executes test bench backend tool calls and formats responses.")]
        public async Task TestToolCall_Execution_Parity()
        {
            // Execute test bench call to configured mock backend server
            var callArgs = JsonDocument.Parse(@"{
                ""serverId"": ""mock-calc-server"",
                ""toolName"": ""calculate"",
                ""arguments"": { ""expression"": ""1 + 1"" }
            }").RootElement;

            var res = await _adminMcpServer.CallToolAsync("test_tool_call", callArgs, "admin_tester");
            var doc = AssertIsSuccess(res);
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.Contains("Calculation result: 2", text);

            // Error path: Non-existent server
            var ghostCallArgs = JsonDocument.Parse(@"{
                ""serverId"": ""non-existent-backend"",
                ""toolName"": ""calculate""
            }").RootElement;

            var ghostRes = await _adminMcpServer.CallToolAsync("test_tool_call", ghostCallArgs, "admin_tester");
            var ghostDoc = AssertIsError(ghostRes);
            Assert.Contains("not found", ghostDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!);
        }

        [Fact]
        [Requirement("MCP-ADMIN-PARITY-JSONRPC-DISPATCH", "MCP", RequirementType.Positive, "AdminMcpServer processes standard JSON-RPC 2.0 requests (tools/list, tools/call, ping).")]
        public async Task AdminTools_ProcessRequest_JsonRpcProtocol()
        {
            // 1. tools/list
            var listReq = new JsonRpcRequest
            {
                Id = "jsonrpc-list",
                Method = "tools/list"
            };

            var listResp = await _adminMcpServer.ProcessRequestAsync(listReq, "admin_user");
            Assert.Null(listResp.Error);
            Assert.True(listResp.Result.HasValue);
            var tools = listResp.Result.Value.GetProperty("tools").EnumerateArray().ToList();
            Assert.Equal(10, tools.Count);

            // 2. ping
            var pingReq = new JsonRpcRequest
            {
                Id = "jsonrpc-ping",
                Method = "ping"
            };

            var pingResp = await _adminMcpServer.ProcessRequestAsync(pingReq, "admin_user");
            Assert.Null(pingResp.Error);

            // 3. tools/call
            var callReq = new JsonRpcRequest
            {
                Id = "jsonrpc-call",
                Method = "tools/call",
                Params = JsonDocument.Parse("{\"name\":\"manage_system\",\"arguments\":{\"action\":\"diagnostics\"}}").RootElement
            };

            var callResp = await _adminMcpServer.ProcessRequestAsync(callReq, "admin_user");
            Assert.Null(callResp.Error);
            Assert.True(callResp.Result.HasValue);
            Assert.Contains("activeSessions", callResp.Result.Value.GetProperty("content")[0].GetProperty("text").GetString()!);

            // 4. tools/call missing name
            var missingNameReq = new JsonRpcRequest
            {
                Id = "jsonrpc-missing-name",
                Method = "tools/call",
                Params = JsonDocument.Parse("{\"arguments\":{}}").RootElement
            };

            var missingNameResp = await _adminMcpServer.ProcessRequestAsync(missingNameReq, "admin_user");
            Assert.NotNull(missingNameResp.Error);
            Assert.Equal(-32602, missingNameResp.Error.Code);

            // 5. Unknown method
            var unknownReq = new JsonRpcRequest
            {
                Id = "jsonrpc-unknown",
                Method = "custom/unknown"
            };

            var unknownResp = await _adminMcpServer.ProcessRequestAsync(unknownReq, "admin_user");
            Assert.NotNull(unknownResp.Error);
            Assert.Equal(-32601, unknownResp.Error.Code);
        }

        #region Assert Helpers

        private static JsonDocument AssertIsSuccess(object result)
        {
            var json = JsonSerializer.Serialize(result);
            var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("isError", out var isErrorProp));
            Assert.False(isErrorProp.GetBoolean(), $"Expected success response, but received error: {json}");
            return doc;
        }

        private static JsonDocument AssertIsError(object result)
        {
            var json = JsonSerializer.Serialize(result);
            var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("isError", out var isErrorProp));
            Assert.True(isErrorProp.GetBoolean(), $"Expected error response, but received success: {json}");
            return doc;
        }

        #endregion
    }
}
