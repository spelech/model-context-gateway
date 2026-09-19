using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace ModelContextGateway.Components.Capabilities
{
    public static class CapabilityEndpoints
    {
        public static IEndpointRouteBuilder MapCapabilityEndpoints(this IEndpointRouteBuilder app)
        {
            var api = app.MapGroup("").RequireAuthorization("AdminPolicy");

            api.MapGet("/api/me", async (HttpContext context, [FromServices] CompositeIdentityProvider identityProvider) =>
            {
                var identity = await identityProvider.ResolveIdentityAsync(context);
                if (identity == null || identity.Username == "guest" || identity.Username == "anonymous")
                {
                    return Results.Ok(new { authenticated = false });
                }

                return Results.Ok(new
                {
                    authenticated = true,
                    username = identity.Username,
                    name = identity.Username,
                    email = "",
                    groups = identity.GroupNames
                });
            });

            // --- TEST BENCH & LOGS ENDPOINTS ---

            // 1. Logs API
            api.MapGet("/api/logs", () => Results.Ok(LogBuffer.GetLogs()));
            api.MapDelete("/api/logs", async (HttpContext ctx) =>
            {
                LogBuffer.Clear();
                var audit = ctx.RequestServices.GetService<IAuditLogger>();
                if (audit != null)
                {
                    await audit.LogAdminActionAsync(ctx.User?.Identity?.Name ?? "unknown", "logs.clear", "InMemoryLogBuffer", "", true);
                }

                return Results.Ok(new { success = true });
            });

            // 1.2 Audit Query API
            api.MapGet("/api/audit", async (HttpContext ctx, [FromServices] IDbConnectionFactory dbf,
                string? user, string? server, DateTime? since, int take = 200, int skip = 0) =>
            {
                take = Math.Clamp(take, 1, 1000);
                using var conn = dbf.CreateConnection();
                string sql;
                if (dbf.ProviderName == "mssql")
                {
                    sql = @"SELECT Timestamp, UserPrincipalName, UserSid, ServerCodeName, ItemName, RequestMethod, StatusCode, ExecutionTimeMs, ErrorMessage
                            FROM AuditLogs
                            WHERE (@user   IS NULL OR UserPrincipalName = @user)
                              AND (@server IS NULL OR ServerCodeName = @server)
                              AND (@since  IS NULL OR Timestamp >= @since)
                            ORDER BY Timestamp DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;";
                }
                else
                {
                    sql = @"SELECT Timestamp, UserPrincipalName, UserSid, ServerCodeName, ItemName, RequestMethod, StatusCode, ExecutionTimeMs, ErrorMessage
                            FROM AuditLogs
                            WHERE (@user   IS NULL OR UserPrincipalName = @user)
                              AND (@server IS NULL OR ServerCodeName = @server)
                              AND (@since  IS NULL OR Timestamp >= @since)
                            ORDER BY Timestamp DESC LIMIT @take OFFSET @skip;";
                }
                var rows = await conn.QueryAsync(sql, new { user, server, since, take, skip });
                var audit = ctx.RequestServices.GetService<IAuditLogger>();
                if (audit != null)
                {
                    await audit.LogAdminActionAsync(ctx.User?.Identity?.Name ?? "unknown", "audit.query", "AuditLogs", "", true);
                }

                return Results.Ok(rows);
            });

            // 1.5. Settings API
            api.MapGet("/api/settings", (DynamicEmbeddingService embeddingService) =>
                Results.Ok(embeddingService.GetSettings()));

            api.MapPost("/api/settings", (RouterSettings settings, DynamicEmbeddingService embeddingService, HttpContext httpContext, [FromServices] IAuditLogger auditLogger) =>
            {
                var username = httpContext.User.Identity?.Name ?? "anonymous";
                try
                {
                    embeddingService.SaveSettings(settings);
                    _ = auditLogger.LogAdminActionAsync(username, "UpdateSettings", "embedding-settings", JsonSerializer.Serialize(settings), true);
                    return Results.Ok(new { success = true, settings = embeddingService.GetSettings() });
                }
                catch (ArgumentException ex)
                {
                    _ = auditLogger.LogAdminActionAsync(username, "UpdateSettings", "embedding-settings", JsonSerializer.Serialize(settings), false, ex.Message);
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

            api.MapPost("/api/config/branding/logo", async (HttpRequest request, [FromServices] ISettingRepository settingsRepo, [FromServices] DynamicEmbeddingService embeddingService, [FromServices] IAuditLogger auditLogger, HttpContext httpContext) =>
            {
                if (!request.HasFormContentType || request.Form.Files.Count == 0)
                {
                    return Results.BadRequest(new { error = "No file uploaded" });
                }

                var file = request.Form.Files[0];
                if (file.Length > 2 * 1024 * 1024)
                {
                    return Results.BadRequest(new { error = "File size exceeds 2MB limit" });
                }

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".svg", ".ico", ".webp" };
                if (!allowedExtensions.Contains(ext))
                {
                    return Results.BadRequest(new { error = "Unsupported image format" });
                }

                var dir = Path.Combine(AppContext.BaseDirectory, "data", "branding");
                Directory.CreateDirectory(dir);

                // Remove older logo files
                foreach (var old in Directory.GetFiles(dir, "logo.*"))
                {
                    try { File.Delete(old); } catch { }
                }

                var targetPath = Path.Combine(dir, $"logo{ext}");
                using (var stream = new FileStream(targetPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var settings = await settingsRepo.GetSettingsAsync() ?? new RouterSettings();
                settings.DashboardIcon = "/api/config/branding/logo";
                await settingsRepo.SaveSettingsAsync(settings);
                embeddingService.ReloadSettings(settings);

                var username = httpContext.User.Identity?.Name ?? "admin";
                await auditLogger.LogAdminActionAsync(username, "branding.logo.upload", "BrandingLogo", "/api/config/branding/logo", true);

                return Results.Ok(new { url = "/api/config/branding/logo", success = true });
            }).DisableAntiforgery();

            api.MapGet("/api/diagnostics", ([FromServices] SessionManager sessionManager) =>
            {
                var proc = System.Diagnostics.Process.GetCurrentProcess();
                int fdCount = 0;

                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                {
                    try
                    {
                        fdCount = Directory.GetFiles($"/proc/{proc.Id}/fd").Length;
                    }
                    catch { /* Fallback if restricted */ }
                }

                return Results.Ok(new
                {
                    activeSessions = sessionManager.ActiveSessionsCount,
                    workingSet64 = proc.WorkingSet64,
                    handleCount = fdCount > 0 ? fdCount : proc.HandleCount
                });
            }).AllowAnonymous();

            api.MapGet("/api/test/tools", async (string? serverId, [FromServices] IServerRepository serverRepo, [FromServices] HttpClient httpClient, [FromServices] SessionManager sessionManager, ILogger<Program> logger, HttpContext httpContext) =>
            {
                var secretRetriever = httpContext.RequestServices.GetService<CompositeSecretRetriever>();
                var rawServers = await serverRepo.GetEnabledServersAsync();
                var servers = rawServers.ToList();

                if (!string.IsNullOrWhiteSpace(serverId))
                {
                    servers = servers.Where(s =>
                        string.Equals(s.Id, serverId, StringComparison.OrdinalIgnoreCase) ||
                        (s.Categories != null && s.Categories.Any(c => string.Equals(c, serverId, StringComparison.OrdinalIgnoreCase)))
                    ).ToList();
                }
                var allTools = new List<object>();
                var missingServers = new List<McpServer>();

                foreach (var server in servers)
                {
                    if (server.Type == "custom")
                    {
                        continue;
                    }

                    var cached = sessionManager.GetServerToolsCache(server.Id);
                    if (cached != null)
                    {
                        logger.LogInformation("Server tools cache HIT for: {ServerId}", server.Id);
                        allTools.AddRange(cached);
                    }
                    else
                    {
                        logger.LogInformation("Server tools cache MISS for: {ServerId}", server.Id);
                        missingServers.Add(server);
                    }
                }

                if (missingServers.Count > 0)
                {
                    var backendConnections = new System.Collections.Concurrent.ConcurrentDictionary<string, BackendConnection>();
                    try
                    {
                        var tasks = missingServers.Where(s => s.Type != "custom").Select(async server =>
                        {
                            BackendConnection? conn = null;
                            try
                            {
                                conn = new BackendConnection(server, httpClient, logger, secretRetriever);
                                if (server.Type != "http" && server.Type != "streamable")
                                {
                                    using var ctsTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                                    await conn.ConnectAsync().WaitAsync(ctsTimeout.Token);
                                    conn.StartReader(msg => Task.CompletedTask);
                                }
                                using var ctsInit = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                                var initReq = GatewayMetadata.BuildTestBenchInitializeRequest();
                                await conn.SendRequestAsync("initialize", initReq).WaitAsync(ctsInit.Token);
                                await conn.SendNotificationAsync("notifications/initialized", "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
                                backendConnections[server.Id] = conn;
                            }
                            catch (Exception ex)
                            {
                                conn?.Dispose();
                                logger.LogError(ex, "Failed to connect to server {ServerId} for tool listing", server.Id);
                            }
                        });
                        var allTasks = Task.WhenAll(tasks);
                        await Task.WhenAny(allTasks, Task.Delay(3000));

                        var routing = new ToolRoutingManager();
                        await routing.PopulateToolsCacheAsync("{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\",\"id\":\"test-list\"}", backendConnections, logger, missingServers, sessionManager);

                        // Add tools from the newly fetched cache
                        foreach (var server in missingServers)
                        {
                            var newlyCached = sessionManager.GetServerToolsCache(server.Id);
                            if (newlyCached != null)
                            {
                                allTools.AddRange(newlyCached);
                            }
                        }
                    }
                    finally
                    {
                        // Dispose backend connections after query
                        foreach (var conn in backendConnections.Values)
                        {
                            conn.Dispose();
                        }
                    }
                }

                return Results.Ok(allTools);
            });

            // 3. Test Call API
            Delegate handleTestCall = async (
                [FromBody] TestCallModel model,
                [FromServices] IServerRepository serverRepo,
                [FromServices] HttpClient httpClient,
                [FromServices] IAuditLogger auditLogger,
                ILogger<Program> logger,
                HttpContext httpContext) =>
            {
                var username = httpContext.User?.Identity?.Name ?? "unknown";
                var secretRetriever = httpContext.RequestServices.GetService<CompositeSecretRetriever>();
                var toolName = model.ResolvedToolName;
                var serverId = model.ServerId;

                // If serverId is missing or "custom", check if toolName contains server prefix "serverId__tool"
                if ((string.IsNullOrEmpty(serverId) || serverId == "custom") && toolName.Contains("__"))
                {
                    var parts = toolName.Split("__", 2);
                    serverId = parts[0];
                }

                var server = await serverRepo.GetServerByIdAsync(serverId ?? "");
                if (server == null && serverId != "custom")
                {
                    var msg = $"Server {serverId} not found";
                    await auditLogger.LogAdminActionAsync(username, "testbench.tools/call", toolName, JsonSerializer.Serialize(new { serverId, arguments = model.Arguments }), false, msg);
                    return Results.NotFound(msg);
                }

                if (server == null)
                {
                    var msg = "Server not found";
                    await auditLogger.LogAdminActionAsync(username, "testbench.tools/call", toolName, JsonSerializer.Serialize(new { serverId, arguments = model.Arguments }), false, msg);
                    return Results.NotFound();
                }

                var targetToolName = toolName;
                if (!string.IsNullOrEmpty(serverId) && targetToolName.StartsWith(serverId + "__"))
                {
                    targetToolName = targetToolName.Substring(serverId.Length + 2);
                }

                try
                {
                    var userSecretStore = httpContext.RequestServices.GetService<ModelContextGateway.Infrastructure.Secrets.IUserSecretStore>();
                    string? targetUser = username;
                    if (server.EnableOAuth3Lo || server.SecretProvider == "UserProvided")
                    {
                        if (string.IsNullOrEmpty(targetUser) || targetUser == "unknown" || targetUser.Equals("admin", StringComparison.OrdinalIgnoreCase))
                        {
                            var dbFactory = httpContext.RequestServices.GetService<ModelContextGateway.Infrastructure.Persistence.IDbConnectionFactory>();
                            if (dbFactory != null)
                            {
                                using var db = dbFactory.CreateConnection();
                                var matchedUser = await db.QueryFirstOrDefaultAsync<string>(
                                    "SELECT Username FROM UserServerCredentials WHERE ServerId = @ServerId ORDER BY Id DESC LIMIT 1;",
                                    new { ServerId = serverId });
                                if (!string.IsNullOrEmpty(matchedUser))
                                {
                                    targetUser = matchedUser;
                                }
                            }
                        }
                    }

                    // Direct routing to backend
                    using var conn = new BackendConnection(server, httpClient, logger, secretRetriever, forwardedUser: targetUser, userSecretStore: userSecretStore);
                    if (server.Type != "http" && server.Type != "streamable")
                    {
                        using var ctsTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await conn.ConnectAsync().WaitAsync(ctsTimeout.Token);
                        conn.StartReader(msg => Task.CompletedTask);
                    }

                    using var ctsInit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var initReq = GatewayMetadata.BuildTestBenchInitializeRequest();
                    await conn.SendRequestAsync("initialize", initReq).WaitAsync(ctsInit.Token);
                    await conn.SendNotificationAsync("notifications/initialized", "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");

                    var targetPayload = new
                    {
                        jsonrpc = "2.0",
                        id = "test-call-id",
                        method = "tools/call",
                        @params = new
                        {
                            name = targetToolName,
                            arguments = model.Arguments.ValueKind == JsonValueKind.Undefined ? (object)new Dictionary<string, object>() : model.Arguments
                        }
                    };
                    var targetBody = JsonSerializer.Serialize(targetPayload);
                    var result = await conn.SendRequestAsync("tools/call", targetBody);

                    var details = JsonSerializer.Serialize(new { serverId, toolName = targetToolName, arguments = model.Arguments });
                    await auditLogger.LogAdminActionAsync(username, "testbench.tools/call", toolName, details, true);

                    return Results.Ok(result);
                }
                catch (Exception ex)
                {
                    var details = JsonSerializer.Serialize(new { serverId, toolName = targetToolName, arguments = model.Arguments });
                    await auditLogger.LogAdminActionAsync(username, "testbench.tools/call", toolName, details, false, ex.Message);
                    return Results.Problem("An unexpected error occurred.");
                }
            };

            api.MapPost("/api/test/call", handleTestCall);
            api.MapPost("/api/test/call-tool", handleTestCall);

            // 4. Test Semantic Search API
            api.MapPost("/api/test/semantic-search", async ([FromBody] SearchModel model, [FromServices] IServerRepository serverRepo, [FromServices] HttpClient httpClient, [FromServices] IEmbeddingService embeddingService, [FromServices] SessionManager sessionManager, ILogger<Program> logger, HttpContext httpContext) =>
            {
                var secretRetriever = httpContext.RequestServices.GetService<CompositeSecretRetriever>();
                var rawServers = await serverRepo.GetEnabledServersAsync();
                var servers = rawServers.ToList();
                var allTools = new List<object>();
                var missingServers = new List<McpServer>();

                foreach (var server in servers)
                {
                    if (server.Type == "custom")
                    {
                        continue;
                    }

                    var cached = sessionManager.GetServerToolsCache(server.Id);
                    if (cached != null)
                    {
                        allTools.AddRange(cached);
                    }
                    else
                    {
                        missingServers.Add(server);
                    }
                }

                if (missingServers.Count > 0)
                {
                    var backendConnections = new System.Collections.Concurrent.ConcurrentDictionary<string, BackendConnection>();
                    try
                    {
                        var tasks = missingServers.Where(s => s.Type != "custom").Select(async server =>
                        {
                            BackendConnection? conn = null;
                            try
                            {
                                conn = new BackendConnection(server, httpClient, logger, secretRetriever);
                                if (server.Type != "http" && server.Type != "streamable")
                                {
                                    using var ctsTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                                    await conn.ConnectAsync().WaitAsync(ctsTimeout.Token);
                                    conn.StartReader(msg => Task.CompletedTask);
                                }
                                using var ctsInit = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                                var initReq = GatewayMetadata.BuildTestBenchInitializeRequest();
                                await conn.SendRequestAsync("initialize", initReq).WaitAsync(ctsInit.Token);
                                await conn.SendNotificationAsync("notifications/initialized", "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
                                backendConnections[server.Id] = conn;
                            }
                            catch (Exception ex)
                            {
                                conn?.Dispose();
                                logger.LogError(ex, "Failed to connect to server {ServerId} for tool search", server.Id);
                            }
                        });
                        var allTasks = Task.WhenAll(tasks);
                        await Task.WhenAny(allTasks, Task.Delay(3000));

                        var routing = new ToolRoutingManager();
                        await routing.PopulateToolsCacheAsync("{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\",\"id\":\"test-list\"}", backendConnections, logger, missingServers, sessionManager);

                        foreach (var server in missingServers)
                        {
                            var newlyCached = sessionManager.GetServerToolsCache(server.Id);
                            if (newlyCached != null)
                            {
                                allTools.AddRange(newlyCached);
                            }
                        }
                    }
                    finally
                    {
                        foreach (var conn in backendConnections.Values)
                        {
                            conn.Dispose();
                        }
                    }
                }

                var scoredResults = await SemanticSearchService.SearchToolsSemanticAsync(model.Query, allTools, embeddingService, logger);
                return Results.Ok(scoredResults);
            });

            // 2b. Test Prompts List API
            api.MapGet("/api/test/prompts", async (string? serverId, [FromServices] IServerRepository serverRepo, [FromServices] HttpClient httpClient, [FromServices] SessionManager sessionManager, ILogger<Program> logger, HttpContext httpContext) =>
            {
                var secretRetriever = httpContext.RequestServices.GetService<CompositeSecretRetriever>();
                var rawServers = await serverRepo.GetEnabledServersAsync();
                var servers = rawServers.ToList();

                if (!string.IsNullOrWhiteSpace(serverId))
                {
                    servers = servers.Where(s =>
                        string.Equals(s.Id, serverId, StringComparison.OrdinalIgnoreCase) ||
                        (s.Categories != null && s.Categories.Any(c => string.Equals(c, serverId, StringComparison.OrdinalIgnoreCase)))
                    ).ToList();
                }
                var allPrompts = new List<object>();
                var missingServers = new List<McpServer>();

                foreach (var server in servers)
                {
                    if (server.Type == "custom")
                    {
                        continue;
                    }

                    var cached = sessionManager.GetServerPromptsCache(server.Id);
                    if (cached != null)
                    {
                        allPrompts.AddRange(cached);
                    }
                    else
                    {
                        missingServers.Add(server);
                    }
                }

                if (missingServers.Count > 0)
                {
                    var backendConnections = new System.Collections.Concurrent.ConcurrentDictionary<string, BackendConnection>();
                    try
                    {
                        var tasks = missingServers.Where(s => s.Type != "custom").Select(async server =>
                        {
                            BackendConnection? conn = null;
                            try
                            {
                                conn = new BackendConnection(server, httpClient, logger, secretRetriever);
                                if (server.Type != "http" && server.Type != "streamable")
                                {
                                    using var ctsTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                                    await conn.ConnectAsync().WaitAsync(ctsTimeout.Token);
                                    conn.StartReader(msg => Task.CompletedTask);
                                }
                                using var ctsInit = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                                var initReq = GatewayMetadata.BuildTestBenchInitializeRequest();
                                await conn.SendRequestAsync("initialize", initReq).WaitAsync(ctsInit.Token);
                                await conn.SendNotificationAsync("notifications/initialized", "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
                                backendConnections[server.Id] = conn;
                            }
                            catch (Exception ex)
                            {
                                conn?.Dispose();
                                logger.LogError(ex, "Failed to connect to server {ServerId} for prompt listing", server.Id);
                            }
                        });
                        var allTasks = Task.WhenAll(tasks);
                        await Task.WhenAny(allTasks, Task.Delay(3000));

                        var routing = new PromptRoutingManager();
                        await routing.ListPromptsAsync("{\"jsonrpc\":\"2.0\",\"method\":\"prompts/list\",\"id\":\"test-list\"}", backendConnections, logger, () => Task.CompletedTask, sessionManager);

                        foreach (var server in missingServers)
                        {
                            var newlyCached = sessionManager.GetServerPromptsCache(server.Id);
                            if (newlyCached != null)
                            {
                                allPrompts.AddRange(newlyCached);
                            }
                        }
                    }
                    finally
                    {
                        // Dispose backend connections after query
                        foreach (var conn in backendConnections.Values)
                        {
                            conn.Dispose();
                        }
                    }
                }

                // Append built-in meta-prompts
                allPrompts.Add(new Dictionary<string, object> {
                    { "name", "router__diagnose_failure" },
                    { "description", "[router] Diagnose an MCP tool execution failure and generate remediation suggestions." },
                    { "arguments", new[] {
                        new { name = "tool_name", description = "Name of the failing tool", required = true },
                        new { name = "error_message", description = "Exception message or error payload", required = true }
                    } }
                });
                allPrompts.Add(new Dictionary<string, object> {
                    { "name", "router__suggest_remix" },
                    { "description", "[router] Generate alternative tool call configurations or argument combinations based on current state." },
                    { "arguments", new[] {
                        new { name = "query", description = "User intent or goal description", required = true },
                        new { name = "failed_attempts", description = "Log of tried tool names and arguments", required = false }
                    } }
                });

                return Results.Ok(allPrompts);
            });

            // 2c. Test Resources List API
            api.MapGet("/api/test/resources", async (string? serverId, [FromServices] IServerRepository serverRepo, [FromServices] HttpClient httpClient, [FromServices] SessionManager sessionManager, ILogger<Program> logger, HttpContext httpContext) =>
            {
                var secretRetriever = httpContext.RequestServices.GetService<CompositeSecretRetriever>();
                var rawServers = await serverRepo.GetEnabledServersAsync();
                var servers = rawServers.ToList();

                if (!string.IsNullOrWhiteSpace(serverId))
                {
                    servers = servers.Where(s =>
                        string.Equals(s.Id, serverId, StringComparison.OrdinalIgnoreCase) ||
                        (s.Categories != null && s.Categories.Any(c => string.Equals(c, serverId, StringComparison.OrdinalIgnoreCase)))
                    ).ToList();
                }
                var allResources = new List<object>();
                var allTemplates = new List<object>();
                var missingServers = new List<McpServer>();

                // Load custom file-based resources from data/resources
                var resourcesDir = Path.Combine(AppContext.BaseDirectory, "data", "resources");
                if (!Directory.Exists(resourcesDir))
                {
                    resourcesDir = Path.Combine(Directory.GetCurrentDirectory(), "data", "resources");
                }
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

                // Add built-in template
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

                foreach (var server in servers)
                {
                    if (server.Type == "custom")
                    {
                        continue;
                    }

                    var cachedRes = sessionManager.GetServerResourcesCache(server.Id);
                    var cachedTemp = sessionManager.GetServerResourceTemplatesCache(server.Id);
                    if (cachedRes != null && cachedTemp != null)
                    {
                        allResources.AddRange(cachedRes);
                        allTemplates.AddRange(cachedTemp);
                    }
                    else
                    {
                        missingServers.Add(server);
                    }
                }

                if (missingServers.Count > 0)
                {
                    var backendConnections = new System.Collections.Concurrent.ConcurrentDictionary<string, BackendConnection>();
                    try
                    {
                        var tasks = missingServers.Where(s => s.Type != "custom").Select(async server =>
                        {
                            BackendConnection? conn = null;
                            try
                            {
                                conn = new BackendConnection(server, httpClient, logger, secretRetriever);
                                if (server.Type != "http" && server.Type != "streamable")
                                {
                                    using var ctsTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                                    await conn.ConnectAsync().WaitAsync(ctsTimeout.Token);
                                    conn.StartReader(msg => Task.CompletedTask);
                                }
                                using var ctsInit = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                                var initReq = GatewayMetadata.BuildTestBenchInitializeRequest();
                                await conn.SendRequestAsync("initialize", initReq).WaitAsync(ctsInit.Token);
                                await conn.SendNotificationAsync("notifications/initialized", "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
                                backendConnections[server.Id] = conn;
                            }
                            catch (Exception ex)
                            {
                                conn?.Dispose();
                                logger.LogError(ex, "Failed to connect to server {ServerId} for resource listing", server.Id);
                            }
                        });
                        var allTasks = Task.WhenAll(tasks);
                        await Task.WhenAny(allTasks, Task.Delay(3000));

                        var routing = new ResourceRoutingManager();
                        await routing.ListResourcesAsync("{\"jsonrpc\":\"2.0\",\"method\":\"resources/list\",\"id\":\"test-list\"}", backendConnections, logger, () => Task.CompletedTask, sessionManager);
                        await routing.ListResourceTemplatesAsync("{\"jsonrpc\":\"2.0\",\"method\":\"resources/templates/list\",\"id\":\"test-list\"}", backendConnections, logger, () => Task.CompletedTask, sessionManager);

                        foreach (var server in missingServers)
                        {
                            var newlyCachedRes = sessionManager.GetServerResourcesCache(server.Id);
                            var newlyCachedTemp = sessionManager.GetServerResourceTemplatesCache(server.Id);
                            if (newlyCachedRes != null)
                            {
                                allResources.AddRange(newlyCachedRes);
                            }

                            if (newlyCachedTemp != null)
                            {
                                allTemplates.AddRange(newlyCachedTemp);
                            }
                        }
                    }
                    finally
                    {
                        // Dispose backend connections after query
                        foreach (var conn in backendConnections.Values)
                        {
                            conn.Dispose();
                        }
                    }
                }

                // Append built-in router resources at the end
                allResources.Add(new Dictionary<string, object> {
                    { "uri", "router://status" },
                    { "name", "Router Connection Status" },
                    { "mimeType", "application/json" },
                    { "description", "[router] View connection status and metadata for all backend MCP servers." }
                });
                allResources.Add(new Dictionary<string, object> {
                    { "uri", "router://config" },
                    { "name", "Active Configuration" },
                    { "mimeType", "application/json" },
                    { "description", "[router] View the router's active configuration database representation." }
                });

                return Results.Ok(new { resources = allResources, templates = allTemplates });
            });

            // 3b. Test Prompt Get API
            Delegate handleTestPromptGet = async ([FromBody] TestPromptGetModel model, [FromServices] IServerRepository serverRepo, [FromServices] HttpClient httpClient, ILogger<Program> logger, HttpContext httpContext) =>
            {
                var secretRetriever = httpContext.RequestServices.GetService<CompositeSecretRetriever>();
                var rawServers = await serverRepo.GetEnabledServersAsync();
                var servers = rawServers.ToList();
                var backendConnections = new System.Collections.Concurrent.ConcurrentDictionary<string, BackendConnection>();

                var promptName = model.ResolvedPromptName;
                var serverId = model.ServerId;

                if ((string.IsNullOrEmpty(serverId) || serverId == "custom") && promptName.Contains("__"))
                {
                    var parts = promptName.Split("__", 2);
                    serverId = parts[0];
                }

                if (serverId != "router" && !servers.Any(s => s.Id == serverId))
                {
                    return Results.NotFound($"Server {serverId} not found");
                }

                var targetPromptName = promptName;
                if (!string.IsNullOrEmpty(serverId) && targetPromptName.StartsWith(serverId + "__"))
                {
                    targetPromptName = targetPromptName.Substring(serverId.Length + 2);
                }

                var routing = new PromptRoutingManager();

                Func<string, string, string, string> rewriteRequestJson = (json, key, value) =>
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("params", out var paramsProp))
                        {
                            var paramsDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(paramsProp.GetRawText());
                            if (paramsDict != null)
                            {
                                paramsDict[key] = JsonSerializer.SerializeToElement(value);
                                var newParams = JsonSerializer.Serialize(paramsDict);
                                var rootDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                                if (rootDict != null)
                                {
                                    rootDict["params"] = JsonDocument.Parse(newParams).RootElement;
                                    return JsonSerializer.Serialize(rootDict);
                                }
                            }
                        }
                    }
                    catch { }
                    return json;
                };

                var payload = new
                {
                    jsonrpc = "2.0",
                    id = "test-prompt-id",
                    method = "prompts/get",
                    @params = new
                    {
                        name = targetPromptName,
                        arguments = model.Arguments.ValueKind == JsonValueKind.Undefined ? (object)new Dictionary<string, object>() : model.Arguments
                    }
                };
                var body = JsonSerializer.Serialize(payload);

                if (serverId == "router")
                {
                    var res = await routing.GetPromptAsync(targetPromptName, body, backendConnections, () => Task.CompletedTask, rewriteRequestJson);
                    return Results.Ok(res);
                }

                var targetServer = servers.First(s => s.Id == serverId);
                using var conn = new BackendConnection(targetServer, httpClient, logger, secretRetriever);
                if (targetServer.Type != "http" && targetServer.Type != "streamable")
                {
                    using var ctsTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await conn.ConnectAsync().WaitAsync(ctsTimeout.Token);
                    conn.StartReader(msg => Task.CompletedTask);
                }
                using var ctsInit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var initReq = GatewayMetadata.BuildTestBenchInitializeRequest();
                await conn.SendRequestAsync("initialize", initReq).WaitAsync(ctsInit.Token);
                await conn.SendNotificationAsync("notifications/initialized", "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
                backendConnections[targetServer.Id] = conn;

                var promptRes = await routing.GetPromptAsync(targetPromptName, body, backendConnections, () => Task.CompletedTask, rewriteRequestJson);
                return Results.Ok(promptRes);
            };

            api.MapPost("/api/test/prompts/get", handleTestPromptGet);
            api.MapPost("/api/test/get-prompt", handleTestPromptGet);

            // 3c. Test Resource Read API
            Delegate handleTestResourceRead = async ([FromBody] TestResourceReadModel model, [FromServices] IServerRepository serverRepo, [FromServices] HttpClient httpClient, [FromServices] SessionManager sessionManager, ILogger<Program> logger, HttpContext httpContext) =>
            {
                var secretRetriever = httpContext.RequestServices.GetService<CompositeSecretRetriever>();
                var rawServers = await serverRepo.GetEnabledServersAsync();
                var servers = rawServers.ToList();
                var backendConnections = new System.Collections.Concurrent.ConcurrentDictionary<string, BackendConnection>();

                var uri = model.Uri;
                var routing = new ResourceRoutingManager();
                Func<string, string, string, string> rewriteRequestJson = (json, key, value) => json;

                var payload = new
                {
                    jsonrpc = "2.0",
                    id = "test-resource-id",
                    method = "resources/read",
                    @params = new
                    {
                        uri = uri
                    }
                };
                var body = JsonSerializer.Serialize(payload);

                if (uri.StartsWith("router://") || uri.StartsWith("logs://"))
                {
                    var localRes = await routing.ReadResourceAsync(uri, body, backendConnections, () => Task.CompletedTask, rewriteRequestJson, sessionManager);
                    return Results.Ok(localRes);
                }

                string serverId = model.ServerId;
                if (string.IsNullOrEmpty(serverId) && Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri) && parsedUri.Scheme == "mcp")
                {
                    serverId = parsedUri.Host;
                }

                if (string.IsNullOrEmpty(serverId) && servers.Count == 1)
                {
                    serverId = servers[0].Id;
                }

                if (string.IsNullOrEmpty(serverId) || !servers.Any(s => s.Id == serverId))
                {
                    return Results.BadRequest("Invalid resource URI or server not found");
                }

                var targetServer = servers.First(s => s.Id == serverId);
                using var conn = new BackendConnection(targetServer, httpClient, logger, secretRetriever);
                if (targetServer.Type != "http" && targetServer.Type != "streamable")
                {
                    using var ctsTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await conn.ConnectAsync().WaitAsync(ctsTimeout.Token);
                    conn.StartReader(msg => Task.CompletedTask);
                }
                using var ctsInit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var initReq = GatewayMetadata.BuildTestBenchInitializeRequest();
                await conn.SendRequestAsync("initialize", initReq).WaitAsync(ctsInit.Token);
                await conn.SendNotificationAsync("notifications/initialized", "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
                backendConnections[targetServer.Id] = conn;

                var res = await routing.ReadResourceAsync(uri, body, backendConnections, () => Task.CompletedTask, rewriteRequestJson, sessionManager);
                return Results.Ok(res);
            };

            api.MapPost("/api/test/resources/read", handleTestResourceRead);
            api.MapPost("/api/test/read-resource", handleTestResourceRead);

            // --- Custom Files Management APIs ---

            string SanitizeFileName(string name)
            {
                if (string.IsNullOrEmpty(name) || name.Contains("..") || name.Contains("/") || name.Contains("\\"))
                {
                    return string.Empty;
                }
                var safeName = Path.GetFileName(name);
                var invalidChars = Path.GetInvalidFileNameChars();
                return new string(safeName.Where(c => !invalidChars.Contains(c)).ToArray());
            }

            string GetCustomFilesDirectory(string type)
            {
                string folder = type == "prompts" ? "prompts" : "resources";
                var path = Path.Combine(AppContext.BaseDirectory, "data", folder);
                if (!Directory.Exists(path))
                {
                    path = Path.Combine(Directory.GetCurrentDirectory(), "data", folder);
                }
                Directory.CreateDirectory(path);
                return path;
            }

            bool IsSafePath(string filePath, string dir)
            {
                var fullFilePath = Path.GetFullPath(filePath);
                var fullDirPath = Path.GetFullPath(dir);
                if (!fullDirPath.EndsWith(Path.DirectorySeparatorChar.ToString()) && !fullDirPath.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
                {
                    fullDirPath += Path.DirectorySeparatorChar;
                }
                return fullFilePath.StartsWith(fullDirPath, StringComparison.OrdinalIgnoreCase);
            }

            api.MapGet("/api/custom-files", (ILogger<Program> logger) =>
            {
                var result = new List<object>();
                try
                {
                    foreach (var type in new[] { "prompts", "resources" })
                    {
                        var dir = GetCustomFilesDirectory(type);
                        foreach (var file in Directory.GetFiles(dir))
                        {
                            var info = new FileInfo(file);
                            result.Add(new
                            {
                                type = type,
                                name = info.Name,
                                sizeBytes = info.Length,
                                lastModified = info.LastWriteTimeUtc
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An unexpected error occurred.");
                    return Results.Problem("An unexpected error occurred.");
                }
                return Results.Ok(result);
            });

            api.MapGet("/api/custom-files/{type}/{name}", ([FromRoute] string type, [FromRoute] string name, ILogger<Program> logger) =>
            {
                if (type != "prompts" && type != "resources")
                {
                    return Results.BadRequest("Invalid type");
                }

                var cleanName = SanitizeFileName(name);
                if (string.IsNullOrEmpty(cleanName))
                {
                    return Results.BadRequest("Invalid file name");
                }

                var dir = GetCustomFilesDirectory(type);
                var filePath = Path.GetFullPath(Path.Combine(dir, Path.GetFileName(cleanName)));
                if (!IsSafePath(filePath, dir))
                {
                    return Results.BadRequest("Invalid file path");
                }

                if (!File.Exists(filePath))
                {
                    return Results.NotFound("File not found");
                }

                try
                {
                    var text = File.ReadAllText(filePath);
                    return Results.Ok(new { content = text });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An unexpected error occurred.");
                    return Results.Problem("An unexpected error occurred.");
                }
            });

            api.MapPost("/api/custom-files/{type}/{name}", async ([FromRoute] string type, [FromRoute] string name, [FromBody] JsonElement body, ILogger<Program> logger) =>
            {
                if (type != "prompts" && type != "resources")
                {
                    return Results.BadRequest("Invalid type");
                }

                var cleanName = SanitizeFileName(name);
                if (string.IsNullOrEmpty(cleanName))
                {
                    return Results.BadRequest("Invalid file name");
                }

                if (type == "prompts" && !cleanName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    cleanName += ".json";
                }

                if (!body.TryGetProperty("content", out var contentProp))
                {
                    return Results.BadRequest("Missing content field");
                }

                var content = contentProp.GetString() ?? "";

                if (type == "prompts")
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(content);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Invalid JSON format.");
                        return Results.BadRequest("Invalid JSON format.");
                    }
                }

                var dir = GetCustomFilesDirectory(type);
                var filePath = Path.GetFullPath(Path.Combine(dir, Path.GetFileName(cleanName)));
                if (!IsSafePath(filePath, dir))
                {
                    return Results.BadRequest("Invalid file path");
                }

                try
                {
                    await File.WriteAllTextAsync(filePath, content);
                    return Results.Ok(new { success = true, name = cleanName });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An unexpected error occurred.");
                    return Results.Problem("An unexpected error occurred.");
                }
            });

            api.MapDelete("/api/custom-files/{type}/{name}", ([FromRoute] string type, [FromRoute] string name, ILogger<Program> logger) =>
            {
                if (type != "prompts" && type != "resources")
                {
                    return Results.BadRequest("Invalid type");
                }

                var cleanName = SanitizeFileName(name);
                if (string.IsNullOrEmpty(cleanName))
                {
                    return Results.BadRequest("Invalid file name");
                }

                var dir = GetCustomFilesDirectory(type);
                var filePath = Path.GetFullPath(Path.Combine(dir, Path.GetFileName(cleanName)));
                if (!IsSafePath(filePath, dir))
                {
                    return Results.BadRequest("Invalid file path");
                }

                if (!File.Exists(filePath))
                {
                    return Results.NotFound("File not found");
                }

                try
                {
                    File.Delete(filePath);
                    return Results.Ok(new { success = true });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An unexpected error occurred.");
                    return Results.Problem("An unexpected error occurred.");
                }
            });

            return app;
        }
    }
}
