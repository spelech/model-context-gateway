using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace ModelContextGateway.Components.Servers
{
    public static class ServerEndpoints
    {
        public static IEndpointRouteBuilder MapServerEndpoints(this IEndpointRouteBuilder app)
        {
            var api = app.MapGroup("").RequireAuthorization("AdminPolicy");

            api.MapGet("/api/servers", async ([FromServices] IDbConnectionFactory dbFactory, [FromServices] SessionManager sessionManager, ILogger<Program> logger) =>
            {
                try
                {
                    using var conn = dbFactory.CreateConnection();
                    var rawServers = (await conn.QueryAsync(@"SELECT Id, Alias, DisplayName, Url, Enabled, Hidden, Type, Categories, SecretProvider, SecretItemKey, AuthShape, CustomHeaderName, ApiKey, HeadersJson, AllowPassThroughAuth, DynamicAuthPrompt FROM Servers")).ToList();
                    var statuses = sessionManager.BackendStatuses;

                    var sanitized = rawServers.Select(s =>
                    {
                        var idStr = Convert.ToString(s.Id) ?? string.Empty;
                        bool isEnabled = false;
                        if (s.Enabled is long longEnabled)
                        {
                            isEnabled = longEnabled != 0L;
                        }
                        else if (s.Enabled is bool boolEnabled)
                        {
                            isEnabled = boolEnabled;
                        }
                        else if (s.Enabled != null)
                        {
                            isEnabled = Convert.ToBoolean(s.Enabled);
                        }

                        bool isAllowPass = false;
                        if (s.AllowPassThroughAuth is long longAllowPass)
                        {
                            isAllowPass = longAllowPass != 0L;
                        }
                        else if (s.AllowPassThroughAuth is bool boolAllowPass)
                        {
                            isAllowPass = boolAllowPass;
                        }
                        else if (s.AllowPassThroughAuth != null)
                        {
                            isAllowPass = Convert.ToBoolean(s.AllowPassThroughAuth);
                        }

                        bool isHidden = false;
                        if (s.Hidden is long longHidden)
                        {
                            isHidden = longHidden != 0L;
                        }
                        else if (s.Hidden is bool boolHidden)
                        {
                            isHidden = boolHidden;
                        }
                        else if (s.Hidden != null)
                        {
                            isHidden = Convert.ToBoolean(s.Hidden);
                        }

                        var catStr = (string?)s.Categories ?? "[]";
                        List<string> categories;
                        try { categories = JsonSerializer.Deserialize<List<string>>(catStr) ?? new(); }
                        catch { categories = new(); }

                        BackendStatus? status = null;
                        if (!string.IsNullOrEmpty(idStr))
                        {
                            statuses.TryGetValue(idStr, out status);
                        }
                        return new
                        {
                            Id = idStr,
                            Alias = (string?)s.Alias,
                            DisplayName = (string)s.DisplayName,
                            Url = (string)s.Url,
                            Enabled = isEnabled,
                            Hidden = isHidden,
                            Type = (string)(s.Type ?? "sse"),
                            Categories = categories,
                            SecretProvider = (string)(s.SecretProvider ?? "None"),
                            SecretItemKey = (string?)s.SecretItemKey,
                            AuthShape = (string)(s.AuthShape ?? "bearer"),
                            CustomHeaderName = (string?)s.CustomHeaderName,
                            HeadersJson = (string?)s.HeadersJson,
                            HasApiKey = !string.IsNullOrEmpty((string?)s.ApiKey),
                            AllowPassThroughAuth = isAllowPass,
                            DynamicAuthPrompt = (string?)s.DynamicAuthPrompt,
                            ConnectionStatus = isEnabled ? (status?.Status ?? "Disconnected") : "Disabled",
                            ConnectionAttempts = status?.Attempts ?? 0,
                            ConnectionError = status?.Error ?? string.Empty
                        };
                    });
                    return Results.Ok(sanitized);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error executing GET /api/servers");
                    return Results.Problem("An unexpected error occurred.");
                }
            });

            api.MapPost("/api/servers/{id}/reconnect", async (string id, [FromServices] IDbConnectionFactory dbFactory, [FromServices] SessionManager sessionManager, [FromServices] BackendHealthCheckService healthCheckSvc, ILogger<Program> logger) =>
            {
                using var conn = dbFactory.CreateConnection();
                var server = await conn.QueryFirstOrDefaultAsync<McpServer>("SELECT * FROM Servers WHERE Id = @id", new { id });
                if (server == null)
                {
                    return Results.NotFound();
                }

                logger.LogInformation("Triggering manual reconnect request for backend {ServerId} ({DisplayName})", id, server.DisplayName);

                await healthCheckSvc.ProbeServerAsync(server);

                var activeSessions = sessionManager.GetActiveSessions();
                foreach (var session in activeSessions)
                {
                    session.StartInitializationForBackend(id);
                }

                return Results.Ok(new { success = true, message = $"Reconnection triggered for server {server.DisplayName}" });
            });

            api.MapPost("/api/servers/reconnect-all", async ([FromServices] BackendHealthCheckService healthCheckSvc) =>
            {
                await healthCheckSvc.ProbeAllServersAsync();
                return Results.Ok(new { success = true });
            });

            api.MapPut("/api/servers/{id}", async (string id, [FromBody] McpServer update, [FromServices] IDbConnectionFactory dbFactory, [FromServices] SessionManager sessionManager, HttpContext httpContext, [FromServices] IAuditLogger auditLogger) =>
            {
                var username = httpContext.User.Identity?.Name ?? "anonymous";
                using var conn = dbFactory.CreateConnection();
                var server = await conn.QueryFirstOrDefaultAsync<McpServer>("SELECT * FROM Servers WHERE Id = @id", new { id });
                if (server == null)
                {
                    _ = auditLogger.LogAdminActionAsync(username, "UpdateServer", id, JsonSerializer.Serialize(update), false, "Server not found");
                    return Results.NotFound();
                }

                server.Enabled = update.Enabled;
                server.Hidden = update.Hidden;

                if (!string.IsNullOrEmpty(update.DisplayName))
                {
                    server.DisplayName = update.DisplayName;
                }

                var allowedTypes = new[] { "sse", "http", "streamable", "stdio", "custom" };
                var targetType = (server.Type ?? "sse").ToLowerInvariant();
                if (!string.IsNullOrEmpty(update.Type))
                {
                    var lowerType = update.Type.ToLowerInvariant();
                    if (!allowedTypes.Contains(lowerType))
                    {
                        var typeErr = $"Transport type '{update.Type}' is not supported.";
                        _ = auditLogger.LogAdminActionAsync(username, "UpdateServer", id, JsonSerializer.Serialize(update), false, typeErr);
                        return Results.BadRequest(new { error = typeErr });
                    }
                    targetType = lowerType;
                }

                var targetUrl = !string.IsNullOrEmpty(update.Url) ? update.Url : server.Url;

                if (targetType == "stdio")
                {
                    if (!ServerValidationHelper.IsValidStdioCommand(targetUrl, out var err))
                    {
                        _ = auditLogger.LogAdminActionAsync(username, "UpdateServer", id, JsonSerializer.Serialize(update), false, err);
                        return Results.BadRequest(new { error = err });
                    }
                }
                else if (targetType != "custom")
                {
                    if (!ServerValidationHelper.IsValidServerUrl(targetUrl, httpContext.RequestServices.GetRequiredService<IConfiguration>(), out var err))
                    {
                        _ = auditLogger.LogAdminActionAsync(username, "UpdateServer", id, JsonSerializer.Serialize(update), false, err);
                        return Results.BadRequest(new { error = err });
                    }
                }

                server.Type = targetType;
                server.Url = targetUrl;
                if (update.SecretProvider != null)
                {
                    server.SecretProvider = update.SecretProvider;
                }

                if (update.SecretItemKey != null)
                {
                    server.SecretItemKey = update.SecretItemKey;
                }

                if (!string.IsNullOrEmpty(update.AuthShape))
                {
                    server.AuthShape = update.AuthShape;
                }

                if (update.CustomHeaderName != null)
                {
                    server.CustomHeaderName = update.CustomHeaderName;
                }

                if (update.Categories != null)
                {
                    server.Categories = update.Categories;
                }

                if (!string.IsNullOrWhiteSpace(update.ApiKey))
                {
                    server.ApiKey = update.ApiKey;
                }

                if (update.HeadersJson != null)
                {
                    server.HeadersJson = update.HeadersJson;
                }

                if (update.Alias != null)
                {
                    var existingServers = (await conn.QueryAsync<McpServer>("SELECT * FROM Servers")).ToList();
                    var aliasErr = ServerValidationHelper.ValidateAlias(update.Alias, id, existingServers);
                    if (aliasErr != null)
                    {
                        _ = auditLogger.LogAdminActionAsync(username, "UpdateServer", id, JsonSerializer.Serialize(update), false, aliasErr);
                        return Results.BadRequest(new { error = aliasErr });
                    }
                    server.Alias = string.IsNullOrWhiteSpace(update.Alias) ? null : update.Alias.Trim();
                }

                var catJson = JsonSerializer.Serialize(server.Categories ?? new());
                await conn.ExecuteAsync(@"UPDATE Servers SET Alias = @Alias, DisplayName = @DisplayName, Url = @Url, Enabled = @Enabled, Hidden = @Hidden, Type = @Type,
                    SecretProvider = @SecretProvider, SecretItemKey = @SecretItemKey, AuthShape = @AuthShape, CustomHeaderName = @CustomHeaderName,
                    Categories = @Categories, ApiKey = @ApiKey, HeadersJson = @HeadersJson, AllowPassThroughAuth = @AllowPassThroughAuth, DynamicAuthPrompt = @DynamicAuthPrompt WHERE Id = @Id",
                    new
                    {
                        server.Alias,
                        server.DisplayName,
                        server.Url,
                        Enabled = server.Enabled ? 1 : 0,
                        Hidden = server.Hidden ? 1 : 0,
                        server.Type,
                        server.SecretProvider,
                        server.SecretItemKey,
                        server.AuthShape,
                        server.CustomHeaderName,
                        Categories = catJson,
                        server.ApiKey,
                        AllowPassThroughAuth = server.AllowPassThroughAuth ? 1 : 0,
                        server.DynamicAuthPrompt,
                        server.HeadersJson,
                        server.Id
                    });

                sessionManager.RemoveServerCache(id);
                sessionManager.ResetAll();

                _ = auditLogger.LogAdminActionAsync(username, "UpdateServer", id, JsonSerializer.Serialize(update), true);

                return Results.Ok(server);
            });

            api.MapPost("/api/servers", async ([FromBody] McpServer server, [FromServices] IDbConnectionFactory dbFactory, [FromServices] SessionManager sessionManager, HttpContext httpContext, [FromServices] IAuditLogger auditLogger, ILogger<Program> logger) =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var username = httpContext.User.Identity?.Name ?? "anonymous";
                logger.LogInformation("POST /api/servers started for {url}", server.Url?.Replace(Environment.NewLine, "")?.Replace("\n", "")?.Replace("\r", ""));

                var allowedTypes = new[] { "sse", "http", "streamable", "stdio", "custom" };
                var lowerType = (server.Type ?? "sse").ToLowerInvariant();
                if (!allowedTypes.Contains(lowerType))
                {
                    var typeErr = $"Transport type '{server.Type}' is not supported.";
                    _ = auditLogger.LogAdminActionAsync(username, "CreateServer", server.Id ?? "unknown", JsonSerializer.Serialize(server), false, typeErr);
                    return Results.BadRequest(new { error = typeErr });
                }
                server.Type = lowerType;

                if (server.Type == "stdio")
                {
                    if (!ServerValidationHelper.IsValidStdioCommand(server.Url, out var err))
                    {
                        _ = auditLogger.LogAdminActionAsync(username, "CreateServer", server.Id ?? "unknown", JsonSerializer.Serialize(server), false, err);
                        return Results.BadRequest(new { error = err });
                    }
                }
                else if (server.Type != "custom" && !ServerValidationHelper.IsValidServerUrl(server.Url, httpContext.RequestServices.GetRequiredService<IConfiguration>(), out var err))
                {
                    logger.LogInformation("IsValidServerUrl failed after {ms}ms", sw.ElapsedMilliseconds);
                    _ = auditLogger.LogAdminActionAsync(username, "CreateServer", server.Id ?? "unknown", JsonSerializer.Serialize(server), false, err);
                    return Results.BadRequest(new { error = err });
                }
                logger.LogInformation("IsValidServerUrl passed after {ms}ms", sw.ElapsedMilliseconds);

                if (string.IsNullOrEmpty(server.Id))
                {
                    server.Id = Guid.NewGuid().ToString("N").Substring(0, 8);
                }

                using var conn = dbFactory.CreateConnection();
                var existingServers = (await conn.QueryAsync<McpServer>("SELECT * FROM Servers")).ToList();
                var aliasErr = ServerValidationHelper.ValidateAlias(server.Alias, server.Id, existingServers);
                if (aliasErr != null)
                {
                    _ = auditLogger.LogAdminActionAsync(username, "CreateServer", server.Id, JsonSerializer.Serialize(server), false, aliasErr);
                    return Results.BadRequest(new { error = aliasErr });
                }

                if (!string.IsNullOrWhiteSpace(server.Alias))
                {
                    server.Alias = server.Alias.Trim();
                }
                else
                {
                    server.Alias = null;
                }

                var catJson = JsonSerializer.Serialize(server.Categories ?? new());
                var dbStart = sw.ElapsedMilliseconds;
                await conn.ExecuteAsync(@"INSERT INTO Servers (Id, Alias, DisplayName, Url, Enabled, Hidden, Type, SecretProvider, SecretItemKey, AuthShape, CustomHeaderName, Categories, ApiKey, HeadersJson, AllowPassThroughAuth, DynamicAuthPrompt)
                    VALUES (@Id, @Alias, @DisplayName, @Url, @Enabled, @Hidden, @Type, @SecretProvider, @SecretItemKey, @AuthShape, @CustomHeaderName, @Categories, @ApiKey, @HeadersJson, @AllowPassThroughAuth, @DynamicAuthPrompt)",
                    new
                    {
                        server.Id,
                        server.Alias,
                        server.DisplayName,
                        server.Url,
                        Enabled = server.Enabled ? 1 : 0,
                        Hidden = server.Hidden ? 1 : 0,
                        Type = server.Type ?? "sse",
                        SecretProvider = server.SecretProvider ?? "None",
                        server.SecretItemKey,
                        AuthShape = server.AuthShape ?? "bearer",
                        server.CustomHeaderName,
                        Categories = catJson,
                        server.ApiKey,
                        AllowPassThroughAuth = server.AllowPassThroughAuth ? 1 : 0,
                        server.DynamicAuthPrompt,
                        server.HeadersJson
                    });
                logger.LogInformation("DB Insert finished after {ms}ms", sw.ElapsedMilliseconds - dbStart);

                var resetStart = sw.ElapsedMilliseconds;
                sessionManager.ResetAll();
                logger.LogInformation("ResetAll finished after {ms}ms", sw.ElapsedMilliseconds - resetStart);

                _ = auditLogger.LogAdminActionAsync(username, "CreateServer", server.Id, JsonSerializer.Serialize(server), true);

                return Results.Ok(server);
            });

            api.MapDelete("/api/servers/{id}", async (string id, [FromServices] IDbConnectionFactory dbFactory, [FromServices] SessionManager sessionManager, HttpContext httpContext, [FromServices] IAuditLogger auditLogger) =>
            {
                var username = httpContext.User.Identity?.Name ?? "anonymous";
                using var conn = dbFactory.CreateConnection();
                var server = await conn.QueryFirstOrDefaultAsync<McpServer>("SELECT * FROM Servers WHERE Id = @id", new { id });
                if (server == null)
                {
                    _ = auditLogger.LogAdminActionAsync(username, "DeleteServer", id, "", false, "Server not found");
                    return Results.NotFound();
                }

                await conn.ExecuteAsync("DELETE FROM Servers WHERE Id = @id", new { id });

                sessionManager.RemoveServerCache(id);
                sessionManager.ResetAll();

                _ = auditLogger.LogAdminActionAsync(username, "DeleteServer", id, "", true);

                return Results.Ok(new { success = true });
            });

            api.MapGet("/api/servers/{id}/inspect", async (string id, [FromServices] IDbConnectionFactory dbFactory, [FromServices] SessionManager sessionManager, ILogger<Program> logger, HttpContext httpContext) =>
            {
                using var conn = dbFactory.CreateConnection();
                var server = await conn.QueryFirstOrDefaultAsync<McpServer>("SELECT * FROM Servers WHERE Id = @id", new { id });
                if (server == null)
                {
                    return Results.NotFound(new { error = "Server not found" });
                }

                var sessionId = "inspect-" + id + "-" + Guid.NewGuid().ToString("N")[..8];
                var tools = new List<object>();
                var prompts = new List<object>();
                var resources = new List<object>();

                try
                {
                    var session = await sessionManager.CreateSessionAsync(sessionId, null!, id, false);

                    try
                    {
                        tools = await session.ListToolsAsync("{}", httpContext);
                    }
                    catch (Exception exTools)
                    {
                        logger.LogWarning(exTools, "Failed to list tools for server {ServerId} during inspect", id);
                    }

                    try
                    {
                        prompts = await session.ListPromptsAsync("{}", httpContext);
                    }
                    catch (Exception exPrompts)
                    {
                        logger.LogWarning(exPrompts, "Failed to list prompts for server {ServerId} during inspect", id);
                    }

                    try
                    {
                        resources = await session.ListResourcesAsync("{}", httpContext);
                    }
                    catch (Exception exRes)
                    {
                        logger.LogWarning(exRes, "Failed to list resources for server {ServerId} during inspect", id);
                    }

                    return Results.Ok(new
                    {
                        server = new { id = server.Id, alias = server.Alias, displayName = server.DisplayName, type = server.Type, url = server.Url, enabled = server.Enabled },
                        tools,
                        prompts,
                        resources
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error creating session or connecting to server {ServerId} during inspect", id);
                    return Results.Ok(new
                    {
                        server = new { id = server.Id, alias = server.Alias, displayName = server.DisplayName, type = server.Type, url = server.Url, enabled = server.Enabled },
                        tools = new List<object>(),
                        prompts = new List<object>(),
                        resources = new List<object>(),
                        error = "An unexpected error occurred."
                    });
                }
                finally
                {
                    sessionManager.CloseSession(sessionId);
                }
            });

            return app;
        }
    }
}
