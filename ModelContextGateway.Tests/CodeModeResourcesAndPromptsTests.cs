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
    public class CodeModeResourcesAndPromptsTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly IDbConnectionFactory _dbFactory;
        private readonly Mock<IAuditLogger> _mockAuditLogger;
        private readonly List<McpServer> _servers;

        public CodeModeResourcesAndPromptsTests()
        {
            var dbName = $"Data Source=CodeModeResDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
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

        private ClientSession CreateSession(HttpContext context, out MockHttpMessageHandler handler, bool isMetaMode = true)
        {
            handler = new MockHttpMessageHandler
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
                        var tool = host == "docker"
                            ? (object)new { name = "list_containers", description = "List containers", inputSchema = new { type = "object", properties = new { all = new { type = "boolean" } } } }
                            : new { name = "turn_on", description = "Turn on entity", inputSchema = new { type = "object", properties = new { entity_id = new { type = "string" } }, required = new[] { "entity_id" } } };
                        return JsonResp(new { jsonrpc = "2.0", id = "t1", result = new { tools = new[] { tool } } });
                    }
                    if (body.Contains("\"resources/list\""))
                    {
                        var res = host == "docker"
                            ? new { uri = "docker://containers/nginx", name = "nginx container", description = "Web proxy container", mimeType = "application/json" }
                            : new { uri = "ha://states/light.kitchen", name = "Kitchen Light", description = "Light state", mimeType = "application/json" };
                        return JsonResp(new { jsonrpc = "2.0", id = "r1", result = new { resources = new object[] { res } } });
                    }
                    if (body.Contains("\"resources/read\""))
                    {
                        var content = new { uri = "docker://containers/nginx", mimeType = "application/json", text = "{\"id\":\"c1\",\"state\":\"running\"}" };
                        return JsonResp(new { jsonrpc = "2.0", id = "rr1", result = new { contents = new object[] { content } } });
                    }
                    if (body.Contains("\"prompts/list\""))
                    {
                        var prompts = host == "docker"
                            ? new object[] { new { name = "inspect_container", description = "Inspect container health" } }
                            : Array.Empty<object>();
                        return JsonResp(new { jsonrpc = "2.0", id = "p1", result = new { prompts } });
                    }
                    if (body.Contains("\"prompts/get\""))
                    {
                        var msg = new { role = "user", content = new { type = "text", text = "Inspect nginx" } };
                        return JsonResp(new { jsonrpc = "2.0", id = "pg1", result = new { messages = new object[] { msg } } });
                    }
                    if (body.Contains("\"completion/complete\""))
                    {
                        using var doc = JsonDocument.Parse(body);
                        var refElem = doc.RootElement.GetProperty("params").GetProperty("ref");
                        var forwardedTemplate = refElem.TryGetProperty("uriTemplate", out var ut) ? ut.GetString() : "";
                        return JsonResp(new { jsonrpc = "2.0", id = "c1", result = new { completion = new { values = new[] { $"unquoted:{forwardedTemplate}" }, hasMore = false } } });
                    }
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }
            };

            var httpClient = new HttpClient(handler);
            var logger = new Mock<ILogger<ClientSession>>();
            var embeddingService = new Mock<IEmbeddingService>();
            var sessionManager = new SessionManager(
                context.RequestServices,
                new Mock<IHttpClientFactory>().Object,
                new Mock<ILogger<SessionManager>>().Object);

            return new ClientSession(
                sessionId: "test-cm-session",
                clientResponse: context.Response,
                servers: _servers,
                httpClient: httpClient,
                embeddingService: embeddingService.Object,
                sessionManager: sessionManager,
                logger: logger.Object,
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
        [Requirement("MCP-54", "MCP", RequirementType.Positive, "Meta-Mode compacts downstream mcp:// resources into router:// catalog and per-server resources while non-Meta-Mode preserves flat mcp:// listings.")]
        public async Task ListResourcesAsync_CompactsDownstreamResourcesInMetaMode_AndPreservesFlatListInDirectMode()
        {
            var context = CreateHttpContext("admin", sids: new List<string> { "S-1-5-32-544" });
            var metaSession = CreateSession(context, out _, isMetaMode: true);

            var metaResources = await metaSession.ListResourcesAsync("{}", context);
            var metaUris = metaResources
                .Select(r => (r as IDictionary<string, object>)?["uri"]?.ToString())
                .Where(u => u != null)
                .ToList();

            metaUris.Should().Contain("router://code-mode/guide");
            metaUris.Should().Contain("router://code-mode/sdk.d.ts");
            metaUris.Should().Contain("router://resources/catalog");
            metaUris.Should().Contain("router://prompts/catalog");
            metaUris.Should().Contain("router://resources/server/docker");
            metaUris.Should().Contain("router://code-mode/types/docker");
            metaUris.Should().NotContain(u => u!.StartsWith("mcp://"));

            var directSession = CreateSession(context, out _, isMetaMode: false);
            var directResources = await directSession.ListResourcesAsync("{}", context);
            var directUris = directResources
                .Select(r => (r as IDictionary<string, object>)?["uri"]?.ToString())
                .Where(u => u != null)
                .ToList();

            directUris.Should().Contain(u => u!.StartsWith("mcp://docker/"));
            directUris.Should().Contain(u => u!.StartsWith("mcp://ha/"));
        }

        [Fact]
        [Requirement("MCP-54", "MCP", RequirementType.Negative, "Code Mode SDK declarations and resource catalogs enforce RBAC per server and block unauthorized server type/resource reads.")]
        public async Task ReadResourceAsync_CodeModeDeclarationsAndCatalogs_RespectRbacPolicies()
        {
            SeedPolicy("p-docker", "server:docker", "DevOps", true);

            var context = CreateHttpContext("devUser", groups: new List<string> { "DevOps" });
            var session = CreateSession(context, out _, isMetaMode: true);

            var sdkRes = await session.ReadResourceAsync("router://code-mode/sdk.d.ts", "{}", context);
            var sdkText = JsonSerializer.SerializeToElement(sdkRes).GetProperty("contents")[0].GetProperty("text").GetString()!;
            sdkText.Should().Contain("interface Server_docker_Tools");
            sdkText.Should().NotContain("interface Server_ha_Tools");

            var dockerTypesRes = await session.ReadResourceAsync("router://code-mode/types/docker", "{}", context);
            var dockerTypesText = JsonSerializer.SerializeToElement(dockerTypesRes).GetProperty("contents")[0].GetProperty("text").GetString()!;
            dockerTypesText.Should().Contain("interface Server_docker_Tools");

            Func<Task> readHaTypes = async () => await session.ReadResourceAsync("router://code-mode/types/ha", "{}", context);
            await readHaTypes.Should().ThrowAsync<UnauthorizedAccessException>();

            Func<Task> readHaServerResources = async () => await session.ReadResourceAsync("router://resources/server/ha", "{}", context);
            await readHaServerResources.Should().ThrowAsync<UnauthorizedAccessException>();

            var catalogRes = await session.ReadResourceAsync("router://resources/catalog", "{}", context);
            var catalogText = JsonSerializer.SerializeToElement(catalogRes).GetProperty("contents")[0].GetProperty("text").GetString()!;
            catalogText.Should().Contain("mcp://docker/");
            catalogText.Should().NotContain("mcp://ha/");
        }

        [Fact]
        [Requirement("MCP-53", "MCP", RequirementType.Positive, "Sandbox mcg.* APIs allow scripts to list servers, search/read resources with auto-unboxing, and list/get prompts under session RBAC.")]
        public async Task ExecuteScript_ViaClientSession_SupportsMcgResourcePromptAndServerApis()
        {
            SeedPolicy("p-docker", "server:docker", "DevOps", true);
            var context = CreateHttpContext("devUser", groups: new List<string> { "DevOps" });
            var session = CreateSession(context, out _, isMetaMode: true);

            var script = @"
                const servers = await mcg.listServers();
                const found = await mcg.searchResources('nginx');
                const containerData = await mcg.readResource(found[0].uri);
                const prompts = await mcg.listPrompts('docker');
                const promptMsg = await mcg.getPrompt('docker__inspect_container', {});
                return {
                    serverCount: servers.length,
                    serverId: servers[0].id,
                    containerState: containerData.state,
                    promptName: prompts[0].name,
                    promptRole: promptMsg.messages[0].role
                };
            ";

            var body = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "exec-1",
                method = "tools/call",
                @params = new { name = "execute_script", arguments = new { script } }
            });

            var result = await session.CallToolAsync("execute_script", body, _dbFactory, context);
            var elem = JsonSerializer.SerializeToElement(result);
            elem.GetProperty("isError").GetBoolean().Should().BeFalse();

            var structured = elem.GetProperty("structuredContent").GetProperty("result");
            structured.GetProperty("serverCount").GetInt32().Should().Be(1);
            structured.GetProperty("serverId").GetString().Should().Be("docker");
            structured.GetProperty("containerState").GetString().Should().Be("running");
            structured.GetProperty("promptName").GetString().Should().Be("docker__inspect_container");
            structured.GetProperty("promptRole").GetString().Should().Be("user");
        }

        [Fact]
        [Requirement("MCP-53", "MCP", RequirementType.Negative, "Sandbox mcg.readResource and mcg.getPrompt reject unauthorized downstream servers with an error.")]
        public async Task ExecuteScript_ViaClientSession_BlocksUnauthorizedResourceAndPromptAccess()
        {
            SeedPolicy("p-docker", "server:docker", "DevOps", true);
            var context = CreateHttpContext("devUser", groups: new List<string> { "DevOps" });
            var session = CreateSession(context, out _, isMetaMode: true);

            var script = @"
                let resDenied = false;
                let promptDenied = false;
                try {
                    await mcg.readResource('mcp://ha/ha%3A%2F%2Fstates%2Flight.kitchen');
                } catch (e) {
                    resDenied = String(e).includes('permission');
                }
                try {
                    await mcg.getPrompt('ha__secret_prompt', {});
                } catch (e) {
                    promptDenied = String(e).includes('permission');
                }
                return { resDenied, promptDenied };
            ";

            var body = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "exec-deny",
                method = "tools/call",
                @params = new { name = "execute_script", arguments = new { script } }
            });

            var result = await session.CallToolAsync("execute_script", body, _dbFactory, context);
            var structured = JsonSerializer.SerializeToElement(result).GetProperty("structuredContent").GetProperty("result");
            structured.GetProperty("resDenied").GetBoolean().Should().BeTrue();
            structured.GetProperty("promptDenied").GetBoolean().Should().BeTrue();
        }

        [Fact]
        [Requirement("MCP-54", "MCP", RequirementType.Positive, "Built-in Code Mode prompts router__code_mode_guide and router__write_code_mode_script embed TypeScript tool declarations.")]
        public async Task GetPromptAsync_CodeModePrompts_EmbedTypeScriptDeclarations()
        {
            var context = CreateHttpContext("admin", sids: new List<string> { "S-1-5-32-544" });
            var session = CreateSession(context, out _, isMetaMode: true);

            var prompts = await session.ListPromptsAsync("{}", context);
            var promptNames = prompts.Select(p => (p as IDictionary<string, object>)?["name"]?.ToString()).ToList();
            promptNames.Should().Contain("router__code_mode_guide");
            promptNames.Should().Contain("router__write_code_mode_script");

            var getBody = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "pg-cm",
                method = "prompts/get",
                @params = new
                {
                    name = "router__write_code_mode_script",
                    arguments = new { goal = "Check containers", servers = "docker" }
                }
            });

            var promptRes = await session.GetPromptAsync("router__write_code_mode_script", getBody, context);
            var text = JsonSerializer.SerializeToElement(promptRes).GetProperty("messages")[0].GetProperty("content").GetProperty("text").GetString()!;
            text.Should().Contain("Check containers");
            text.Should().Contain("interface Server_docker_Tools");
            text.Should().Contain("list_containers");
        }

        [Fact]
        [Requirement("MCP-55", "MCP", RequirementType.Positive, "CompleteAsync resolves local router__* prompts, router:// templates, and unescapes mcp:// templates with alias support.")]
        public async Task CompleteAsync_SupportsLocalRouterPromptsTemplatesAndUnescapesMcpUris()
        {
            var context = CreateHttpContext("admin", sids: new List<string> { "S-1-5-32-544" });
            var session = CreateSession(context, out _, isMetaMode: true);

            var promptCompleteBody = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "c-p1",
                method = "completion/complete",
                @params = new
                {
                    @ref = new { type = "ref/prompt", name = "router__code_mode_guide" },
                    argument = new { name = "server_name", value = "do" }
                }
            });
            var promptCompRes = JsonSerializer.SerializeToElement(await session.CompleteAsync(promptCompleteBody, context));
            promptCompRes.GetProperty("completion").GetProperty("values").EnumerateArray().Select(v => v.GetString()).Should().Contain("docker");

            var templateCompleteBody = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "c-r1",
                method = "completion/complete",
                @params = new
                {
                    @ref = new { type = "ref/resource", uriTemplate = "router://code-mode/types/{server_name}" },
                    argument = new { name = "server_name", value = "h" }
                }
            });
            var templateCompRes = JsonSerializer.SerializeToElement(await session.CompleteAsync(templateCompleteBody, context));
            templateCompRes.GetProperty("completion").GetProperty("values").EnumerateArray().Select(v => v.GetString()).Should().Contain("ha");

            var downstreamCompleteBody = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "c-d1",
                method = "completion/complete",
                @params = new
                {
                    @ref = new { type = "ref/resource", uriTemplate = "mcp://dkr/docker%3A%2F%2Fcontainers%2F%7Bid%7D" },
                    argument = new { name = "id", value = "ng" }
                }
            });
            var downstreamCompRes = JsonSerializer.SerializeToElement(await session.CompleteAsync(downstreamCompleteBody, context));
            downstreamCompRes.GetProperty("completion").GetProperty("values").EnumerateArray().Select(v => v.GetString()).Should().Contain("unquoted:docker://containers/{id}");
        }
    }
}
