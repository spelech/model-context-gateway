using System.Collections.Concurrent;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextGateway.Core.VectorSearch;
using Moq;

namespace ModelContextGateway.Tests
{
    public class ToolRoutingManagerFallbackTests
    {
        private (SqliteConnection conn, IDbConnectionFactory factory) CreateDbFactory()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();
            connection.Execute("CREATE TABLE IF NOT EXISTS Settings (Id TEXT PRIMARY KEY);");

            var mockDbFactory = new Mock<IDbConnectionFactory>();
            mockDbFactory.Setup(f => f.CreateConnection()).Returns(connection);
            mockDbFactory.Setup(f => f.ProviderName).Returns("sqlite");
            return (connection, mockDbFactory.Object);
        }

        [Fact]
        [Requirement("MCP-34", "MCP", RequirementType.Positive, "ToolRoutingManager gracefully falls back to keyword matching when NoOpEmbeddingProvider is active.")]
        public async Task SearchToolsAsync_FallsBackToKeyword_WhenNoOpEmbeddingProviderUsed()
        {
            var manager = new ToolRoutingManager(NoOpEmbeddingProvider.Instance);

            var candidateTools = new List<object>
            {
                new Dictionary<string, object> { ["name"] = "docker__ps", ["description"] = "List active containers" },
                new Dictionary<string, object> { ["name"] = "homeassistant__toggle", ["description"] = "Toggle light switch" }
            };

            var results = await manager.SearchToolsAsync("active containers", candidateTools);

            results.Should().NotBeEmpty();
            ((IDictionary<string, object>)results[0])["name"].Should().Be("docker__ps");
        }

        [Fact]
        [Requirement("MCP-34", "MCP", RequirementType.FailClosedGuardrail, "ToolRoutingManager handles embedding provider exceptions fail-closed without crashing tool routing.")]
        public async Task SearchToolsAsync_FallsBackToKeyword_WhenEmbeddingProviderThrows()
        {
            var failingProvider = new Mock<IEmbeddingProvider>();
            failingProvider.Setup(p => p.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Embedding upstream service unavailable (503)"));

            var manager = new ToolRoutingManager(failingProvider.Object);

            var candidateTools = new List<object>
            {
                new Dictionary<string, object> { ["name"] = "plex__scan", ["description"] = "Scan Plex movie folders" },
                new Dictionary<string, object> { ["name"] = "docker__logs", ["description"] = "Fetch container log tail" }
            };

            var results = await manager.SearchToolsAsync("plex movie folders", candidateTools, logger: NullLogger.Instance);

            results.Should().NotBeEmpty();
            ((IDictionary<string, object>)results[0])["name"].Should().Be("plex__scan");
        }

        [Fact]
        [Requirement("MCP-34", "MCP", RequirementType.Positive, "ToolRoutingManager operates with zero configuration when embedding provider is null.")]
        public async Task SearchToolsAsync_FallsBackToKeyword_WhenEmbeddingProviderIsNull()
        {
            var manager = new ToolRoutingManager(embeddingProvider: null);

            var candidateTools = new List<object>
            {
                new Dictionary<string, object> { ["name"] = "k8s__pods", ["description"] = "Get cluster pods" }
            };

            var results = await manager.SearchToolsAsync("cluster pods", candidateTools);

            results.Should().NotBeEmpty();
            ((IDictionary<string, object>)results[0])["name"].Should().Be("k8s__pods");
        }

        [Fact]
        [Requirement("MCP-34", "MCP", RequirementType.Positive, "ToolRoutingManager returns fallback candidate subset when query yields no matches.")]
        public async Task SearchToolsAsync_ReturnsDefaultCandidates_WhenQueryHasNoMatches()
        {
            var manager = new ToolRoutingManager(NoOpEmbeddingProvider.Instance);

            var candidateTools = new List<object>
            {
                new Dictionary<string, object> { ["name"] = "tool_1", ["description"] = "Alpha" },
                new Dictionary<string, object> { ["name"] = "tool_2", ["description"] = "Beta" }
            };

            var results = await manager.SearchToolsAsync("completely unrelated zebra query 12345", candidateTools);

            results.Should().NotBeEmpty();
            results.Count.Should().Be(2);
        }

        [Fact]
        [Requirement("MCP-34", "MCP", RequirementType.Positive, "ToolRoutingManager CallToolAsync search_tools routes through hybrid search and returns complete JSON-RPC content.")]
        public async Task CallToolAsync_SearchTools_ExecutesHybridRrfSearch()
        {
            var manager = new ToolRoutingManager();
            var (conn, dbFactory) = CreateDbFactory();
            var connections = new ConcurrentDictionary<string, BackendConnection>();
            var servers = new List<McpServer>();

            var mockEmbedding = new Mock<IEmbeddingService>();
            mockEmbedding.Setup(e => e.GetEmbeddingAsync(It.IsAny<string>()))
                         .ReturnsAsync(new float[128]);

            var body = "{\"params\":{\"arguments\":{\"query\":\"Excel document data\"}}}";
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
    }
}
