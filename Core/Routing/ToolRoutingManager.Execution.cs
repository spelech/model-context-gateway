using System.Collections.Concurrent;
using System.Text.Json;

namespace ModelContextGateway.Core.Routing
{
    public partial class ToolRoutingManager
    {
        public async Task<object> CallToolAsync(
            string toolName,
            string body,
            IDbConnectionFactory dbFactory,
            ConcurrentDictionary<string, BackendConnection> backendConnections,
            IEnumerable<McpServer> servers,
            ILogger logger,
            HttpClient httpClient,
            IEmbeddingService embeddingService,
            Func<Task> ensureBackendsInitializedAsync,
            Func<string, string, string, string> rewriteRequestJson,
            CancellationToken cancellationToken = default,
            SessionManager? sessionManager = null,
            string? clientSessionId = null,
            Func<List<object>, Task<List<object>>>? filterAuthorizedToolsAsync = null)
        {
            try
            {
                var task = CallToolInternalAsync(toolName, body, dbFactory, backendConnections, servers, logger, httpClient, embeddingService, ensureBackendsInitializedAsync, rewriteRequestJson, cancellationToken, sessionManager, clientSessionId, filterAuthorizedToolsAsync);
                return await task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Execution of tool '{ToolName}' was cancelled.", toolName);
                return new
                {
                    resultType = "complete",
                    isError = true,
                    content = new[] {
                        new {
                            type = "text",
                            text = "Error: request was cancelled by the client."
                        }
                    }
                };
            }
        }

        private async Task<object> CallToolInternalAsync(
            string toolName,
            string body,
            IDbConnectionFactory dbFactory,
            ConcurrentDictionary<string, BackendConnection> backendConnections,
            IEnumerable<McpServer> servers,
            ILogger logger,
            HttpClient httpClient,
            IEmbeddingService embeddingService,
            Func<Task> ensureBackendsInitializedAsync,
            Func<string, string, string, string> rewriteRequestJson,
            CancellationToken cancellationToken,
            SessionManager? sessionManager,
            string? clientSessionId,
            Func<List<object>, Task<List<object>>>? filterAuthorizedToolsAsync = null)
        {
            await ensureBackendsInitializedAsync();

            if (toolName == "search_tools")
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                string query = "";
                if (root.TryGetProperty("params", out var paramsProp) &&
                    paramsProp.TryGetProperty("arguments", out var argsProp) &&
                    argsProp.TryGetProperty("query", out var queryProp))
                {
                    query = queryProp.GetString() ?? "";
                }

                var tools = new List<object>();
                lock (_cacheLock)
                {
                    tools.AddRange(_cachedTools);
                }

                // Cold-start fallback 1: Seed from SessionManager's global server cache
                if (tools.Count == 0 && sessionManager != null)
                {
                    var globalCached = sessionManager.GetAllCachedTools();
                    if (globalCached.Count > 0)
                    {
                        lock (_cacheLock)
                        {
                            _cachedTools.Clear();
                            _cachedTools.AddRange(globalCached);
                            _isCachePopulated = true;

                            // CRITICAL FIX: Also populate the routing table from cached tool names
                            foreach (var item in globalCached)
                            {
                                string? fullName = null;
                                if (item is IDictionary<string, object> dict && dict.TryGetValue("name", out var nObj))
                                {
                                    fullName = nObj?.ToString();
                                }
                                else if (item is JsonElement je && je.TryGetProperty("name", out var pProp))
                                {
                                    fullName = pProp.GetString();
                                }
                                else if (item != null)
                                {
                                    try
                                    {
                                        using var itemDoc = JsonDocument.Parse(JsonSerializer.Serialize(item));
                                        if (itemDoc.RootElement.TryGetProperty("name", out var nameEl))
                                        {
                                            fullName = nameEl.GetString();
                                        }
                                    }
                                    catch { }
                                }

                                if (!string.IsNullOrEmpty(fullName))
                                {
                                    var splitIdx = fullName.IndexOf("__", StringComparison.Ordinal);
                                    if (splitIdx > 0)
                                    {
                                        var srvId = fullName.Substring(0, splitIdx);
                                        _toolRoutingTable[fullName] = srvId;
                                    }
                                }
                            }
                        }
                        tools.AddRange(globalCached);
                    }
                }

                // Cold-start fallback 2: On-demand populate if still empty and backends exist
                if (tools.Count == 0 && backendConnections.Any())
                {
                    try
                    {
                        await PopulateToolsCacheAsync("{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\",\"id\":\"ondemand-list\"}", backendConnections, logger, servers, sessionManager);
                        lock (_cacheLock)
                        {
                            tools.Clear();
                            tools.AddRange(_cachedTools);
                        }
                    }
                    catch (Exception exPop)
                    {
                        logger.LogWarning(exPop, "On-demand tools cache population failed during search_tools");
                    }
                }

                if (filterAuthorizedToolsAsync != null)
                {
                    tools = await filterAuthorizedToolsAsync(tools);
                }

                var results = await SemanticSearchService.SearchToolsSemanticAsync(query, tools, embeddingService, logger);
                return new
                {
                    resultType = "complete",
                    content = new[] {
                        new {
                            type = "text",
                            text = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true })
                        }
                    }
                };
            }
            else if (toolName == "execute_tool")
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                string targetName = "";
                JsonElement targetArgs = default;
                string? targetAuthToken = null;

                if (root.TryGetProperty("params", out var paramsProp) &&
                    paramsProp.TryGetProperty("arguments", out var argsProp))
                {
                    if (argsProp.TryGetProperty("name", out var nameProp))
                    {
                        targetName = nameProp.GetString() ?? "";
                    }
                    if (argsProp.TryGetProperty("arguments", out var targetArgsProp))
                    {
                        targetArgs = targetArgsProp.Clone();
                    }
                    if (argsProp.TryGetProperty("target_auth_token", out var targetAuthTokenProp))
                    {
                        targetAuthToken = targetAuthTokenProp.GetString();
                    }
                }

                if (string.IsNullOrEmpty(targetName))
                {
                    return new
                    {
                        resultType = "complete",
                        isError = true,
                        content = new[] {
                            new {
                                type = "text",
                                text = "Error: target tool name is required."
                            }
                        }
                    };
                }

                var activeServerIds = servers.Where(s => s.Enabled).Select(s => s.Id).ToList();
                if (!SecurityValidationHelper.ValidateToolOrPromptName(targetName, activeServerIds))
                {
                    return new
                    {
                        resultType = "complete",
                        isError = true,
                        content = new[] {
                            new {
                                type = "text",
                                text = $"Security Error: Invalid or spoofed namespaced identifier in execute_tool: '{targetName}'."
                            }
                        }
                    };
                }

                var targetPayload = new
                {
                    jsonrpc = "2.0",
                    method = "tools/call",
                    @params = new
                    {
                        name = targetName,
                        arguments = targetArgs.ValueKind == JsonValueKind.Undefined ? (object)new Dictionary<string, object>() : targetArgs
                    }
                };
                var targetBody = JsonSerializer.Serialize(targetPayload);

                try
                {
                    var result = await ExecuteTargetToolAsync(targetName, targetBody, targetAuthToken, dbFactory, backendConnections, servers, logger, httpClient, ensureBackendsInitializedAsync, rewriteRequestJson, cancellationToken, sessionManager, clientSessionId);
                    return result;
                }
                catch (Exception ex)
                {
                    return new
                    {
                        resultType = "complete",
                        isError = true,
                        content = new[] {
                            new {
                                type = "text",
                                text = $"Error executing target tool {targetName}: {ex.Message}"
                            }
                        }
                    };
                }
            }

            return await ExecuteTargetToolAsync(toolName, body, null, dbFactory, backendConnections, servers, logger, httpClient, ensureBackendsInitializedAsync, rewriteRequestJson, cancellationToken, sessionManager, clientSessionId);
        }

        private async Task<object> ExecuteTargetToolAsync(
            string toolName,
            string body,
            string? targetAuthToken,
            IDbConnectionFactory dbFactory,
            ConcurrentDictionary<string, BackendConnection> backendConnections,
            IEnumerable<McpServer> servers,
            ILogger logger,
            HttpClient httpClient,
            Func<Task> ensureBackendsInitializedAsync,
            Func<string, string, string, string> rewriteRequestJson,
            CancellationToken cancellationToken,
            SessionManager? sessionManager,
            string? clientSessionId)
        {
            if (!_toolRoutingTable.ContainsKey(toolName))
            {
                logger.LogInformation("Tool '{ToolName}' not found in routing table. Refreshing tools cache...", toolName);
                try
                {
                    await PopulateToolsCacheAsync("{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\",\"id\":\"refresh-list\"}", backendConnections, logger, servers);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to refresh tools cache during CallToolAsync for '{ToolName}'", toolName);
                }
            }

            if (!_toolRoutingTable.ContainsKey(toolName))
            {
                var sepIdx = toolName.IndexOf("__", StringComparison.Ordinal);
                if (sepIdx > 0)
                {
                    var candidateServerId = toolName.Substring(0, sepIdx);
                    if (servers.Any(s => s.Id == candidateServerId && s.Enabled))
                    {
                        _toolRoutingTable[toolName] = candidateServerId;
                        logger.LogInformation("Dynamically registered resilient prefix route for tool '{ToolName}' to server '{ServerId}'", toolName, candidateServerId);
                    }
                }
            }

            if (_toolRoutingTable.TryGetValue(toolName, out var serverId) && backendConnections.TryGetValue(serverId, out var conn))
            {
                logger.LogInformation("Routing tool call '{ToolName}' to server '{ServerId}'", toolName, serverId);

                string routingBody = body;
                var prefix = serverId + "__";
                if (toolName.StartsWith(prefix))
                {
                    var realToolName = toolName.Substring(prefix.Length);
                    routingBody = rewriteRequestJson(body, "name", realToolName);
                }

                try
                {
                    var resp = await conn.SendRequestAsync("tools/call", routingBody, targetAuthToken);
                    if (resp.Error != null)
                    {
                        var transformed = ToolErrorFormatter.TransformError(resp.Error, toolName, serverId);
                        return new
                        {
                            resultType = "complete",
                            isError = true,
                            content = new[] {
                                new {
                                    type = "text",
                                    text = transformed
                                }
                            }
                        };
                    }
                    if (resp.Result.HasValue)
                    {
                        return ProtocolHelper.EnsureResultType(resp.Result.Value);
                    }
                    return resp;
                }
                catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    var srv = servers.FirstOrDefault(s => s.Id == serverId);
                    var prompt = (srv != null && !string.IsNullOrEmpty(srv.DynamicAuthPrompt)) ? srv.DynamicAuthPrompt : "401 Unauthorized. Please provide a valid target_auth_token via execute_tool.";
                    return new
                    {
                        resultType = "complete",
                        isError = true,
                        content = new[] {
                            new {
                                type = "text",
                                text = prompt
                            }
                        }
                    };
                }
                catch (Exception ex)
                {
                    var transformed = ToolErrorFormatter.TransformException(ex, toolName, serverId);
                    return new
                    {
                        resultType = "complete",
                        isError = true,
                        content = new[] {
                            new {
                                type = "text",
                                text = transformed
                            }
                        }
                    };
                }
            }

            throw new KeyNotFoundException($"Tool {toolName} not found in routing table.");
        }
    }
}
