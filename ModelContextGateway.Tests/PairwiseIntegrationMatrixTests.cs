using System.Security.Claims;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ModelContextGateway.Tests
{
    /// <summary>
    /// Comprehensive table-driven pairwise integration and contract test matrix (Issue #50).
    /// Tests pairwise combinations of Authentication methods, Identity/SIDs, AppKey scopes,
    /// MCP capabilities, transports, and fail-closed edge conditions.
    /// </summary>
    public class PairwiseIntegrationMatrixTests : IDisposable
    {
        private readonly string _connectionString;
        private readonly SqliteConnection _masterConnection;
        private readonly IDbConnectionFactory _dbFactory;
        private readonly Mock<IAuditLogger> _mockAuditLogger;
        private readonly List<McpServer> _servers;
        private readonly IConfiguration _config;

        public PairwiseIntegrationMatrixTests()
        {
            _connectionString = $"Data Source=PairwiseIntegrationMatrix_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            _masterConnection = new SqliteConnection(_connectionString);
            _masterConnection.Open();

            _masterConnection.Execute(@"
                CREATE TABLE IF NOT EXISTS Settings (
                    Id TEXT PRIMARY KEY,
                    DashboardTitle TEXT DEFAULT 'MCP Gateway', DashboardIcon TEXT DEFAULT 'fa-solid fa-network-wired', GlobalMaxKeys INTEGER DEFAULT 100,
                    UserMaxKeys INTEGER DEFAULT 5
                );
                CREATE TABLE IF NOT EXISTS Servers (
                    Id TEXT PRIMARY KEY,
                    DisplayName TEXT,
                    Type TEXT,
                    Url TEXT,
                    Categories TEXT,
                    Enabled INTEGER DEFAULT 1,
                    IsLocal INTEGER DEFAULT 1,
                    ExecutionTarget TEXT DEFAULT 'auto',
                    RequiresManualApproval INTEGER DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS AccessPolicies (
                    Id TEXT PRIMARY KEY,
                    TargetId TEXT,
                    TargetType TEXT,
                    RequiredGroup TEXT,
                    IsAllowed INTEGER DEFAULT 1
                );
                CREATE TABLE IF NOT EXISTS GroupMappings (
                    ExternalId TEXT PRIMARY KEY,
                    InternalGroup TEXT
                );
                CREATE TABLE IF NOT EXISTS AppKeys (
                    Id TEXT PRIMARY KEY,
                    Name TEXT,
                    Username TEXT,
                    KeyPrefix TEXT,
                    EncryptedKey TEXT,
                    ScopesJson TEXT DEFAULT '[]',
                    OwnerSid TEXT,
                    KeyType TEXT DEFAULT 'personal',
                    ExpiresAt TEXT,
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );
            ");

            // Seed default settings and servers across diverse categories
            _masterConnection.Execute("INSERT INTO Settings (Id, GlobalMaxKeys, UserMaxKeys) VALUES ('default', 100, 5);");
            _masterConnection.Execute("INSERT INTO Servers (Id, DisplayName, Type, Url, Categories, Enabled) VALUES ('ha', 'Home Assistant', 'http', 'http://ha:8123/mcp', '[\"smarthome\",\"iot\"]', 1);");
            _masterConnection.Execute("INSERT INTO Servers (Id, DisplayName, Type, Url, Categories, Enabled) VALUES ('docker', 'Docker Host', 'http', 'http://docker:8000/mcp', '[\"infrastructure\",\"devops\"]', 1);");
            _masterConnection.Execute("INSERT INTO Servers (Id, DisplayName, Type, Url, Categories, Enabled) VALUES ('plex', 'Plex Media', 'http', 'http://plex:32400/mcp', '[\"media\"]', 1);");

            // Seed RBAC Access Policies
            _masterConnection.Execute("INSERT INTO AccessPolicies (Id, TargetId, RequiredGroup, IsAllowed) VALUES ('p_ha_op', 'server:ha', 'SmartHomeOperators', 1);");
            _masterConnection.Execute("INSERT INTO AccessPolicies (Id, TargetId, RequiredGroup, IsAllowed) VALUES ('p_docker_op', 'server:docker', 'DevOps', 1);");
            _masterConnection.Execute("INSERT INTO AccessPolicies (Id, TargetId, RequiredGroup, IsAllowed) VALUES ('p_plex_op', 'server:plex', 'MediaViewers', 1);");
            _masterConnection.Execute("INSERT INTO AccessPolicies (Id, TargetId, RequiredGroup, IsAllowed) VALUES ('p_ha_tool', 'tool:ha__turn_on', 'LightOperators', 1);");
            _masterConnection.Execute("INSERT INTO AccessPolicies (Id, TargetId, RequiredGroup, IsAllowed) VALUES ('p_ha_prompt', 'prompt:ha__summary', 'PromptUsers', 1);");
            _masterConnection.Execute("INSERT INTO AccessPolicies (Id, TargetId, RequiredGroup, IsAllowed) VALUES ('p_ha_resource', 'resource:mcp://ha/states', 'ResourceReaders', 1);");

            // Seed explicit deny policies
            _masterConnection.Execute("INSERT INTO AccessPolicies (Id, TargetId, RequiredGroup, IsAllowed) VALUES ('p_ha_deny_guests', 'server:ha', 'Guests', 0);");
            _masterConnection.Execute("INSERT INTO AccessPolicies (Id, TargetId, RequiredGroup, IsAllowed) VALUES ('p_docker_deny_guests', 'server:docker', 'Guests', 0);");
            _masterConnection.Execute("INSERT INTO AccessPolicies (Id, TargetId, RequiredGroup, IsAllowed) VALUES ('p_ha_danger_deny', 'tool:ha__dangerous_action', 'RestrictedUsers', 0);");

            // Seed Group Mappings (External SID / External Claim -> Internal Group)
            _masterConnection.Execute("INSERT INTO GroupMappings (ExternalId, InternalGroup) VALUES ('S-1-5-21-500', 'SmartHomeOperators');");
            _masterConnection.Execute("INSERT INTO GroupMappings (ExternalId, InternalGroup) VALUES ('S-1-5-21-600', 'DevOps');");
            _masterConnection.Execute("INSERT INTO GroupMappings (ExternalId, InternalGroup) VALUES ('S-1-5-21-700', 'MediaViewers');");
            _masterConnection.Execute("INSERT INTO GroupMappings (ExternalId, InternalGroup) VALUES ('Oidc_DevOps', 'DevOps');");
            _masterConnection.Execute("INSERT INTO GroupMappings (ExternalId, InternalGroup) VALUES ('Oidc_SmartHome', 'SmartHomeOperators');");
            _masterConnection.Execute("INSERT INTO GroupMappings (ExternalId, InternalGroup) VALUES ('DomainUsers', 'StandardUsers');");

            var mockFactory = new Mock<IDbConnectionFactory>();
            mockFactory.Setup(f => f.CreateConnection()).Returns(() =>
            {
                var conn = new SqliteConnection(_connectionString);
                conn.Open();
                return conn;
            });
            mockFactory.Setup(f => f.ProviderName).Returns("sqlite");
            _dbFactory = mockFactory.Object;

            _mockAuditLogger = new Mock<IAuditLogger>();

            _config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Admin:GroupSid", "S-1-5-32-544" },
                { "Admin:Username", "admin_user" },
                { "Audit:FailClosed", "false" }
            }).Build();

            _servers = new List<McpServer>
            {
                new McpServer { Id = "ha", DisplayName = "Home Assistant", Type = "http", Url = "http://ha:8123/mcp", Categories = new List<string> { "smarthome", "iot" }, Enabled = true },
                new McpServer { Id = "docker", DisplayName = "Docker Host", Type = "http", Url = "http://docker:8000/mcp", Categories = new List<string> { "infrastructure", "devops" }, Enabled = true },
                new McpServer { Id = "plex", DisplayName = "Plex Media", Type = "http", Url = "http://plex:32400/mcp", Categories = new List<string> { "media" }, Enabled = true }
            };
        }

        public void Dispose()
        {
            _masterConnection.Dispose();
        }

        private HttpContext CreateHttpContext(
            string username = "testuser",
            List<string>? groups = null,
            List<string>? sids = null,
            bool isAppKey = false,
            List<string>? appKeyScopes = null,
            string? rawScopesJson = null,
            bool anonymous = false)
        {
            var context = new DefaultHttpContext();

            if (!anonymous)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, username)
                };

                if (groups != null)
                {
                    foreach (var g in groups)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, g));
                        claims.Add(new Claim("Group", g));
                        claims.Add(new Claim("groups", g));
                    }
                }

                if (sids != null)
                {
                    foreach (var sid in sids)
                    {
                        claims.Add(new Claim("Sid", sid));
                        claims.Add(new Claim(ClaimTypes.GroupSid, sid));
                    }
                }

                var authType = isAppKey ? "AppKey" : "SSO";
                var identity = new ClaimsIdentity(claims, authType);
                context.User = new ClaimsPrincipal(identity);
            }
            else
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity());
            }

            if (isAppKey)
            {
                context.Items["AppKeyUsed"] = true;
                if (rawScopesJson != null)
                {
                    context.Items["AppKeyScopes"] = rawScopesJson;
                }
                else if (appKeyScopes != null)
                {
                    context.Items["AppKeyScopes"] = JsonSerializer.Serialize(appKeyScopes);
                }
            }

            var services = new ServiceCollection();
            services.AddSingleton(_dbFactory);
            services.AddSingleton(_mockAuditLogger.Object);
            services.AddSingleton<IConfiguration>(_config);

            var mockProvider = new Mock<IIdentityProvider>();
            mockProvider.Setup(p => p.ResolveIdentityAsync(It.IsAny<HttpContext>()))
                .ReturnsAsync(() =>
                {
                    if (anonymous)
                    {
                        return new UserIdentityContext(string.Empty, "Anonymous", new List<string>(), string.Empty, new List<string>());
                    }
                    var authType = isAppKey ? "AppKey" : "SSO";
                    return new UserIdentityContext(username, authType, groups ?? new List<string>(), string.Empty, sids ?? new List<string>());
                });

            var compositeProvider = new CompositeIdentityProvider(new[] { mockProvider.Object });
            services.AddSingleton(compositeProvider);

            context.RequestServices = services.BuildServiceProvider();
            return context;
        }

        private ClientSession CreateSession(HttpContext context, MockHttpMessageHandler? httpHandler = null)
        {
            var handler = httpHandler ?? new MockHttpMessageHandler();
            var httpClient = new HttpClient(handler);
            var logger = new Mock<ILogger<ClientSession>>();
            var embeddingService = new Mock<IEmbeddingService>();

            return new ClientSession(
                sessionId: $"session_{Guid.NewGuid():N}",
                clientResponse: context.Response,
                servers: _servers,
                httpClient: httpClient,
                embeddingService: embeddingService.Object,
                sessionManager: null,
                logger: logger.Object,
                rootServices: context.RequestServices
            );
        }

        #region Theory 1: AppKey Scopes vs Capabilities & Target Matrix

        /// <summary>
        /// Verifies pairwise combination of AppKey scopes across all MCP capabilities and backend targets.
        /// </summary>
        [Theory]
        [Requirement("AUTH-02", "AppKey scopes restrict access precisely across all MCP capabilities and backend targets", Type = RequirementType.Positive, Category = "AUTH")]
        // Wildcard Scopes -> Grants all capabilities across all servers
        [InlineData("*", "tools/call", "ha__turn_on", true)]
        [InlineData("all", "prompts/get", "ha__summary", true)]
        [InlineData("mcp_client", "resources/read", "mcp://ha/states", true)]
        [InlineData("*", "resources/templates/list", "mcp://ha/sensor/{id}", true)]
        [InlineData("all", "completion/complete", "ha__summary", true)]
        [InlineData("*", "tools/call", "docker__restart", true)]
        // Server-level scope -> Grants all capabilities under specific server only
        [InlineData("server:ha", "tools/call", "ha__turn_on", true)]
        [InlineData("server:ha", "prompts/get", "ha__summary", true)]
        [InlineData("server:ha", "resources/read", "mcp://ha/states", true)]
        [InlineData("server:ha", "resources/templates/list", "mcp://ha/sensor/{id}", true)]
        [InlineData("server:ha", "completion/complete", "ha__summary", true)]
        [InlineData("server:ha", "tools/call", "docker__restart", false)]
        [InlineData("server:docker", "tools/call", "docker__restart", true)]
        [InlineData("server:docker", "tools/call", "ha__turn_on", false)]
        // Category-level scopes -> Grants access to all servers resolving to that category
        [InlineData("category:smarthome", "tools/call", "ha__turn_on", true)]
        [InlineData("category:smarthome", "prompts/get", "ha__summary", true)]
        [InlineData("category:smarthome", "resources/read", "mcp://ha/states", true)]
        [InlineData("category:smarthome", "tools/call", "docker__restart", false)]
        [InlineData("category:infrastructure", "tools/call", "docker__restart", true)]
        [InlineData("category:infrastructure", "tools/call", "ha__turn_on", false)]
        [InlineData("category:media", "tools/call", "plex__play", true)]
        [InlineData("category:media", "tools/call", "ha__turn_on", false)]
        // Group alias scopes -> behaves identically to category scopes
        [InlineData("group:smarthome", "tools/call", "ha__turn_on", true)]
        [InlineData("group:infrastructure", "tools/call", "docker__restart", true)]
        [InlineData("group:infrastructure", "tools/call", "ha__turn_on", false)]
        // Granular capability scopes
        [InlineData("tool:ha__turn_on", "tools/call", "ha__turn_on", true)]
        [InlineData("tool:ha__turn_on", "tools/call", "ha__turn_off", false)]
        [InlineData("tool:ha__turn_on", "prompts/get", "ha__summary", false)]
        [InlineData("prompt:ha__summary", "prompts/get", "ha__summary", true)]
        [InlineData("prompt:ha__summary", "tools/call", "ha__turn_on", false)]
        [InlineData("resource:mcp://ha/states", "resources/read", "mcp://ha/states", true)]
        [InlineData("resource:mcp://ha/states", "tools/call", "ha__turn_on", false)]
        // Router meta-mode tools in standard session without wildcard
        [InlineData("tool:ha__turn_on", "tools/call", "docker__restart", false)]
        // Invalid / unknown scopes
        [InlineData("category:unknown_category", "tools/call", "ha__turn_on", false)]
        [InlineData("server:nonexistent_server", "tools/call", "ha__turn_on", false)]
        [InlineData("tool:unrelated_tool", "tools/call", "ha__turn_on", false)]
        public async Task Pairwise_AppKeyScopes_RestrictsAccessPrecisely(
            string scope,
            string capabilityMethod,
            string targetId,
            bool expectedAuthorized)
        {
            // Arrange - Caller authenticated via AppKey with given scope
            // We also seed a general user policy allowing the caller identity to access server:ha, server:docker, server:plex
            using (var conn = _dbFactory.CreateConnection())
            {
                conn.Execute("INSERT INTO AccessPolicies (Id, TargetId, RequiredGroup, IsAllowed) VALUES ('p_k_ha', 'server:ha', 'appKeyUser', 1);");
                conn.Execute("INSERT INTO AccessPolicies (Id, TargetId, RequiredGroup, IsAllowed) VALUES ('p_k_docker', 'server:docker', 'appKeyUser', 1);");
                conn.Execute("INSERT INTO AccessPolicies (Id, TargetId, RequiredGroup, IsAllowed) VALUES ('p_k_plex', 'server:plex', 'appKeyUser', 1);");
            }

            var context = CreateHttpContext("appKeyUser", isAppKey: true, appKeyScopes: new List<string> { scope });
            var session = CreateSession(context);

            // Act
            var isAuthorized = await session.IsUserAuthorizedAsync(capabilityMethod, targetId, context);

            // Assert
            isAuthorized.Should().Be(expectedAuthorized, $"Scope '{scope}' with method '{capabilityMethod}' on target '{targetId}' should evaluate to {expectedAuthorized}");
        }

        #endregion

        #region Theory 2: SSO Identity & Group Mappings Pairwise Verification

        /// <summary>
        /// Verifies pairwise combination of SSO identities, SIDs, and group mappings across all targets.
        /// </summary>
        [Theory]
        [Requirement("AUTH-03", "SSO identity and group mappings resolve Windows SIDs and OIDC claims to internal access roles", Type = RequirementType.Positive, Category = "AUTH")]
        // Administrator bypass via well-known Admin SID
        [InlineData("admin_sid", null, "S-1-5-32-544", "tools/call", "ha__turn_on", true)]
        [InlineData("admin_sid", null, "S-1-5-32-544", "tools/call", "docker__restart", true)]
        [InlineData("admin_sid", null, "S-1-5-32-544", "prompts/get", "ha__summary", true)]
        [InlineData("admin_sid", null, "S-1-5-32-544", "resources/read", "mcp://ha/states", true)]
        [InlineData("admin_sid", null, "S-1-5-32-544", "completion/complete", "ha__summary", true)]
        // Admin with full_admin group AND Admin SID
        [InlineData("admin_user", "full_admin", "S-1-5-32-544", "tools/call", "ha__turn_on", true)]
        [InlineData("admin_user", "full_admin", "S-1-5-32-544", "tools/call", "docker__restart", true)]
        // Direct group membership
        [InlineData("operator1", "SmartHomeOperators", null, "tools/call", "ha__turn_on", true)]
        [InlineData("operator1", "SmartHomeOperators", null, "prompts/get", "ha__summary", true)]
        [InlineData("operator1", "SmartHomeOperators", null, "resources/read", "mcp://ha/states", true)]
        [InlineData("operator1", "SmartHomeOperators", null, "tools/call", "docker__restart", false)]
        [InlineData("devops1", "DevOps", null, "tools/call", "docker__restart", true)]
        [InlineData("devops1", "DevOps", null, "tools/call", "ha__turn_on", false)]
        [InlineData("media1", "MediaViewers", null, "tools/call", "plex__play", true)]
        [InlineData("media1", "MediaViewers", null, "tools/call", "ha__turn_on", false)]
        // Group mapping from external SID (e.g. S-1-5-21-500 -> SmartHomeOperators)
        [InlineData("sidUser1", null, "S-1-5-21-500", "tools/call", "ha__turn_on", true)]
        [InlineData("sidUser1", null, "S-1-5-21-500", "prompts/get", "ha__summary", true)]
        [InlineData("sidUser1", null, "S-1-5-21-500", "tools/call", "docker__restart", false)]
        // Group mapping from external SID (e.g. S-1-5-21-600 -> DevOps)
        [InlineData("sidUser2", null, "S-1-5-21-600", "tools/call", "docker__restart", true)]
        [InlineData("sidUser2", null, "S-1-5-21-600", "tools/call", "ha__turn_on", false)]
        // Group mapping from external OIDC claim (e.g. Oidc_DevOps -> DevOps)
        [InlineData("oidcUser1", "Oidc_DevOps", null, "tools/call", "docker__restart", true)]
        [InlineData("oidcUser1", "Oidc_DevOps", null, "tools/call", "ha__turn_on", false)]
        // Explicit deny policy takes precedence over allow
        [InlineData("guest1", "Guests,SmartHomeOperators", null, "tools/call", "ha__turn_on", false)]
        [InlineData("guest2", "Guests,DevOps", null, "tools/call", "docker__restart", false)]
        // Unmapped / Invalid SID fails closed
        [InlineData("unknownSidUser", null, "S-1-5-21-9999", "tools/call", "ha__turn_on", false)]
        // Standard user without matching policies fails closed
        [InlineData("standardUser", "StandardUsers", null, "tools/call", "ha__turn_on", false)]
        public async Task Pairwise_SsoIdentityAndGroupMappings_EvaluateCorrectly(
            string username,
            string? groupCsv,
            string? sidCsv,
            string capabilityMethod,
            string targetId,
            bool expectedAuthorized)
        {
            // Arrange
            var groups = groupCsv?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            var sids = sidCsv?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            var context = CreateHttpContext(username, groups: groups, sids: sids);
            var session = CreateSession(context);

            // Act
            var isAuthorized = await session.IsUserAuthorizedAsync(capabilityMethod, targetId, context);

            // Assert
            isAuthorized.Should().Be(expectedAuthorized, $"User '{username}' with groups '{groupCsv}' and sids '{sidCsv}' calling '{capabilityMethod}' on '{targetId}' should be {expectedAuthorized}");
        }

        #endregion

        #region Theory 3: All 5 Capability Methods under Admin vs Permitted vs Denied vs Anonymous

        /// <summary>
        /// Verifies that all 5 MCP capability methods enforce caller role authorizations consistently.
        /// </summary>
        [Theory]
        [Requirement("MCP-02", "All MCP protocol capabilities enforce caller role authorizations consistently", Type = RequirementType.Positive, Category = "MCP")]
        // tools/call
        [InlineData("tools/call", "ha__turn_on", "Admin", true)]
        [InlineData("tools/call", "ha__turn_on", "Permitted", true)]
        [InlineData("tools/call", "ha__turn_on", "Denied", false)]
        [InlineData("tools/call", "ha__turn_on", "Unseeded", false)]
        [InlineData("tools/call", "ha__turn_on", "Anonymous", false)]
        // prompts/get
        [InlineData("prompts/get", "ha__summary", "Admin", true)]
        [InlineData("prompts/get", "ha__summary", "Permitted", true)]
        [InlineData("prompts/get", "ha__summary", "Denied", false)]
        [InlineData("prompts/get", "ha__summary", "Unseeded", false)]
        [InlineData("prompts/get", "ha__summary", "Anonymous", false)]
        // resources/read
        [InlineData("resources/read", "mcp://ha/states", "Admin", true)]
        [InlineData("resources/read", "mcp://ha/states", "Permitted", true)]
        [InlineData("resources/read", "mcp://ha/states", "Denied", false)]
        [InlineData("resources/read", "mcp://ha/states", "Unseeded", false)]
        [InlineData("resources/read", "mcp://ha/states", "Anonymous", false)]
        // resources/templates/list
        [InlineData("resources/templates/list", "mcp://ha/sensor/{id}", "Admin", true)]
        [InlineData("resources/templates/list", "mcp://ha/sensor/{id}", "Permitted", true)]
        [InlineData("resources/templates/list", "mcp://ha/sensor/{id}", "Denied", false)]
        [InlineData("resources/templates/list", "mcp://ha/sensor/{id}", "Unseeded", false)]
        [InlineData("resources/templates/list", "mcp://ha/sensor/{id}", "Anonymous", false)]
        // completion/complete
        [InlineData("completion/complete", "ha__summary", "Admin", true)]
        [InlineData("completion/complete", "ha__summary", "Permitted", true)]
        [InlineData("completion/complete", "ha__summary", "Denied", false)]
        [InlineData("completion/complete", "ha__summary", "Unseeded", false)]
        [InlineData("completion/complete", "ha__summary", "Anonymous", false)]
        // completion for resource template
        [InlineData("completion/complete", "mcp://ha/sensor/{id}", "Admin", true)]
        [InlineData("completion/complete", "mcp://ha/sensor/{id}", "Permitted", true)]
        [InlineData("completion/complete", "mcp://ha/sensor/{id}", "Denied", false)]
        [InlineData("completion/complete", "mcp://ha/sensor/{id}", "Unseeded", false)]
        [InlineData("completion/complete", "mcp://ha/sensor/{id}", "Anonymous", false)]
        public async Task Pairwise_AllCapabilities_UnderCallerRoles_EvaluateCorrectly(
            string capabilityMethod,
            string targetId,
            string roleProfile,
            bool expectedAuthorized)
        {
            // Arrange
            HttpContext context;
            switch (roleProfile)
            {
                case "Admin":
                    context = CreateHttpContext("adminUser", sids: new List<string> { "S-1-5-32-544" });
                    break;
                case "Permitted":
                    context = CreateHttpContext("opUser", groups: new List<string> { "SmartHomeOperators" });
                    break;
                case "Denied":
                    context = CreateHttpContext("deniedUser", groups: new List<string> { "SmartHomeOperators", "Guests" });
                    break;
                case "Unseeded":
                    context = CreateHttpContext("unseededUser", groups: new List<string> { "OtherGroup" });
                    break;
                case "Anonymous":
                    context = CreateHttpContext(anonymous: true);
                    break;
                default:
                    throw new ArgumentException($"Unknown profile: {roleProfile}");
            }

            var session = CreateSession(context);

            // Act
            var isAuthorized = await session.IsUserAuthorizedAsync(capabilityMethod, targetId, context);

            // Assert
            isAuthorized.Should().Be(expectedAuthorized, $"Method '{capabilityMethod}' on '{targetId}' under role profile '{roleProfile}' should be {expectedAuthorized}");
        }

        #endregion

        #region Theory 4: Fail-Closed Boundary & Malformed Inputs

        /// <summary>
        /// Ensures null or whitespace capability targets fail closed immediately.
        /// </summary>
        [Theory]
        [Requirement("GUARD-AUTH-NULL-TARGET", "Null or empty capability targets must immediately fail closed and return unauthorized", Type = RequirementType.Negative, Category = "GUARD")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\t\n")]
        public async Task Pairwise_NullOrEmptyTarget_FailsClosed_ReturnsFalse(string? targetId)
        {
            // Arrange - Even with Admin credentials, empty target must fail closed cleanly
            var context = CreateHttpContext("adminUser", sids: new List<string> { "S-1-5-32-544" });
            var session = CreateSession(context);

            // Act
            var result = await session.IsUserAuthorizedAsync("tools/call", targetId!, context);

            // Assert
            result.Should().BeFalse("Null or whitespace target must immediately return false");
        }

        /// <summary>
        /// Ensures corrupted AppKey scopes JSON fails closed safely and rejects execution.
        /// </summary>
        [Fact]
        [Requirement("GUARD-APPKEY-MALFORMED-SCOPES", "Corrupted AppKey scopes JSON must fail closed and reject execution", Type = RequirementType.Negative, Category = "GUARD")]
        public async Task Pairwise_CorruptedAppKeyScopesJson_FailsClosed_ReturnsFalse()
        {
            // Arrange - AppKey with unparseable corrupt JSON string in items
            var context = CreateHttpContext("appKeyCaller", isAppKey: true, rawScopesJson: "{corrupted json syntax invalid}");
            var session = CreateSession(context);

            // Act
            var result = await session.IsUserAuthorizedAsync("tools/call", "ha__turn_on", context);

            // Assert
            result.Should().BeFalse("Corrupted scopes JSON must fail closed");
        }

        /// <summary>
        /// Ensures malformed completion payloads or unmapped backends throw or fail closed.
        /// </summary>
        [Theory]
        [Requirement("GUARD-04", "Malformed completion payloads or unmapped backends must fail closed safely", Type = RequirementType.Negative, Category = "GUARD")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"completion/complete\",\"params\":{}}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"completion/complete\",\"params\":{\"ref\":null}}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"completion/complete\",\"params\":{\"ref\":{\"type\":\"unknown/type\"}}}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"completion/complete\",\"params\":{\"ref\":{\"type\":\"ref/prompt\",\"name\":\"ghost_server__prompt\"}}}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"completion/complete\",\"params\":{\"ref\":{\"type\":\"ref/resource\",\"uriTemplate\":\"mcp://ghost_server/sensor/{id}\"}}}")]
        public async Task Pairwise_CompleteAsync_MalformedOrMissingBackends_ThrowsOrFailsClosed(string payload)
        {
            // Arrange
            var context = CreateHttpContext("adminUser", sids: new List<string> { "S-1-5-32-544" });
            var session = CreateSession(context);

            // Act & Assert - must throw an exception (UnauthorizedAccessException or InvalidOperationException)
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await session.CompleteAsync(payload, context);
            });
        }

        /// <summary>
        /// Ensures database disconnection or failure fails closed safely without leaking access.
        /// </summary>
        [Fact]
        [Requirement("GUARD-04", "Database disconnection or internal faults must fail closed safely without leaking access", Type = RequirementType.Negative, Category = "GUARD")]
        public async Task Pairwise_DatabaseDisconnection_FailsClosedSafely()
        {
            // Arrange - Mock DB Factory that throws an exception when connecting
            var brokenDbFactory = new Mock<IDbConnectionFactory>();
            brokenDbFactory.Setup(f => f.CreateConnection()).Throws(new InvalidOperationException("DB Down"));
            brokenDbFactory.Setup(f => f.ProviderName).Returns("sqlite");

            var services = new ServiceCollection();
            services.AddSingleton(brokenDbFactory.Object);
            services.AddSingleton(_mockAuditLogger.Object);
            services.AddSingleton(_config);

            var mockProvider = new Mock<IIdentityProvider>();
            mockProvider.Setup(p => p.ResolveIdentityAsync(It.IsAny<HttpContext>()))
                .ReturnsAsync(new UserIdentityContext("operator1", "SSO", new List<string> { "SmartHomeOperators" }, string.Empty, new List<string>()));
            services.AddSingleton(new CompositeIdentityProvider(new[] { mockProvider.Object }));

            var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
            var session = CreateSession(context);

            // Act
            var isAuthorized = await session.IsUserAuthorizedAsync("tools/call", "ha__turn_on", context);

            // Assert
            isAuthorized.Should().BeFalse("Database connection failures must fail closed safely");
        }

        #endregion

        #region Theory 5: Meta-Mode Search & Execute Routing Contract

        /// <summary>
        /// Verifies that router meta-mode execute_tool strictly enforces target tool authorization policies.
        /// </summary>
        [Theory]
        [Requirement("MCP-01", "Meta-mode execute_tool strictly enforces target tool authorization policies", Type = RequirementType.Positive, Category = "MCP")]
        [InlineData("ha__turn_on", true)]
        [InlineData("docker__restart", false)]
        public async Task Pairwise_MetaMode_ExecuteTool_EnforcesTargetAuthorization(string targetToolName, bool expectedAuthorized)
        {
            // Arrange
            var context = CreateHttpContext("opUser", groups: new List<string> { "SmartHomeOperators" });
            var session = CreateSession(context);
            session.IsMetaMode = true;

            // Act - Calling execute_tool with specific target tool name
            var isAuthorized = await session.IsUserAuthorizedAsync("tools/call", targetToolName, context);

            // Assert
            isAuthorized.Should().Be(expectedAuthorized, $"ExecuteTool targeting '{targetToolName}' for SmartHomeOperators should be {expectedAuthorized}");
        }

        #endregion
    }
}
