namespace ModelContextGateway.Core.VectorSearch
{
    /// <summary>
    /// Represents an abstraction for generating vector embeddings for text strings and batch inputs.
    /// Supports OpenAI, Ollama, Azure OpenAI, LiteLLM, and local ONNX embedding models.
    /// </summary>
    public interface IEmbeddingProvider
    {
        /// <summary>
        /// Asynchronously generates a vector embedding for a single text input.
        /// </summary>
        /// <param name="text">The natural language string to embed.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A float array representing the embedding vector.</returns>
        Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously generates vector embeddings for a batch of text inputs.
        /// </summary>
        /// <param name="texts">The collection of strings to embed.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of float arrays representing the embedding vectors corresponding to the input order.</returns>
        Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
    }
}
