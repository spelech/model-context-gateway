using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using ModelContextGateway.Core.CodeMode;
using ModelContextGateway.Core.VectorSearch;

namespace ModelContextGateway.Core.Routing
{
    public class MetaToolDefinition
    {
        public string name { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public object inputSchema { get; set; } = new();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? outputSchema { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? annotations { get; set; }
    }

    /// <summary>
    /// Manages backend tool listing, caching, namespaced routing tables, and tool invocation execution.
    /// </summary>
    public partial class ToolRoutingManager
    {
        private readonly ConcurrentDictionary<string, string> _toolRoutingTable = new();
        private readonly List<object> _cachedTools = new();
        private readonly object _cacheLock = new();
        private bool _isCachePopulated = false;

        private IEmbeddingProvider? _embeddingProvider;
        private IToolVectorStore? _vectorStore;
        private ICodeModeExecutionEngine? _codeModeEngine;

        public ToolRoutingManager(IEmbeddingProvider? embeddingProvider = null, IToolVectorStore? vectorStore = null, ICodeModeExecutionEngine? codeModeEngine = null)
        {
            _embeddingProvider = embeddingProvider;
            _vectorStore = vectorStore ?? new InMemorySimdToolVectorStore();
            _codeModeEngine = codeModeEngine ?? new CodeModeExecutionEngine();
        }

        /// <summary>
        /// Gets or sets the active code mode execution engine.
        /// </summary>
        public ICodeModeExecutionEngine? CodeModeEngine
        {
            get => _codeModeEngine;
            set => _codeModeEngine = value;
        }

        /// <summary>
        /// Gets or sets the active vector embedding provider.
        /// </summary>
        public IEmbeddingProvider? EmbeddingProvider
        {
            get => _embeddingProvider;
            set => _embeddingProvider = value;
        }

        /// <summary>
        /// Gets or sets the active tool vector store.
        /// </summary>
        public IToolVectorStore? VectorStore
        {
            get => _vectorStore;
            set => _vectorStore = value;
        }

        /// <summary>
        /// Gets the active thread-safe namespaced tool-to-server routing map.
        /// </summary>
        public ConcurrentDictionary<string, string> ToolRoutingTable => _toolRoutingTable;

        public List<MetaToolDefinition> GetMetaTools(bool enableCodeMode = true) => GetMetaModeTools(enableCodeMode);

        public static List<MetaToolDefinition> GetMetaModeTools(bool enableCodeMode = false)
        {
            var tools = new List<MetaToolDefinition>
            {
                new()
                {
                    name = "search_tools",
                    description = "Semantically search across all registered internal MCP tools (Excel, Docker, Plex, Home Assistant, etc.) using keywords. Returns the matching tool names, descriptions, and input schemas (or compact TypeScript call signatures when format='typescript'). Use this first to discover what tools are available.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new { type = "string", description = "The natural language query or intent to find relevant tools." },
                            limit = new { type = "integer", description = "Optional maximum number of matching tools to return. Overrides the admin configured default limit." },
                            min_score = new { type = "number", description = "Optional minimum score threshold between 0.0 and 1.0 to filter low-confidence noise." },
                            score_dropoff_ratio = new { type = "number", description = "Optional relative score dropoff ratio between 0.0 and 1.0. Filters tools scoring below top_score * ratio." },
                            mode = new { type = "string", @enum = new[] { "hybrid", "semantic", "lexical" }, description = "Search mode: 'hybrid' (default), 'semantic' (pure vector), or 'lexical' (pure keyword)." },
                            dense_weight = new { type = "number", description = "Weight between 0.0 (pure lexical) and 1.0 (pure semantic). Default is 0.5 (balanced)." },
                            format = new { type = "string", @enum = new[] { "json", "typescript" }, description = "Output format: 'json' (default, returns full JSON Schemas) or 'typescript' (returns compact TypeScript function declarations with JSDoc for Code Mode)." },
                            include_typescript = new { type = "boolean", description = "Optional flag to attach a 'typescriptSignature' field to each returned JSON tool schema." }
                        },
                        required = new[] { "query" }
                    },
                    outputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            tools = new { type = "array", items = new { type = "object" } },
                            format = new { type = "string" },
                            count = new { type = "integer" }
                        },
                        required = new[] { "tools", "format", "count" }
                    },
                    annotations = new
                    {
                        title = "Search MCP Tools",
                        readOnlyHint = true,
                        destructiveHint = false,
                        idempotentHint = true,
                        openWorldHint = false
                    }
                },
                new()
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
                    },
                    annotations = new
                    {
                        title = "Execute MCP Tool",
                        readOnlyHint = false,
                        destructiveHint = true,
                        idempotentHint = false,
                        openWorldHint = true
                    }
                }
            };

            if (enableCodeMode)
            {
                tools.Add(new MetaToolDefinition
                {
                    name = "execute_script",
                    description = "Execute a JavaScript script to programmatically chain and orchestrate internal MCP tools, resources, and prompts using an in-memory sandbox. Tools can be called via await tools.<server>.<tool>(arguments) or await mcg.callTool(name, arguments). Also supports await mcg.listServers(), await mcg.listResources(serverId?), await mcg.searchResources(query), await mcg.readResource(uri), await mcg.listPrompts(serverId?), and await mcg.getPrompt(name, args). Read resource 'router://code-mode/sdk.d.ts' or 'router://code-mode/types/{server_name}' for TypeScript signatures.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            script = new { type = "string", description = "The JavaScript script content to execute." },
                            arguments = new { type = "object", description = "Optional JSON object with caller parameters accessible in the script as global args." }
                        },
                        required = new[] { "script" }
                    },
                    outputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            result = new { description = "Value returned by the JavaScript script." },
                            logs = new { type = "array", items = new { type = "string" }, description = "Captured console.log/warn/error entries." },
                            executionTimeMs = new { type = "integer", description = "Total wall-clock execution duration in milliseconds." },
                            toolCallsCount = new { type = "integer", description = "Total number of sandboxed MCP calls executed." }
                        },
                        required = new[] { "result", "logs", "executionTimeMs", "toolCallsCount" }
                    },
                    annotations = new
                    {
                        title = "Execute Code Mode Script",
                        readOnlyHint = false,
                        destructiveHint = true,
                        idempotentHint = false,
                        openWorldHint = true
                    }
                });
            }

            return tools;
        }

        /// <summary>
        /// Evaluates whether the caller is authorized to execute Code Mode scripts based on gateway settings and user groups.
        /// </summary>
        public static bool IsCodeModeAuthorized(RouterSettings settings, IEnumerable<string>? userGroups, bool isAdmin)
        {
            if (isAdmin)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(settings.CodeModeAllowedGroups) || settings.CodeModeAllowedGroups.Contains('*'))
            {
                return true;
            }

            if (userGroups == null)
            {
                return false;
            }

            var allowedGroups = settings.CodeModeAllowedGroups
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var userGroupSet = new HashSet<string>(userGroups.Where(g => !string.IsNullOrWhiteSpace(g)), StringComparer.OrdinalIgnoreCase);

            return allowedGroups.Any(allowed => userGroupSet.Contains(allowed));
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
