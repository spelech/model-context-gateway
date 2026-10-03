using System.Text.Json;
using ModelContextGateway.Core.VectorSearch;

namespace ModelContextGateway.Core.Routing
{
    public partial class ToolRoutingManager
    {
        /// <summary>
        /// Asynchronously performs calibrated hybrid semantic search across candidate tools, returning rich tool
        /// search results with decomposed dense/sparse scores, ranks, and server metadata.
        /// </summary>
        /// <param name="query">The natural language intent query.</param>
        /// <param name="candidateTools">Candidate tool list (defaults to cached tools if null).</param>
        /// <param name="embeddingProvider">Optional embedding provider override.</param>
        /// <param name="vectorStore">Optional tool vector store override.</param>
        /// <param name="logger">Optional logger for telemetry and failure reporting.</param>
        /// <param name="limit">Maximum number of tool schemas to return.</param>
        /// <param name="searchMode">Search mode: "hybrid", "semantic", or "lexical" (defaults to "hybrid").</param>
        /// <param name="denseWeight">Weight alpha for dense similarity between 0.0 and 1.0 (defaults to 0.5).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ranked tool search results with decomposed diagnostics.</returns>
        public async Task<List<ToolSearchResult>> SearchToolsDetailedAsync(
            string query,
            List<object>? candidateTools = null,
            IEmbeddingProvider? embeddingProvider = null,
            IToolVectorStore? vectorStore = null,
            ILogger? logger = null,
            int limit = 15,
            string searchMode = "hybrid",
            double? denseWeight = 0.5,
            CancellationToken cancellationToken = default)
        {
            var tools = candidateTools ?? GetCachedTools();
            if (tools.Count == 0)
            {
                return new List<ToolSearchResult>();
            }

            var toolMetas = tools.Select(ExtractToolMetadata).ToList();

            if (string.IsNullOrWhiteSpace(query))
            {
                return toolMetas.Take(limit).Select(m => new ToolSearchResult
                {
                    Tool = m.Tool,
                    ToolName = m.Name,
                    ServerId = ResolveServerId(m),
                    Score = 0.0,
                    DenseScore = null,
                    SparseScore = null,
                    DenseRank = null,
                    SparseRank = null
                }).ToList();
            }

            var mode = searchMode?.Trim().ToLowerInvariant() ?? "hybrid";
            bool computeDense = mode == "hybrid" || mode == "semantic";
            bool computeSparse = mode == "hybrid" || mode == "lexical";

            double alpha;
            if (mode == "semantic")
            {
                alpha = 1.0;
            }
            else if (mode == "lexical")
            {
                alpha = 0.0;
            }
            else
            {
                double rawWeight = denseWeight ?? 0.5;
                if (double.IsNaN(rawWeight) || double.IsInfinity(rawWeight))
                {
                    rawWeight = 0.5;
                }
                alpha = Math.Clamp(rawWeight, 0.0, 1.0);
            }

            // 1. Lexical / Keyword Scoring
            var rawLexicalScores = new Dictionary<ToolMetadata, double>();
            var sparseRanks = new Dictionary<ToolMetadata, int>();
            var normalizedSparseScores = new Dictionary<ToolMetadata, double>();

            if (computeSparse)
            {
                var queryWords = TokenizeQuery(query);
                var keywordScored = new List<(ToolMetadata Meta, double Score)>();

                foreach (var meta in toolMetas)
                {
                    double score = CalculateLexicalScore(query, queryWords, meta);
                    rawLexicalScores[meta] = score;
                    if (score > 0)
                    {
                        keywordScored.Add((meta, score));
                    }
                }

                var keywordRanked = keywordScored
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.Meta.Name, StringComparer.Ordinal)
                    .ToList();

                for (int i = 0; i < keywordRanked.Count; i++)
                {
                    sparseRanks[keywordRanked[i].Meta] = i + 1;
                }

                if (keywordScored.Count > 0)
                {
                    double maxLexical = keywordScored.Max(x => x.Score);

                    foreach (var meta in toolMetas)
                    {
                        double raw = rawLexicalScores[meta];
                        if (raw <= 0)
                        {
                            normalizedSparseScores[meta] = 0.0;
                        }
                        else if (maxLexical <= 0.0)
                        {
                            normalizedSparseScores[meta] = 1.0;
                        }
                        else
                        {
                            normalizedSparseScores[meta] = Math.Clamp(raw / maxLexical, 0.0, 1.0);
                        }
                    }
                }
                else
                {
                    foreach (var meta in toolMetas)
                    {
                        normalizedSparseScores[meta] = 0.0;
                    }
                }
            }

            // 2. Vector Similarity Search (SIMD accelerated)
            var denseScores = new Dictionary<ToolMetadata, double>();
            var denseRanks = new Dictionary<ToolMetadata, int>();

            if (computeDense)
            {
                var effectiveProvider = embeddingProvider ?? _embeddingProvider;
                var effectiveStore = vectorStore ?? _vectorStore;

                IReadOnlyList<(string ToolName, float Score)>? vectorMatches = null;

                if (effectiveProvider != null && !(effectiveProvider is NoOpEmbeddingProvider) && effectiveStore != null)
                {
                    try
                    {
                        float[]? queryEmbedding = null;
                        try
                        {
                            queryEmbedding = await effectiveProvider.GenerateEmbeddingAsync(query, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            logger?.LogWarning(ex, "Failed to generate query embedding for '{Query}'. Falling back to keyword ranking.", query);
                        }

                        if (queryEmbedding != null && queryEmbedding.Length > 0)
                        {
                            await EnsureToolsIndexedAsync(toolMetas, effectiveProvider, effectiveStore, logger, cancellationToken);
                            vectorMatches = await effectiveStore.SearchSimilarAsync(queryEmbedding, limit: 50, cancellationToken: cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Vector search encountered an error. Falling back gracefully to keyword ranking.");
                        vectorMatches = null;
                    }
                }

                if (vectorMatches != null && vectorMatches.Count > 0)
                {
                    var vectorRanked = vectorMatches
                        .Where(m => !float.IsNaN(m.Score) && m.Score > 0f)
                        .ToList();

                    for (int i = 0; i < vectorRanked.Count; i++)
                    {
                        var vec = vectorRanked[i];
                        var matched = FindMatchingToolMeta(vec.ToolName, toolMetas);
                        if (matched != null && !denseScores.ContainsKey(matched))
                        {
                            denseScores[matched] = Math.Clamp((double)vec.Score, 0.0, 1.0);
                            denseRanks[matched] = i + 1;
                        }
                    }
                }
                else if (mode == "hybrid" && (effectiveProvider == null || effectiveProvider is NoOpEmbeddingProvider || effectiveStore == null))
                {
                    // Graceful fallback to pure keyword scoring if vector search is unconfigured or disabled
                    alpha = 0.0;
                }
            }

            // 3. Fused Linear Combination Scoring
            var searchResults = new List<ToolSearchResult>(toolMetas.Count);

            foreach (var meta in toolMetas)
            {
                double? denseScore = denseScores.TryGetValue(meta, out var ds) ? ds : null;
                int? denseRank = denseRanks.TryGetValue(meta, out var dr) ? dr : null;

                double? sparseScore = normalizedSparseScores.TryGetValue(meta, out var ss) ? ss : null;
                int? sparseRank = sparseRanks.TryGetValue(meta, out var sr) ? sr : null;

                double combinedScore = (alpha * (denseScore ?? 0.0)) + ((1.0 - alpha) * (sparseScore ?? 0.0));

                searchResults.Add(new ToolSearchResult
                {
                    Tool = meta.Tool,
                    ToolName = meta.Name,
                    ServerId = ResolveServerId(meta),
                    Score = combinedScore,
                    DenseScore = denseScore,
                    SparseScore = sparseScore,
                    DenseRank = denseRank,
                    SparseRank = sparseRank
                });
            }

            return searchResults
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.SparseRank ?? int.MaxValue)
                .ThenBy(r => r.DenseRank ?? int.MaxValue)
                .ThenBy(r => r.ToolName, StringComparer.Ordinal)
                .Take(limit)
                .ToList();
        }

        /// <summary>
        /// Asynchronously performs hybrid semantic search across candidate tools using calibrated linear combination
        /// of dense vector similarity and sparse keyword scoring.
        /// Gracefully falls back to pure keyword matching if the embedding provider is unconfigured, disabled, or offline.
        /// </summary>
        /// <param name="query">The natural language intent query.</param>
        /// <param name="candidateTools">Candidate tool list (defaults to cached tools if null).</param>
        /// <param name="embeddingProvider">Optional embedding provider override.</param>
        /// <param name="vectorStore">Optional tool vector store override.</param>
        /// <param name="logger">Optional logger for telemetry and failure reporting.</param>
        /// <param name="limit">Maximum number of tool schemas to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ranked candidate tool schemas.</returns>
        public async Task<List<object>> SearchToolsAsync(
            string query,
            List<object>? candidateTools = null,
            IEmbeddingProvider? embeddingProvider = null,
            IToolVectorStore? vectorStore = null,
            ILogger? logger = null,
            int limit = 15,
            CancellationToken cancellationToken = default)
        {
            var detailedResults = await SearchToolsDetailedAsync(
                query,
                candidateTools,
                embeddingProvider,
                vectorStore,
                logger,
                limit,
                searchMode: "hybrid",
                denseWeight: 0.5,
                cancellationToken: cancellationToken);

            return detailedResults.Select(r => r.Tool).ToList();
        }

        /// <summary>
        /// Synchronously searches tools. Executes hybrid RRF search or falls back to keyword matching.
        /// </summary>
        public List<object> SearchTools(
            string query,
            List<object>? candidateTools = null,
            IEmbeddingProvider? embeddingProvider = null,
            IToolVectorStore? vectorStore = null,
            ILogger? logger = null,
            int limit = 15)
        {
            var effectiveProvider = embeddingProvider ?? _embeddingProvider;
            if (effectiveProvider == null || effectiveProvider is NoOpEmbeddingProvider)
            {
                // Pure synchronous keyword search
                var tools = candidateTools ?? GetCachedTools();
                if (tools.Count == 0)
                {
                    return new List<object>();
                }
                if (string.IsNullOrWhiteSpace(query))
                {
                    return tools.Take(limit).ToList();
                }

                var metas = tools.Select(ExtractToolMetadata).ToList();
                var words = TokenizeQuery(query);
                var scored = metas
                    .Select(m => (m.Tool, Score: CalculateLexicalScore(query, words, m)))
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .Select(x => x.Tool)
                    .Take(limit)
                    .ToList();

                return scored.Count > 0 ? scored : tools.Take(Math.Min(10, limit)).ToList();
            }

            return SearchToolsAsync(query, candidateTools, embeddingProvider, vectorStore, logger, limit)
                .GetAwaiter().GetResult();
        }

        private static List<string> TokenizeQuery(string query)
        {
            var queryLower = query.Trim().ToLowerInvariant();
            var words = queryLower
                .Split(new[] { ' ', ',', '.', ';', ':', '-', '_', '/', '\\', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 2)
                .ToList();

            if (words.Count == 0)
            {
                words = queryLower
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .ToList();
            }

            return words;
        }

        private static double CalculateLexicalScore(string rawQuery, List<string> queryWords, ToolMetadata meta)
        {
            var queryLower = rawQuery.Trim().ToLowerInvariant();
            var nameLower = meta.Name.ToLowerInvariant();
            var descLower = meta.Description.ToLowerInvariant();

            double score = 0.0;
            int matches = 0;

            // Full query matches
            if (nameLower == queryLower)
            {
                score += 20.0;
                matches++;
            }
            else if (nameLower.Contains(queryLower))
            {
                score += 10.0;
                matches++;
            }

            if (descLower.Contains(queryLower))
            {
                score += 5.0;
                matches++;
            }

            // Word matches
            foreach (var word in queryWords)
            {
                bool wordMatched = false;

                if (nameLower.Contains(word))
                {
                    score += 4.0;
                    wordMatched = true;
                }

                if (meta.Tags.Any(t => t.Contains(word, StringComparison.OrdinalIgnoreCase)))
                {
                    score += 3.0;
                    wordMatched = true;
                }

                if (descLower.Contains(word))
                {
                    score += 2.0;
                    wordMatched = true;
                }

                foreach (var param in meta.Parameters)
                {
                    if (param.Name.Contains(word, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 2.0;
                        wordMatched = true;
                    }
                    if (param.Description.Contains(word, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 1.0;
                        wordMatched = true;
                    }
                }

                if (wordMatched)
                {
                    matches++;
                }
            }

            // Multi-token match bonus
            if (matches > 1)
            {
                score += matches * 2.0;
            }

            return score;
        }

        private static ToolMetadata? FindMatchingToolMeta(string vectorToolName, List<ToolMetadata> metas)
        {
            var exact = metas.FirstOrDefault(m => string.Equals(m.Name, vectorToolName, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                return exact;
            }

            // Try normalized match across delimiter formats ('/', ':', '__')
            var normVector = NormalizeToolName(vectorToolName);
            return metas.FirstOrDefault(m => NormalizeToolName(m.Name) == normVector);
        }

        private static string NormalizeToolName(string name)
        {
            return name.Replace("__", "/").Replace(":", "/").Trim();
        }

        private static async Task EnsureToolsIndexedAsync(
            List<ToolMetadata> metas,
            IEmbeddingProvider provider,
            IToolVectorStore store,
            ILogger? logger,
            CancellationToken cancellationToken)
        {
            if (store is InMemorySimdToolVectorStore inMem)
            {
                var unindexed = metas.Where(m => !inMem.TryGetEmbedding(m.Name, out _)).ToList();
                if (unindexed.Count == 0)
                {
                    return;
                }

                var texts = unindexed.Select(m => m.TextToEmbed).ToList();
                try
                {
                    var embeddings = await provider.GenerateEmbeddingsAsync(texts, cancellationToken);
                    for (int i = 0; i < unindexed.Count && i < embeddings.Count; i++)
                    {
                        var vec = embeddings[i];
                        if (vec != null && vec.Length > 0)
                        {
                            await store.UpsertToolEmbeddingAsync(unindexed[i].Name, vec, cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to batch embed {Count} tools for vector store indexing.", unindexed.Count);
                }
            }
        }

        private static ToolMetadata ExtractToolMetadata(object tool)
        {
            string name = "";
            string description = "";
            string? serverId = null;
            var tags = new List<string>();
            var parameters = new List<(string Name, string Description)>();

            if (tool is JsonElement je)
            {
                if (je.TryGetProperty("name", out var n))
                {
                    name = n.GetString() ?? "";
                }
                if (je.TryGetProperty("description", out var d))
                {
                    description = d.GetString() ?? "";
                }
                if (je.TryGetProperty("serverId", out var s) || je.TryGetProperty("server_id", out s))
                {
                    serverId = s.GetString();
                }
                if (je.TryGetProperty("tags", out var t) && t.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tag in t.EnumerateArray())
                    {
                        var str = tag.GetString();
                        if (!string.IsNullOrEmpty(str))
                        {
                            tags.Add(str);
                        }
                    }
                }

                if (je.TryGetProperty("inputSchema", out var schema) && schema.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in props.EnumerateObject())
                    {
                        var pName = prop.Name;
                        var pDesc = prop.Value.TryGetProperty("description", out var pd) ? pd.GetString() ?? "" : "";
                        parameters.Add((pName, pDesc));
                    }
                }
            }
            else if (tool is IDictionary<string, object> dict)
            {
                if (dict.TryGetValue("name", out var n))
                {
                    name = n?.ToString() ?? "";
                }
                if (dict.TryGetValue("description", out var d))
                {
                    description = d?.ToString() ?? "";
                }
                if (dict.TryGetValue("serverId", out var s) || dict.TryGetValue("server_id", out s))
                {
                    serverId = s?.ToString();
                }
                if (dict.TryGetValue("tags", out var t))
                {
                    if (t is IEnumerable<string> strSeq)
                    {
                        tags.AddRange(strSeq);
                    }
                    else if (t is JsonElement jeTags && jeTags.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var tag in jeTags.EnumerateArray())
                        {
                            var str = tag.GetString();
                            if (!string.IsNullOrEmpty(str))
                            {
                                tags.Add(str);
                            }
                        }
                    }
                }

                if (dict.TryGetValue("inputSchema", out var sObj))
                {
                    ExtractParametersFromSchemaObject(sObj, parameters);
                }
            }
            else if (tool is System.Collections.IDictionary legacyDict)
            {
                if (legacyDict.Contains("name"))
                {
                    name = legacyDict["name"]?.ToString() ?? "";
                }
                if (legacyDict.Contains("description"))
                {
                    description = legacyDict["description"]?.ToString() ?? "";
                }
                if (legacyDict.Contains("serverId"))
                {
                    serverId = legacyDict["serverId"]?.ToString();
                }
                else if (legacyDict.Contains("server_id"))
                {
                    serverId = legacyDict["server_id"]?.ToString();
                }
            }
            else
            {
                var type = tool.GetType();
                name = type.GetProperty("name")?.GetValue(tool)?.ToString() ??
                       type.GetProperty("Name")?.GetValue(tool)?.ToString() ?? "";
                description = type.GetProperty("description")?.GetValue(tool)?.ToString() ??
                              type.GetProperty("Description")?.GetValue(tool)?.ToString() ?? "";
                serverId = type.GetProperty("ServerId")?.GetValue(tool)?.ToString() ??
                           type.GetProperty("serverId")?.GetValue(tool)?.ToString();
            }

            var textToEmbed = $"{name}: {description}";
            if (parameters.Count > 0)
            {
                textToEmbed += " " + string.Join(" ", parameters.Select(p => $"{p.Name} {p.Description}"));
            }

            return new ToolMetadata(tool, name, description, tags, parameters, textToEmbed, serverId);
        }

        private string? ResolveServerId(ToolMetadata meta)
        {
            if (!string.IsNullOrWhiteSpace(meta.ServerId))
            {
                return meta.ServerId;
            }

            if (!string.IsNullOrWhiteSpace(meta.Name))
            {
                if (_toolRoutingTable.TryGetValue(meta.Name, out var mappedId))
                {
                    return mappedId;
                }

                var slashIdx = meta.Name.IndexOf('/');
                if (slashIdx > 0)
                {
                    return meta.Name.Substring(0, slashIdx);
                }
                var underIdx = meta.Name.IndexOf("__", StringComparison.Ordinal);
                if (underIdx > 0)
                {
                    return meta.Name.Substring(0, underIdx);
                }
                var colonIdx = meta.Name.IndexOf(':');
                if (colonIdx > 0)
                {
                    return meta.Name.Substring(0, colonIdx);
                }
            }

            return null;
        }

        private static void ExtractParametersFromSchemaObject(object? schemaObj, List<(string Name, string Description)> parameters)
        {
            if (schemaObj is JsonElement je && je.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in props.EnumerateObject())
                {
                    var pName = prop.Name;
                    var pDesc = prop.Value.TryGetProperty("description", out var pd) ? pd.GetString() ?? "" : "";
                    parameters.Add((pName, pDesc));
                }
            }
            else if (schemaObj is IDictionary<string, object> sDict && sDict.TryGetValue("properties", out var pObj))
            {
                if (pObj is IDictionary<string, object> propsDict)
                {
                    foreach (var kvp in propsDict)
                    {
                        string pDesc = "";
                        if (kvp.Value is IDictionary<string, object> valDict && valDict.TryGetValue("description", out var d))
                        {
                            pDesc = d?.ToString() ?? "";
                        }
                        parameters.Add((kvp.Key, pDesc));
                    }
                }
            }
        }

        private record ToolMetadata(
            object Tool,
            string Name,
            string Description,
            List<string> Tags,
            List<(string Name, string Description)> Parameters,
            string TextToEmbed,
            string? ServerId = null);
    }
}
