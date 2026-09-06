using System.Data;
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
    public class UnifiedMcpAuthorizationTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly IDbConnectionFactory _dbFactory;
        private readonly Mock<IAuditLogger> _mockAuditLogger;
        private readonly List<McpServer> _servers;

        public UnifiedMcpAuthorizationTests()
        {
            var dbName = $"Data Source=UnifiedAuthDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            _connection = new SqliteConnection(dbName);
            _connection.Open();

            _connection.Execute(@"
                CREATE TABLE IF NOT EXISTS AccessPolicies (
                    Id TEXT PRIMARY KEY,
                    TargetId TEXT,
                    RequiredGroup TEXT,
                    IsAllowed INTEGER DEFAULT 1
                );
                CREATE TABLE IF NOT EXISTS GroupMappings (
                    ExternalId TEXT PRIMARY KEY,
                    InternalGroup TEXT
                );
            ");

            var mockFactory = new Mock<IDbConnectionFactory>();
            mockFactory.Setup(f => f.CreateConnection()).Returns(() => new SqliteConnection(dbName));
            mockFactory.Setup(f => f.ProviderName).Returns("sqlite");
            _dbFactory = mockFactory.Object;

            _mockAuditLogger = new Mock<IAuditLogger>();

            _servers = new List<McpServer>
            {
                new McpServer { Id = "ha", DisplayName = "Home Assistant", Type = "http", Url = "http://ha:8123/mcp", Enabled = true },
                new McpServer { Id = "docker", DisplayName = "Docker", Type = "http", Url = "http://docker:8000/mcp", Enabled = true },
                new McpServer { Id = "plex", DisplayName = "Plex", Type = "http", Url = "http://plex:32400/mcp", Enabled = true },
                new McpServer { Id = "postgres-mcp-homebox", Alias = "homebox_db", DisplayName = "Homebox DB", Type = "http", Url = "http://postgres-mcp-homebox:8000/mcp", Enabled = true }
            };
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private void SeedPolicy(string id, string targetId, string requiredGroup, bool isAllowed)
        {
            _connection.Execute(@"
                INSERT INTO AccessPolicies (Id, TargetId, RequiredGroup, IsAllowed)
                VALUES (@Id, @TargetId, @RequiredGroup, @IsAllowed);",
                new { Id = id, TargetId = targetId, RequiredGroup = requiredGroup, IsAllowed = isAllowed ? 1 : 0 });
        }

        private HttpContext CreateHttpContext(
            string username = "testuser",
            List<string>? groups = null,
            List<string>? sids = null,
            bool isAppKey = false,
            List<string>? appKeyScopes = null)
        {
            var context = new DefaultHttpContext();

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
                }
            }

            if (sids != null)
            {
                foreach (var sid in sids)
                {
                    claims.Add(new Claim("Sid", sid));
                }
            }

            var authType = isAppKey ? "AppKey" : "SSO";
            var identity = new ClaimsIdentity(claims, authType);
            context.User = new ClaimsPrincipal(identity);

            if (isAppKey)
            {
                context.Items["AppKeyUsed"] = true;
                if (appKeyScopes != null)
                {
                    context.Items["AppKeyScopes"] = JsonSerializer.Serialize(appKeyScopes);
                }
            }

            var services = new ServiceCollection();
            services.AddSingleton(_dbFactory);
            services.AddSingleton(_mockAuditLogger.Object);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Admin:GroupSid", "S-1-5-32-544" },
                { "Audit:FailClosed", "false" }
            }).Build();
            services.AddSingleton<IConfiguration>(config);

            var mockProvider = new Mock<IIdentityProvider>();
            mockProvider.Setup(p => p.ResolveIdentityAsync(It.IsAny<HttpContext>()))
                .ReturnsAsync(new UserIdentityContext(username, authType, groups ?? new List<string>(), "", sids ?? new List<string>()));
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
                sessionId: "test-unified-session",
                clientResponse: context.Response,
                servers: _servers,
                httpClient: httpClient,
                embeddingService: embeddingService.Object,
                sessionManager: null,
                logger: logger.Object,
                rootServices: context.RequestServices
            );
        }

        [Theory]
        [InlineData("tools/call", "ha__turn_on")]
        [InlineData("prompts/get", "ha__summarize")]
        [InlineData("resources/read", "mcp://ha/states")]
        [InlineData("resources/templates/list", "mcp://ha/sensor/{id}")]
        [InlineData("completion/complete", "ha__summarize")]
        [InlineData("completion/complete", "mcp://ha/sensor/{id}")]
        [InlineData("resources/read", "logs://ha/today")]
        [InlineData("resources/read", "router://metrics")]
        [Requirement("AUTH-01", "AUTH", RequirementType.Positive, "Administrator identities bypass granular capability policies and have full access to all MCP methods.")]
        public async Task AdminBypass_AllowsAllCapabilities_EvenWithoutDbPolicies(string method, string targetId)
        {
            // Arrange - Caller has Admin SID (S-1-5-32-544)
            var context = CreateHttpContext("adminUser", sids: new List<string> { "S-1-5-32-544" });
            var session = CreateSession(context);

            // Act
            var isAuthorized = await session.IsUserAuthorizedAsync(method, targetId, context);

            // Assert
            isAuthorized.Should().BeTrue($"Admin SID should bypass authorization for {method} targeting {targetId}");
        }

        [Theory]
        [InlineData("tools/call", "ha__turn_on")]
        [InlineData("prompts/get", "ha__summarize")]
        [InlineData("resources/read", "mcp://ha/states")]
        [InlineData("resources/templates/list", "mcp://ha/sensor/{id}")]
        [InlineData("completion/complete", "ha__summarize")]
        [InlineData("completion/complete", "mcp://ha/sensor/{id}")]
        [Requirement("GUARD-01", "GUARD", RequirementType.Negative, "Non-admin identities default to denied fail-closed when no matching policies exist.")]
        public async Task NonAdmin_DefaultsToDeny_WhenNoMatchingPoliciesConfigured(string method, string targetId)
        {
            // Arrange - Non-admin user with no policies in DB
            var context = CreateHttpContext("regularUser", groups: new List<string> { "Users" }, sids: new List<string> { "S-1-5-32-545" });
            var session = CreateSession(context);

            // Act
            var isAuthorized = await session.IsUserAuthorizedAsync(method, targetId, context);

            // Assert
            isAuthorized.Should().BeFalse($"Fail-closed default should deny {method} for {targetId} when no policies exist");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [Requirement("GUARD-01", "GUARD", RequirementType.Negative, "IsUserAuthorizedAsync fails closed on null, empty, or whitespace target identifiers.")]
        public async Task IsUserAuthorizedAsync_FailsClosed_OnNullOrWhitespaceTarget(string? targetId)
        {
            var context = CreateHttpContext("adminUser", sids: new List<string> { "S-1-5-32-544" });
            var session = CreateSession(context);

            var result = await session.IsUserAuthorizedAsync("tools/call", targetId!, context);
            result.Should().BeFalse();
        }

        [Fact]
        [Requirement("AUTH-01", "AUTH", RequirementType.Positive, "Server-level access policies authorize all child tools, prompts, and resources under that server.")]
        public async Task ServerLevelPolicy_AuthorizesAllCapabilitiesUnderServer()
        {
            // Arrange - Allow all capabilities under server 'ha' for 'SmartHomeOperators'
            SeedPolicy("p1", "server:ha", "SmartHomeOperators", true);

            var context = CreateHttpContext("operatorUser", groups: new List<string> { "SmartHomeOperators" });
            var session = CreateSession(context);

            // Act & Assert across all capability types
            (await session.IsUserAuthorizedAsync("tools/call", "ha__turn_on", context)).Should().BeTrue("tool under ha");
            (await session.IsUserAuthorizedAsync("tools/call", "ha__turn_off", context)).Should().BeTrue("second tool under ha");
            (await session.IsUserAuthorizedAsync("prompts/get", "ha__summarize", context)).Should().BeTrue("prompt under ha");
            (await session.IsUserAuthorizedAsync("resources/read", "mcp://ha/states", context)).Should().BeTrue("resource under ha");
            (await session.IsUserAuthorizedAsync("resources/templates/list", "mcp://ha/sensor/{id}", context)).Should().BeTrue("template under ha");
            (await session.IsUserAuthorizedAsync("completion/complete", "ha__summarize", context)).Should().BeTrue("completion for prompt under ha");
            (await session.IsUserAuthorizedAsync("completion/complete", "mcp://ha/sensor/{id}", context)).Should().BeTrue("completion for template under ha");

            // Server 'docker' should still be denied
            (await session.IsUserAuthorizedAsync("tools/call", "docker__restart", context)).Should().BeFalse("docker tool should remain denied");
        }

        [Fact]
        [Requirement("GUARD-01", "GUARD", RequirementType.Negative, "Explicit policy deny rules override group allows.")]
        public async Task ExplicitDeny_OverridesGroupAllow()
        {
            // Arrange
            SeedPolicy("p1", "server:ha", "SmartHomeOperators", true);
            SeedPolicy("p2", "tool:ha__dangerous_action", "RestrictedUsers", false);

            // User belongs to both SmartHomeOperators and RestrictedUsers
            var context = CreateHttpContext("restrictedOperator", groups: new List<string> { "SmartHomeOperators", "RestrictedUsers" });
            var session = CreateSession(context);

            // Act & Assert
            (await session.IsUserAuthorizedAsync("tools/call", "ha__turn_on", context)).Should().BeTrue("standard tool allowed by server policy");
            (await session.IsUserAuthorizedAsync("tools/call", "ha__dangerous_action", context)).Should().BeFalse("explicit deny overrides server allow");
        }

        [Theory]
        [InlineData("all", true, true, true, true)]
        [InlineData("mcp_client", true, true, true, true)]
        [InlineData("*", true, true, true, true)]
        [InlineData("server:ha", true, true, true, false)]
        [InlineData("tool:ha__turn_on", true, false, false, false)]
        [InlineData("prompt:ha__summarize", false, true, false, false)]
        [InlineData("resource:mcp://ha/states", false, false, true, false)]
        [InlineData("resource_template:mcp://ha/sensor/{id}", false, false, false, false)]
        [Requirement("AUTH-02", "AUTH", RequirementType.Positive, "AppKey scopes precisely restrict access across tools, prompts, and resources.")]
        public async Task AppKeyScopes_RestrictTargetAccessPrecisely(
            string scope,
            bool expectToolTurnOn,
            bool expectPromptSummarize,
            bool expectResourceStates,
            bool expectDockerRestart)
        {
            // Arrange
            var context = CreateHttpContext("appKeyCaller", isAppKey: true, appKeyScopes: new List<string> { scope });
            var session = CreateSession(context);

            // Seed DB policies allowing 'appKeyCaller'
            SeedPolicy("p1", "server:ha", "appKeyCaller", true);
            SeedPolicy("p2", "server:docker", "appKeyCaller", true);

            // Act & Assert
            (await session.IsUserAuthorizedAsync("tools/call", "ha__turn_on", context)).Should().Be(expectToolTurnOn);
            (await session.IsUserAuthorizedAsync("prompts/get", "ha__summarize", context)).Should().Be(expectPromptSummarize);
            (await session.IsUserAuthorizedAsync("resources/read", "mcp://ha/states", context)).Should().Be(expectResourceStates);
            (await session.IsUserAuthorizedAsync("tools/call", "docker__restart", context)).Should().Be(expectDockerRestart);
        }

        [Theory]
        [InlineData("ha/turn_on")]
        [InlineData("ha:turn_on")]
        [InlineData("ha__turn_on")]
        [Requirement("MCP-30", "MCP", RequirementType.Positive, "IsUserAuthorizedAsync matches granular tool policies across /, :, and __ delimiters.")]
        public async Task IsUserAuthorizedAsync_MatchesToolPolicy_AcrossDelimiters(string targetTool)
        {
            var context = CreateHttpContext("userA", groups: new List<string> { "Operators" });
            var session = CreateSession(context);

            SeedPolicy("p1", "tool:ha__turn_on", "Operators", true);

            var isAuth = await session.IsUserAuthorizedAsync("tools/call", targetTool, context);
            isAuth.Should().BeTrue();
        }

        [Fact]
        [Requirement("MCP-30", "MCP", RequirementType.Positive, "IsUserAuthorizedAsync resolves server aliases and IDs interchangeably when evaluating server policies.")]
        public async Task IsUserAuthorizedAsync_ResolvesAliasesAndServerIds_ForServerPolicies()
        {
            var context = CreateHttpContext("userA", groups: new List<string> { "DbAdmins" });
            var session = CreateSession(context);

            // Policy seeded with full server ID
            SeedPolicy("p1", "server:postgres-mcp-homebox", "DbAdmins", true);

            // Verified against alias tool invocation across slash, colon, and double underscore
            (await session.IsUserAuthorizedAsync("tools/call", "homebox_db/execute_sql", context)).Should().BeTrue();
            (await session.IsUserAuthorizedAsync("tools/call", "homebox_db:execute_sql", context)).Should().BeTrue();
            (await session.IsUserAuthorizedAsync("tools/call", "homebox_db__execute_sql", context)).Should().BeTrue();
            (await session.IsUserAuthorizedAsync("tools/call", "postgres-mcp-homebox/execute_sql", context)).Should().BeTrue();
        }

        [Fact]
        [Requirement("AUTH-01", "AUTH", RequirementType.Positive, "tools/list filters exposed backend tools according to caller permissions.")]
        public async Task ListToolsAsync_FiltersUnauthorizedTools()
        {
            // Arrange
            var handler = new MockHttpMessageHandler
            {
                Handler = async (req) =>
                {
                    var body = await req.Content!.ReadAsStringAsync();
                    if (body.Contains("initialize"))
                    {
                        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":\"init\",\"result\":{\"protocolVersion\":\"2024-11-05\"}}", System.Text.Encoding.UTF8, "application/json")
                        };
                    }
                    if (body.Contains("tools/list"))
                    {
                        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":\"t-list\",\"result\":{\"tools\":[{\"name\":\"turn_on\",\"description\":\"Turn on light\"},{\"name\":\"turn_off\",\"description\":\"Turn off light\"}]}}", System.Text.Encoding.UTF8, "application/json")
                        };
                    }
                    return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
                }
            };

            // Only authorize ha__turn_on
            SeedPolicy("p1", "tool:ha__turn_on", "SmartHomeGroup", true);

            var context = CreateHttpContext("userA", groups: new List<string> { "SmartHomeGroup" });
            var session = CreateSession(context, handler);
            session.IsMetaMode = false;

            // Act
            var tools = await session.ListToolsAsync("{}", context);

            // Assert
            tools.Should().NotBeNull();
            var toolNames = tools.Select(t => (t as Dictionary<string, object>)?["name"] as string).ToList();
            toolNames.Should().Contain("ha/turn_on");
            toolNames.Should().NotContain("ha/turn_off");
        }

        [Fact]
        [Requirement("AUTH-01", "AUTH", RequirementType.Positive, "prompts/list filters exposed prompts according to caller permissions.")]
        public async Task ListPromptsAsync_FiltersUnauthorizedPrompts()
        {
            // Arrange
            var handler = new MockHttpMessageHandler
            {
                Handler = async (req) =>
                {
                    var body = await req.Content!.ReadAsStringAsync();
                    if (body.Contains("initialize"))
                    {
                        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":\"init\",\"result\":{\"protocolVersion\":\"2024-11-05\"}}", System.Text.Encoding.UTF8, "application/json")
                        };
                    }
                    if (body.Contains("prompts/list"))
                    {
                        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":\"p-list\",\"result\":{\"prompts\":[{\"name\":\"allowed_prompt\",\"description\":\"Allowed\"},{\"name\":\"secret_prompt\",\"description\":\"Secret\"}]}}", System.Text.Encoding.UTF8, "application/json")
                        };
                    }
                    return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
                }
            };

            SeedPolicy("p1", "prompt:ha__allowed_prompt", "PromptGroup", true);

            var context = CreateHttpContext("userA", groups: new List<string> { "PromptGroup" });
            var session = CreateSession(context, handler);

            // Act
            var prompts = await session.ListPromptsAsync("{}", context);

            // Assert
            prompts.Should().NotBeNull();
            var promptNames = prompts.Select(p => (p as Dictionary<string, object>)?["name"] as string).ToList();
            promptNames.Should().Contain("ha__allowed_prompt");
            promptNames.Should().NotContain("ha__secret_prompt");
        }

        [Fact]
        [Requirement("AUTH-01", "AUTH", RequirementType.Positive, "resources/list filters exposed resources according to caller permissions.")]
        public async Task ListResourcesAsync_FiltersUnauthorizedResources()
        {
            // Arrange
            var handler = new MockHttpMessageHandler
            {
                Handler = async (req) =>
                {
                    var body = await req.Content!.ReadAsStringAsync();
                    if (body.Contains("initialize"))
                    {
                        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":\"init\",\"result\":{\"protocolVersion\":\"2024-11-05\"}}", System.Text.Encoding.UTF8, "application/json")
                        };
                    }
                    if (body.Contains("resources/list"))
                    {
                        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":\"r-list\",\"result\":{\"resources\":[{\"uri\":\"public_data\",\"name\":\"Public\"},{\"uri\":\"secret_data\",\"name\":\"Secret\"}]}}", System.Text.Encoding.UTF8, "application/json")
                        };
                    }
                    return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
                }
            };

            SeedPolicy("p1", "resource:mcp://ha/public_data", "ResourceGroup", true);

            var context = CreateHttpContext("userA", groups: new List<string> { "ResourceGroup" });
            var session = CreateSession(context, handler);

            // Act
            var resources = await session.ListResourcesAsync("{}", context);

            // Assert
            resources.Should().NotBeNull();
            var resourceUris = resources.Select(r => (r as Dictionary<string, object>)?["uri"] as string).ToList();
            resourceUris.Should().Contain("mcp://ha/public_data");
            resourceUris.Should().NotContain("mcp://ha/secret_data");
        }

        [Fact]
        [Requirement("AUTH-01", "AUTH", RequirementType.Positive, "resources/templates/list filters exposed resource templates according to caller permissions.")]
        public async Task ListResourceTemplatesAsync_FiltersUnauthorizedTemplates()
        {
            // Arrange
            SeedPolicy("p1", "server:ha", "SmartHomeGroup", true);

            var context = CreateHttpContext("userA", groups: new List<string> { "SmartHomeGroup" });
            var session = CreateSession(context);

            // Act
            var templates = await session.ListResourceTemplatesAsync("{}", context);

            // Assert
            templates.Should().NotBeNull();
            foreach (var item in templates)
            {
                var dict = item as Dictionary<string, object>;
                if (dict != null && dict.TryGetValue("uriTemplate", out var uriObj) && uriObj is string uriTemplate)
                {
                    (await session.IsUserAuthorizedAsync("resources/templates/list", uriTemplate, context)).Should().BeTrue();
                }
            }
        }

        [Fact]
        [Requirement("MCP-08", "MCP", RequirementType.Positive, "completion/complete forwards prompt completions to backend when caller is authorized.")]
        public async Task CompleteAsync_ForPrompt_ForwardsToBackend_WhenAuthorized()
        {
            string? forwardedBody = null;
            var handler = new MockHttpMessageHandler
            {
                Handler = async (req) =>
                {
                    var body = await req.Content!.ReadAsStringAsync();
                    if (body.Contains("initialize"))
                    {
                        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":\"init\",\"result\":{\"protocolVersion\":\"2024-11-05\"}}", System.Text.Encoding.UTF8, "application/json")
                        };
                    }
                    if (body.Contains("completion/complete"))
                    {
                        forwardedBody = body;
                        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"completion\":{\"values\":[\"arg1_val\",\"arg2_val\"],\"hasMore\":false}}}", System.Text.Encoding.UTF8, "application/json")
                        };
                    }
                    return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
                }
            };

            SeedPolicy("p1", "server:ha", "SmartHomeGroup", true);

            var context = CreateHttpContext("userA", groups: new List<string> { "SmartHomeGroup" });
            var session = CreateSession(context, handler);

            var requestBody = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"completion/complete\",\"params\":{\"ref\":{\"type\":\"ref/prompt\",\"name\":\"ha__summarize\"},\"argument\":{\"name\":\"arg\",\"value\":\"test\"}}}";

            // Act
            var result = await session.CompleteAsync(requestBody, context);

            // Assert
            result.Should().NotBeNull();
            forwardedBody.Should().NotBeNull();
            forwardedBody.Should().Contain("\"name\":\"summarize\""); // Rewritten to rawName
            _mockAuditLogger.Verify(a => a.LogInvocationAsync(
                It.IsAny<string>(),
                "userA",
                It.IsAny<string>(),
                "ha",
                "ha__summarize",
                "completion/complete",
                It.IsAny<int>(),
                200,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                null
            ), Times.Once);
        }

        [Fact]
        [Requirement("MCP-08", "MCP", RequirementType.Positive, "completion/complete forwards resource template completions to backend when caller is authorized.")]
        public async Task CompleteAsync_ForResourceTemplate_ForwardsToBackend_WhenAuthorized()
        {
            string? forwardedBody = null;
            var handler = new MockHttpMessageHandler
            {
                Handler = async (req) =>
                {
                    var body = await req.Content!.ReadAsStringAsync();
                    if (body.Contains("initialize"))
                    {
                        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":\"init\",\"result\":{\"protocolVersion\":\"2024-11-05\"}}", System.Text.Encoding.UTF8, "application/json")
                        };
                    }
                    if (body.Contains("completion/complete"))
                    {
                        forwardedBody = body;
                        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"completion\":{\"values\":[\"sensor1\",\"sensor2\"],\"hasMore\":false}}}", System.Text.Encoding.UTF8, "application/json")
                        };
                    }
                    return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
                }
            };

            SeedPolicy("p1", "server:ha", "SmartHomeGroup", true);

            var context = CreateHttpContext("userA", groups: new List<string> { "SmartHomeGroup" });
            var session = CreateSession(context, handler);

            var requestBody = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"completion/complete\",\"params\":{\"ref\":{\"type\":\"ref/resource\",\"uriTemplate\":\"mcp://ha/sensor/{id}\"},\"argument\":{\"name\":\"id\",\"value\":\"temp\"}}}";

            // Act
            var result = await session.CompleteAsync(requestBody, context);

            // Assert
            result.Should().NotBeNull();
            forwardedBody.Should().NotBeNull();
            forwardedBody.Should().Contain("\"uriTemplate\":\"sensor/{id}\""); // Rewritten to backend template
            _mockAuditLogger.Verify(a => a.LogInvocationAsync(
                It.IsAny<string>(),
                "userA",
                It.IsAny<string>(),
                "ha",
                "mcp://ha/sensor/{id}",
                "completion/complete",
                It.IsAny<int>(),
                200,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                null
            ), Times.Once);
        }

        [Fact]
        [Requirement("MCP-08", "MCP", RequirementType.Positive, "completion/complete returns only completions for authorized backend servers.")]
        public async Task CompleteAsync_LogsTemplate_ReturnsOnlyAuthorizedServers()
        {
            // Arrange - Authorize 'ha' but NOT 'docker' or 'plex'
            SeedPolicy("p1", "server:ha", "SmartHomeGroup", true);
            SeedPolicy("p2", "resource:logs://{server_name}/today", "SmartHomeGroup", true);

            var context = CreateHttpContext("userA", groups: new List<string> { "SmartHomeGroup" });
            var session = CreateSession(context);

            var requestBody = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"completion/complete\",\"params\":{\"ref\":{\"type\":\"ref/resource\",\"uriTemplate\":\"logs://{server_name}/today\"},\"argument\":{\"name\":\"server_name\",\"value\":\"\"}}}";

            // Act
            var result = await session.CompleteAsync(requestBody, context);

            // Assert
            result.Should().NotBeNull();
            var json = JsonSerializer.Serialize(result);
            json.Should().Contain("ha");
            json.Should().NotContain("docker");
            json.Should().NotContain("plex");
        }

        [Fact]
        [Requirement("GUARD-01", "GUARD", RequirementType.Negative, "completion/complete throws UnauthorizedAccessException when caller lacks prompt permissions.")]
        public async Task CompleteAsync_ForPrompt_ThrowsUnauthorized_WhenCallerDenied()
        {
            // Arrange - Non-admin user with no policy for ha__prompt1
            var context = CreateHttpContext("userWithoutAccess", groups: new List<string> { "Users" });
            var session = CreateSession(context);

            var body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"completion/complete\",\"params\":{\"ref\":{\"type\":\"ref/prompt\",\"name\":\"ha__secret_prompt\"},\"argument\":{\"name\":\"arg\",\"value\":\"test\"}}}";

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await session.CompleteAsync(body, context);
            });

            ex.Message.Should().Contain("Security Error");
            _mockAuditLogger.Verify(a => a.LogInvocationAsync(
                It.IsAny<string>(),
                "userWithoutAccess",
                It.IsAny<string>(),
                "ha",
                "ha__secret_prompt",
                "completion/complete",
                It.IsAny<int>(),
                403,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.Is<string?>(msg => msg != null && msg.Contains("Security Error"))
            ), Times.Once);
        }

        [Fact]
        [Requirement("GUARD-01", "GUARD", RequirementType.Negative, "completion/complete throws UnauthorizedAccessException when caller lacks resource template permissions.")]
        public async Task CompleteAsync_ForResourceTemplate_ThrowsUnauthorized_WhenCallerDenied()
        {
            // Arrange
            var context = CreateHttpContext("userWithoutAccess", groups: new List<string> { "Users" });
            var session = CreateSession(context);

            var body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"completion/complete\",\"params\":{\"ref\":{\"type\":\"ref/resource\",\"uriTemplate\":\"mcp://ha/sensor/{id}\"},\"argument\":{\"name\":\"id\",\"value\":\"temp\"}}}";

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await session.CompleteAsync(body, context);
            });

            ex.Message.Should().Contain("Security Error");
            _mockAuditLogger.Verify(a => a.LogInvocationAsync(
                It.IsAny<string>(),
                "userWithoutAccess",
                It.IsAny<string>(),
                "ha",
                "mcp://ha/sensor/{id}",
                "completion/complete",
                It.IsAny<int>(),
                403,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.Is<string?>(msg => msg != null && msg.Contains("Security Error"))
            ), Times.Once);
        }

        [Theory]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"completion/complete\",\"params\":{}}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"completion/complete\",\"params\":{\"ref\":{\"type\":\"ref/unknown\"}}}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"completion/complete\",\"params\":{\"ref\":{\"type\":\"ref/prompt\",\"name\":\"nonexistentServer__prompt1\"}}}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"completion/complete\",\"params\":{\"ref\":{\"type\":\"ref/resource\",\"uriTemplate\":\"mcp://nonexistentServer/path/{id}\"}}}")]
        [Requirement("GUARD-01", "GUARD", RequirementType.Negative, "completion/complete fails closed on unknown or unresolved completion references.")]
        public async Task CompleteAsync_FailsClosed_OnUnknownOrUnresolvedTargets(string payload)
        {
            // Arrange
            var context = CreateHttpContext("adminUser", sids: new List<string> { "S-1-5-32-544" });
            var session = CreateSession(context);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await session.CompleteAsync(payload, context);
            });

            ex.Message.Should().Contain("Security Error");
        }
    }
}
