using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ModelContextGateway.Tests
{
    public class SemanticSearchEndpointTests : IClassFixture<PipelineIntegrationFactory>
    {
        private readonly PipelineIntegrationFactory _factory;

        public SemanticSearchEndpointTests(PipelineIntegrationFactory factory)
        {
            _factory = factory;
        }

        private static (SqliteConnection conn, IDbConnectionFactory factory) CreateDbFactory()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();
            connection.Execute("CREATE TABLE IF NOT EXISTS Settings (Id TEXT PRIMARY KEY);");

            var mockDbFactory = new Mock<IDbConnectionFactory>();
            mockDbFactory.Setup(f => f.CreateConnection()).Returns(connection);
            mockDbFactory.Setup(f => f.ProviderName).Returns("sqlite");
            return (connection, mockDbFactory.Object);
        }

        private HttpClient CreateAuthenticatedClient()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Forwarded-For", "127.0.0.1");
            client.DefaultRequestHeaders.Add("Remote-User", "admin_user");
            client.DefaultRequestHeaders.Add("Remote-Groups", "full_admin");
            return client;
        }

        [Fact]
        [Requirement("MCP-41", "MCP", RequirementType.Positive, "Explicit search modes (hybrid, semantic, lexical) and decomposed score diagnostics in ToolRoutingManager.")]
        public async Task SemanticSearchEndpoint_ReturnsCalibratedHybridResultsWithDiagnostics()
        {
            var sessionManager = _factory.Services.GetRequiredService<SessionManager>();
            var tool1 = new Dictionary<string, object>
            {
                ["name"] = "docker__restart_container",
                ["description"] = "Restart an active docker container service",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["container_id"] = new Dictionary<string, object> { ["type"] = "string" }
                    }
                }
            };
            var tool2 = new Dictionary<string, object>
            {
                ["name"] = "docker__inspect_container",
                ["description"] = "Inspect container attributes",
                ["inputSchema"] = new Dictionary<string, object> { ["type"] = "object" }
            };
            var tool3 = new Dictionary<string, object>
            {
                ["name"] = "media__get_status",
                ["description"] = "Get status of active media transcoder",
                ["inputSchema"] = new Dictionary<string, object> { ["type"] = "object" }
            };
            sessionManager.SetServerToolsCache("docker", new List<object> { tool1, tool2 });
            sessionManager.SetServerToolsCache("media", new List<object> { tool3 });

            var client = CreateAuthenticatedClient();
            var payload = new
            {
                query = "restart container",
                mode = "hybrid",
                denseWeight = 0.6,
                limit = 10
            };

            var response = await client.PostAsJsonAsync("/api/test/semantic-search", payload);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = doc.RootElement;

            // Verify top-level envelope
            root.GetProperty("query").GetString().Should().Be("restart container");
            root.GetProperty("mode").GetString().Should().Be("hybrid");
            root.GetProperty("denseWeight").GetDouble().Should().BeApproximately(0.6, 0.001);

            // Verify decomposed score diagnostics in results
            var results = root.GetProperty("results");
            results.GetArrayLength().Should().Be(3);

            var first = results[0];
            first.GetProperty("toolName").GetString().Should().Be("docker__restart_container");
            first.GetProperty("serverId").GetString().Should().Be("docker");
            
            // Validate real numerical bounds and mathematical consistency
            var score = first.GetProperty("score").GetDouble();
            score.Should().BeInRange(0.0, 1.0);
            score.Should().BeGreaterThan(0.0);

            var sparseScore = first.GetProperty("sparseScore").GetDouble();
            sparseScore.Should().BeInRange(0.0, 1.0);
            sparseScore.Should().BeApproximately(1.0, 0.01); // Top keyword hit

            // In absence of configured vector store in mock DI, denseScore is null and score equals sparseScore
            if (first.TryGetProperty("denseScore", out var denseProp) && denseProp.ValueKind != JsonValueKind.Null)
            {
                var denseScore = denseProp.GetDouble();
                denseScore.Should().BeInRange(0.0, 1.0);
                score.Should().BeApproximately(0.6 * denseScore + 0.4 * sparseScore, 0.01);
            }
            else
            {
                score.Should().BeApproximately(sparseScore, 0.01);
            }
        }

        [Fact]
        [Requirement("MCP-41", "MCP", RequirementType.Positive, "Explicit search modes (hybrid, semantic, lexical) and decomposed score diagnostics in ToolRoutingManager.")]
        public async Task SemanticSearchEndpoint_RespectsSearchModeAndDenseWeightParameters()
        {
            var sessionManager = _factory.Services.GetRequiredService<SessionManager>();
            var tool1 = new Dictionary<string, object>
            {
                ["name"] = "media__scan_library",
                ["description"] = "Trigger media refresh and index library sections",
                ["inputSchema"] = new Dictionary<string, object> { ["type"] = "object" }
            };
            var tool2 = new Dictionary<string, object>
            {
                ["name"] = "docker__list_containers",
                ["description"] = "List active running containers",
                ["inputSchema"] = new Dictionary<string, object> { ["type"] = "object" }
            };
            var tool3 = new Dictionary<string, object>
            {
                ["name"] = "media__reboot_server",
                ["description"] = "Bounce physical server host",
                ["inputSchema"] = new Dictionary<string, object> { ["type"] = "object" }
            };
            sessionManager.SetServerToolsCache("media", new List<object> { tool1, tool3 });
            sessionManager.SetServerToolsCache("docker", new List<object> { tool2 });

            var client = CreateAuthenticatedClient();
            var payload = new
            {
                query = "scan library",
                mode = "lexical",
                denseWeight = 0.0,
                limit = 5
            };

            var response = await client.PostAsJsonAsync("/api/test/semantic-search", payload);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = doc.RootElement;

            root.GetProperty("mode").GetString().Should().Be("lexical");
            root.GetProperty("denseWeight").GetDouble().Should().Be(0.0);
            var results = root.GetProperty("results");
            results.GetArrayLength().Should().Be(3);

            // In lexical mode, media__scan_library MUST rank first with sparseScore = 1.0 and denseScore = null
            var first = results[0];
            first.GetProperty("toolName").GetString().Should().Be("media__scan_library");
            first.GetProperty("denseScore").ValueKind.Should().Be(JsonValueKind.Null);
            first.GetProperty("sparseScore").GetDouble().Should().BeApproximately(1.0, 0.01);
            first.GetProperty("score").GetDouble().Should().BeApproximately(1.0, 0.01);

            // Other tools with zero keyword matches must have sparseScore = 0.0 and score = 0.0
            var second = results[1];
            second.GetProperty("sparseScore").GetDouble().Should().Be(0.0);
            second.GetProperty("score").GetDouble().Should().Be(0.0);
        }

        [Fact]
        [Requirement("MCP-42", "MCP", RequirementType.Positive, "Meta-mode search_tools MCP tool accepts optional mode and dense_weight arguments and executes calibrated tool retrieval.")]
        public void MetaModeTools_SearchToolsSchema_ExposesModeAndDenseWeight()
        {
            var metaTools = ToolRoutingManager.GetMetaModeTools();
            var searchToolsObj = metaTools.FirstOrDefault(t =>
            {
                var json = JsonSerializer.Serialize(t);
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("name", out var n) && n.GetString() == "search_tools";
            });

            searchToolsObj.Should().NotBeNull();
            var toolJson = JsonSerializer.Serialize(searchToolsObj);
            using var toolDoc = JsonDocument.Parse(toolJson);
            var props = toolDoc.RootElement.GetProperty("inputSchema").GetProperty("properties");

            // Verify query property
            props.TryGetProperty("query", out var queryProp).Should().BeTrue();
            queryProp.GetProperty("type").GetString().Should().Be("string");

            // Verify mode property with enum
            props.TryGetProperty("mode", out var modeProp).Should().BeTrue();
            modeProp.GetProperty("type").GetString().Should().Be("string");
            var enumVals = modeProp.GetProperty("enum").EnumerateArray().Select(e => e.GetString()).ToList();
            enumVals.Should().Contain(new[] { "hybrid", "semantic", "lexical" });

            // Verify dense_weight property
            props.TryGetProperty("dense_weight", out var weightProp).Should().BeTrue();
            weightProp.GetProperty("type").GetString().Should().Be("number");
        }

        [Fact]
        [Requirement("MCP-42", "MCP", RequirementType.Positive, "Meta-mode search_tools MCP tool accepts optional mode and dense_weight arguments and executes calibrated tool retrieval.")]
        public async Task SearchTools_CustomToolCall_ExecutesWithModeAndDenseWeight()
        {
            var manager = new ToolRoutingManager();
            var (conn, dbFactory) = CreateDbFactory();
            var connections = new ConcurrentDictionary<string, BackendConnection>();
            var servers = new List<McpServer>
            {
                new McpServer { Id = "docker", Enabled = true }
            };

            var mockEmbedding = new Mock<IEmbeddingService>();
            mockEmbedding.Setup(e => e.GetEmbeddingAsync(It.IsAny<string>()))
                         .ReturnsAsync(new float[128]);

            var sessionManager = _factory.Services.GetRequiredService<SessionManager>();
            var tool1 = new Dictionary<string, object>
            {
                ["name"] = "docker__list_containers",
                ["description"] = "List active running containers",
                ["inputSchema"] = new Dictionary<string, object> { ["type"] = "object" }
            };
            var tool2 = new Dictionary<string, object>
            {
                ["name"] = "docker__prune_volumes",
                ["description"] = "Remove all unused local volumes",
                ["inputSchema"] = new Dictionary<string, object> { ["type"] = "object" }
            };
            var tool3 = new Dictionary<string, object>
            {
                ["name"] = "weather__forecast",
                ["description"] = "Retrieve meteorology forecast",
                ["inputSchema"] = new Dictionary<string, object> { ["type"] = "object" }
            };
            sessionManager.SetServerToolsCache("docker", new List<object> { tool1, tool2, tool3 });

            // Call search_tools with explicit lexical mode and dense_weight
            var body = "{\"params\":{\"arguments\":{\"query\":\"list containers\",\"mode\":\"lexical\",\"dense_weight\":0.0}}}";
            var result = await manager.CallToolAsync(
                "search_tools",
                body,
                dbFactory,
                connections,
                servers,
                Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
                new HttpClient(),
                mockEmbedding.Object,
                () => Task.CompletedTask,
                (b, k, v) => b,
                sessionManager: sessionManager
            );

            result.Should().NotBeNull();
            var resultJson = JsonSerializer.Serialize(result);
            using var doc = JsonDocument.Parse(resultJson);
            doc.RootElement.GetProperty("resultType").GetString().Should().Be("complete");
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            text.Should().NotBeNull();
            
            // Prove differential ranking: matching tool must rank #1
            var toolSchemas = JsonSerializer.Deserialize<List<JsonElement>>(text!);
            toolSchemas.Should().NotBeNull();
            toolSchemas![0].GetProperty("name").GetString().Should().Be("docker__list_containers");

            // Prove lexical mode bypassed embedding generation entirely (anti-theater)
            mockEmbedding.Verify(e => e.GetEmbeddingAsync(It.IsAny<string>()), Times.Never);
        }
    }
}
