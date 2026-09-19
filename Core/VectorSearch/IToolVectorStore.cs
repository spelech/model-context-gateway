namespace ModelContextGateway.Core.VectorSearch
{
    /// <summary>
    /// Abstraction for storing and performing similarity search over MCP tool vector embeddings.
    /// Provides extensible backend support for In-Memory SIMD, Postgres pgvector, and Qdrant.
    /// </summary>
    public interface IToolVectorStore
    {
        /// <summary>
        /// Upserts or updates the embedding vector for a specified tool.
        /// </summary>
        /// <param name="toolName">The unique or namespaced tool identifier.</param>
        /// <param name="embedding">The vector embedding float array.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UpsertToolEmbeddingAsync(string toolName, float[] embedding, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs similarity search across indexed tool embeddings for a given query embedding.
        /// </summary>
        /// <param name="queryEmbedding">The query vector float array.</param>
        /// <param name="limit">Maximum number of results to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ranked list of tool names and their similarity scores descending.</returns>
        Task<IReadOnlyList<(string ToolName, float Score)>> SearchSimilarAsync(float[] queryEmbedding, int limit = 30, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the embedding for a specified tool.
        /// </summary>
        Task RemoveToolEmbeddingAsync(string toolName, CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Clears all indexed tool embeddings.
        /// </summary>
        Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
