using System.Collections.Concurrent;
using System.Text.Json;
using Dapper;
using ModelContextGateway.Core.CodeMode;

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
            IEmbeddingService? embeddingService,
            Func<Task> ensureBackendsInitializedAsync,
            Func<string, string, string, string> rewriteRequestJson,
            CancellationToken cancellationToken = default,
            SessionManager? sessionManager = null,
            string? clientSessionId = null,
            Func<List<object>, Task<List<object>>>? filterAuthorizedToolsAsync = null,
            IEnumerable<string>? callerGroups = null,
            bool? isCallerAdmin = null,
            CodeModeSandboxCallbacks? sandboxCallbacks = null,
            int? requestTimeoutSecondsOverride = null,
            int? requestMaxOutputCharsOverride = null,
            string? ownerKey = null)
        {
            try
            {
                var task = CallToolInternalAsync(
                    toolName,
                    body,
                    dbFactory,
                    backendConnections,
                    servers,
                    logger,
                    httpClient,
                    embeddingService,
                    ensureBackendsInitializedAsync,
                    rewriteRequestJson,
                    cancellationToken,
                    sessionManager,
                    clientSessionId,
                    filterAuthorizedToolsAsync,
                    callerGroups,
                    isCallerAdmin,
                    sandboxCallbacks,
                    requestTimeoutSecondsOverride,
                    requestMaxOutputCharsOverride,
                    ownerKey);
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
            IEmbeddingService? embeddingService,
            Func<Task> ensureBackendsInitializedAsync,
            Func<string, string, string, string> rewriteRequestJson,
            CancellationToken cancellationToken,
            SessionManager? sessionManager,
            string? clientSessionId,
            Func<List<object>, Task<List<object>>>? filterAuthorizedToolsAsync = null,
            IEnumerable<string>? callerGroups = null,
            bool? isCallerAdmin = null,
            CodeModeSandboxCallbacks? sandboxCallbacks = null,
            int? requestTimeoutSecondsOverride = null,
            int? requestMaxOutputCharsOverride = null,
            string? ownerKey = null)
        {
            await ensureBackendsInitializedAsync();

            var resolvedOwnerKey = !string.IsNullOrWhiteSpace(ownerKey)
                ? ownerKey!
                : (!string.IsNullOrWhiteSpace(clientSessionId) ? $"session:{clientSessionId}" : "anonymous");

            if (toolName == "search_tools")
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                string query = "";
                string? serverFilter = null;
                string mode = "hybrid";
                double denseWeight = 0.5;
                int? callerLimit = null;
                double? callerMinScore = null;
                double? callerScoreDropoffRatio = null;
                string format = "json";
                bool includeTypeScript = false;
                int? callerOffset = null;
                int? callerMaxChars = null;

                if (root.TryGetProperty("params", out var paramsProp) &&
                    paramsProp.TryGetProperty("arguments", out var argsProp))
                {
                    if (argsProp.TryGetProperty("query", out var queryProp))
                    {
                        query = queryProp.GetString() ?? "";
                    }
                    if (argsProp.TryGetProperty("server", out var serverProp) && serverProp.ValueKind == JsonValueKind.String)
                    {
                        serverFilter = serverProp.GetString();
                    }
                    if (argsProp.TryGetProperty("mode", out var modeProp) && modeProp.ValueKind == JsonValueKind.String)
                    {
                        mode = modeProp.GetString() ?? "hybrid";
                    }
                    if (argsProp.TryGetProperty("dense_weight", out var dwProp) || argsProp.TryGetProperty("denseWeight", out dwProp))
                    {
                        if (dwProp.ValueKind == JsonValueKind.Number && dwProp.TryGetDouble(out var dwVal))
                        {
                            denseWeight = dwVal;
                        }
                    }
                    if (argsProp.TryGetProperty("limit", out var limitProp))
                    {
                        if (limitProp.ValueKind == JsonValueKind.Number && limitProp.TryGetInt32(out var lVal) && lVal > 0)
                        {
                            callerLimit = lVal;
                        }
                    }
                    if (argsProp.TryGetProperty("min_score", out var minScoreProp) || argsProp.TryGetProperty("minScore", out minScoreProp))
                    {
                        if (minScoreProp.ValueKind == JsonValueKind.Number && minScoreProp.TryGetDouble(out var msVal))
                        {
                            callerMinScore = msVal;
                        }
                    }
                    if (argsProp.TryGetProperty("score_dropoff_ratio", out var sdrProp) || argsProp.TryGetProperty("scoreDropoffRatio", out sdrProp))
                    {
                        if (sdrProp.ValueKind == JsonValueKind.Number && sdrProp.TryGetDouble(out var sdrVal))
                        {
                            callerScoreDropoffRatio = sdrVal;
                        }
                    }
                    if (argsProp.TryGetProperty("format", out var formatProp) && formatProp.ValueKind == JsonValueKind.String)
                    {
                        format = (formatProp.GetString() ?? "json").Trim().ToLowerInvariant();
                    }
                    if (argsProp.TryGetProperty("include_typescript", out var incTsProp) || argsProp.TryGetProperty("includeTypescript", out incTsProp))
                    {
                        if (incTsProp.ValueKind == JsonValueKind.True || incTsProp.ValueKind == JsonValueKind.False)
                        {
                            includeTypeScript = incTsProp.GetBoolean();
                        }
                    }
                    if (TryGetOptionalInt(argsProp, out var offVal, "offset"))
                    {
                        callerOffset = offVal;
                    }
                    if (TryGetOptionalInt(argsProp, out var mcVal, "max_chars", "maxChars"))
                    {
                        callerMaxChars = mcVal;
                    }
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

                            foreach (var item in globalCached)
                            {
                                string? fullName = ExtractToolNameString(item);
                                if (!string.IsNullOrEmpty(fullName))
                                {
                                    string? srvId = null;
                                    string? rawToolName = null;

                                    if (fullName.Contains('/'))
                                    {
                                        var idx = fullName.IndexOf('/');
                                        srvId = fullName.Substring(0, idx);
                                        rawToolName = fullName.Substring(idx + 1);
                                    }
                                    else if (fullName.Contains(':'))
                                    {
                                        var idx = fullName.IndexOf(':');
                                        srvId = fullName.Substring(0, idx);
                                        rawToolName = fullName.Substring(idx + 1);
                                    }
                                    else if (fullName.Contains("__"))
                                    {
                                        var idx = fullName.IndexOf("__", StringComparison.Ordinal);
                                        srvId = fullName.Substring(0, idx);
                                        rawToolName = fullName.Substring(idx + 2);
                                    }

                                    if (!string.IsNullOrEmpty(srvId) && !string.IsNullOrEmpty(rawToolName))
                                    {
                                        var matchedServer = servers?.FirstOrDefault(s =>
                                            string.Equals(s.Id, srvId, StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(s.Alias, srvId, StringComparison.OrdinalIgnoreCase));
                                        var actualServerId = matchedServer?.Id ?? srvId;
                                        var ns = !string.IsNullOrWhiteSpace(matchedServer?.Alias) ? matchedServer.Alias : actualServerId;

                                        _toolRoutingTable[fullName] = actualServerId;
                                        _toolRoutingTable[$"{actualServerId}/{rawToolName}"] = actualServerId;
                                        _toolRoutingTable[$"{actualServerId}__{rawToolName}"] = actualServerId;
                                        _toolRoutingTable[$"{actualServerId}:{rawToolName}"] = actualServerId;

                                        if (!string.IsNullOrEmpty(ns) && !string.Equals(ns, actualServerId, StringComparison.OrdinalIgnoreCase))
                                        {
                                            _toolRoutingTable[$"{ns}/{rawToolName}"] = actualServerId;
                                            _toolRoutingTable[$"{ns}__{rawToolName}"] = actualServerId;
                                            _toolRoutingTable[$"{ns}:{rawToolName}"] = actualServerId;
                                        }
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
                        await PopulateToolsCacheAsync("{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\",\"id\":\"ondemand-list\"}", backendConnections, logger, servers ?? Enumerable.Empty<McpServer>(), sessionManager);
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

                if (!string.IsNullOrWhiteSpace(serverFilter))
                {
                    tools = FilterToolsByServer(tools, serverFilter!, servers);
                }

                var routerSettings = ResolveRouterSettings(embeddingService, dbFactory, logger);
                int defaultLimit = routerSettings?.ToolSearchDefaultLimit ?? 5;
                double defaultMinScore = routerSettings?.ToolSearchMinScore ?? 0.35;
                double defaultDropoff = routerSettings?.ToolSearchScoreDropoffRatio ?? 0.60;

                int effectiveLimit = callerLimit ?? defaultLimit;
                double effectiveMinScore = callerMinScore ?? defaultMinScore;
                double effectiveDropoff = callerScoreDropoffRatio ?? defaultDropoff;

                var effectiveProvider = _embeddingProvider ?? (embeddingService != null ? new ModelContextGateway.Core.VectorSearch.EmbeddingServiceAdapter(embeddingService) : null);
                var effectiveStore = _vectorStore;
                var detailedResults = await SearchToolsDetailedAsync(
                    query,
                    tools,
                    effectiveProvider,
                    effectiveStore,
                    logger,
                    effectiveLimit,
                    searchMode: mode,
                    denseWeight: denseWeight,
                    minScore: effectiveMinScore,
                    scoreDropoffRatio: effectiveDropoff,
                    cancellationToken: cancellationToken);
                var toolSchemas = detailedResults.Select(r => r.Tool).ToList();

                string outputText;
                object structuredPayload;

                if (format == "typescript" || format == "ts")
                {
                    // Never include full inputSchema blobs inside structuredContent for lean discovery
                    outputText = CodeModeTypeGenerator.GenerateCompactToolSignatures(toolSchemas);
                    var leanTools = detailedResults.Select(r => new
                    {
                        name = r.ToolName,
                        serverId = r.ServerId,
                        signature = CodeModeTypeGenerator.GenerateSingleToolSignature(r.Tool)
                    }).ToList();

                    structuredPayload = new
                    {
                        tools = leanTools,
                        format = "typescript",
                        count = leanTools.Count
                    };
                }
                else if (format == "compact")
                {
                    var compactTools = BuildCompactSearchItems(detailedResults);
                    outputText = JsonSerializer.Serialize(compactTools, new JsonSerializerOptions { WriteIndented = true });
                    structuredPayload = new
                    {
                        tools = compactTools,
                        format = "compact",
                        count = compactTools.Count
                    };
                }
                else if (format == "names")
                {
                    var names = detailedResults.Select(r => r.ToolName).ToList();
                    outputText = string.Join("\n", names);
                    structuredPayload = new
                    {
                        tools = names,
                        format = "names",
                        count = names.Count
                    };
                }
                else
                {
                    if (includeTypeScript)
                    {
                        var enrichedSchemas = new List<object>(toolSchemas.Count);
                        foreach (var toolItem in toolSchemas)
                        {
                            try
                            {
                                var elem = toolItem is JsonElement je ? je : JsonSerializer.SerializeToElement(toolItem);
                                if (elem.ValueKind == JsonValueKind.Object)
                                {
                                    var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(elem.GetRawText()) ?? new();
                                    dict["typescriptSignature"] = CodeModeTypeGenerator.GenerateSingleToolSignature(elem);
                                    enrichedSchemas.Add(dict);
                                }
                                else
                                {
                                    enrichedSchemas.Add(toolItem);
                                }
                            }
                            catch
                            {
                                enrichedSchemas.Add(toolItem);
                            }
                        }
                        toolSchemas = enrichedSchemas;
                    }

                    outputText = JsonSerializer.Serialize(toolSchemas, new JsonSerializerOptions { WriteIndented = true });
                    structuredPayload = new
                    {
                        tools = toolSchemas,
                        format = "json",
                        count = toolSchemas.Count
                    };
                }

                int? effectiveMaxChars = (callerMaxChars is > 0 ? callerMaxChars : null)
                    ?? (requestMaxOutputCharsOverride is > 0 ? requestMaxOutputCharsOverride : null)
                    ?? (routerSettings?.MaxToolOutputChars is > 0 ? routerSettings.MaxToolOutputChars : null);

                if ((effectiveMaxChars.HasValue && outputText.Length > effectiveMaxChars.Value) || (callerOffset is > 0))
                {
                    var shaped = OutputBuffer.ShapeText(resolvedOwnerKey, "search_tools", outputText, effectiveMaxChars, callerOffset, filter: null);
                    outputText = shaped.Text;
                    if (shaped.WasTruncated || (callerOffset is > 0))
                    {
                        structuredPayload = new
                        {
                            tools = Array.Empty<object>(),
                            format = format,
                            count = toolSchemas.Count,
                            truncated = shaped.WasTruncated,
                            output_id = shaped.OutputId,
                            total_chars = shaped.TotalChars,
                            returned_chars = shaped.ReturnedChars,
                            offset = shaped.Offset,
                            has_more = shaped.HasMore,
                            next_offset = shaped.NextOffset
                        };
                    }
                }

                return new
                {
                    resultType = "complete",
                    structuredContent = structuredPayload,
                    content = new[] {
                        new {
                            type = "text",
                            text = outputText
                        }
                    }
                };
            }
            else if (toolName == "read_output" || toolName == "mcg/read_output")
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                string outputId = "";
                int offset = 0;
                int maxChars = 4000;

                if (root.TryGetProperty("params", out var paramsProp) &&
                    paramsProp.TryGetProperty("arguments", out var argsProp) &&
                    argsProp.ValueKind == JsonValueKind.Object)
                {
                    if ((argsProp.TryGetProperty("output_id", out var oidProp) || argsProp.TryGetProperty("outputId", out oidProp)) &&
                        oidProp.ValueKind == JsonValueKind.String)
                    {
                        outputId = oidProp.GetString() ?? "";
                    }
                    if (TryGetOptionalInt(argsProp, out var offVal, "offset") && offVal >= 0)
                    {
                        offset = offVal;
                    }
                    if (TryGetOptionalInt(argsProp, out var mcVal, "max_chars", "maxChars") && mcVal > 0)
                    {
                        maxChars = mcVal;
                    }
                }

                if (string.IsNullOrWhiteSpace(outputId))
                {
                    return new
                    {
                        resultType = "complete",
                        isError = true,
                        content = new[] {
                            new {
                                type = "text",
                                text = "Error: output_id is required."
                            }
                        }
                    };
                }

                if (!OutputBuffer.TryReadChunk(resolvedOwnerKey, outputId, offset, maxChars, out var chunk) || chunk == null)
                {
                    return new
                    {
                        resultType = "complete",
                        isError = true,
                        content = new[] {
                            new {
                                type = "text",
                                text = "Error: Output buffer entry not found or expired for this session."
                            }
                        }
                    };
                }

                var chunkMeta = new
                {
                    output_id = chunk.OutputId,
                    tool_name = chunk.ToolName,
                    total_chars = chunk.TotalChars,
                    returned_chars = chunk.ReturnedChars,
                    offset = chunk.Offset,
                    has_more = chunk.HasMore,
                    next_offset = chunk.NextOffset
                };

                var contentBlocks = new List<object>
                {
                    new { type = "text", text = chunk.Text }
                };
                if (chunk.HasMore)
                {
                    contentBlocks.Add(new
                    {
                        type = "text",
                        text = JsonSerializer.Serialize(chunkMeta)
                    });
                }

                return new
                {
                    resultType = "complete",
                    structuredContent = chunkMeta,
                    content = contentBlocks
                };
            }
            else if (toolName == "execute_tool")
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                string targetName = "";
                JsonElement targetArgs = default;
                string? targetAuthToken = null;
                int? callerTimeoutSeconds = null;
                int? callerMaxChars = null;
                int? callerOffset = null;
                string? callerFilter = null;
                string? callerConcurrencyKey = null;

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
                    if ((argsProp.TryGetProperty("target_auth_token", out var targetAuthTokenProp) ||
                         argsProp.TryGetProperty("targetAuthToken", out targetAuthTokenProp)) &&
                        targetAuthTokenProp.ValueKind == JsonValueKind.String)
                    {
                        targetAuthToken = targetAuthTokenProp.GetString();
                    }
                    if (TryGetOptionalInt(argsProp, out var tsVal, "timeout_seconds", "timeoutSeconds") && tsVal > 0)
                    {
                        callerTimeoutSeconds = tsVal;
                    }
                    if (TryGetOptionalInt(argsProp, out var mcVal, "max_chars", "maxChars") && mcVal > 0)
                    {
                        callerMaxChars = mcVal;
                    }
                    if (TryGetOptionalInt(argsProp, out var offVal, "offset") && offVal >= 0)
                    {
                        callerOffset = offVal;
                    }
                    if (argsProp.TryGetProperty("filter", out var filterProp) && filterProp.ValueKind == JsonValueKind.String)
                    {
                        callerFilter = filterProp.GetString();
                    }
                    if ((argsProp.TryGetProperty("concurrency_key", out var ckProp) ||
                         argsProp.TryGetProperty("concurrencyKey", out ckProp)) &&
                        ckProp.ValueKind == JsonValueKind.String)
                    {
                        callerConcurrencyKey = ckProp.GetString();
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

                var (normalizedTargetName, ambiguityError) = NormalizeTargetToolName(targetName, servers, logger);
                if (!string.IsNullOrEmpty(ambiguityError))
                {
                    return new
                    {
                        resultType = "complete",
                        isError = true,
                        content = new[] {
                            new {
                                type = "text",
                                text = ambiguityError
                            }
                        }
                    };
                }
                targetName = normalizedTargetName;

                var activeServerIds = servers.Where(s => s.Enabled)
                    .SelectMany(s => string.IsNullOrWhiteSpace(s.Alias) ? new[] { s.Id } : new[] { s.Id, s.Alias })
                    .Distinct()
                    .ToList();
                if (!SecurityValidationHelper.ValidateToolOrPromptName(targetName, activeServerIds))
                {
                    return new
                    {
                        resultType = "complete",
                        isError = true,
                        content = new[] {
                            new {
                                type = "text",
                                text = $"Security Error: Invalid or unknown namespaced identifier in execute_tool: '{targetName}'. Available servers: {string.Join(", ", activeServerIds)}."
                            }
                        }
                    };
                }

                var effectiveTargetArgs = targetArgs.ValueKind == JsonValueKind.Undefined
                    ? (object)new Dictionary<string, object>()
                    : targetArgs;

                var targetPayload = new
                {
                    jsonrpc = "2.0",
                    id = Guid.NewGuid().ToString("N"),
                    method = "tools/call",
                    @params = new
                    {
                        name = targetName,
                        arguments = effectiveTargetArgs
                    }
                };
                var targetBody = JsonSerializer.Serialize(targetPayload);

                if (targetName == "search_tools" || targetName == "read_output" || targetName == "mcg/read_output")
                {
                    return await CallToolInternalAsync(
                        targetName,
                        targetBody,
                        dbFactory,
                        backendConnections,
                        servers,
                        logger,
                        httpClient,
                        embeddingService,
                        ensureBackendsInitializedAsync,
                        rewriteRequestJson,
                        cancellationToken,
                        sessionManager,
                        clientSessionId,
                        filterAuthorizedToolsAsync,
                        callerGroups,
                        isCallerAdmin,
                        sandboxCallbacks,
                        requestTimeoutSecondsOverride,
                        requestMaxOutputCharsOverride,
                        resolvedOwnerKey);
                }

                var routerSettings = ResolveRouterSettings(embeddingService, dbFactory, logger);

                try
                {
                    var result = await ExecuteTargetToolAsync(
                        targetName,
                        targetBody,
                        targetAuthToken,
                        dbFactory,
                        backendConnections,
                        servers,
                        logger,
                        httpClient,
                        ensureBackendsInitializedAsync,
                        rewriteRequestJson,
                        cancellationToken,
                        sessionManager,
                        clientSessionId,
                        routerSettings,
                        resolvedOwnerKey,
                        callerTimeoutSeconds ?? requestTimeoutSecondsOverride,
                        callerMaxChars ?? requestMaxOutputCharsOverride,
                        callerOffset,
                        callerFilter,
                        callerConcurrencyKey,
                        targetArgs);
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
            else if (toolName == "execute_script")
            {
                var settings = ResolveRouterSettings(embeddingService, dbFactory, logger);

                if (!settings.EnableCodeMode)
                {
                    return new
                    {
                        resultType = "complete",
                        isError = true,
                        content = new[] {
                            new {
                                type = "text",
                                text = "Code mode is currently disabled in gateway settings."
                            }
                        }
                    };
                }

                IEnumerable<string>? resolvedGroups = callerGroups;
                bool resolvedIsAdmin = isCallerAdmin ?? false;

                if (callerGroups == null && !isCallerAdmin.HasValue && sessionManager != null && !string.IsNullOrEmpty(clientSessionId))
                {
                    var session = sessionManager.GetSession(clientSessionId);
                    if (session != null)
                    {
                        var identity = await session.ResolveUserIdentityAsync();
                        resolvedGroups = identity.GroupNames;
                        var contextToUse = session.GetClientResponse()?.HttpContext;
                        var config = contextToUse?.RequestServices?.GetService<IConfiguration>();
                        resolvedIsAdmin = SecurityValidationHelper.IsAdmin(identity, config, contextToUse);
                    }
                }

                if (!IsCodeModeAuthorized(settings, resolvedGroups, resolvedIsAdmin))
                {
                    return new
                    {
                        resultType = "complete",
                        isError = true,
                        content = new[] {
                            new {
                                type = "text",
                                text = "Security Error: User is not authorized to execute code mode scripts."
                            }
                        }
                    };
                }

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                string? script = null;
                JsonElement? scriptArgs = null;
                int? callerScriptTimeoutSeconds = null;

                if (root.TryGetProperty("params", out var paramsProp) &&
                    paramsProp.TryGetProperty("arguments", out var argsProp) &&
                    argsProp.ValueKind == JsonValueKind.Object)
                {
                    if ((argsProp.TryGetProperty("script", out var scriptProp) || argsProp.TryGetProperty("code", out scriptProp)) && scriptProp.ValueKind == JsonValueKind.String)
                    {
                        script = scriptProp.GetString();
                    }
                    if (argsProp.TryGetProperty("arguments", out var argumentsProp) && argumentsProp.ValueKind == JsonValueKind.Object)
                    {
                        scriptArgs = argumentsProp.Clone();
                    }
                    if (TryGetOptionalInt(argsProp, out var tsVal, "timeout_seconds", "timeoutSeconds") && tsVal > 0)
                    {
                        callerScriptTimeoutSeconds = tsVal;
                    }
                }

                if (string.IsNullOrWhiteSpace(script))
                {
                    return new
                    {
                        resultType = "complete",
                        isError = true,
                        content = new[] {
                            new {
                                type = "text",
                                text = "Error: script parameter is required."
                            }
                        }
                    };
                }

                int effectiveScriptTimeout = callerScriptTimeoutSeconds
                    ?? (requestTimeoutSecondsOverride is > 0 ? requestTimeoutSecondsOverride.Value : settings.CodeModeExecutionTimeoutSeconds);

                var effectiveSettings = effectiveScriptTimeout == settings.CodeModeExecutionTimeoutSeconds
                    ? settings
                    : CloneSettingsWithTimeout(settings, effectiveScriptTimeout);

                async Task<object> DispatchScriptToolAsync(string targetName, JsonElement targetArgs, CodeModeCallOptions? options, CancellationToken scriptCt)
                {
                    var (normalizedTargetName, ambiguityError) = NormalizeTargetToolName(targetName, servers, logger);
                    if (!string.IsNullOrEmpty(ambiguityError))
                    {
                        return new
                        {
                            resultType = "complete",
                            isError = true,
                            content = new[] {
                                new {
                                    type = "text",
                                    text = ambiguityError
                                }
                            }
                        };
                    }
                    targetName = normalizedTargetName;

                    if (sessionManager != null && !string.IsNullOrEmpty(clientSessionId))
                    {
                        var session = sessionManager.GetSession(clientSessionId);
                        if (session != null)
                        {
                            var isTargetAuth = await session.IsUserAuthorizedAsync("tools/call", targetName, session.GetClientResponse()?.HttpContext);
                            if (!isTargetAuth)
                            {
                                var identity = await session.ResolveUserIdentityAsync();
                                return new
                                {
                                    resultType = "complete",
                                    isError = true,
                                    content = new[] {
                                        new {
                                            type = "text",
                                            text = $"Security Error: User '{identity.Username}' does not have permission to execute tool '{targetName}'."
                                        }
                                    }
                                };
                            }
                        }
                    }

                    var activeServerIds = servers.Where(s => s.Enabled)
                        .SelectMany(s => string.IsNullOrWhiteSpace(s.Alias) ? new[] { s.Id } : new[] { s.Id, s.Alias })
                        .Distinct()
                        .ToList();
                    if (!SecurityValidationHelper.ValidateToolOrPromptName(targetName, activeServerIds))
                    {
                        return new
                        {
                            resultType = "complete",
                            isError = true,
                            content = new[] {
                                new {
                                    type = "text",
                                    text = $"Security Error: Invalid or unknown namespaced identifier in execute_script: '{targetName}'. Available servers: {string.Join(", ", activeServerIds)}."
                                }
                            }
                        };
                    }

                    var targetPayload = new
                    {
                        jsonrpc = "2.0",
                        id = Guid.NewGuid().ToString("N"),
                        method = "tools/call",
                        @params = new
                        {
                            name = targetName,
                            arguments = targetArgs.ValueKind == JsonValueKind.Undefined ? (object)new Dictionary<string, object>() : targetArgs
                        }
                    };
                    var targetBody = JsonSerializer.Serialize(targetPayload);

                    if (targetName == "search_tools" || targetName == "read_output" || targetName == "mcg/read_output")
                    {
                        return await CallToolAsync(
                            targetName,
                            targetBody,
                            dbFactory!,
                            backendConnections,
                            servers,
                            logger,
                            httpClient,
                            embeddingService,
                            ensureBackendsInitializedAsync,
                            rewriteRequestJson,
                            scriptCt,
                            sessionManager,
                            clientSessionId,
                            filterAuthorizedToolsAsync,
                            callerGroups,
                            isCallerAdmin,
                            sandboxCallbacks,
                            options?.TimeoutSeconds ?? requestTimeoutSecondsOverride,
                            options?.MaxChars ?? requestMaxOutputCharsOverride,
                            resolvedOwnerKey);
                    }

                    return await ExecuteTargetToolAsync(
                        targetName,
                        targetBody,
                        options?.AuthToken,
                        dbFactory!,
                        backendConnections,
                        servers,
                        logger,
                        httpClient,
                        ensureBackendsInitializedAsync,
                        rewriteRequestJson,
                        scriptCt,
                        sessionManager,
                        clientSessionId,
                        effectiveSettings,
                        resolvedOwnerKey,
                        options?.TimeoutSeconds ?? requestTimeoutSecondsOverride,
                        options?.MaxChars ?? requestMaxOutputCharsOverride,
                        options?.Offset,
                        options?.Filter,
                        options?.ConcurrencyKey,
                        targetArgs);
                }

                Func<string, JsonElement, string?, Task<object>> toolDispatcher = (targetName, targetArgs, targetAuthToken) =>
                    DispatchScriptToolAsync(targetName, targetArgs, new CodeModeCallOptions(AuthToken: targetAuthToken), cancellationToken);

                var effectiveCallbacks = new CodeModeSandboxCallbacks
                {
                    ListServersAsync = sandboxCallbacks?.ListServersAsync ?? (async () =>
                    {
                        var candidateServers = (servers ?? Enumerable.Empty<McpServer>()).Where(s => s.Enabled).ToList();
                        if (filterAuthorizedToolsAsync != null)
                        {
                            var rawTools = candidateServers
                                .Select(s => (object)new Dictionary<string, object> { ["name"] = $"server:{s.Id}" })
                                .ToList();
                            var allowed = await filterAuthorizedToolsAsync(rawTools);
                            var allowedIds = allowed
                                .OfType<IDictionary<string, object>>()
                                .Select(d => d.TryGetValue("name", out var n) ? n?.ToString()?.Substring("server:".Length) : null)
                                .Where(id => !string.IsNullOrEmpty(id))
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);
                            candidateServers = candidateServers.Where(s => allowedIds.Contains(s.Id)).ToList();
                        }
                        var serverList = candidateServers
                            .Select(s => (object)new
                            {
                                id = s.Id,
                                alias = s.Alias,
                                name = s.DisplayName,
                                enabled = s.Enabled,
                                instructions = sessionManager?.GetServerInstructionsCache(s.Id)
                            })
                            .ToList();
                        return serverList;
                    }),
                    ListResourcesAsync = sandboxCallbacks?.ListResourcesAsync,
                    SearchResourcesAsync = sandboxCallbacks?.SearchResourcesAsync,
                    ReadResourceAsync = sandboxCallbacks?.ReadResourceAsync,
                    ListPromptsAsync = sandboxCallbacks?.ListPromptsAsync,
                    GetPromptAsync = sandboxCallbacks?.GetPromptAsync,
                    CallToolWithOptionsAsync = sandboxCallbacks?.CallToolWithOptionsAsync ?? DispatchScriptToolAsync,
                    ReadOutputAsync = sandboxCallbacks?.ReadOutputAsync ?? (async (outputId, offsetArg, maxCharsArg) =>
                    {
                        if (!OutputBuffer.TryReadChunk(resolvedOwnerKey, outputId, offsetArg ?? 0, maxCharsArg ?? 4000, out var chunk) || chunk == null)
                        {
                            return new
                            {
                                error = "Output buffer entry not found or expired for this session."
                            };
                        }

                        return new
                        {
                            output_id = chunk.OutputId,
                            tool_name = chunk.ToolName,
                            text = chunk.Text,
                            total_chars = chunk.TotalChars,
                            returned_chars = chunk.ReturnedChars,
                            offset = chunk.Offset,
                            has_more = chunk.HasMore,
                            next_offset = chunk.NextOffset
                        };
                    })
                };

                var engine = _codeModeEngine ?? new CodeModeExecutionEngine();
                var execResult = await engine.ExecuteAsync(script, scriptArgs, effectiveSettings, toolDispatcher, cancellationToken, effectiveCallbacks);

                object? resultPayload = null;
                if (execResult.IsError)
                {
                    resultPayload = execResult.ErrorMessage;
                }
                else if (!string.IsNullOrEmpty(execResult.ResultJson))
                {
                    try
                    {
                        resultPayload = JsonSerializer.Deserialize<JsonElement>(execResult.ResultJson);
                    }
                    catch
                    {
                        resultPayload = execResult.ResultJson;
                    }
                }

                var envelope = new
                {
                    result = resultPayload,
                    logs = execResult.Logs,
                    executionTimeMs = execResult.ExecutionTimeMs,
                    toolCallsCount = execResult.ToolCallsCount
                };

                var serializedEnvelope = JsonSerializer.Serialize(envelope);
                object finalResponse = new
                {
                    resultType = "complete",
                    isError = execResult.IsError,
                    structuredContent = envelope,
                    content = new[] {
                        new {
                            type = "text",
                            text = serializedEnvelope
                        }
                    }
                };

                int? scriptOutputLimit = (requestMaxOutputCharsOverride is > 0 ? requestMaxOutputCharsOverride : null)
                    ?? (effectiveSettings.MaxToolOutputChars > 0 ? effectiveSettings.MaxToolOutputChars : null);

                if (scriptOutputLimit.HasValue)
                {
                    return OutputBuffer.ShapeToolResult(finalResponse, resolvedOwnerKey, "execute_script", scriptOutputLimit, offset: null, filter: null);
                }

                return finalResponse;
            }

            // Direct downstream tool invocation (non-meta mode or direct call)
            var directSettings = ResolveRouterSettings(embeddingService, dbFactory, logger);
            JsonElement directArgs = default;
            try
            {
                using var directDoc = JsonDocument.Parse(body);
                if (directDoc.RootElement.TryGetProperty("params", out var p) &&
                    p.TryGetProperty("arguments", out var a))
                {
                    directArgs = a.Clone();
                }
            }
            catch { }

            return await ExecuteTargetToolAsync(
                toolName,
                body,
                null,
                dbFactory,
                backendConnections,
                servers,
                logger,
                httpClient,
                ensureBackendsInitializedAsync,
                rewriteRequestJson,
                cancellationToken,
                sessionManager,
                clientSessionId,
                directSettings,
                resolvedOwnerKey,
                requestTimeoutSecondsOverride,
                requestMaxOutputCharsOverride,
                callerOffset: null,
                callerFilter: null,
                callerConcurrencyKey: null,
                parsedTargetArgs: directArgs);
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
            string? clientSessionId,
            RouterSettings? routerSettings = null,
            string? ownerKey = null,
            int? timeoutSecondsOverride = null,
            int? maxCharsOverride = null,
            int? callerOffset = null,
            string? callerFilter = null,
            string? callerConcurrencyKey = null,
            JsonElement parsedTargetArgs = default)
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
                string? candidatePrefix = null;
                if (toolName.Contains('/'))
                {
                    candidatePrefix = toolName.Substring(0, toolName.IndexOf('/'));
                }
                else if (toolName.Contains(':'))
                {
                    candidatePrefix = toolName.Substring(0, toolName.IndexOf(':'));
                }
                else if (toolName.Contains("__"))
                {
                    candidatePrefix = toolName.Substring(0, toolName.IndexOf("__", StringComparison.Ordinal));
                }

                if (!string.IsNullOrEmpty(candidatePrefix))
                {
                    var matchedServer = servers.FirstOrDefault(s => (string.Equals(s.Id, candidatePrefix, StringComparison.OrdinalIgnoreCase) || string.Equals(s.Alias, candidatePrefix, StringComparison.OrdinalIgnoreCase)) && s.Enabled);
                    if (matchedServer != null)
                    {
                        _toolRoutingTable[toolName] = matchedServer.Id;
                        logger.LogInformation("Dynamically registered resilient prefix route for tool '{ToolName}' to server '{ServerId}'", toolName, matchedServer.Id);
                    }
                }
            }

            if (_toolRoutingTable.TryGetValue(toolName, out var serverId) && backendConnections.TryGetValue(serverId, out var conn))
            {
                logger.LogInformation("Routing tool call '{ToolName}' to server '{ServerId}'", toolName, serverId);

                string routingBody = body;
                var rawToolName = toolName;
                var targetServer = servers.FirstOrDefault(s => string.Equals(s.Id, serverId, StringComparison.OrdinalIgnoreCase));
                if (toolName.StartsWith(serverId + "__", StringComparison.OrdinalIgnoreCase))
                {
                    rawToolName = toolName.Substring(serverId.Length + 2);
                }
                else if (targetServer?.Alias != null && toolName.StartsWith(targetServer.Alias + "__", StringComparison.OrdinalIgnoreCase))
                {
                    rawToolName = toolName.Substring(targetServer.Alias.Length + 2);
                }
                else
                {
                    var sepIdx = toolName.IndexOf("__", StringComparison.Ordinal);
                    if (sepIdx > 0)
                    {
                        rawToolName = toolName.Substring(sepIdx + 2);
                    }
                }

                if (rawToolName != toolName)
                {
                    routingBody = rewriteRequestJson(body, "name", rawToolName);
                }

                // Compute effective timeout: caller/header override -> server.RequestTimeoutSeconds -> global ToolExecutionTimeoutSeconds -> 120s
                int effectiveTimeoutSec = (timeoutSecondsOverride is > 0 ? timeoutSecondsOverride : null)
                    ?? (targetServer?.RequestTimeoutSeconds is > 0 ? targetServer.RequestTimeoutSeconds : null)
                    ?? (routerSettings?.ToolExecutionTimeoutSeconds is > 0 ? (int?)routerSettings.ToolExecutionTimeoutSeconds : null)
                    ?? 120;
                var effectiveTimeout = TimeSpan.FromSeconds(effectiveTimeoutSec);

                // Compute effective output cap
                int? effectiveMaxChars = (maxCharsOverride is > 0 ? maxCharsOverride : null)
                    ?? (routerSettings?.MaxToolOutputChars is > 0 ? (int?)routerSettings.MaxToolOutputChars : null);

                var resolvedOwnerKey = !string.IsNullOrWhiteSpace(ownerKey)
                    ? ownerKey!
                    : (!string.IsNullOrWhiteSpace(clientSessionId) ? $"session:{clientSessionId}" : "anonymous");

                var slotKey = BackendConcurrencyGate.ResolveSlotKey(targetServer, parsedTargetArgs, callerConcurrencyKey);

                try
                {
                    await using var lease = await ConcurrencyGate.AcquireAsync(
                        serverId,
                        slotKey,
                        targetServer?.MaxConcurrentRequests,
                        cancellationToken);

                    var resp = await conn.SendRequestAsync("tools/call", routingBody, targetAuthToken, cancellationToken, effectiveTimeout);
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
                        var normalizedResult = ProtocolHelper.EnsureResultType(resp.Result.Value);
                        return OutputBuffer.ShapeToolResult(
                            normalizedResult,
                            resolvedOwnerKey,
                            toolName,
                            effectiveMaxChars,
                            callerOffset,
                            callerFilter);
                    }
                    return resp;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    var prompt = (targetServer != null && !string.IsNullOrEmpty(targetServer.DynamicAuthPrompt))
                        ? targetServer.DynamicAuthPrompt
                        : "401 Unauthorized. Please provide a valid target_auth_token via execute_tool.";
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

        private static RouterSettings ResolveRouterSettings(
            IEmbeddingService? embeddingService,
            IDbConnectionFactory? dbFactory,
            ILogger logger)
        {
            var settings = (embeddingService as DynamicEmbeddingService)?.GetSettings();
            if (settings != null)
            {
                return settings;
            }

            if (dbFactory != null)
            {
                try
                {
                    using var conn = dbFactory.CreateConnection();
                    if (conn != null)
                    {
                        settings = conn.QueryFirstOrDefault<RouterSettings>("SELECT * FROM Settings WHERE Id = 'default'");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Could not load RouterSettings from database; using defaults.");
                }
            }

            return settings ?? new RouterSettings();
        }

        private static RouterSettings CloneSettingsWithTimeout(RouterSettings source, int codeModeTimeoutSeconds)
        {
            return new RouterSettings
            {
                Id = source.Id,
                DashboardTitle = source.DashboardTitle,
                DashboardIcon = source.DashboardIcon,
                EmbeddingProvider = source.EmbeddingProvider,
                EmbeddingApiUrl = source.EmbeddingApiUrl,
                EmbeddingApiKey = source.EmbeddingApiKey,
                EmbeddingApiModel = source.EmbeddingApiModel,
                EmbeddingModelDir = source.EmbeddingModelDir,
                GlobalMaxKeys = source.GlobalMaxKeys,
                UserMaxKeys = source.UserMaxKeys,
                UserSecretStorage = source.UserSecretStorage,
                AllowOpenClientRegistration = source.AllowOpenClientRegistration,
                ToolSearchDefaultLimit = source.ToolSearchDefaultLimit,
                ToolSearchMinScore = source.ToolSearchMinScore,
                ToolSearchScoreDropoffRatio = source.ToolSearchScoreDropoffRatio,
                EnableCodeMode = source.EnableCodeMode,
                CodeModeExecutionTimeoutSeconds = codeModeTimeoutSeconds,
                ToolExecutionTimeoutSeconds = source.ToolExecutionTimeoutSeconds,
                MaxToolOutputChars = source.MaxToolOutputChars,
                CodeModeMaxToolCalls = source.CodeModeMaxToolCalls,
                CodeModeMaxMemoryMb = source.CodeModeMaxMemoryMb,
                CodeModeMaxStatements = source.CodeModeMaxStatements,
                CodeModeAllowedGroups = source.CodeModeAllowedGroups
            };
        }

        private static List<object> FilterToolsByServer(List<object> tools, string serverFilter, IEnumerable<McpServer>? servers)
        {
            var trimmed = serverFilter.Trim();
            var matchedServer = servers?.FirstOrDefault(s =>
                string.Equals(s.Id, trimmed, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(s.Alias) && string.Equals(s.Alias, trimmed, StringComparison.OrdinalIgnoreCase)));

            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { trimmed };
            if (matchedServer != null)
            {
                candidates.Add(matchedServer.Id);
                if (!string.IsNullOrWhiteSpace(matchedServer.Alias))
                {
                    candidates.Add(matchedServer.Alias);
                }
            }

            var filtered = new List<object>();
            foreach (var item in tools)
            {
                var meta = ExtractToolMetadata(item);
                bool matches = false;

                if (!string.IsNullOrWhiteSpace(meta.ServerId) && candidates.Contains(meta.ServerId))
                {
                    matches = true;
                }
                else if (!string.IsNullOrWhiteSpace(meta.Name))
                {
                    foreach (var p in candidates)
                    {
                        if (meta.Name.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase) ||
                            meta.Name.StartsWith(p + "__", StringComparison.OrdinalIgnoreCase) ||
                            meta.Name.StartsWith(p + ":", StringComparison.OrdinalIgnoreCase))
                        {
                            matches = true;
                            break;
                        }
                    }
                }

                if (matches)
                {
                    filtered.Add(item);
                }
            }

            return filtered;
        }

        private static List<object> BuildCompactSearchItems(List<ToolSearchResult> results)
        {
            var list = new List<object>(results.Count);
            foreach (var r in results)
            {
                var meta = ExtractToolMetadata(r.Tool);
                var desc = meta.Description ?? string.Empty;
                if (desc.Length > 160)
                {
                    desc = desc.Substring(0, 157) + "...";
                }

                list.Add(new
                {
                    name = r.ToolName,
                    serverId = r.ServerId,
                    description = desc,
                    @params = meta.Parameters.Select(p => p.Name).ToArray()
                });
            }
            return list;
        }

        private static string? ExtractToolNameString(object? item)
        {
            if (item == null)
            {
                return null;
            }
            if (item is IDictionary<string, object> dict && dict.TryGetValue("name", out var nObj))
            {
                return nObj?.ToString();
            }
            if (item is JsonElement je && je.TryGetProperty("name", out var pProp))
            {
                return pProp.GetString();
            }
            try
            {
                using var itemDoc = JsonDocument.Parse(JsonSerializer.Serialize(item));
                if (itemDoc.RootElement.TryGetProperty("name", out var nameEl))
                {
                    return nameEl.GetString();
                }
            }
            catch { }
            return null;
        }

        private static bool TryGetOptionalInt(JsonElement obj, out int value, params string[] names)
        {
            foreach (var name in names)
            {
                if (obj.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out value))
                {
                    return true;
                }
            }
            value = 0;
            return false;
        }
    }
}
