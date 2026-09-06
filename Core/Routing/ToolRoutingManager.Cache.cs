using System.Text.Json;

namespace ModelContextGateway.Core.Routing
{
    public partial class ToolRoutingManager
    {
        /// <summary>
        /// Asynchronously lists available tools from connected backends or returns bootstrap Meta-Mode tools.
        /// </summary>
        public async Task<List<object>> ListToolsAsync(string body, bool isMetaMode, IEnumerable<KeyValuePair<string, BackendConnection>> backendConnections, ILogger logger, Func<Task> ensureBackendsInitializedAsync, IEnumerable<McpServer> servers, SessionManager? sessionManager = null)
        {
            if (isMetaMode)
            {
                return GetMetaModeTools();
            }

            await ensureBackendsInitializedAsync();

            lock (_cacheLock)
            {
                if (_isCachePopulated)
                {
                    return new List<object>(_cachedTools);
                }
            }

            await PopulateToolsCacheAsync(body, backendConnections, logger, servers, sessionManager);
            lock (_cacheLock)
            {
                return new List<object>(_cachedTools);
            }
        }

        /// <summary>
        /// Builds the exposed tool definition with primary slash formatting ({namespace}/{tool_name}),
        /// prepends [{namespace}] to description, and registers dual-key and legacy delimiter routing table entries.
        /// </summary>
        public Dictionary<string, object>? BuildExposedToolDefinition(McpServer? server, JsonElement tool)
        {
            if (!tool.TryGetProperty("name", out var nameProp))
            {
                return null;
            }

            var rawToolName = nameProp.GetString() ?? string.Empty;
            var serverId = server?.Id ?? string.Empty;
            var ns = !string.IsNullOrWhiteSpace(server?.Alias) ? server.Alias : serverId;
            var exposedName = $"{ns}/{rawToolName}";

            if (!string.IsNullOrEmpty(serverId))
            {
                _toolRoutingTable[exposedName] = serverId;
                _toolRoutingTable[$"{serverId}/{rawToolName}"] = serverId;

                // Register legacy delimiters for resilient O(1) resolution
                _toolRoutingTable[$"{ns}__{rawToolName}"] = serverId;
                _toolRoutingTable[$"{ns}:{rawToolName}"] = serverId;
                _toolRoutingTable[$"{serverId}__{rawToolName}"] = serverId;
                _toolRoutingTable[$"{serverId}:{rawToolName}"] = serverId;
            }

            var toolDict = JsonSerializer.Deserialize<Dictionary<string, object>>(tool.GetRawText());
            if (toolDict != null)
            {
                toolDict["name"] = exposedName;
                if (toolDict.TryGetValue("description", out var desc))
                {
                    toolDict["description"] = $"[{ns}] " + desc;
                }

                if (server != null && (server.AllowPassThroughAuth || !string.IsNullOrEmpty(server.DynamicAuthPrompt)))
                {
                    var authPrompt = !string.IsNullOrEmpty(server.DynamicAuthPrompt) ? server.DynamicAuthPrompt : "This tool requires a target authentication token. Call with target_auth_token parameter.";
                    toolDict["description"] = $"{toolDict["description"]}\n\nAUTH REQUIRED: {authPrompt}";
                }

                return toolDict;
            }

            return null;
        }

        public async Task PopulateToolsCacheAsync(string body, IEnumerable<KeyValuePair<string, BackendConnection>> backendConnections, ILogger logger, IEnumerable<McpServer> servers, SessionManager? sessionManager = null)
        {
            var allTools = new List<object>();

            var tasks = new List<Task<(string ServerId, JsonElement Tools)>>();

            foreach (var entry in backendConnections)
            {
                var conn = entry.Value;
                var serverId = entry.Key;

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var reqBody = "{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\",\"id\":\"refresh-list\"}";
                        var resp = await conn.SendRequestAsync("tools/list", reqBody);
                        if (resp.Result != null && resp.Result.Value.TryGetProperty("tools", out var toolsList))
                        {
                            return (serverId, toolsList);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error listing tools on server {ServerId}", serverId);
                    }
                    return (serverId, default(JsonElement));
                }));
            }

            var completed = await Task.WhenAll(tasks);
            foreach (var item in completed)
            {
                if (item.Tools.ValueKind == JsonValueKind.Array)
                {
                    var serverTools = new List<object>();
                    var srv = servers.FirstOrDefault(s => s.Id == item.ServerId) ?? new McpServer { Id = item.ServerId };
                    foreach (var tool in item.Tools.EnumerateArray())
                    {
                        var toolDict = BuildExposedToolDefinition(srv, tool);
                        if (toolDict != null)
                        {
                            serverTools.Add(toolDict);
                            allTools.Add(toolDict);
                        }
                    }
                    if (sessionManager != null)
                    {
                        sessionManager.SetServerToolsCache(item.ServerId, serverTools);
                    }
                }
            }

            lock (_cacheLock)
            {
                _cachedTools.Clear();
                _cachedTools.AddRange(allTools);
                _isCachePopulated = true;
            }
        }
    }
}
