using System.Collections.Concurrent;

namespace ModelContextGateway.Core.Routing
{
    /// <summary>
    /// Manages backend tool listing, caching, namespaced routing tables, and tool invocation execution.
    /// </summary>
    public partial class ToolRoutingManager
    {
        private readonly ConcurrentDictionary<string, string> _toolRoutingTable = new();
        private readonly List<object> _cachedTools = new();
        private readonly object _cacheLock = new();
        private bool _isCachePopulated = false;

        /// <summary>
        /// Gets the active thread-safe namespaced tool-to-server routing map.
        /// </summary>
        public ConcurrentDictionary<string, string> ToolRoutingTable => _toolRoutingTable;

        public static List<object> GetMetaModeTools()
        {
            return new List<object>
            {
                new
                {
                    name = "search_tools",
                    description = "Semantically search across all registered internal MCP tools (Excel, Docker, Plex, Home Assistant, etc.) using keywords. Returns the matching tool names, descriptions, and input schemas. Use this first to discover what tools are available.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new { type = "string", description = "The natural language query describing what you want to do (e.g. 'read Excel file data', 'restart Docker container')." }
                        },
                        required = new[] { "query" }
                    }
                },
                new
                {
                    name = "execute_tool",
                    description = "Execute a specific internal MCP tool by name with arguments. Accepts namespaced tool names ('server__tool' or 'server/tool') or bare tool names ('tool') if unambiguous. Obtain available tools by calling search_tools first.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string", description = "The name of the tool to execute (e.g. 'docker__list_containers', 'docker/list_containers', or bare 'list_containers')." },
                            arguments = new { type = "object", description = "The arguments JSON object expected by the target tool." },
                            target_auth_token = new { type = "string", description = "Optional authentication token if the backend tool requires dynamic pass-through authorization." }
                        },
                        required = new[] { "name", "arguments" }
                    }
                }
            };
        }

        /// <summary>
        /// Normalizes various tool name formats (e.g. 'server/tool', 'server:tool', or bare 'tool') into the canonical 'server__tool' format.
        /// </summary>
        public (string NormalizedName, string? AmbiguityError) NormalizeTargetToolName(string targetName, IEnumerable<McpServer> servers, Microsoft.Extensions.Logging.ILogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(targetName))
            {
                return (targetName, null);
            }

            var trimmed = targetName.Trim();

            // 1. Already in canonical server__tool format
            if (trimmed.Contains("__"))
            {
                return (trimmed, null);
            }

            // 2. Delimited by slash or colon (e.g. docker/list_containers, server:tool)
            if (trimmed.Contains('/'))
            {
                var idx = trimmed.IndexOf('/');
                return ($"{trimmed.Substring(0, idx)}__{trimmed.Substring(idx + 1)}", null);
            }

            if (trimmed.Contains(':'))
            {
                var idx = trimmed.IndexOf(':');
                return ($"{trimmed.Substring(0, idx)}__{trimmed.Substring(idx + 1)}", null);
            }

            // 3. Bare tool name — search routing table for matching suffix
            var matchingKeys = _toolRoutingTable.Keys
                .Where(k => k.EndsWith($"__{trimmed}", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingKeys.Count == 1)
            {
                logger?.LogInformation("Auto-resolved bare tool name '{BareName}' to '{ResolvedName}'", trimmed, matchingKeys[0]);
                return (matchingKeys[0], null);
            }

            if (matchingKeys.Count > 1)
            {
                var options = string.Join(", ", matchingKeys.Select(k => $"'{k}'"));
                return (trimmed, $"Ambiguous tool name '{trimmed}'. Matching tools found across multiple servers: {options}. Please call execute_tool with the full namespaced name.");
            }

            // 4. Fallback search in cached tools if routing table wasn't yet populated
            lock (_cacheLock)
            {
                var cachedMatches = new List<string>();
                foreach (var item in _cachedTools)
                {
                    string? name = null;
                    if (item is System.Text.Json.JsonElement je && je.TryGetProperty("name", out var np))
                    {
                        name = np.GetString();
                    }
                    else if (item is IDictionary<string, object> dict && dict.TryGetValue("name", out var no))
                    {
                        name = no?.ToString();
                    }

                    if (!string.IsNullOrEmpty(name) && name.EndsWith($"__{trimmed}", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!cachedMatches.Contains(name))
                        {
                            cachedMatches.Add(name);
                        }
                    }
                }

                if (cachedMatches.Count == 1)
                {
                    logger?.LogInformation("Auto-resolved bare tool name '{BareName}' from cache to '{ResolvedName}'", trimmed, cachedMatches[0]);
                    return (cachedMatches[0], null);
                }

                if (cachedMatches.Count > 1)
                {
                    var options = string.Join(", ", cachedMatches.Select(k => $"'{k}'"));
                    return (trimmed, $"Ambiguous tool name '{trimmed}'. Matching tools found across multiple servers: {options}. Please call execute_tool with the full namespaced name.");
                }
            }

            return (trimmed, null);
        }

        public void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _isCachePopulated = false;
                _cachedTools.Clear();
            }
        }

        public List<object> GetCachedTools()
        {
            lock (_cacheLock)
            {
                return new List<object>(_cachedTools);
            }
        }
    }
}
