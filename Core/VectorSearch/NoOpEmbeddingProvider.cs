namespace ModelContextGateway.Core.VectorSearch
{
    /// <summary>
    /// Fallback no-op embedding provider used when semantic vector embeddings are disabled or unconfigured.
    /// Returns empty vector arrays to signal graceful degradation to pure keyword ranking.
    /// </summary>
    public class NoOpEmbeddingProvider : IEmbeddingProvider
    {
        public static readonly NoOpEmbeddingProvider Instance = new();

        /// <inheritdoc />
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Array.Empty<float>());
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            if (texts == null || texts.Count == 0)
            {
                return Task.FromResult<IReadOnlyList<float[]>>(Array.Empty<float[]>());
            }

            IReadOnlyList<float[]> emptyList = texts.Select(_ => Array.Empty<float>()).ToList();
            return Task.FromResult(emptyList);
        }
    }
}
