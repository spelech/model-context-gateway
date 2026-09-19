using FluentAssertions;
using ModelContextGateway.Core.VectorSearch;

namespace ModelContextGateway.Tests
{
    public class InMemorySimdToolVectorStoreTests
    {
        [Fact]
        [Requirement("MCP-32", "MCP", RequirementType.Positive, "InMemorySimdToolVectorStore scores tool embeddings using .NET 10 hardware SIMD TensorPrimitives.")]
        public async Task SearchSimilarAsync_ScoresIdenticalVectors_WithMaxSimilarity()
        {
            var store = new InMemorySimdToolVectorStore();
            var vec = new float[] { 0.5f, 0.5f, 0.5f, 0.5f };

            await store.UpsertToolEmbeddingAsync("docker__restart", vec);

            var results = await store.SearchSimilarAsync(vec, limit: 10);

            results.Should().NotBeEmpty();
            results[0].ToolName.Should().Be("docker__restart");
            results[0].Score.Should().BeApproximately(1.0f, 0.0001f);
        }

        [Fact]
        [Requirement("MCP-32", "MCP", RequirementType.Positive, "InMemorySimdToolVectorStore scores orthogonal vectors with zero similarity.")]
        public async Task SearchSimilarAsync_ScoresOrthogonalVectors_WithZeroSimilarity()
        {
            var store = new InMemorySimdToolVectorStore();
            await store.UpsertToolEmbeddingAsync("tool_x", new float[] { 1.0f, 0.0f, 0.0f });

            var query = new float[] { 0.0f, 1.0f, 0.0f };
            var results = await store.SearchSimilarAsync(query, limit: 10);

            results.Should().NotBeEmpty();
            results[0].Score.Should().BeApproximately(0.0f, 0.0001f);
        }

        [Fact]
        [Requirement("MCP-32", "MCP", RequirementType.Positive, "InMemorySimdToolVectorStore scores opposite vectors with negative similarity.")]
        public async Task SearchSimilarAsync_ScoresOppositeVectors_WithNegativeSimilarity()
        {
            var store = new InMemorySimdToolVectorStore();
            await store.UpsertToolEmbeddingAsync("tool_neg", new float[] { 1.0f, 0.0f, 0.0f });

            var query = new float[] { -1.0f, 0.0f, 0.0f };
            var results = await store.SearchSimilarAsync(query, limit: 10);

            results.Should().NotBeEmpty();
            results[0].Score.Should().BeApproximately(-1.0f, 0.0001f);
        }

        [Fact]
        [Requirement("MCP-32", "MCP", RequirementType.Positive, "InMemorySimdToolVectorStore ranks results descending by SIMD cosine similarity.")]
        public async Task SearchSimilarAsync_RanksDescendingByCosineSimilarity()
        {
            var store = new InMemorySimdToolVectorStore();
            var query = new float[] { 1.0f, 0.0f, 0.0f };

            // Closest: [1, 0, 0] -> similarity 1.0
            await store.UpsertToolEmbeddingAsync("tool_close", new float[] { 1.0f, 0.0f, 0.0f });
            // Mid: [0.707, 0.707, 0] -> similarity ~0.707
            await store.UpsertToolEmbeddingAsync("tool_mid", new float[] { 0.7071f, 0.7071f, 0.0f });
            // Far: [0, 1, 0] -> similarity 0.0
            await store.UpsertToolEmbeddingAsync("tool_far", new float[] { 0.0f, 1.0f, 0.0f });

            var results = await store.SearchSimilarAsync(query, limit: 2);

            results.Should().HaveCount(2);
            results[0].ToolName.Should().Be("tool_close");
            results[1].ToolName.Should().Be("tool_mid");
            results[0].Score.Should().BeGreaterThan(results[1].Score);
        }

        [Fact]
        [Requirement("MCP-32", "MCP", RequirementType.FailClosedGuardrail, "InMemorySimdToolVectorStore handles dimension mismatches and empty inputs gracefully.")]
        public async Task SearchSimilarAsync_HandlesDimensionMismatch_AndEmptyGracefully()
        {
            var store = new InMemorySimdToolVectorStore();

            // Empty store
            var emptyRes = await store.SearchSimilarAsync(new float[] { 1.0f, 0.0f });
            emptyRes.Should().BeEmpty();

            // Store has 3D vector, query is 2D vector
            await store.UpsertToolEmbeddingAsync("tool_3d", new float[] { 1.0f, 0.0f, 0.0f });
            var mismatchRes = await store.SearchSimilarAsync(new float[] { 1.0f, 0.0f });
            mismatchRes.Should().BeEmpty();

            // Empty query vector
            var emptyQueryRes = await store.SearchSimilarAsync(Array.Empty<float>());
            emptyQueryRes.Should().BeEmpty();
        }

        [Fact]
        [Requirement("MCP-32", "MCP", RequirementType.Positive, "InMemorySimdToolVectorStore supports upsert, remove, and clear operations.")]
        public async Task UpsertAndRemove_ManagesToolEmbeddings_Correctly()
        {
            var store = new InMemorySimdToolVectorStore();
            store.Count.Should().Be(0);

            await store.UpsertToolEmbeddingAsync("tool_a", new float[] { 1.0f, 2.0f });
            store.Count.Should().Be(1);
            store.TryGetEmbedding("tool_a", out var emb).Should().BeTrue();
            emb.Should().Equal(new float[] { 1.0f, 2.0f });

            // Update existing
            await store.UpsertToolEmbeddingAsync("tool_a", new float[] { 3.0f, 4.0f });
            store.Count.Should().Be(1);
            store.TryGetEmbedding("tool_a", out emb).Should().BeTrue();
            emb.Should().Equal(new float[] { 3.0f, 4.0f });

            // Remove
            await store.RemoveToolEmbeddingAsync("tool_a");
            store.Count.Should().Be(0);
            store.TryGetEmbedding("tool_a", out _).Should().BeFalse();

            // Clear
            await store.UpsertToolEmbeddingAsync("tool_b", new float[] { 1.0f });
            await store.UpsertToolEmbeddingAsync("tool_c", new float[] { 2.0f });
            store.Count.Should().Be(2);
            await store.ClearAsync();
            store.Count.Should().Be(0);
        }
    }
}
