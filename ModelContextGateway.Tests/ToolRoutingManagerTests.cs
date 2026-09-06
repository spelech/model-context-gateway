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
        public void InvalidateCache_ClearsPopulatedState()
        {
            var manager = new ToolRoutingManager();
            manager.InvalidateCache();
            Assert.Empty(manager.GetCachedTools());
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

            Assert.NotNull(result);
        }

        [Fact]
        [Requirement("GUARD-01", "GUARD", RequirementType.Negative, "ToolRoutingManager returns an error when execute_tool is invoked without the mandatory tool name parameter.")]
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

            Assert.NotNull(result);
        }

        [Fact]
        [Requirement("GUARD-01", "GUARD", RequirementType.Negative, "ToolRoutingManager propagates task cancellation gracefully with a standardized JSON-RPC error response.")]
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

            Assert.NotNull(result);
        }

        [Fact]
        [Requirement("GUARD-01", "GUARD", RequirementType.Negative, "ToolRoutingManager throws KeyNotFoundException when calling a tool not registered in the routing table.")]
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
            Assert.Contains("db1__query", error);
            Assert.Contains("db2__query", error);
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
    }
}
