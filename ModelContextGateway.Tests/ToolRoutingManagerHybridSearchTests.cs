using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextGateway.Core.VectorSearch;

namespace ModelContextGateway.Tests
{
    public class ToolRoutingManagerHybridSearchTests
    {
        private class MockDeterministicEmbeddingProvider : IEmbeddingProvider
        {
            private readonly Dictionary<string, float[]> _embeddings = new(StringComparer.OrdinalIgnoreCase);

            public void SetEmbedding(string text, float[] vector)
            {
                _embeddings[text] = vector;
            }

            public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
            {
                if (_embeddings.TryGetValue(text, out var vec))
                {
                    return Task.FromResult(vec);
                }

                // Default orthogonal vector if not explicitly configured
                return Task.FromResult(new float[] { 0.0f, 0.0f, 0.0f });
            }

            public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
            {
                IReadOnlyList<float[]> results = texts.Select(t =>
                    _embeddings.TryGetValue(t, out var vec) ? vec : new float[] { 0.0f, 0.0f, 0.0f }
                ).ToList();

                return Task.FromResult(results);
            }
        }

        [Fact]
        [Requirement("MCP-33", "MCP", RequirementType.Positive, "ToolRoutingManager fuses lexical and semantic vector candidate rankings using Reciprocal Rank Fusion (RRF, k=60).")]
        public async Task SearchToolsAsync_CombinesKeywordAndVectorRanks_UsingRRF()
        {
            var provider = new MockDeterministicEmbeddingProvider();
            var vectorStore = new InMemorySimdToolVectorStore();

            // Setup 3 tools
            // Tool 1: Matches keyword "restart" AND vector
            var tool1 = new Dictionary<string, object>
            {
                ["name"] = "docker__restart",
                ["description"] = "Restart running containers in Docker"
            };
            // Tool 2: High vector similarity for "reboot", but no "restart" keyword in name/desc
            var tool2 = new Dictionary<string, object>
            {
                ["name"] = "system__reboot",
                ["description"] = "Bounce host operating system daemon"
            };
            // Tool 3: Matches keyword "restart", but completely orthogonal vector
            var tool3 = new Dictionary<string, object>
            {
                ["name"] = "service__restart",
                ["description"] = "Restart generic background service"
            };

            var candidateTools = new List<object> { tool1, tool2, tool3 };

            // Query embedding: [1, 0, 0]
            provider.SetEmbedding("restart system container", new float[] { 1.0f, 0.0f, 0.0f });

            // Tool 1 embedding: close [0.9f, 0.1f, 0.0f]
            await vectorStore.UpsertToolEmbeddingAsync("docker__restart", new float[] { 0.99f, 0.14f, 0.0f });
            // Tool 2 embedding: closest [1.0f, 0.0f, 0.0f] (rank 1 in vector!)
            await vectorStore.UpsertToolEmbeddingAsync("system__reboot", new float[] { 1.0f, 0.0f, 0.0f });
            // Tool 3 embedding: orthogonal [0.0f, 1.0f, 0.0f] (low/zero vector similarity)
            await vectorStore.UpsertToolEmbeddingAsync("service__restart", new float[] { 0.0f, 1.0f, 0.0f });

            var manager = new ToolRoutingManager(provider, vectorStore);

            var results = await manager.SearchToolsAsync("restart system container", candidateTools, logger: NullLogger.Instance);

            results.Should().NotBeEmpty();

            // Tool 1 should be ranked #1 because it has both strong keyword rank (rank 1/2) and strong vector rank (rank 2)
            // RRF = 1/(60+1) + 1/(60+2) = 0.01639 + 0.01613 = 0.03252
            // Compare to Tool 2 which has 0 keyword matches: RRF = 1/(60+1) = 0.01639
            var firstTool = results[0] as IDictionary<string, object>;
            firstTool.Should().NotBeNull();
            firstTool!["name"].Should().Be("docker__restart");
        }

        [Fact]
        [Requirement("MCP-33", "MCP", RequirementType.Positive, "ToolRoutingManager scores lexical matches across name, description, tags, and parameters.")]
        public async Task SearchToolsAsync_ScoresLexicalSignals_AcrossNameDescriptionTagsAndParameters()
        {
            var manager = new ToolRoutingManager(NoOpEmbeddingProvider.Instance);

            var toolWithParams = new Dictionary<string, object>
            {
                ["name"] = "docker__inspect",
                ["description"] = "Inspect a specific workload",
                ["tags"] = new[] { "containers", "devops" },
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["container_id"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Target container identifier or hash"
                        }
                    }
                }
            };

            var otherTool = new Dictionary<string, object>
            {
                ["name"] = "plex__list_media",
                ["description"] = "List media library items"
            };

            var candidates = new List<object> { otherTool, toolWithParams };

            // 1. Search by tag
            var tagResults = await manager.SearchToolsAsync("devops", candidates);
            tagResults.Should().NotBeEmpty();
            ((IDictionary<string, object>)tagResults[0])["name"].Should().Be("docker__inspect");

            // 2. Search by parameter name
            var paramResults = await manager.SearchToolsAsync("container_id", candidates);
            paramResults.Should().NotBeEmpty();
            ((IDictionary<string, object>)paramResults[0])["name"].Should().Be("docker__inspect");

            // 3. Search by parameter description
            var paramDescResults = await manager.SearchToolsAsync("identifier hash", candidates);
            paramDescResults.Should().NotBeEmpty();
            ((IDictionary<string, object>)paramDescResults[0])["name"].Should().Be("docker__inspect");
        }

        [Fact]
        [Requirement("MCP-33", "MCP", RequirementType.Positive, "ToolRoutingManager synchronous SearchTools method returns ranked candidate tools.")]
        public void SearchTools_SynchronousMethod_ReturnsRankedCandidates()
        {
            var manager = new ToolRoutingManager();
            var tools = new List<object>
            {
                new Dictionary<string, object> { ["name"] = "git__commit", ["description"] = "Commit staged files" },
                new Dictionary<string, object> { ["name"] = "git__push", ["description"] = "Push commits to remote" }
            };

            var results = manager.SearchTools("push commits", tools);

            results.Should().NotBeEmpty();
            ((IDictionary<string, object>)results[0])["name"].Should().Be("git__push");
        }
    }
}
