namespace ModelContextGateway.Core.Routing
{
    /// <summary>
    /// Represents a ranked tool search result with decomposed dense/sparse scores and ranking diagnostics.
    /// </summary>
    public class ToolSearchResult
    {
        /// <summary>
        /// The raw underlying tool object or dictionary.
        /// </summary>
        public required object Tool { get; init; }

        /// <summary>
        /// Resolved exposed tool identifier.
        /// </summary>
        public required string ToolName { get; init; }

        /// <summary>
        /// Upstream server identifier if namespaced or resolvable.
        /// </summary>
        public string? ServerId { get; init; }

        /// <summary>
        /// Calibrated fused score between 0.0 and 1.0.
        /// </summary>
        public double Score { get; init; }

        /// <summary>
        /// Clamped dense vector cosine similarity score between 0.0 and 1.0 (null if dense search not executed or no match).
        /// </summary>
        public double? DenseScore { get; init; }

        /// <summary>
        /// Min-max normalized sparse lexical keyword score between 0.0 and 1.0 (null if sparse search not executed).
        /// </summary>
        public double? SparseScore { get; init; }

        /// <summary>
        /// 1-based rank among dense vector search hits.
        /// </summary>
        public int? DenseRank { get; init; }

        /// <summary>
        /// 1-based rank among sparse lexical keyword hits.
        /// </summary>
        public int? SparseRank { get; init; }
    }
}
