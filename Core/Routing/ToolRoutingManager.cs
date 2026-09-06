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
                    description = "Execute a specific internal MCP tool by name with arguments. Accepts namespaced tool names ('server/tool' or 'server__tool') or bare tool names ('tool') if unambiguous. Obtain available tools by calling search_tools first.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string", description = "The name of the tool to execute (e.g. 'docker/list_containers', 'docker__list_containers', or bare 'list_containers')." },
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

            // 1. Check for namespace delimiters ('/', ':', '__')
            string? prefix = null;
            string? tool = null;

            if (trimmed.Contains('/'))
            {
                var idx = trimmed.IndexOf('/');
                prefix = trimmed.Substring(0, idx);
                tool = trimmed.Substring(idx + 1);
            }
            else if (trimmed.Contains(':'))
            {
                var idx = trimmed.IndexOf(':');
                prefix = trimmed.Substring(0, idx);
                tool = trimmed.Substring(idx + 1);
            }
            else if (trimmed.Contains("__"))
            {
                var idx = trimmed.IndexOf("__", StringComparison.Ordinal);
                prefix = trimmed.Substring(0, idx);
                tool = trimmed.Substring(idx + 2);
            }

            if (!string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(tool))
            {
                var srv = servers.FirstOrDefault(s =>
                    string.Equals(s.Alias, prefix, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.Id, prefix, StringComparison.OrdinalIgnoreCase));

                var normalized = $"{prefix}__{tool}";
                if (srv != null)
                {
                    _toolRoutingTable[normalized] = srv.Id;
                    _toolRoutingTable[$"{srv.Id}__{tool}"] = srv.Id;
                    if (!string.IsNullOrWhiteSpace(srv.Alias))
                    {
                        _toolRoutingTable[$"{srv.Alias}__{tool}"] = srv.Id;
                    }
                }

                return (normalized, null);
            }

            // 2. Bare tool name — search routing table for matching suffix across all delimiters
            var matchingServerMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in _toolRoutingTable)
            {
                var key = kvp.Key;
                var serverId = kvp.Value;

                bool isMatch = false;
                if (key.EndsWith("/" + trimmed, StringComparison.OrdinalIgnoreCase) ||
                    key.EndsWith("__" + trimmed, StringComparison.OrdinalIgnoreCase) ||
                    key.EndsWith(":" + trimmed, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    isMatch = true;
                }

                if (isMatch)
                {
                    var srv = servers.FirstOrDefault(s => string.Equals(s.Id, serverId, StringComparison.OrdinalIgnoreCase));
                    var ns = !string.IsNullOrWhiteSpace(srv?.Alias) ? srv.Alias : serverId;
                    matchingServerMap[serverId] = $"{ns}/{trimmed}";
                }
            }

            // 3. Fallback search in cached tools if routing table wasn't yet populated
            if (matchingServerMap.Count == 0)
            {
                lock (_cacheLock)
                {
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

                        if (!string.IsNullOrEmpty(name))
                        {
                            string? cachedPrefix = null;
                            string? cachedTool = null;
                            if (name.Contains('/'))
                            {
                                var idx = name.IndexOf('/');
                                cachedPrefix = name.Substring(0, idx);
                                cachedTool = name.Substring(idx + 1);
                            }
                            else if (name.Contains(':'))
                            {
                                var idx = name.IndexOf(':');
                                cachedPrefix = name.Substring(0, idx);
                                cachedTool = name.Substring(idx + 1);
                            }
                            else if (name.Contains("__"))
                            {
                                var idx = name.IndexOf("__", StringComparison.Ordinal);
                                cachedPrefix = name.Substring(0, idx);
                                cachedTool = name.Substring(idx + 2);
                            }

                            if (string.Equals(cachedTool, trimmed, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(cachedPrefix))
                            {
                                var srv = servers.FirstOrDefault(s =>
                                    string.Equals(s.Alias, cachedPrefix, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(s.Id, cachedPrefix, StringComparison.OrdinalIgnoreCase));

                                var serverId = srv?.Id ?? cachedPrefix;
                                var ns = !string.IsNullOrWhiteSpace(srv?.Alias) ? srv.Alias : serverId;
                                matchingServerMap[serverId] = $"{ns}/{trimmed}";
                            }
                        }
                    }
                }
            }

            if (matchingServerMap.Count > 1)
            {
                var options = string.Join(", ", matchingServerMap.Values.Distinct().OrderBy(x => x).Select(k => $"'{k}'"));
                return (trimmed, $"Ambiguous tool name '{trimmed}'. Matching tools found across multiple servers: {options}. Please call execute_tool with the full namespaced name.");
            }

            if (matchingServerMap.Count == 1)
            {
                var serverId = matchingServerMap.Keys.First();
                var srv = servers.FirstOrDefault(s => string.Equals(s.Id, serverId, StringComparison.OrdinalIgnoreCase));
                var ns = !string.IsNullOrWhiteSpace(srv?.Alias) ? srv.Alias : serverId;
                var resolved = $"{ns}__{trimmed}";
                _toolRoutingTable[resolved] = serverId;
                logger?.LogInformation("Auto-resolved bare tool name '{BareName}' to '{ResolvedName}'", trimmed, resolved);
                return (resolved, null);
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
