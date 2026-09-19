using System.Text.Json;
using ModelContextGateway.Core.VectorSearch;

namespace ModelContextGateway.Core.Routing
{
    public partial class ToolRoutingManager
    {
        private const double RrfK = 60.0;

        /// <summary>
        /// Asynchronously performs hybrid semantic search across candidate tools using lexical keyword scoring,
        /// hardware-accelerated SIMD vector similarity, and Reciprocal Rank Fusion (RRF, k=60).
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
            var tools = candidateTools ?? GetCachedTools();
            if (tools.Count == 0)
            {
                return new List<object>();
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return tools.Take(limit).ToList();
            }

            // 1. Extract metadata from candidate tools
            var toolMetas = tools.Select(ExtractToolMetadata).ToList();

            // 2. Lexical / Keyword Scoring
            var queryWords = TokenizeQuery(query);
            var keywordScored = new List<(ToolMetadata Meta, double Score)>();

            foreach (var meta in toolMetas)
            {
                double score = CalculateLexicalScore(query, queryWords, meta);
                if (score > 0)
                {
                    keywordScored.Add((meta, score));
                }
            }

            var keywordRanked = keywordScored
                .OrderByDescending(x => x.Score)
                .Select((item, index) => (item.Meta, item.Score, Rank: index + 1))
                .ToList();

            // 3. Vector Similarity Search (SIMD accelerated)
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
                        // Ensure tools are indexed in the vector store
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

            // 4. Reciprocal Rank Fusion (RRF, k=60)
            if (vectorMatches != null && vectorMatches.Count > 0)
            {
                var vectorRanked = vectorMatches
                    .Where(m => !float.IsNaN(m.Score) && m.Score > 0f)
                    .Select((item, index) => (item.ToolName, item.Score, Rank: index + 1))
                    .ToList();

                var rrfScores = new Dictionary<object, double>();
                var toolMetaLookup = toolMetas.ToDictionary(m => m.Tool, m => m);

                // Add reciprocal rank for keyword results
                foreach (var kw in keywordRanked)
                {
                    double rrf = 1.0 / (RrfK + kw.Rank);
                    rrfScores[kw.Meta.Tool] = rrf;
                }

                // Add reciprocal rank for vector results
                foreach (var vec in vectorRanked)
                {
                    var matched = FindMatchingToolMeta(vec.ToolName, toolMetas);
                    if (matched != null)
                    {
                        double rrf = 1.0 / (RrfK + vec.Rank);
                        if (rrfScores.TryGetValue(matched.Tool, out var existing))
                        {
                            rrfScores[matched.Tool] = existing + rrf;
                        }
                        else
                        {
                            rrfScores[matched.Tool] = rrf;
                        }
                    }
                }

                if (rrfScores.Count > 0)
                {
                    var fused = rrfScores
                        .OrderByDescending(kvp => kvp.Value)
                        .Select(kvp => kvp.Key)
                        .Take(limit)
                        .ToList();

                    return fused;
                }
            }

            // 5. Graceful Fallback: Pure Keyword Ranking
            if (keywordRanked.Count > 0)
            {
                return keywordRanked
                    .Select(x => x.Meta.Tool)
                    .Take(limit)
                    .ToList();
            }

            return tools.Take(Math.Min(10, limit)).ToList();
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
                if (je.TryGetProperty("tags", out var t) && t.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tag in t.EnumerateArray())
                    {
                        var s = tag.GetString();
                        if (!string.IsNullOrEmpty(s))
                        {
                            tags.Add(s);
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
                            var s = tag.GetString();
                            if (!string.IsNullOrEmpty(s))
                            {
                                tags.Add(s);
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
            }
            else
            {
                var type = tool.GetType();
                name = type.GetProperty("name")?.GetValue(tool)?.ToString() ??
                       type.GetProperty("Name")?.GetValue(tool)?.ToString() ?? "";
                description = type.GetProperty("description")?.GetValue(tool)?.ToString() ??
                              type.GetProperty("Description")?.GetValue(tool)?.ToString() ?? "";
            }

            var textToEmbed = $"{name}: {description}";
            if (parameters.Count > 0)
            {
                textToEmbed += " " + string.Join(" ", parameters.Select(p => $"{p.Name} {p.Description}"));
            }

            return new ToolMetadata(tool, name, description, tags, parameters, textToEmbed);
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
            string TextToEmbed);
    }
}
