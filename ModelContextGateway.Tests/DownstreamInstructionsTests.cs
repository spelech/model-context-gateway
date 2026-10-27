using System.Data;
using System.Net;
using System.Security.Claims;
using System.Text;
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
    public class DownstreamInstructionsTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly IDbConnectionFactory _dbFactory;
        private readonly Mock<IAuditLogger> _mockAuditLogger;
        private readonly List<McpServer> _servers;

        public DownstreamInstructionsTests()
        {
            var dbName = $"Data Source=DownstreamInstrDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
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
                CREATE TABLE IF NOT EXISTS Settings (
                    Id TEXT PRIMARY KEY,
                    EnableCodeMode INTEGER DEFAULT 1,
                    CodeModeTimeoutMs INTEGER DEFAULT 5000,
                    CodeModeMaxMemoryMb INTEGER DEFAULT 64,
                    CodeModeMaxStatements INTEGER DEFAULT 10000,
                    CodeModeMaxToolCalls INTEGER DEFAULT 25
                );
                INSERT INTO Settings (Id, EnableCodeMode, CodeModeTimeoutMs, CodeModeMaxMemoryMb, CodeModeMaxStatements, CodeModeMaxToolCalls)
                VALUES ('default', 1, 5000, 64, 10000, 25);
            ");

            var mockFactory = new Mock<IDbConnectionFactory>();
            mockFactory.Setup(f => f.CreateConnection()).Returns(() => new SqliteConnection(dbName));
            mockFactory.Setup(f => f.ProviderName).Returns("sqlite");
            _dbFactory = mockFactory.Object;

            _mockAuditLogger = new Mock<IAuditLogger>();

            _servers = new List<McpServer>
            {
                new McpServer { Id = "docker", Alias = "dkr", DisplayName = "Docker", Type = "http", Url = "http://docker:8000/mcp", Enabled = true },
                new McpServer { Id = "ha", DisplayName = "Home Assistant", Type = "http", Url = "http://ha:8123/mcp", Enabled = true }
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

        private HttpContext CreateHttpContext(string username, List<string>? groups = null, List<string>? sids = null)
        {
            var context = new DefaultHttpContext();
            var claims = new List<Claim> { new Claim(ClaimTypes.Name, username) };
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
                foreach (var s in sids)
                {
                    claims.Add(new Claim("Sid", s));
                }
            }
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "SSO"));

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
                .ReturnsAsync(new UserIdentityContext(username, "SSO", groups ?? new List<string>(), "", sids ?? new List<string>()));
            services.AddSingleton(new CompositeIdentityProvider(new[] { mockProvider.Object }));

            context.RequestServices = services.BuildServiceProvider();
            return context;
        }

        private ClientSession CreateSession(HttpContext context, bool isMetaMode = true)
        {
            var handler = new MockHttpMessageHandler
            {
                Handler = async (req) =>
                {
                    var body = req.Content != null ? await req.Content.ReadAsStringAsync() : "";
                    var host = req.RequestUri?.Host ?? "";

                    if (body.Contains("\"initialize\""))
                    {
                        var instr = host == "docker"
                            ? "Always inspect container health before restarting."
                            : "Verify entity_id domain before calling turn_on.";
                        return JsonResp(new { jsonrpc = "2.0", id = "init", result = new { protocolVersion = "2026-07-28", instructions = instr } });
                    }
                    if (body.Contains("\"tools/list\""))
                    {
                        if (host == "docker")
                        {
                            return JsonResp(new
                            {
                                jsonrpc = "2.0",
                                id = "t1",
                                result = new
                                {
                                    tools = new object[]
                                    {
                                        new { name = "list_containers", description = "List containers", inputSchema = new { type = "object" } }
                                    }
                                }
                            });
                        }
                        return JsonResp(new
                        {
                            jsonrpc = "2.0",
                            id = "t2",
                            result = new
                            {
                                tools = new object[]
                                {
                                    new { name = "turn_on", description = "Turn on entity", inputSchema = new { type = "object" } }
                                }
                            }
                        });
                    }
                    if (body.Contains("\"resources/list\""))
                    {
                        if (host == "docker")
                        {
                            return JsonResp(new
                            {
                                jsonrpc = "2.0",
                                id = "r1",
                                result = new
                                {
                                    resources = new object[]
                                    {
                                        new { uri = "docker://containers/nginx", name = "nginx container", mimeType = "application/json" }
                                    }
                                }
                            });
                        }
                        return JsonResp(new
                        {
                            jsonrpc = "2.0",
                            id = "r2",
                            result = new
                            {
                                resources = new object[]
                                {
                                    new { uri = "ha://states/light.kitchen", name = "Kitchen Light", mimeType = "application/json" }
                                }
                            }
                        });
                    }
                    return JsonResp(new { jsonrpc = "2.0", id = "ok", result = new { } });
                }
            };

            var sessionManager = new SessionManager(
                context.RequestServices,
                new Mock<IHttpClientFactory>().Object,
                new Mock<ILogger<SessionManager>>().Object);

            return new ClientSession(
                sessionId: "test-instr-session",
                clientResponse: context.Response,
                servers: _servers,
                httpClient: new HttpClient(handler),
                embeddingService: new Mock<IEmbeddingService>().Object,
                sessionManager: sessionManager,
                logger: new Mock<ILogger<ClientSession>>().Object,
                rootServices: context.RequestServices
            )
            {
                IsMetaMode = isMetaMode
            };
        }

        private static HttpResponseMessage JsonResp(object payload) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

        [Fact]
        [Requirement("MCP-55", "MCP", RequirementType.Positive, "GatewayMetadata.BuildGatewayInstructions generates MCP 2026-07-28 initialize instructions documenting Code Mode and resource catalogs.")]
        public void BuildGatewayInstructions_IncludesCodeModeAndResourceCatalogGuidance()
        {
            var instructions = GatewayMetadata.BuildGatewayInstructions(enableCodeMode: true);
            instructions.Should().Contain("search_tools");
            instructions.Should().Contain("execute_script");
            instructions.Should().Contain("router://code-mode/sdk.d.ts");
            instructions.Should().Contain("router://resources/catalog");
        }

        [Fact]
        [Requirement("MCP-56", "MCP", RequirementType.Positive, "Downstream server initialize instructions are captured, cached in SessionManager, and returned directly in direct proxy mode (Surface 1) and rolled up in Meta-Mode initialize instructions (Surface 4).")]
        public async Task ResolveSessionInstructionsAsync_SurfacesDownstreamInstructions_InDirectProxyAndMetaMode()
        {
            var context = CreateHttpContext("admin", sids: new List<string> { "S-1-5-32-544" });

            var sessionManager = new SessionManager(
                context.RequestServices,
                new Mock<IHttpClientFactory>().Object,
                new Mock<ILogger<SessionManager>>().Object);
            var singleServer = new List<McpServer>
            {
                new McpServer { Id = "docker", Alias = "dkr", DisplayName = "Docker Engine", Type = "http", Url = "http://docker:8000/mcp", Enabled = true }
            };
            var singleDirectSession = new ClientSession(
                "direct-docker-sess",
                context.Response,
                singleServer,
                new HttpClient(new MockHttpMessageHandler
                {
                    Handler = async (_) => JsonResp(new
                    {
                        jsonrpc = "2.0",
                        id = "init",
                        result = new { protocolVersion = "2026-07-28", instructions = "Always inspect container health before restarting." }
                    })
                }),
                new Mock<IEmbeddingService>().Object,
                sessionManager,
                new Mock<ILogger<ClientSession>>().Object,
                context.RequestServices)
            {
                IsMetaMode = false
            };

            var directInstructions = await singleDirectSession.ResolveSessionInstructionsAsync(
                enableCodeMode: true,
                initialRequestBody: GatewayMetadata.BuildInitializeRequest(),
                httpContext: context);
            directInstructions.Should().Be("Always inspect container health before restarting.");
            sessionManager.GetServerInstructionsCache("docker").Should().Be("Always inspect container health before restarting.");

            var metaSession = CreateSession(context, isMetaMode: true);
            var metaInstructions = await metaSession.ResolveSessionInstructionsAsync(
                enableCodeMode: true,
                initialRequestBody: GatewayMetadata.BuildInitializeRequest(),
                httpContext: context);
            metaInstructions.Should().Contain("Downstream Server Instructions:");
            metaInstructions.Should().Contain("- [docker]: Always inspect container health before restarting.");
            metaInstructions.Should().Contain("- [ha]: Verify entity_id domain before calling turn_on.");
        }

        [Fact]
        [Requirement("MCP-56", "MCP", RequirementType.Positive, "Downstream server instructions are exposed in router://resources/server/{id}, router://active-servers, mcg.listServers() (Surface 2), and Code Mode TypeScript .d.ts JSDoc headers (Surface 3).")]
        public async Task DownstreamServerInstructions_AreExposedAcrossServerResourcesSandboxAndTypeScriptDeclarations()
        {
            var context = CreateHttpContext("admin", sids: new List<string> { "S-1-5-32-544" });
            var session = CreateSession(context, isMetaMode: true);

            var resourcesList = await session.ListResourcesAsync("{}", context);
            var dockerIndexEntry = resourcesList
                .Select(r => JsonSerializer.SerializeToElement(r))
                .First(r => r.GetProperty("uri").GetString() == "router://resources/server/docker");
            dockerIndexEntry.GetProperty("description").GetString()
                .Should().Contain("Always inspect container health before restarting.");

            var serverRes = await session.ReadResourceAsync("router://resources/server/docker", "{}", context);
            var serverResJson = JsonDocument.Parse(JsonSerializer.SerializeToElement(serverRes).GetProperty("contents")[0].GetProperty("text").GetString()!).RootElement;
            serverResJson.GetProperty("serverId").GetString().Should().Be("docker");
            serverResJson.GetProperty("instructions").GetString().Should().Be("Always inspect container health before restarting.");

            var activeServersRes = await session.ReadResourceAsync("router://active-servers", "{}", context);
            var activeServersJson = JsonDocument.Parse(JsonSerializer.SerializeToElement(activeServersRes).GetProperty("contents")[0].GetProperty("text").GetString()!).RootElement;
            var dockerActive = activeServersJson.EnumerateArray().First(e => e.GetProperty("id").GetString() == "docker");
            dockerActive.GetProperty("instructions").GetString().Should().Be("Always inspect container health before restarting.");

            var scriptBody = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "exec-srv-instr",
                method = "tools/call",
                @params = new { name = "execute_script", arguments = new { script = "return await mcg.listServers();" } }
            });
            var scriptRes = await session.CallToolAsync("execute_script", scriptBody, _dbFactory, context);
            var serversArr = JsonSerializer.SerializeToElement(scriptRes).GetProperty("structuredContent").GetProperty("result");
            var dockerSrv = serversArr.EnumerateArray().First(s => s.GetProperty("id").GetString() == "docker");
            dockerSrv.GetProperty("instructions").GetString().Should().Be("Always inspect container health before restarting.");

            var sdkRes = await session.ReadResourceAsync("router://code-mode/sdk.d.ts", "{}", context);
            var sdkDts = JsonSerializer.SerializeToElement(sdkRes).GetProperty("contents")[0].GetProperty("text").GetString()!;
            sdkDts.Should().Contain("Server Instructions: Always inspect container health before restarting.");
            sdkDts.Should().Contain("Server Instructions: Verify entity_id domain before calling turn_on.");

            var dockerTypesRes = await session.ReadResourceAsync("router://code-mode/types/docker", "{}", context);
            var dockerDts = JsonSerializer.SerializeToElement(dockerTypesRes).GetProperty("contents")[0].GetProperty("text").GetString()!;
            dockerDts.Should().Contain("Server Instructions: Always inspect container health before restarting.");
        }

        [Fact]
        [Requirement("MCP-56", "MCP", RequirementType.Negative, "Unprivileged callers do not receive downstream instructions for unauthorized servers across initialize, router://active-servers, or Code Mode prompts.")]
        public async Task DownstreamServerInstructions_EnforceRbacFiltering_AcrossInitializeActiveServersAndPrompts()
        {
            SeedPolicy("p-docker-only", "server:docker", "DevOps", true);
            SeedPolicy("p-active-srv", "router://active-servers", "DevOps", true);

            var devContext = CreateHttpContext("devUser", groups: new List<string> { "DevOps" });
            var session = CreateSession(devContext, isMetaMode: true);
            await session.EnsureBackendsInitializedAsync();

            // 1. Meta-Mode initialize rollup includes docker instructions, omits unauthorized ha instructions
            var metaInstructions = await session.ResolveSessionInstructionsAsync(enableCodeMode: true, httpContext: devContext);
            metaInstructions.Should().Contain("- [docker]: Always inspect container health before restarting.");
            metaInstructions.Should().NotContain("Verify entity_id domain before calling turn_on.");
            metaInstructions.Should().NotContain("[ha]");

            // 2. router://active-servers includes docker, omits unauthorized ha
            var activeServersRes = await session.ReadResourceAsync("router://active-servers", "{}", devContext);
            var activeServersText = JsonSerializer.SerializeToElement(activeServersRes).GetProperty("contents")[0].GetProperty("text").GetString()!;
            activeServersText.Should().Contain("Always inspect container health before restarting.");
            activeServersText.Should().NotContain("Verify entity_id domain before calling turn_on.");

            // 3. router__write_code_mode_script requesting restricted server 'ha' omits ha declarations and instructions
            var getPromptBody = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "pg-sec",
                method = "prompts/get",
                @params = new
                {
                    name = "router__write_code_mode_script",
                    arguments = new { goal = "Test", servers = "docker,ha" }
                }
            });
            var promptRes = await session.GetPromptAsync("router__write_code_mode_script", getPromptBody, devContext);
            var promptText = JsonSerializer.SerializeToElement(promptRes).GetProperty("messages")[0].GetProperty("content").GetProperty("text").GetString()!;
            promptText.Should().Contain("Always inspect container health before restarting.");
            promptText.Should().NotContain("Verify entity_id domain before calling turn_on.");
            promptText.Should().NotContain("Server_ha_Tools");
        }

        [Fact]
        [Requirement("MCP-56", "MCP", RequirementType.Positive, "Cold-start Code Mode sandbox mcg.listServers() and mcg.getServerInstructions() initialize backends on demand and return downstream server instructions.")]
        public async Task ColdStart_CodeModeSandbox_ListServers_InitializesBackendsAndReturnsInstructions()
        {
            var context = CreateHttpContext("admin", sids: new List<string> { "S-1-5-32-544" });
            var coldSession = CreateSession(context, isMetaMode: true);

            var scriptBody = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "cold-exec",
                method = "tools/call",
                @params = new
                {
                    name = "execute_script",
                    arguments = new
                    {
                        script = "return await mcg.getServerInstructions('docker');"
                    }
                }
            });

            var res = await coldSession.CallToolAsync("execute_script", scriptBody, _dbFactory, context);
            var instr = JsonSerializer.SerializeToElement(res).GetProperty("structuredContent").GetProperty("result").GetString();
            instr.Should().Be("Always inspect container health before restarting.");
        }
    }
}
