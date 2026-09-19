namespace ModelContextGateway.Core.VectorSearch
{
    /// <summary>
    /// Adapter allowing existing IEmbeddingService instances (Local ONNX, Dynamic, Api) to be consumed as IEmbeddingProvider.
    /// </summary>
    public class EmbeddingServiceAdapter : IEmbeddingProvider
    {
        private readonly IEmbeddingService _embeddingService;

        public EmbeddingServiceAdapter(IEmbeddingService embeddingService)
        {
            _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        }

        public IEmbeddingService InnerService => _embeddingService;

        /// <inheritdoc />
        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            return await _embeddingService.GetEmbeddingAsync(text);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            if (texts == null || texts.Count == 0)
            {
                return Array.Empty<float[]>();
            }

            var tasks = texts.Select(t => _embeddingService.GetEmbeddingAsync(t));
            var results = await Task.WhenAll(tasks);
            return results;
        }
    }
}
