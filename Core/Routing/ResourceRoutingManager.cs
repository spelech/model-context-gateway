using System.Collections.Concurrent;
using System.Text.Json;

namespace ModelContextGateway.Core.Routing
{
    /// <summary>
    /// Coordinates resource routing, resource template resolution, and multi-backend resource listing for MCP resources.
    /// </summary>
    public class ResourceRoutingManager
    {
        private readonly Dictionary<string, string> _resourceRoutingTable = new();
        private readonly ResourceCatalogManager _catalogManager = new();

        /// <summary>
        /// Searches through a list of resources matching the provided natural language query.
        /// </summary>
        /// <param name="query">The search term or query string.</param>
        /// <param name="resources">The raw list of resource definitions.</param>
        /// <returns>A task returning the top 15 matching resource items.</returns>
        public async Task<List<object>> SearchResourcesAsync(string query, List<object> resources)
        {
            return await _catalogManager.SearchResourcesAsync(query, resources);
        }

        public async Task<List<object>> ListResourcesAsync(string body, IEnumerable<KeyValuePair<string, BackendConnection>> backendConnections, ILogger logger, Func<Task> ensureBackendsInitializedAsync, SessionManager? sessionManager = null)
        {
            var allResources = new List<object>();
            var tasks = new List<Task<(string ServerId, JsonElement Resources)>>();

            await ensureBackendsInitializedAsync();

            foreach (var entry in backendConnections)
            {
                var conn = entry.Value;
                var serverId = entry.Key;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var reqBody = "{\"jsonrpc\":\"2.0\",\"method\":\"resources/list\",\"id\":\"refresh-res-list\"}";
                        var resp = await conn.SendRequestAsync("resources/list", reqBody);
                        if (resp.Result != null && resp.Result.Value.TryGetProperty("resources", out var resourcesList))
                        {
                            return (serverId, resourcesList);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error listing resources on server {ServerId}", serverId);
                    }
                    return (serverId, default(JsonElement));
                }));
            }

            var completed = await Task.WhenAll(tasks);
            foreach (var item in completed)
            {
                var serverResources = new List<object>();
                if (item.Resources.ValueKind == JsonValueKind.Array)
                {
                    foreach (var resource in item.Resources.EnumerateArray())
                    {
                        if (resource.TryGetProperty("uri", out var uriProp))
                        {
                            var rawUri = uriProp.GetString() ?? string.Empty;
                            var exposedUri = $"mcp://{item.ServerId}/{Uri.EscapeDataString(rawUri)}";

                            _resourceRoutingTable[exposedUri] = item.ServerId;

                            var resourceDict = JsonSerializer.Deserialize<Dictionary<string, object>>(resource.GetRawText());
                            if (resourceDict != null)
                            {
                                resourceDict["uri"] = exposedUri;
                                if (resourceDict.TryGetValue("name", out var nameVal))
                                {
                                    resourceDict["name"] = $"[{item.ServerId}] {nameVal}";
                                }

                                serverResources.Add(resourceDict);
                                allResources.Add(resourceDict);
                            }
                        }
                    }
                }
                if (sessionManager != null)
                {
                    sessionManager.SetServerResourcesCache(item.ServerId, serverResources);
                }
            }

            // Load custom file-based resources from data/resources
            var resourcesDir = Path.Combine(AppContext.BaseDirectory, "data", "resources");
            if (!Directory.Exists(resourcesDir))
            {
                resourcesDir = Path.Combine(Directory.GetCurrentDirectory(), "data", "resources");
            }
            resourcesDir = Path.GetFullPath(resourcesDir);

            if (Directory.Exists(resourcesDir))
            {
                foreach (var file in Directory.GetFiles(resourcesDir))
                {
                    try
                    {
                        var filename = Path.GetFileName(file);
                        var ext = Path.GetExtension(file).ToLowerInvariant();
                        var mimeType = "text/plain";
                        if (ext == ".md")
                        {
                            mimeType = "text/markdown";
                        }
                        else if (ext == ".json")
                        {
                            mimeType = "application/json";
                        }
                        else if (ext == ".html")
                        {
                            mimeType = "text/html";
                        }

                        allResources.Add(new Dictionary<string, object> {
                            { "uri", "router://resources/" + filename },
                            { "name", "Local File: " + filename },
                            { "mimeType", mimeType },
                            { "description", "[custom] User-configured local resource file." }
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to load custom resource file {File}", file);
                    }
                }
            }

            // Append built-in router resources at the end to keep backend resources at index 0 for unit tests
            allResources.Add(new Dictionary<string, object> {
                { "uri", "router://status" },
                { "name", "Router Connection Status" },
                { "mimeType", "application/json" },
                { "description", "Real-time connection status of the MCP gateway and active sessions." }
            });
            allResources.Add(new Dictionary<string, object> {
                { "uri", "router://active-servers" },
                { "name", "Active Backend Servers" },
                { "mimeType", "application/json" },
                { "description", "Details about all registered backend servers and their connectivity status." }
            });
            allResources.Add(new Dictionary<string, object> {
                { "uri", "router://metrics" },
                { "name", "Gateway Operational Metrics" },
                { "mimeType", "application/json" },
                { "description", "Operational metrics, including tool counts, session counts, and system telemetry." }
            });

            return allResources;
        }

        public async Task<List<object>> ListResourceTemplatesAsync(string body, IEnumerable<KeyValuePair<string, BackendConnection>> backendConnections, ILogger logger, Func<Task> ensureBackendsInitializedAsync, SessionManager? sessionManager = null)
        {
            var allTemplates = new List<object>();

            // Add built-in templates
            allTemplates.Add(new Dictionary<string, object> {
                { "uriTemplate", "logs://{server_name}/today" },
                { "name", "Backend Server Log" },
                { "description", "Fetch today's real-time logs for a specific backend server." },
                { "parameters", new Dictionary<string, object> {
                    { "server_name", new Dictionary<string, object> {
                        { "description", "The unique identifier of the backend server (e.g., ha, unifi, docker)" }
                    } }
                } }
            });

            await ensureBackendsInitializedAsync();

            var tasks = new List<Task<(string ServerId, JsonElement Templates)>>();
            foreach (var entry in backendConnections)
            {
                var conn = entry.Value;
                var serverId = entry.Key;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var reqBody = "{\"jsonrpc\":\"2.0\",\"method\":\"resources/templates/list\",\"id\":\"refresh-temp-list\"}";
                        var resp = await conn.SendRequestAsync("resources/templates/list", reqBody);
                        if (resp.Result != null && resp.Result.Value.TryGetProperty("templates", out var templatesList))
                        {
                            return (serverId, templatesList);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error listing templates on server {ServerId}", serverId);
                    }
                    return (serverId, default(JsonElement));
                }));
            }

            var completed = await Task.WhenAll(tasks);
            foreach (var item in completed)
            {
                var serverTemplates = new List<object>();
                if (item.Templates.ValueKind == JsonValueKind.Array)
                {
                    foreach (var template in item.Templates.EnumerateArray())
                    {
                        if (template.TryGetProperty("uriTemplate", out var uriTemplateProp))
                        {
                            var rawTemplate = uriTemplateProp.GetString() ?? string.Empty;
                            var exposedTemplate = $"mcp://{item.ServerId}/{rawTemplate}";

                            var templateDict = JsonSerializer.Deserialize<Dictionary<string, object>>(template.GetRawText());
                            if (templateDict != null)
                            {
                                templateDict["uriTemplate"] = exposedTemplate;
                                if (templateDict.TryGetValue("name", out var nameVal))
                                {
                                    templateDict["name"] = $"[{item.ServerId}] {nameVal}";
                                }

                                serverTemplates.Add(templateDict);
                                allTemplates.Add(templateDict);
                            }
                        }
                    }
                }
                if (sessionManager != null)
                {
                    sessionManager.SetServerResourceTemplatesCache(item.ServerId, serverTemplates);
                }
            }
            return allTemplates;
        }

        public async Task<object?> ReadResourceAsync(string resourceUri, string body, ConcurrentDictionary<string, BackendConnection> backendConnections, Func<Task> ensureBackendsInitializedAsync, Func<string, string, string, string> rewriteRequestJson, SessionManager? sessionManager = null)
        {
            if (resourceUri.StartsWith("router://"))
            {
                return ResolveLocalResource(resourceUri, backendConnections, sessionManager);
            }
            if (resourceUri.StartsWith("logs://"))
            {
                return ResolveLocalLogResource(resourceUri);
            }

            await ensureBackendsInitializedAsync();

            if (_resourceRoutingTable.TryGetValue(resourceUri, out var serverId) && backendConnections.TryGetValue(serverId, out var conn))
            {
                var prefix = $"mcp://{serverId}/";
                var rawUri = resourceUri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? Uri.UnescapeDataString(resourceUri.Substring(prefix.Length))
                    : resourceUri;
                string routingBody = rewriteRequestJson(body, "uri", rawUri);
                var resp = await conn.SendRequestAsync("resources/read", routingBody);
                return resp.Result;
            }

            // Fallback for cold-start mcp://{serverId}/{rawUri}
            if (resourceUri.StartsWith("mcp://", StringComparison.OrdinalIgnoreCase))
            {
                var withoutScheme = resourceUri.Substring("mcp://".Length);
                var slashIdx = withoutScheme.IndexOf('/');
                if (slashIdx > 0)
                {
                    var targetServer = withoutScheme.Substring(0, slashIdx);
                    var rawUri = Uri.UnescapeDataString(withoutScheme.Substring(slashIdx + 1));
                    var matchedConn = backendConnections.FirstOrDefault(c => string.Equals(c.Key, targetServer, StringComparison.OrdinalIgnoreCase));
                    if (matchedConn.Value != null)
                    {
                        _resourceRoutingTable[resourceUri] = matchedConn.Key;
                        string routingBody = rewriteRequestJson(body, "uri", rawUri);
                        var resp = await matchedConn.Value.SendRequestAsync("resources/read", routingBody);
                        return resp.Result;
                    }
                }
            }

            throw new KeyNotFoundException($"Resource {resourceUri} not found in routing table.");
        }

        private object ResolveLocalResource(string uri, ConcurrentDictionary<string, BackendConnection> backendConnections, SessionManager? sessionManager)
        {
            if (uri.StartsWith("router://resources/"))
            {
                var filename = uri.Substring("router://resources/".Length);
                filename = Path.GetFileName(filename);

                var resourcesDir = Path.Combine(AppContext.BaseDirectory, "data", "resources");
                if (!Directory.Exists(resourcesDir))
                {
                    resourcesDir = Path.Combine(Directory.GetCurrentDirectory(), "data", "resources");
                }
                resourcesDir = Path.GetFullPath(resourcesDir);
                var filePath = Path.GetFullPath(Path.Combine(resourcesDir, filename));

                if (!filePath.StartsWith(resourcesDir, StringComparison.OrdinalIgnoreCase))
                {
                    throw new KeyNotFoundException($"Local resource file '{filename}' was not found in data/resources/.");
                }

                if (File.Exists(filePath))
                {
                    try
                    {
                        var ext = Path.GetExtension(filePath).ToLowerInvariant();
                        var mimeType = "text/plain";
                        if (ext == ".md")
                        {
                            mimeType = "text/markdown";
                        }
                        else if (ext == ".json")
                        {
                            mimeType = "application/json";
                        }
                        else if (ext == ".html")
                        {
                            mimeType = "text/html";
                        }

                        var text = File.ReadAllText(filePath);
                        return new
                        {
                            contents = new[] {
                                new {
                                    uri = uri,
                                    mimeType = mimeType,
                                    text = text
                                }
                            }
                        };
                    }
                    catch (Exception ex)
                    {
                        return new
                        {
                            contents = new[] {
                                new {
                                    uri = uri,
                                    mimeType = "text/plain",
                                    text = $"Error reading local resource file: {ex.Message}"
                                }
                            }
                        };
                    }
                }
                throw new KeyNotFoundException($"Local resource file '{filename}' was not found in data/resources/.");
            }

            string jsonText = "{}";
            if (uri == "router://status")
            {
                var statusObj = new
                {
                    status = "online",
                    activeSessions = sessionManager?.ActiveSessionsCount ?? 0,
                    backendCount = backendConnections.Count,
                    timestamp = DateTime.UtcNow
                };
                jsonText = JsonSerializer.Serialize(statusObj);
            }
            else if (uri == "router://active-servers")
            {
                var serversList = new List<object>();
                var statuses = sessionManager?.BackendStatuses;
                if (statuses != null)
                {
                    foreach (var entry in statuses)
                    {
                        serversList.Add(new
                        {
                            id = entry.Key,
                            status = entry.Value.Status,
                            attempts = entry.Value.Attempts,
                            error = entry.Value.Error
                        });
                    }
                }
                jsonText = JsonSerializer.Serialize(serversList);
            }
            else if (uri == "router://metrics")
            {
                var metricsObj = new
                {
                    totalRequests = sessionManager?.TotalRequests ?? 0,
                    activeConnections = sessionManager?.ActiveSessionsCount ?? 0,
                    memoryUsageBytes = GC.GetTotalMemory(false),
                    upTimeSeconds = (DateTime.UtcNow - (sessionManager?.StartTime ?? DateTime.UtcNow)).TotalSeconds,
                    totalInputTokens = sessionManager?.TotalInputTokens ?? 0,
                    totalOutputTokens = sessionManager?.TotalOutputTokens ?? 0,
                    totalDurationMs = sessionManager?.TotalDurationMs ?? 0,
                    averageLatencyMs = (sessionManager?.TotalRequests ?? 0) > 0
                        ? (sessionManager!.TotalDurationMs / (double)sessionManager.TotalRequests)
                        : 0
                };
                jsonText = JsonSerializer.Serialize(metricsObj);
            }

            return new
            {
                contents = new[] {
                    new {
                        uri = uri,
                        mimeType = "application/json",
                        text = jsonText
                    }
                }
            };
        }

        private object ResolveLocalLogResource(string uri)
        {
            string logText = "No logs found for this backend server.";
            if (_catalogManager.TryMatchLogsTemplate(uri, out var serverId))
            {
                var filteredLogs = LogBuffer.GetLogs()
                    .Where(l => l.Message.Contains($"backend {serverId}", StringComparison.OrdinalIgnoreCase) ||
                                l.Message.Contains($"backend server: {serverId}", StringComparison.OrdinalIgnoreCase) ||
                                l.Message.Contains($"connect to backend {serverId}", StringComparison.OrdinalIgnoreCase) ||
                                l.Message.Contains(serverId, StringComparison.OrdinalIgnoreCase))
                    .Take(100) // limit to 100 log lines to avoid payload bloat
                    .Select(l => $"[{l.Timestamp:yyyy-MM-dd HH:mm:ss}] [{l.Level}] {l.Message}")
                    .ToList();
                if (filteredLogs.Count > 0)
                {
                    logText = string.Join("\n", filteredLogs);
                }
            }

            return new
            {
                contents = new[] {
                    new {
                        uri = uri,
                        mimeType = "text/plain",
                        text = logText
                    }
                }
            };
        }
    }
}
