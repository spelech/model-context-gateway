using System.Collections.Concurrent;
using System.Numerics.Tensors;

namespace ModelContextGateway.Core.VectorSearch
{
    /// <summary>
    /// Thread-safe in-memory tool vector store utilizing .NET 10 hardware SIMD 
    /// (TensorPrimitives.CosineSimilarity) for accelerated vector similarity searches.
    /// </summary>
    public class InMemorySimdToolVectorStore : IToolVectorStore
    {
        private readonly ConcurrentDictionary<string, float[]> _embeddings = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the total number of currently indexed tool vectors.
        /// </summary>
        public int Count => _embeddings.Count;

        /// <inheritdoc />
        public Task UpsertToolEmbeddingAsync(string toolName, float[] embedding, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
            ArgumentNullException.ThrowIfNull(embedding);

            _embeddings[toolName] = embedding;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<(string ToolName, float Score)>> SearchSimilarAsync(float[] queryEmbedding, int limit = 30, CancellationToken cancellationToken = default)
        {
            if (queryEmbedding == null || queryEmbedding.Length == 0 || _embeddings.IsEmpty)
            {
                return Task.FromResult<IReadOnlyList<(string ToolName, float Score)>>(Array.Empty<(string, float)>());
            }

            ReadOnlySpan<float> querySpan = queryEmbedding;
            var results = new List<(string ToolName, float Score)>(_embeddings.Count);

            foreach (var kvp in _embeddings)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (kvp.Value == null || kvp.Value.Length != queryEmbedding.Length)
                {
                    continue;
                }

                ReadOnlySpan<float> toolSpan = kvp.Value;
                float similarity = TensorPrimitives.CosineSimilarity(querySpan, toolSpan);

                if (!float.IsNaN(similarity))
                {
                    results.Add((kvp.Key, similarity));
                }
            }

            IReadOnlyList<(string ToolName, float Score)> sorted = results
                .OrderByDescending(r => r.Score)
                .Take(limit)
                .ToList();

            return Task.FromResult(sorted);
        }

        /// <inheritdoc />
        public Task RemoveToolEmbeddingAsync(string toolName, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(toolName))
            {
                _embeddings.TryRemove(toolName, out _);
            }
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            _embeddings.Clear();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Attempts to retrieve the vector embedding for a specific tool.
        /// </summary>
        public bool TryGetEmbedding(string toolName, out float[]? embedding)
        {
            return _embeddings.TryGetValue(toolName, out embedding);
        }
    }
}
