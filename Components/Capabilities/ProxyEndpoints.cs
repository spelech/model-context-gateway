using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace ModelContextGateway.Components.Capabilities
{
    public static class ProxyEndpoints
    {
        private static readonly string AppVersion = GatewayMetadata.Version;

        public static IEndpointRouteBuilder MapProxyEndpoints(this IEndpointRouteBuilder app)
        {
            // ----------------------------------------------------
            // MCP CLIENT SSE HANDLER
            // ----------------------------------------------------
            app.MapMethods("/sse", new[] { "GET", "POST", "HEAD" }, async (HttpContext httpContext, [FromServices] SessionManager sessionManager, ILogger<Program> logger) =>
            {
                httpContext.Response.Headers.ContentType = "text/event-stream";
                httpContext.Response.Headers.CacheControl = "no-cache";
                httpContext.Response.Headers.Connection = "keep-alive";

                if (httpContext.Request.Method == "HEAD")
                {
                    return;
                }

                // Read body if POST
                string requestBody = string.Empty;
                string method = string.Empty;
                JsonElement? id = null;
                if (httpContext.Request.Method == "POST")
                {
                    try
                    {
                        httpContext.Request.EnableBuffering();
                        using (var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true))
                        {
                            requestBody = await reader.ReadToEndAsync();
                            httpContext.Request.Body.Position = 0;
                        }

                        if (!string.IsNullOrEmpty(requestBody))
                        {
                            using var doc = JsonDocument.Parse(requestBody);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("method", out var methodProp))
                            {
                                method = methodProp.GetString() ?? string.Empty;
                            }
                            if (root.TryGetProperty("id", out var idProp))
                            {
                                id = idProp.Clone();
                            }
                            logger.LogDebug("[JSON-RPC Client -> Gateway] {Payload}", PiiSanitizer.SanitizePayload(requestBody));
                        }
                    }
                    catch (UnauthorizedAccessException exAuth)
                    {
                        logger.LogWarning(exAuth, "Unauthorized access during stateless request handling");
                        httpContext.Response.StatusCode = 403;
                        httpContext.Response.Headers.ContentType = "application/json";
                        await httpContext.Response.WriteAsJsonAsync(new
                        {
                            jsonrpc = "2.0",
                            error = new { code = -32001, message = exAuth.Message }
                        });
                        return;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to parse POST /sse body");
                    }
                }

                // Determine if this is a subsequent request for an existing stateless/global session
                bool isSubsequentRequest = httpContext.Request.Method == "POST" &&
                                           method != "initialize" &&
                                           method != "server/discover";

                if (isSubsequentRequest)
                {
                    var globalSessionId = "global-stateless-session";
                    var activeSession = sessionManager.GetSession(globalSessionId);
                    if (activeSession == null)
                    {
                        bool isMetaMode = httpContext.Request.Query["meta"] != "false";
                        activeSession = await sessionManager.CreateSessionAsync(globalSessionId, httpContext.Response, targetServerId: null, isMetaMode);
                    }
                    else
                    {
                        activeSession.UpdateClientResponse(httpContext.Response);
                    }

                    httpContext.Response.OnCompleted(() =>
                    {
                        if (activeSession.GetClientResponse() == httpContext.Response)
                        {
                            activeSession.DecoupleClientResponse();
                        }
                        return Task.CompletedTask;
                    });

                    sessionManager.IncrementTotalRequests();
                    logger.LogInformation("Routing stateless POST /sse request method {Method} to global session", method);
                    try
                    {
                        if (string.IsNullOrEmpty(method))
                        {
                            if (id != null)
                            {
                                var idStr = id.Value.ValueKind == JsonValueKind.String ? id.Value.GetString() : id.Value.GetRawText();
                                if (idStr != null && activeSession.TryHandleClientResponse(idStr, requestBody))
                                {
                                    httpContext.Response.StatusCode = 202;
                                    return;
                                }
                            }
                            httpContext.Response.StatusCode = 400;
                            return;
                        }

                        if (method == "notifications/cancelled")
                        {
                            using var doc = JsonDocument.Parse(requestBody);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("params", out var paramsProp) && paramsProp.TryGetProperty("requestId", out var reqIdProp))
                            {
                                var reqId = reqIdProp.ValueKind == JsonValueKind.String ? reqIdProp.GetString() : reqIdProp.GetRawText();
                                if (!string.IsNullOrEmpty(reqId))
                                {
                                    activeSession.CancelRequest(reqId, httpContext.TraceIdentifier);
                                }
                            }
                            httpContext.Response.StatusCode = 202;
                            return;
                        }

                        if (method == "tools/list")
                        {
                            var tools = await activeSession.ListToolsAsync(requestBody, httpContext);
                            var response = new
                            {
                                jsonrpc = "2.0",
                                id = id != null ? (object)id : null,
                                result = ProtocolHelper.EnsureResultType(new { tools })
                            };
                            httpContext.Response.Headers.ContentType = "application/json";
                            await httpContext.Response.WriteAsJsonAsync(response);
                            return;
                        }
                        else if (method == "tools/call")
                        {
                            var dbFactory = httpContext.RequestServices.GetRequiredService<IDbConnectionFactory>();
                            using var doc = JsonDocument.Parse(requestBody);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("params", out var paramsProp) && paramsProp.TryGetProperty("name", out var nameProp))
                            {
                                var toolName = nameProp.GetString() ?? string.Empty;
                                var res = await activeSession.CallToolAsync(toolName, requestBody, dbFactory, httpContext);
                                var targetRes = res is JsonElement je && je.TryGetProperty("result", out var r) ? (object)r : res;
                                var response = new
                                {
                                    jsonrpc = "2.0",
                                    id = id != null ? (object)id : null,
                                    result = ProtocolHelper.EnsureResultType(targetRes)
                                };
                                httpContext.Response.Headers.ContentType = "application/json";
                                await httpContext.Response.WriteAsJsonAsync(response);
                                return;
                            }
                            httpContext.Response.StatusCode = 400;
                            return;
                        }
                        else if (method == "resources/list")
                        {
                            var resources = await activeSession.ListResourcesAsync(requestBody, httpContext);
                            var response = new
                            {
                                jsonrpc = "2.0",
                                id = id != null ? (object)id : null,
                                result = ProtocolHelper.EnsureResultType(new { resources })
                            };
                            httpContext.Response.Headers.ContentType = "application/json";
                            await httpContext.Response.WriteAsJsonAsync(response);
                            return;
                        }
                        else if (method == "resources/templates/list")
                        {
                            var templates = await activeSession.ListResourceTemplatesAsync(requestBody, httpContext);
                            var response = new
                            {
                                jsonrpc = "2.0",
                                id = id != null ? (object)id : null,
                                result = ProtocolHelper.EnsureResultType(new { templates })
                            };
                            httpContext.Response.Headers.ContentType = "application/json";
                            await httpContext.Response.WriteAsJsonAsync(response);
                            return;
                        }
                        else if (method == "resources/read")
                        {
                            using var doc = JsonDocument.Parse(requestBody);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("params", out var paramsProp) && paramsProp.TryGetProperty("uri", out var uriProp))
                            {
                                var uri = uriProp.GetString() ?? string.Empty;
                                var res = await activeSession.ReadResourceAsync(uri, requestBody, httpContext);
                                var targetRes = res is JsonElement je && je.TryGetProperty("result", out var r) ? (object)r : res;
                                var response = new
                                {
                                    jsonrpc = "2.0",
                                    id = id != null ? (object)id : null,
                                    result = ProtocolHelper.EnsureResultType(targetRes)
                                };
                                httpContext.Response.Headers.ContentType = "application/json";
                                await httpContext.Response.WriteAsJsonAsync(response);
                                return;
                            }
                            httpContext.Response.StatusCode = 400;
                            return;
                        }
                        else if (method == "prompts/list")
                        {
                            var prompts = await activeSession.ListPromptsAsync(requestBody, httpContext);
                            var response = new
                            {
                                jsonrpc = "2.0",
                                id = id != null ? (object)id : null,
                                result = ProtocolHelper.EnsureResultType(new { prompts })
                            };
                            httpContext.Response.Headers.ContentType = "application/json";
                            await httpContext.Response.WriteAsJsonAsync(response);
                            return;
                        }
                        else if (method == "prompts/get")
                        {
                            using var doc = JsonDocument.Parse(requestBody);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("params", out var paramsProp) && paramsProp.TryGetProperty("name", out var nameProp))
                            {
                                var name = nameProp.GetString() ?? string.Empty;
                                var res = await activeSession.GetPromptAsync(name, requestBody, httpContext);
                                var targetRes = res is JsonElement je && je.TryGetProperty("result", out var r) ? (object)r : res;
                                var response = new
                                {
                                    jsonrpc = "2.0",
                                    id = id != null ? (object)id : null,
                                    result = ProtocolHelper.EnsureResultType(targetRes)
                                };
                                httpContext.Response.Headers.ContentType = "application/json";
                                await httpContext.Response.WriteAsJsonAsync(response);
                                return;
                            }
                            httpContext.Response.StatusCode = 400;
                            return;
                        }
                        else if (method == "completion/complete")
                        {
                            var res = await activeSession.CompleteAsync(requestBody, httpContext);
                            var response = new
                            {
                                jsonrpc = "2.0",
                                id = id != null ? (object)id : null,
                                result = ProtocolHelper.EnsureResultType(res)
                            };
                            httpContext.Response.Headers.ContentType = "application/json";
                            await httpContext.Response.WriteAsJsonAsync(response);
                            return;
                        }
                        else if (method == "subscriptions/listen")
                        {
                            var response = new
                            {
                                jsonrpc = "2.0",
                                id = id != null ? (object)id : null,
                                result = ProtocolHelper.EnsureResultType(new
                                {
                                    status = "listening",
                                    subscriptions = new { }
                                })
                            };
                            httpContext.Response.Headers.ContentType = "application/json";
                            await httpContext.Response.WriteAsJsonAsync(response);
                            return;
                        }
                        else if (method == "roots/list")
                        {
                            var response = new
                            {
                                jsonrpc = "2.0",
                                id = id != null ? (object)id : null,
                                result = ProtocolHelper.EnsureResultType(new
                                {
                                    roots = new[] {
                                        new {
                                            uri = "file:///containers",
                                            name = "Docker Containers Workspace"
                                        }
                                    }
                                })
                            };
                            httpContext.Response.Headers.ContentType = "application/json";
                            await httpContext.Response.WriteAsJsonAsync(response);
                            return;
                        }
                        else
                        {
                            await activeSession.BroadcastNotificationAsync(method, requestBody);
                            httpContext.Response.StatusCode = 202;
                            return;
                        }
                    }
                    catch (UnauthorizedAccessException exAuth)
                    {
                        logger.LogWarning(exAuth, "Unauthorized access during stateless request handling");
                        httpContext.Response.StatusCode = 403;
                        httpContext.Response.Headers.ContentType = "application/json";
                        await httpContext.Response.WriteAsJsonAsync(new
                        {
                            jsonrpc = "2.0",
                            error = new { code = -32001, message = exAuth.Message }
                        });
                        return;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error routing stateless message");
                        httpContext.Response.StatusCode = 500;
                        return;
                    }
                }

                // Otherwise, this is a new session establishment request (GET /sse or POST with initialize/discover)
                var sessionId = (httpContext.Request.Method == "POST") ? "global-stateless-session" : Guid.NewGuid().ToString("N");
                logger.LogInformation("New client SSE connection ({Method}). SessionId: {SessionId}", httpContext.Request.Method, sessionId);

                // Write SSE endpoint event
                var scheme = httpContext.Request.Headers["X-Forwarded-Proto"].ToString();
                if (string.IsNullOrEmpty(scheme))
                {
                    scheme = httpContext.Request.Scheme;
                }

                var host = httpContext.Request.Host.Value;
                var absoluteUrl = $"{scheme}://{host}/message?sessionId={sessionId}";
                await httpContext.Response.WriteAsync($"event: endpoint\ndata: {absoluteUrl}\n\n");
                await httpContext.Response.Body.FlushAsync();

                bool metaMode = httpContext.Request.Query["meta"] != "false";

                // Retrieve or create session
                ClientSession session;
                var existingSession = sessionManager.GetSession(sessionId);
                if (existingSession != null)
                {
                    session = existingSession;
                    if (sessionId == "global-stateless-session")
                    {
                        session.UpdateClientResponse(httpContext.Response);
                    }
                }
                else
                {
                    session = await sessionManager.CreateSessionAsync(sessionId, httpContext.Response, targetServerId: null, metaMode);
                }

                if (sessionId == "global-stateless-session")
                {
                    httpContext.Response.OnCompleted(() =>
                    {
                        if (session.GetClientResponse() == httpContext.Response)
                        {
                            session.DecoupleClientResponse();
                        }
                        return Task.CompletedTask;
                    });
                }

                if (httpContext.Request.Method == "POST")
                {
                    if (method == "initialize")
                    {
                        string clientProtocolVersion = GatewayMetadata.ProtocolVersion;
                        try
                        {
                            using var doc = JsonDocument.Parse(requestBody);
                            if (doc.RootElement.TryGetProperty("params", out var pElem) && pElem.TryGetProperty("protocolVersion", out var pvElem))
                            {
                                var reqVer = pvElem.GetString();
                                if (!string.IsNullOrWhiteSpace(reqVer))
                                {
                                    clientProtocolVersion = reqVer;
                                }
                            }
                        }
                        catch
                        {
                        }

                        var response = new
                        {
                            jsonrpc = "2.0",
                            id = id != null ? (object)id : null,
                            result = ProtocolHelper.EnsureResultType(new
                            {
                                protocolVersion = clientProtocolVersion,
                                capabilities = new
                                {
                                    tools = new { listChanged = true },
                                    prompts = new { listChanged = true },
                                    resources = new { listChanged = true },
                                    subscriptions = new { }
                                },
                                serverInfo = new { name = "ModelContextGateway", version = AppVersion }
                            })
                        };
                        var json = JsonSerializer.Serialize(response);
                        await httpContext.Response.WriteAsync($"event: message\ndata: {json}\n\n");
                        await httpContext.Response.Body.FlushAsync();
                        session.StartInitialization(requestBody);
                    }
                    else if (method == "server/discover")
                    {
                        var response = new
                        {
                            jsonrpc = "2.0",
                            id = id != null ? (object)id : null,
                            result = ProtocolHelper.EnsureResultType(new
                            {
                                supportedVersions = GatewayMetadata.SupportedProtocolVersions,
                                capabilities = new
                                {
                                    tools = new { listChanged = true },
                                    prompts = new { listChanged = true },
                                    resources = new { listChanged = true },
                                    subscriptions = new { }
                                },
                                serverInfo = new { name = "ModelContextGateway", version = AppVersion }
                            })
                        };
                        var json = JsonSerializer.Serialize(response);
                        await httpContext.Response.WriteAsync($"event: message\ndata: {json}\n\n");
                        await httpContext.Response.Body.FlushAsync();
                        session.StartInitialization(requestBody);
                    }
                }

                // Keep connection alive
                try
                {
                    while (!httpContext.RequestAborted.IsCancellationRequested)
                    {
                        await Task.Delay(15000, httpContext.RequestAborted);
                        await httpContext.Response.WriteAsync(":ping\n\n");
                        await httpContext.Response.Body.FlushAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Client SSE connection closed for SessionId: {SessionId}", sessionId);
                }
                finally
                {
                    if (sessionId != "global-stateless-session")
                    {
                        sessionManager.CloseSession(sessionId);
                    }
                }
            }).RequireAuthorization();

            // Minimal API route for handling GET (SSE initialization) and POST (JSON-RPC requests)
            app.MapMethods("/{targetServerId:regex(^[a-zA-Z0-9_-]+$)}", new[] { "GET", "POST", "HEAD" }, async (HttpContext httpContext, [FromServices] SessionManager sessionManager, ILogger<Program> logger, string targetServerId) =>
            {
                // Target Routing for router-admin and admin virtual server
                if (string.Equals(targetServerId, "router-admin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(targetServerId, "admin", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleTargetAdminAsync(httpContext, targetServerId);
                    return;
                }
                if (string.Equals(targetServerId, "consent", StringComparison.OrdinalIgnoreCase))
                {
                    httpContext.Response.ContentType = "text/html";
                    var indexPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html");
                    if (File.Exists(indexPath))
                    {
                        await httpContext.Response.SendFileAsync(indexPath);
                    }
                    else
                    {
                        httpContext.Response.StatusCode = 404;
                    }
                    return;
                }
                // First check AppKey authorization if authenticated via AppKey
                if (httpContext.Items.TryGetValue("AppKeyUsed", out var appKeyUsedObj) == true && appKeyUsedObj is bool appKeyUsed && appKeyUsed)
                {
                    if (httpContext.Items.TryGetValue("AppKeyScopes", out var scopesObj) == true && scopesObj is string scopesJson)
                    {
                        bool scopeAllowed = false;
                        try
                        {
                            var scopes = JsonSerializer.Deserialize<List<string>>(scopesJson);
                            if (scopes != null)
                            {
                                var dbFactory = httpContext.RequestServices.GetService<IDbConnectionFactory>();
                                List<string>? serverCategories = null;
                                if (dbFactory != null)
                                {
                                    try
                                    {
                                        using var dbConn = dbFactory.CreateConnection();
                                        var rawCat = await dbConn.ExecuteScalarAsync<string>("SELECT Categories FROM Servers WHERE Id = @Id", new { Id = targetServerId });
                                        if (!string.IsNullOrEmpty(rawCat))
                                        {
                                            try { serverCategories = JsonSerializer.Deserialize<List<string>>(rawCat); }
                                            catch { serverCategories = rawCat.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(); }
                                        }
                                    }
                                    catch { }
                                }

                                foreach (var s in scopes)
                                {
                                    var cleanScope = s.Trim().ToLowerInvariant();
                                    if (cleanScope == "all" || cleanScope == "mcp_client" || cleanScope == "*")
                                    {
                                        scopeAllowed = true;
                                        break;
                                    }
                                    if (cleanScope == $"server:{targetServerId}".ToLowerInvariant() || cleanScope == targetServerId.ToLowerInvariant())
                                    {
                                        scopeAllowed = true;
                                        break;
                                    }
                                    if (cleanScope.StartsWith("category:") || cleanScope.StartsWith("group:"))
                                    {
                                        var scopeCategory = cleanScope.StartsWith("category:")
                                            ? cleanScope.Substring("category:".Length).Trim()
                                            : cleanScope.Substring("group:".Length).Trim();

                                        if (!string.IsNullOrEmpty(scopeCategory))
                                        {
                                            if (string.Equals(targetServerId, scopeCategory, StringComparison.OrdinalIgnoreCase))
                                            {
                                                scopeAllowed = true;
                                                break;
                                            }
                                            if (serverCategories != null && serverCategories.Any(c => string.Equals(c, scopeCategory, StringComparison.OrdinalIgnoreCase)))
                                            {
                                                scopeAllowed = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception exScopes)
                        {
                            logger.LogWarning(exScopes, "Failed to parse AppKey scopes JSON: {ScopesJson}", scopesJson);
                        }

                        if (!scopeAllowed)
                        {
                            httpContext.Response.StatusCode = 403;
                            var compositeProvider = httpContext.RequestServices.GetRequiredService<CompositeIdentityProvider>();
                            var identity = await compositeProvider.ResolveIdentityAsync(httpContext);
                            var audit = httpContext.RequestServices.GetService<IAuditLogger>();
                            if (audit != null)
                            {
                                await audit.LogInvocationAsync(
                                    Guid.NewGuid().ToString("N"),
                                    identity.Username,
                                    identity.Sid ?? "",
                                    targetServerId,
                                    "server/connect",
                                    httpContext.Request.Method,
                                    0,
                                    403,
                                    errorMessage: "Access denied"
                                );
                            }
                            await httpContext.Response.WriteAsJsonAsync(new { error = $"Access denied to target server: {targetServerId}" });
                            return;
                        }
                    }
                }
                else
                {
                    // RBAC Check for targetServerId
                    var compositeProvider = httpContext.RequestServices.GetRequiredService<CompositeIdentityProvider>();
                    var identity = await compositeProvider.ResolveIdentityAsync(httpContext);

                    if (!SecurityValidationHelper.IsAdmin(identity, httpContext.RequestServices.GetService<IConfiguration>()))
                    {
                        var dbFactory = httpContext.RequestServices.GetRequiredService<IDbConnectionFactory>();
                        using var conn = dbFactory.CreateConnection();
                        var targetServerKey = $"server:{targetServerId}";

                        if (dbFactory.ProviderName == "sqlite")
                        {
                            const string countSql = "SELECT COUNT(*) FROM AccessPolicies WHERE TargetId = @TargetId;";
                            int policyCount = await conn.ExecuteScalarAsync<int>(countSql, new { TargetId = targetServerKey });
                            if (policyCount == 0)
                            {
                                httpContext.Response.StatusCode = 403;
                                var audit = httpContext.RequestServices.GetService<IAuditLogger>();
                                if (audit != null)
                                {
                                    await audit.LogInvocationAsync(
                                        Guid.NewGuid().ToString("N"),
                                        identity.Username,
                                        identity.Sid ?? "",
                                        targetServerId,
                                        "server/connect",
                                        httpContext.Request.Method,
                                        0,
                                        403,
                                        errorMessage: "Access denied"
                                    );
                                }
                                await httpContext.Response.WriteAsJsonAsync(new { error = $"Access denied to target server: {targetServerId}" });
                                return;
                            }

                            const string denySql = "SELECT COUNT(*) FROM AccessPolicies WHERE TargetId = @TargetId AND RequiredGroup IN @GroupNames AND IsAllowed = 0;";
                            int denyCount = await conn.ExecuteScalarAsync<int>(denySql, new { TargetId = targetServerKey, GroupNames = identity.GroupNames });

                            const string allowSql = "SELECT COUNT(*) FROM AccessPolicies WHERE TargetId = @TargetId AND RequiredGroup IN @GroupNames AND IsAllowed = 1;";
                            int allowCount = await conn.ExecuteScalarAsync<int>(allowSql, new { TargetId = targetServerKey, GroupNames = identity.GroupNames });

                            if (denyCount > 0 || allowCount == 0)
                            {
                                httpContext.Response.StatusCode = 403;
                                var audit = httpContext.RequestServices.GetService<IAuditLogger>();
                                if (audit != null)
                                {
                                    await audit.LogInvocationAsync(
                                        Guid.NewGuid().ToString("N"),
                                        identity.Username,
                                        identity.Sid ?? "",
                                        targetServerId,
                                        "server/connect",
                                        httpContext.Request.Method,
                                        0,
                                        403,
                                        errorMessage: "Access denied"
                                    );
                                }
                                await httpContext.Response.WriteAsJsonAsync(new { error = $"Access denied to target server: {targetServerId}" });
                                return;
                            }
                        }
                        else
                        {
                            var groupNamesCsv = string.Join(",", identity.GroupNames);
                            object parameters = dbFactory.ProviderName == "mysql"
                                ? new
                                {
                                    p_GroupNames = groupNamesCsv,
                                    p_ItemName = targetServerId,
                                    p_RequestMethod = "GET"
                                }
                                : new
                                {
                                    GroupNames = groupNamesCsv,
                                    ItemName = targetServerId,
                                    RequestMethod = "GET"
                                };
                            int isAllowed = await conn.ExecuteScalarAsync<int>(
                                "sp_EvaluateUserAccess",
                                parameters,
                                commandType: System.Data.CommandType.StoredProcedure
                            );
                            if (isAllowed == 0)
                            {
                                httpContext.Response.StatusCode = 403;
                                var audit = httpContext.RequestServices.GetService<IAuditLogger>();
                                if (audit != null)
                                {
                                    await audit.LogInvocationAsync(
                                        Guid.NewGuid().ToString("N"),
                                        identity.Username,
                                        identity.Sid ?? "",
                                        targetServerId,
                                        "server/connect",
                                        httpContext.Request.Method,
                                        0,
                                        403,
                                        errorMessage: "Access denied"
                                    );
                                }
                                await httpContext.Response.WriteAsJsonAsync(new { error = $"Access denied to target server: {targetServerId}" });
                                return;
                            }
                        }
                    }
                }

                var isSse = httpContext.Request.Headers.Accept.ToString().Contains("text/event-stream");
                var isPost = HttpMethods.IsPost(httpContext.Request.Method);
                bool metaMode = httpContext.Request.Query["meta"] == "true";

                string sessionId = Guid.NewGuid().ToString("N");

                // Read body if POST
                string requestBody = string.Empty;
                string method = string.Empty;
                JsonElement? id = null;

                if (httpContext.Request.Method == "POST")
                {
                    try
                    {
                        httpContext.Request.EnableBuffering();
                        using (var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true))
                        {
                            requestBody = await reader.ReadToEndAsync();
                            httpContext.Request.Body.Position = 0;
                        }

                        if (!string.IsNullOrEmpty(requestBody))
                        {
                            using var doc = JsonDocument.Parse(requestBody);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("method", out var methodProp))
                            {
                                method = methodProp.GetString() ?? string.Empty;
                            }
                            if (root.TryGetProperty("id", out var idProp))
                            {
                                id = idProp.Clone();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to parse POST message body for /mcp");
                    }
                }

                httpContext.Response.Headers.ContentType = "text/event-stream";
                httpContext.Response.Headers.CacheControl = "no-cache";
                httpContext.Response.Headers.Connection = "keep-alive";

                logger.LogInformation("New client /mcp SSE connection ({Method}). SessionId: {SessionId}", httpContext.Request.Method, sessionId);

                var scheme = httpContext.Request.Headers["X-Forwarded-Proto"].ToString();
                if (string.IsNullOrEmpty(scheme))
                {
                    scheme = httpContext.Request.Scheme;
                }

                var host = httpContext.Request.Host.Value;
                var absoluteUrl = $"{scheme}://{host}/mcp/message?sessionId={sessionId}";
                await httpContext.Response.WriteAsync($"event: endpoint\ndata: {absoluteUrl}\n\n");
                await httpContext.Response.Body.FlushAsync();

                var session = await sessionManager.CreateSessionAsync(sessionId, httpContext.Response, targetServerId, metaMode);

                if (httpContext.Request.Method == "POST" && (method == "initialize" || method == "server/discover"))
                {
                    try
                    {
                        logger.LogDebug("Processing initial JSON-RPC message in POST /mcp body: {Body}", PiiSanitizer.SanitizePayload(requestBody));
                        var serverName = "ModelContextGateway";
                        if (!string.IsNullOrWhiteSpace(targetServerId))
                        {
                            var dbFactory = httpContext.RequestServices.GetRequiredService<IDbConnectionFactory>();
                            using var dbConn = dbFactory.CreateConnection();
                            var rawServers = await dbConn.QueryAsync<McpServer>("SELECT * FROM Servers");
                            var allServers = rawServers.ToList();
                            var targetServer = allServers.FirstOrDefault(s => s.Id == targetServerId);
                            if (targetServer != null)
                            {
                                serverName = targetServer.DisplayName;
                            }
                            else if (allServers.Any(s => s.Categories != null && s.Categories.Any(c => string.Equals(c, targetServerId, StringComparison.OrdinalIgnoreCase))))
                            {
                                serverName = char.ToUpper(targetServerId[0]) + targetServerId.Substring(1) + " Services";
                            }
                        }
                        var response = method == "server/discover" ? (object)new
                        {
                            jsonrpc = "2.0",
                            id = id != null ? (object)id : null,
                            result = ProtocolHelper.EnsureResultType(new
                            {
                                supportedVersions = GatewayMetadata.SupportedProtocolVersions,
                                capabilities = new
                                {
                                    tools = new { listChanged = true },
                                    prompts = new { listChanged = true },
                                    resources = new { listChanged = true },
                                    subscriptions = new { }
                                },
                                serverInfo = new { name = serverName, version = AppVersion }
                            })
                        } : new
                        {
                            jsonrpc = "2.0",
                            id = id != null ? (object)id : null,
                            result = ProtocolHelper.EnsureResultType(new
                            {
                                protocolVersion = "2024-11-05",
                                capabilities = new
                                {
                                    tools = new { listChanged = true },
                                    prompts = new { listChanged = true },
                                    resources = new { listChanged = true },
                                    subscriptions = new { }
                                },
                                serverInfo = new { name = serverName, version = AppVersion }
                            })
                        };
                        await session.WriteMessageAsync(response);
                        session.StartInitialization(requestBody);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to initialize POST message body for /mcp SessionId: {SessionId}", sessionId);
                    }
                }

                try
                {
                    while (!httpContext.RequestAborted.IsCancellationRequested)
                    {
                        await Task.Delay(15000, httpContext.RequestAborted);
                        await httpContext.Response.WriteAsync(":ping\n\n");
                        await httpContext.Response.Body.FlushAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("/mcp connection closed for SessionId: {SessionId}", sessionId);
                }
                finally
                {
                    sessionManager.CloseSession(sessionId);
                }
            }).RequireAuthorization();

            // ----------------------------------------------------
            // MCP CLIENT MESSAGE ROUTER
            // ----------------------------------------------------
            var handleMessage = async (HttpContext httpContext, string sessionId, [FromServices] SessionManager sessionManager, ILogger<Program> logger) =>
            {
                var session = sessionManager.GetSession(sessionId);
                if (session == null)
                {
                    for (int i = 0; i < 20; i++)
                    {
                        await Task.Delay(50);
                        session = sessionManager.GetSession(sessionId);
                        if (session != null)
                        {
                            break;
                        }
                    }
                }
                if (session == null)
                {
                    return Results.NotFound(new { error = "Session not found." });
                }
                sessionManager.IncrementTotalRequests();

                using var reader = new StreamReader(httpContext.Request.Body);
                var body = await reader.ReadToEndAsync();

                logger.LogDebug("[JSON-RPC Client -> Gateway] {Payload}", PiiSanitizer.SanitizePayload(body));

                try
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("method", out var methodProp))
                    {
                        if (root.TryGetProperty("id", out var idPropClient))
                        {
                            var idStr = idPropClient.GetString() ?? idPropClient.GetRawText();
                            if (session.TryHandleClientResponse(idStr, body))
                            {
                                return Results.Accepted();
                            }
                        }
                        return Results.BadRequest(new { error = "Invalid JSON-RPC: missing method" });
                    }

                    var method = methodProp.GetString() ?? string.Empty;
                    var id = root.TryGetProperty("id", out var idProp) ? idProp.Clone() : (JsonElement?)null;

                    if (method == "initialize")
                    {
                        var response = new
                        {
                            jsonrpc = "2.0",
                            id = id != null ? (object)id : null,
                            result = ProtocolHelper.EnsureResultType(new
                            {
                                protocolVersion = "2024-11-05",
                                capabilities = new
                                {
                                    tools = new { listChanged = true },
                                    prompts = new { listChanged = true },
                                    resources = new { listChanged = true },
                                    subscriptions = new { }
                                },
                                serverInfo = new
                                {
                                    name = "ModelContextGateway",
                                    version = AppVersion
                                }
                            })
                        };

                        await session.WriteMessageAsync(response);
                        session.StartInitialization(body);
                        return Results.Accepted();
                    }
                    else if (method == "server/discover")
                    {
                        var response = new
                        {
                            jsonrpc = "2.0",
                            id = id != null ? (object)id : null,
                            result = ProtocolHelper.EnsureResultType(new
                            {
                                supportedVersions = GatewayMetadata.SupportedProtocolVersions,
                                capabilities = new
                                {
                                    tools = new { listChanged = true },
                                    prompts = new { listChanged = true },
                                    resources = new { listChanged = true },
                                    subscriptions = new { }
                                },
                                serverInfo = new
                                {
                                    name = "ModelContextGateway",
                                    version = AppVersion
                                }
                            })
                        };

                        await session.WriteMessageAsync(response);
                        session.StartInitialization(body);
                        return Results.Accepted();
                    }
                    else if (method == "tools/list")
                    {
                        var tools = await session.ListToolsAsync(body, httpContext);
                        var response = new
                        {
                            jsonrpc = "2.0",
                            id = id != null ? (object)id : null,
                            result = ProtocolHelper.EnsureResultType(new { tools })
                        };
                        await session.WriteMessageAsync(response);
                        return Results.Accepted();
                    }
                    else if (method == "tools/call")
                    {
                        if (root.TryGetProperty("params", out var paramsProp) && paramsProp.TryGetProperty("name", out var nameProp))
                        {
                            var toolName = nameProp.GetString() ?? string.Empty;
                            var dbFactory = httpContext.RequestServices.GetRequiredService<IDbConnectionFactory>();
                            var res = await session.CallToolAsync(toolName, body, dbFactory, httpContext);
                            var targetRes = res is JsonElement je && je.TryGetProperty("result", out var r) ? (object)r : res;

                            var response = new
                            {
                                jsonrpc = "2.0",
                                id = id != null ? (object)id : null,
                                result = ProtocolHelper.EnsureResultType(targetRes)
                            };
                            await session.WriteMessageAsync(response);
                            return Results.Accepted();
                        }
                        return Results.BadRequest(new { error = "Invalid tools/call: missing name parameter" });
                    }
                    else if (method == "resources/list")
                    {
                        var resources = await session.ListResourcesAsync(body, httpContext);
                        var response = new
                        {
                            jsonrpc = "2.0",
                            id = id != null ? (object)id : null,
                            result = ProtocolHelper.EnsureResultType(new { resources })
                        };
                        await session.WriteMessageAsync(response);
                        return Results.Accepted();
                    }
                    else if (method == "resources/read")
                    {
                        if (root.TryGetProperty("params", out var paramsProp) && paramsProp.TryGetProperty("uri", out var uriProp))
                        {
                            var uri = uriProp.GetString() ?? string.Empty;
                            var res = await session.ReadResourceAsync(uri, body, httpContext);
                            var targetRes = res is JsonElement je && je.TryGetProperty("result", out var r) ? (object)r : res;
                            var response = new
                            {
                                jsonrpc = "2.0",
                                id = id != null ? (object)id : null,
                                result = ProtocolHelper.EnsureResultType(targetRes)
                            };
                            await session.WriteMessageAsync(response);
                            return Results.Accepted();
                        }
                        return Results.BadRequest(new { error = "Invalid resources/read: missing uri parameter" });
                    }
                    else if (method == "resources/templates/list")
                    {
                        var templates = await session.ListResourceTemplatesAsync(body, httpContext);
                        var response = new
                        {
                            jsonrpc = "2.0",
                            id = id != null ? (object)id : null,
                            result = ProtocolHelper.EnsureResultType(new { templates })
                        };
                        await session.WriteMessageAsync(response);
                        return Results.Accepted();
                    }
                    else if (method == "completion/complete")
                    {
                        var res = await session.CompleteAsync(body, httpContext);
                        var response = new
                        {
                            jsonrpc = "2.0",
                            id = id != null ? (object)id : null,
                            result = ProtocolHelper.EnsureResultType(res)
                        };
                        await session.WriteMessageAsync(response);
                        return Results.Accepted();
                    }
                    else if (method == "subscriptions/listen")
                    {
                        var response = new
                        {
                            jsonrpc = "2.0",
                            id = id != null ? (object)id : null,
                            result = ProtocolHelper.EnsureResultType(new
                            {
                                status = "listening",
                                subscriptions = new { }
                            })
                        };
                        await session.WriteMessageAsync(response);
                        return Results.Accepted();
                    }
                    else if (method == "roots/list")
                    {
                        var response = new
                        {
                            jsonrpc = "2.0",
                            id = id != null ? (object)id : null,
                            result = ProtocolHelper.EnsureResultType(new
                            {
                                roots = new[] {
                                    new {
                                        uri = "file:///containers",
                                        name = "Docker Containers Workspace"
                                    }
                                }
                            })
                        };
                        await session.WriteMessageAsync(response);
                        return Results.Accepted();
                    }
                    else if (method == "prompts/list")
                    {
                        var prompts = await session.ListPromptsAsync(body, httpContext);
                        var response = new
                        {
                            jsonrpc = "2.0",
                            id = id != null ? (object)id : null,
                            result = ProtocolHelper.EnsureResultType(new { prompts })
                        };
                        await session.WriteMessageAsync(response);
                        return Results.Accepted();
                    }
                    else if (method == "prompts/get")
                    {
                        if (root.TryGetProperty("params", out var paramsProp) && paramsProp.TryGetProperty("name", out var nameProp))
                        {
                            var name = nameProp.GetString() ?? string.Empty;
                            var res = await session.GetPromptAsync(name, body, httpContext);
                            var targetRes = res is JsonElement je && je.TryGetProperty("result", out var r) ? (object)r : res;
                            var response = new
                            {
                                jsonrpc = "2.0",
                                id = id != null ? (object)id : null,
                                result = ProtocolHelper.EnsureResultType(targetRes)
                            };
                            await session.WriteMessageAsync(response);
                            return Results.Accepted();
                        }
                        return Results.BadRequest(new { error = "Invalid prompts/get: missing name parameter" });
                    }
                    else if (method == "notifications/cancelled")
                    {
                        if (root.TryGetProperty("params", out var paramsProp) && paramsProp.TryGetProperty("requestId", out var reqIdProp))
                        {
                            var reqId = reqIdProp.ValueKind == JsonValueKind.String ? reqIdProp.GetString() : reqIdProp.GetRawText();
                            if (!string.IsNullOrEmpty(reqId))
                            {
                                session.CancelRequest(reqId, httpContext?.TraceIdentifier);
                            }
                        }
                        await session.BroadcastNotificationAsync(method, body);
                        return Results.Accepted();
                    }
                    else if (method.StartsWith("notifications/"))
                    {
                        await session.BroadcastNotificationAsync(method, body);
                        return Results.Accepted();
                    }
                    else
                    {
                        logger.LogWarning("Method {Method} not explicitly handled by Router; forwarding to active backends", method);
                        if (id == null)
                        {
                            await session.BroadcastNotificationAsync(method, body);
                        }
                        else
                        {
                            var results = await session.BroadcastRequestAsync(body);
                            if (results.Count > 0)
                            {
                                var response = new
                                {
                                    jsonrpc = "2.0",
                                    id = (object)id,
                                    result = ProtocolHelper.EnsureResultType(results.First().Value)
                                };
                                await session.WriteMessageAsync(response);
                            }
                        }
                        return Results.Accepted();
                    }
                }
                catch (UnauthorizedAccessException exAuth)
                {
                    logger.LogWarning(exAuth, "Unauthorized access during client message routing");
                    httpContext.Response.StatusCode = 403;
                    return Results.Json(new
                    {
                        jsonrpc = "2.0",
                        error = new { code = -32001, message = exAuth.Message }
                    }, statusCode: 403);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error routing client message.");
                    return Results.Problem("An unexpected error occurred.");
                }
            };

            app.MapPost("/message", async (HttpContext httpContext, [FromQuery] string sessionId, [FromServices] SessionManager sessionManager, ILogger<Program> logger) =>
                await handleMessage(httpContext, sessionId, sessionManager, logger)).RequireAuthorization();

            app.MapPost("/mcp/message", async (HttpContext httpContext, [FromQuery] string sessionId, [FromServices] SessionManager sessionManager, ILogger<Program> logger) =>
                await handleMessage(httpContext, sessionId, sessionManager, logger)).RequireAuthorization();

            return app;
        }

        private static async Task HandleTargetAdminAsync(HttpContext httpContext, string targetServerId)
        {
            var compositeProvider = httpContext.RequestServices.GetRequiredService<CompositeIdentityProvider>();
            var identity = await compositeProvider.ResolveIdentityAsync(httpContext);
            var config = httpContext.RequestServices.GetService<IConfiguration>();

            if (!SecurityValidationHelper.IsAdmin(identity, config, httpContext))
            {
                httpContext.Response.StatusCode = 403;
                var audit = httpContext.RequestServices.GetService<IAuditLogger>();
                if (audit != null)
                {
                    await audit.LogInvocationAsync(
                        Guid.NewGuid().ToString("N"),
                        identity.Username,
                        identity.Sid ?? "",
                        targetServerId,
                        "server/connect",
                        httpContext.Request.Method,
                        0,
                        403,
                        errorMessage: "Access denied to admin server"
                    );
                }
                await httpContext.Response.WriteAsJsonAsync(new { error = $"Access denied to admin server: {targetServerId}" });
                return;
            }

            if (targetServerId == "router-admin" || targetServerId == "admin")
            {
                var adminMcpServer = httpContext.RequestServices.GetRequiredService<AdminMcpServer>();
                var logger = httpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var callerUsername = identity?.Username ?? httpContext.User?.Identity?.Name ?? "admin";

                var method = httpContext.Items.TryGetValue("MCP_METHOD", out var mObj) ? mObj as string : null;
                var isNotification = httpContext.Items.TryGetValue("MCP_IS_NOTIFICATION", out var notifObj) && notifObj is bool b && b;
                var rawBody = httpContext.Items.TryGetValue("MCP_RAW_BODY", out var rawObj) ? rawObj as string ?? string.Empty : string.Empty;
                var id = httpContext.Items.TryGetValue("MCP_REQ_ID", out var idObj) ? idObj : null;

                // 1. Direct Streamable HTTP JSON-RPC POST handling
                if (httpContext.Request.Method == "POST" && !string.IsNullOrEmpty(method))
                {
                    if (isNotification)
                    {
                        httpContext.Response.StatusCode = 202;
                        return;
                    }

                    try
                    {
                        var jsonRpcReq = !string.IsNullOrWhiteSpace(rawBody)
                            ? JsonSerializer.Deserialize<JsonRpcRequest>(rawBody)
                            : null;

                        jsonRpcReq ??= new JsonRpcRequest
                        {
                            Method = method,
                            Id = id
                        };

                        var rpcResponse = await adminMcpServer.ProcessRequestAsync(jsonRpcReq, callerUsername);
                        httpContext.Response.Headers.ContentType = "application/json";
                        await httpContext.Response.WriteAsJsonAsync(rpcResponse);
                        return;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing target admin request: {Method}", method);
                        httpContext.Response.Headers.ContentType = "application/json";
                        await httpContext.Response.WriteAsJsonAsync(new
                        {
                            jsonrpc = "2.0",
                            id = id,
                            error = new { code = -32603, message = "An unexpected error occurred." }
                        });
                        return;
                    }
                }

                // 2. Stateful SSE Stream Setup
                httpContext.Response.Headers.ContentType = "text/event-stream";
                httpContext.Response.Headers.CacheControl = "no-cache";
                httpContext.Response.Headers.Connection = "keep-alive";

                if (httpContext.Request.Method == "HEAD")
                {
                    return;
                }

                var sessionId = Guid.NewGuid().ToString("N");
                var scheme = httpContext.Request.Headers["X-Forwarded-Proto"].ToString();
                if (string.IsNullOrEmpty(scheme))
                {
                    scheme = httpContext.Request.Scheme;
                }

                var host = httpContext.Request.Host.Value;
                var absoluteUrl = $"{scheme}://{host}/admin/message?sessionId={sessionId}";
                await httpContext.Response.WriteAsync($"event: endpoint\ndata: {absoluteUrl}\n\n");
                await httpContext.Response.Body.FlushAsync();

                var sseSession = new AdminSseSession(sessionId, httpContext.Response, callerUsername);
                AdminEndpoints.RegisterSession(sseSession);

                try
                {
                    while (!httpContext.RequestAborted.IsCancellationRequested)
                    {
                        await Task.Delay(15000, httpContext.RequestAborted);
                        await httpContext.Response.WriteAsync(":ping\n\n");
                        await httpContext.Response.Body.FlushAsync();
                    }
                }
                catch (OperationCanceledException) { }
                finally
                {
                    AdminEndpoints.UnregisterSession(sessionId);
                }
            }
        }
    }
}
