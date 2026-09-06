using System.Collections.Concurrent;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextGateway.Tests.TestHelpers;
using Moq;

namespace ModelContextGateway.Tests
{
    public class ToolRoutingManagerTests
    {
        private (SqliteConnection conn, IDbConnectionFactory factory) CreateDbFactory(bool requireManualApproval = false)
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Settings (
                    Id TEXT PRIMARY KEY,
                    INTEGER DEFAULT 0
                );
            ");

            if (requireManualApproval)
            {
                connection.Execute("INSERT INTO Settings (Id) VALUES ('default')");
            }

            var mockDbFactory = new Mock<IDbConnectionFactory>();
            mockDbFactory.Setup(f => f.CreateConnection()).Returns(connection);
            mockDbFactory.Setup(f => f.ProviderName).Returns("sqlite");
            return (connection, mockDbFactory.Object);
        }

        [Fact]
        [Requirement("MCP-02", "MCP", RequirementType.Positive, "ToolRoutingManager exposes meta-tools search_tools and execute_tool in meta-mode to minimize context overhead.")]
        public async Task ListToolsAsync_ReturnsMetaTools_InMetaMode()
        {
            var manager = new ToolRoutingManager();
            var connections = new Dictionary<string, BackendConnection>();
            var servers = new List<McpServer>();

            var tools = await manager.ListToolsAsync(
                body: "{}",
                isMetaMode: true,
                backendConnections: connections,
                logger: NullLogger.Instance,
                ensureBackendsInitializedAsync: () => Task.CompletedTask,
                servers: servers
            );

            Assert.NotNull(tools);
            Assert.Equal(2, tools.Count);
        }

        [Fact]
        [Requirement("MCP-02", "MCP", RequirementType.Positive, "ToolRoutingManager clears cached tools table upon cache invalidation.")]
        public async Task InvalidateCache_ClearsPopulatedState()
        {
            var manager = new ToolRoutingManager();
            var mockDownstream = new MockDownstreamMcpServer();
            mockDownstream.AddTool("sample_tool", "Sample tool description");

            var server = new McpServer
            {
                Id = "backend_srv",
                Enabled = true,
                Url = "http://backend:8080/mcp",
                Type = "http"
            };
            var backendConn = new BackendConnection(server, mockDownstream.CreateHttpClient(), NullLogger.Instance);
            var connections = new Dictionary<string, BackendConnection> { ["backend_srv"] = backendConn };

            var populatedTools = await manager.ListToolsAsync(
                body: "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}",
                isMetaMode: false,
                backendConnections: connections,
                logger: NullLogger.Instance,
                ensureBackendsInitializedAsync: () => Task.CompletedTask,
                servers: new[] { server }
            );

            populatedTools.Should().NotBeEmpty();
            manager.GetCachedTools().Should().NotBeEmpty();

            manager.InvalidateCache();
            manager.GetCachedTools().Should().BeEmpty();
        }

        [Fact]
        [Requirement("MCP-12", "MCP", RequirementType.Positive, "ToolRoutingManager routes search_tools queries through semantic and keyword matching.")]
        public async Task CallToolAsync_SearchTools_ReturnsSemanticResults()
        {
            var manager = new ToolRoutingManager();
            var (conn, dbFactory) = CreateDbFactory();
            var connections = new ConcurrentDictionary<string, BackendConnection>();
            var servers = new List<McpServer>();
            var mockEmbedding = new Mock<IEmbeddingService>();
            mockEmbedding.Setup(e => e.GetEmbeddingAsync(It.IsAny<string>()))
                         .ReturnsAsync(new float[384]);

            var body = "{\"params\":{\"arguments\":{\"query\":\"Excel\"}}}";
            var result = await manager.CallToolAsync(
                "search_tools",
                body,
                dbFactory,
                connections,
                servers,
                NullLogger.Instance,
                new HttpClient(),
                mockEmbedding.Object,
                () => Task.CompletedTask,
                (b, k, v) => b
            );

            result.Should().NotBeNull();
            var json = JsonSerializer.Serialize(result);
            json.Should().Contain("\"resultType\":\"complete\"");
            json.Should().Contain("\"content\"");
        }

        [Fact]
        [Requirement("GUARD-TOOL-MANDATORY-PARAMS", "GUARD", RequirementType.Negative, "ToolRoutingManager returns an error when execute_tool is invoked without the mandatory tool name parameter.")]
        public async Task CallToolAsync_ExecuteTool_ReturnsError_WhenNameMissing()
        {
            var manager = new ToolRoutingManager();
            var (conn, dbFactory) = CreateDbFactory();
            var connections = new ConcurrentDictionary<string, BackendConnection>();
            var servers = new List<McpServer>();
            var mockEmbedding = new Mock<IEmbeddingService>();

            var body = "{\"params\":{\"arguments\":{}}}";
            var result = await manager.CallToolAsync(
                "execute_tool",
                body,
                dbFactory,
                connections,
                servers,
                NullLogger.Instance,
                new HttpClient(),
                mockEmbedding.Object,
                () => Task.CompletedTask,
                (b, k, v) => b
            );

            result.Should().NotBeNull();
            var json = JsonSerializer.Serialize(result);
            json.Should().Contain("\"isError\":true");
            json.Should().Contain("target tool name is required");
        }

        [Fact]
        [Requirement("GUARD-TOOL-CANCELLATION", "GUARD", RequirementType.Negative, "ToolRoutingManager propagates task cancellation gracefully with a standardized JSON-RPC error response.")]
        public async Task CallToolAsync_ReturnsCancellationError_WhenCancelled()
        {
            var manager = new ToolRoutingManager();
            var (conn, dbFactory) = CreateDbFactory();
            var connections = new ConcurrentDictionary<string, BackendConnection>();
            var servers = new List<McpServer>();
            var mockEmbedding = new Mock<IEmbeddingService>();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await manager.CallToolAsync(
                "execute_tool",
                "{}",
                dbFactory,
                connections,
                servers,
                NullLogger.Instance,
                new HttpClient(),
                mockEmbedding.Object,
                () => Task.Delay(1000, cts.Token),
                (b, k, v) => b,
                cts.Token
            );

            result.Should().NotBeNull();
            var json = JsonSerializer.Serialize(result);
            json.Should().Contain("\"isError\":true");
            json.Should().Contain("request was cancelled by the client");
        }

        [Fact]
        [Requirement("GUARD-ROUTING-UNKNOWN-TOOL", "GUARD", RequirementType.Negative, "ToolRoutingManager throws KeyNotFoundException when calling a tool not registered in the routing table.")]
        public async Task CallToolAsync_ThrowsKeyNotFound_WhenToolNotInRoutingTable()
        {
            var manager = new ToolRoutingManager();
            var (conn, dbFactory) = CreateDbFactory();
            var connections = new ConcurrentDictionary<string, BackendConnection>();
            var servers = new List<McpServer>();
            var mockEmbedding = new Mock<IEmbeddingService>();

            await Assert.ThrowsAsync<KeyNotFoundException>(() => manager.CallToolAsync(
                "unknown_tool",
                "{}",
                dbFactory,
                connections,
                servers,
                NullLogger.Instance,
                new HttpClient(),
                mockEmbedding.Object,
                () => Task.CompletedTask,
                (b, k, v) => b
            ));
        }

        [Fact]
        [Requirement("AUTH-14", "AUTH", RequirementType.Positive, "Tool execution catches 401 Unauthorized from downstream target servers and returns interactive auth remediation.")]
        public async Task ExecuteTargetToolAsync_Catches401_AndReturnsAuthPrompt()
        {
            var manager = new ToolRoutingManager();
            var (conn, dbFactory) = CreateDbFactory();

            var mockDownstream = new MockDownstreamMcpServer();
            mockDownstream.AddTool("get_secret", "Retrieve secret information");
            mockDownstream.ReturnUnauthorizedOnToolsCall = true;

            var httpClient = mockDownstream.CreateHttpClient();
            var server = new McpServer
            {
                Id = "vault_srv",
                Enabled = true,
                Url = "http://vault:8080/mcp",
                Type = "http",
                DynamicAuthPrompt = "401 Unauthorized: Target service requires interactive authentication credentials."
            };
            var servers = new List<McpServer> { server };

            var backendConn = new BackendConnection(server, httpClient, NullLogger.Instance);
            var connections = new ConcurrentDictionary<string, BackendConnection>();
            connections["vault_srv"] = backendConn;

            var mockEmbedding = new Mock<IEmbeddingService>();
            var body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"vault_srv__get_secret\",\"arguments\":{}}}";

            var result = await manager.CallToolAsync(
                "vault_srv__get_secret",
                body,
                dbFactory,
                connections,
                servers,
                NullLogger.Instance,
                httpClient,
                mockEmbedding.Object,
                () => Task.CompletedTask,
                (b, k, v) => b
            );

            result.Should().NotBeNull();
            var json = JsonSerializer.Serialize(result);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            root.GetProperty("isError").GetBoolean().Should().BeTrue();
            var content = root.GetProperty("content");
            content.GetArrayLength().Should().BeGreaterThan(0);
            var text = content[0].GetProperty("text").GetString();
            text.Should().Contain("401 Unauthorized: Target service requires interactive authentication credentials.");
        }

        [Fact]
        [Requirement("MCP-25", "ToolRoutingManager falls back to SessionManager global server tools cache during cold-start search_tools execution", Type = RequirementType.Positive, Category = "MCP")]
        public async Task CallToolAsync_SearchTools_FallsBackToGlobalSessionManagerCache_WhenLocalCacheEmpty()
        {
            var manager = new ToolRoutingManager();
            var (conn, dbFactory) = CreateDbFactory();
            var connections = new ConcurrentDictionary<string, BackendConnection>();
            var servers = new List<McpServer>
            {
                new McpServer { Id = "ha", Enabled = true, Url = "http://ha:8086/mcp", Type = "http" }
            };

            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider();
            var mockFactory = new Mock<IHttpClientFactory>();
            var sessionManager = new SessionManager(services, mockFactory.Object, NullLogger<SessionManager>.Instance);

            var globalTools = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["name"] = "ha__ha_call_service",
                    ["description"] = "[ha] Execute Home Assistant services to control lights and switches"
                },
                new Dictionary<string, object>
                {
                    ["name"] = "ha__ha_search",
                    ["description"] = "[ha] Search for entities (lights, sensors, nightstand) by name"
                }
            };
            sessionManager.SetServerToolsCache("ha", globalTools);

            var mockEmbedding = new Mock<IEmbeddingService>();
            mockEmbedding.Setup(e => e.GetEmbeddingAsync(It.IsAny<string>())).ReturnsAsync(new float[384]);
            mockEmbedding.Setup(e => e.CosineSimilarity(It.IsAny<float[]>(), It.IsAny<float[]>())).Returns(0.85);

            var body = "{\"params\":{\"arguments\":{\"query\":\"nightstand light\"}}}";
            var result = await manager.CallToolAsync(
                "search_tools",
                body,
                dbFactory,
                connections,
                servers,
                NullLogger.Instance,
                new HttpClient(),
                mockEmbedding.Object,
                () => Task.CompletedTask,
                (b, k, v) => b,
                sessionManager: sessionManager
            );

            Assert.NotNull(result);
            var resultJson = System.Text.Json.JsonSerializer.Serialize(result);
            Assert.Contains("ha__ha_search", resultJson);
        }

        [Fact]
        [Requirement("MCP-26", "MCP", RequirementType.Positive, "ToolRoutingManager normalizes tool name delimiters (slash and colon) to canonical double-underscore format.")]
        public void NormalizeTargetToolName_NormalizesSlashAndColonDelimiters()
        {
            var manager = new ToolRoutingManager();
            var servers = new List<McpServer> { new McpServer { Id = "docker", Enabled = true } };

            var (slashNormalized, slashErr) = manager.NormalizeTargetToolName("docker/list_containers", servers);
            Assert.Null(slashErr);
            Assert.Equal("docker__list_containers", slashNormalized);

            var (colonNormalized, colonErr) = manager.NormalizeTargetToolName("docker:list_containers", servers);
            Assert.Null(colonErr);
            Assert.Equal("docker__list_containers", colonNormalized);
        }

        [Fact]
        [Requirement("MCP-26", "MCP", RequirementType.Positive, "ToolRoutingManager auto-resolves unambiguous bare tool names to their fully namespaced routes.")]
        public void NormalizeTargetToolName_ResolvesBareToolName_WhenUnambiguous()
        {
            var manager = new ToolRoutingManager();
            manager.ToolRoutingTable["local-llm-server-manager__get_gpu_vram"] = "local-llm-server-manager";
            var servers = new List<McpServer> { new McpServer { Id = "local-llm-server-manager", Enabled = true } };

            var (normalized, error) = manager.NormalizeTargetToolName("get_gpu_vram", servers);
            Assert.Null(error);
            Assert.Equal("local-llm-server-manager__get_gpu_vram", normalized);
        }

        [Fact]
        [Requirement("MCP-26", "MCP", RequirementType.Positive, "ToolRoutingManager returns an ambiguity error when a bare tool name exists on multiple backend servers.")]
        public void NormalizeTargetToolName_ReturnsAmbiguityError_WhenToolExistsAcrossMultipleServers()
        {
            var manager = new ToolRoutingManager();
            manager.ToolRoutingTable["db1__query"] = "db1";
            manager.ToolRoutingTable["db2__query"] = "db2";
            var servers = new List<McpServer>
            {
                new McpServer { Id = "db1", Enabled = true },
                new McpServer { Id = "db2", Enabled = true }
            };

            var (_, error) = manager.NormalizeTargetToolName("query", servers);
            Assert.NotNull(error);
            Assert.Contains("Ambiguous tool name 'query'", error);
            Assert.Contains("db1/query", error);
            Assert.Contains("db2/query", error);
        }

        [Fact]
        [Requirement("MCP-26", "MCP", RequirementType.Positive, "search_tools returns a valid JSON array string when no tools match.")]
        public async Task SearchTools_ReturnsValidJsonArray_WhenNoToolsMatch()
        {
            var manager = new ToolRoutingManager();
            var (conn, dbFactory) = CreateDbFactory();
            var connections = new ConcurrentDictionary<string, BackendConnection>();
            var servers = new List<McpServer>
            {
                new McpServer { Id = "docker", Enabled = true }
            };
            var mockEmbedding = new Mock<IEmbeddingService>();

            var body = "{\"params\":{\"arguments\":{\"query\":\"nonexistent_tool_xyz\"}}}";
            var result = await manager.CallToolAsync(
                "search_tools",
                body,
                dbFactory,
                connections,
                servers,
                NullLogger.Instance,
                new HttpClient(),
                mockEmbedding.Object,
                () => Task.CompletedTask,
                (b, k, v) => b
            );

            Assert.NotNull(result);
            var resultJson = System.Text.Json.JsonSerializer.Serialize(result);
            using var doc = System.Text.Json.JsonDocument.Parse(resultJson);
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            Assert.NotNull(text);
            var list = System.Text.Json.JsonSerializer.Deserialize<List<object>>(text);
            Assert.NotNull(list);
            Assert.Empty(list);
        }

        [Fact]
        [Requirement("MCP-27", "MCP", RequirementType.Positive, "ToolRoutingManager exposes tools using {namespace}/{tool_name} format by default with [{namespace}] description prefix and dual-key routing.")]
        public async Task CacheTools_Exposes_Slash_Formatted_Name_With_Server_Alias()
        {
            // MCP-27: Primary exposed name uses namespace/tool format where namespace is Alias ?? Id
            var manager = new ToolRoutingManager();
            var server = new McpServer
            {
                Id = "postgres-mcp-homebox",
                Alias = "homebox_db",
                DisplayName = "Homebox Database"
            };

            // Construct mock tools payload
            var toolsJson = @"[{""name"": ""execute_sql"", ""description"": ""Run SQL query"", ""inputSchema"": {}}]";
            using var doc = System.Text.Json.JsonDocument.Parse(toolsJson);

            // Call internal tool registration helper or PopulateCache
            var exposed = manager.BuildExposedToolDefinition(server, doc.RootElement[0]);
            Assert.NotNull(exposed);
            Assert.Equal("homebox_db/execute_sql", exposed["name"]);
            Assert.Equal("[homebox_db] Run SQL query", exposed["description"]);

            // Verify dual-key routing entries
            Assert.Equal("postgres-mcp-homebox", manager.ToolRoutingTable["homebox_db/execute_sql"]);
            Assert.Equal("postgres-mcp-homebox", manager.ToolRoutingTable["postgres-mcp-homebox/execute_sql"]);

            // Verify legacy delimiter entries
            Assert.Equal("postgres-mcp-homebox", manager.ToolRoutingTable["homebox_db__execute_sql"]);
            Assert.Equal("postgres-mcp-homebox", manager.ToolRoutingTable["homebox_db:execute_sql"]);
            Assert.Equal("postgres-mcp-homebox", manager.ToolRoutingTable["postgres-mcp-homebox__execute_sql"]);
            Assert.Equal("postgres-mcp-homebox", manager.ToolRoutingTable["postgres-mcp-homebox:execute_sql"]);

            await Task.CompletedTask;
        }

        [Fact]
        [Requirement("MCP-27", "MCP", RequirementType.Positive, "ToolRoutingManager falls back to server ID as namespace when alias is null or whitespace.")]
        public void CacheTools_Exposes_Slash_Formatted_Name_With_Server_Id_When_Alias_Empty()
        {
            var manager = new ToolRoutingManager();
            var server = new McpServer
            {
                Id = "docker",
                Alias = "   ",
                DisplayName = "Docker Engine"
            };

            var toolsJson = @"[{""name"": ""list_containers"", ""description"": ""List running containers"", ""inputSchema"": {}}]";
            using var doc = System.Text.Json.JsonDocument.Parse(toolsJson);

            var exposed = manager.BuildExposedToolDefinition(server, doc.RootElement[0]);
            Assert.NotNull(exposed);
            Assert.Equal("docker/list_containers", exposed["name"]);
            Assert.Equal("[docker] List running containers", exposed["description"]);

            Assert.Equal("docker", manager.ToolRoutingTable["docker/list_containers"]);
            Assert.Equal("docker", manager.ToolRoutingTable["docker__list_containers"]);
            Assert.Equal("docker", manager.ToolRoutingTable["docker:list_containers"]);
        }

        [Fact]
        [Requirement("MCP-30", "MCP", RequirementType.Positive, "ToolRoutingManager normalizes slash, colon, and double-underscore delimiters and resolves aliases to underlying servers.")]
        public void NormalizeTargetToolName_Resolves_Multiple_Delimiters_And_Aliases()
        {
            // MCP-30: Resolves alias/tool, alias:tool, alias__tool, serverId/tool, serverId__tool
            var manager = new ToolRoutingManager();
            var servers = new List<McpServer>
            {
                new McpServer { Id = "postgres-mcp-homebox", Alias = "homebox_db" }
            };
            manager.ToolRoutingTable["homebox_db/execute_sql"] = "postgres-mcp-homebox";
            manager.ToolRoutingTable["postgres-mcp-homebox/execute_sql"] = "postgres-mcp-homebox";
            manager.ToolRoutingTable["homebox_db__execute_sql"] = "postgres-mcp-homebox";

            var (norm1, err1) = manager.NormalizeTargetToolName("homebox_db/execute_sql", servers);
            Assert.Null(err1);
            Assert.Equal("homebox_db__execute_sql", norm1);

            var (norm2, err2) = manager.NormalizeTargetToolName("homebox_db:execute_sql", servers);
            Assert.Null(err2);
            Assert.Equal("homebox_db__execute_sql", norm2);
        }

        [Fact]
        [Requirement("MCP-28", "MCP", RequirementType.Negative, "ToolRoutingManager rejects ambiguous bare tool calls when duplicate tool names exist across distinct servers, listing candidates with namespaces.")]
        public void NormalizeTargetToolName_Returns_Ambiguity_Error_Listing_Aliases_For_Duplicates()
        {
            // MCP-28: Detects duplicate tool names across distinct servers and returns candidates formatted with namespaces
            var manager = new ToolRoutingManager();
            var servers = new List<McpServer>
            {
                new McpServer { Id = "postgres-mcp-homebox", Alias = "homebox_db" },
                new McpServer { Id = "postgres-mcp-sure", Alias = "sure_db" }
            };
            manager.ToolRoutingTable["homebox_db/execute_sql"] = "postgres-mcp-homebox";
            manager.ToolRoutingTable["sure_db/execute_sql"] = "postgres-mcp-sure";

            var (_, err) = manager.NormalizeTargetToolName("execute_sql", servers);
            Assert.NotNull(err);
            Assert.Contains("Ambiguous tool name 'execute_sql'", err);
            Assert.Contains("'homebox_db/execute_sql'", err);
            Assert.Contains("'sure_db/execute_sql'", err);
        }
    }
}
