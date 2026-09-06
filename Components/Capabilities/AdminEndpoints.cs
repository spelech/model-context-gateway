using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace ModelContextGateway.Components.Capabilities
{
    public class SetMasterKeyRequest
    {
        public string? NewKey { get; set; }
        public string? MasterKey { get; set; }
        public string? Key { get; set; }
    }

    /// <summary>
    /// Represents an active Admin SSE client session.
    /// </summary>
    public class AdminSseSession
    {
        public string SessionId { get; }
        public HttpResponse Response { get; }
        public string CallerUsername { get; }
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private static readonly JsonSerializerOptions _adminSerializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public AdminSseSession(string sessionId, HttpResponse response, string callerUsername)
        {
            SessionId = sessionId;
            Response = response;
            CallerUsername = callerUsername;
        }

        public async Task WriteMessageAsync(object payload)
        {
            var json = JsonSerializer.Serialize(payload, _adminSerializerOptions);
            await _writeLock.WaitAsync();
            try
            {
                await Response.WriteAsync($"event: message\ndata: {json}\n\n");
                await Response.Body.FlushAsync();
            }
            finally
            {
                _writeLock.Release();
            }
        }
    }

    /// <summary>
    /// Maps and handles dedicated MCP endpoints for administrative operations (/admin, /admin/sse, /admin/message).
    /// </summary>
    public static class AdminEndpoints
    {
        private static readonly ConcurrentDictionary<string, AdminSseSession> _adminSessions = new();
        private static readonly JsonSerializerOptions _adminSerializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static void RegisterSession(AdminSseSession session)
        {
            _adminSessions[session.SessionId] = session;
        }

        public static void UnregisterSession(string sessionId)
        {
            _adminSessions.TryRemove(sessionId, out _);
        }

        public static AdminSseSession? GetSession(string sessionId)
        {
            _adminSessions.TryGetValue(sessionId, out var session);
            return session;
        }

        public static IEndpointRouteBuilder MapAdminMcpEndpoints(this IEndpointRouteBuilder app)
        {
            // 1. /admin (GET/POST/HEAD) guarded by AdminPolicy
            app.MapMethods("/admin", new[] { "GET", "POST", "HEAD" }, HandleAdminSse)
               .RequireAuthorization("AdminPolicy");

            // 2. /admin/sse (GET/POST/HEAD) guarded by AdminPolicy
            app.MapMethods("/admin/sse", new[] { "GET", "POST", "HEAD" }, HandleAdminSse)
               .RequireAuthorization("AdminPolicy");

            // 3. /admin/message (POST) guarded by AdminPolicy
            app.MapPost("/admin/message", HandleAdminMessage)
               .RequireAuthorization("AdminPolicy");

            // 4. /mcg-admin route aliases (GET/POST/HEAD) guarded by AdminPolicy
            app.MapMethods("/mcg-admin", new[] { "GET", "POST", "HEAD" }, HandleAdminSse)
               .RequireAuthorization("AdminPolicy");

            // 5. /mcg-admin/sse (GET/POST/HEAD) guarded by AdminPolicy
            app.MapMethods("/mcg-admin/sse", new[] { "GET", "POST", "HEAD" }, HandleAdminSse)
               .RequireAuthorization("AdminPolicy");

            // 6. /mcg-admin/message (POST) guarded by AdminPolicy
            app.MapPost("/mcg-admin/message", HandleAdminMessage)
               .RequireAuthorization("AdminPolicy");

            // 7. /api/config/master-key (POST) guarded by AdminPolicy
            app.MapPost("/api/config/master-key", HandleSetMasterKey)
               .RequireAuthorization("AdminPolicy");

            return app;
        }

        private static async Task HandleAdminSse(
            HttpContext httpContext,
            [FromServices] AdminMcpServer adminMcpServer,
            [FromServices] CompositeIdentityProvider identityProvider,
            ILogger<Program> logger)
        {
            if (httpContext.Request.Method == "HEAD")
            {
                httpContext.Response.Headers.ContentType = "text/event-stream";
                httpContext.Response.Headers.CacheControl = "no-cache";
                httpContext.Response.Headers.Connection = "keep-alive";
                return;
            }

            var identity = await identityProvider.ResolveIdentityAsync(httpContext);
            var callerUsername = identity?.Username ?? httpContext.User?.Identity?.Name ?? "admin";

            var method = httpContext.Items.TryGetValue("MCP_METHOD", out var mObj) ? mObj as string : null;
            var isNotification = httpContext.Items.TryGetValue("MCP_IS_NOTIFICATION", out var notifObj) && notifObj is bool b && b;
            var rawBody = httpContext.Items.TryGetValue("MCP_RAW_BODY", out var rawObj) ? rawObj as string ?? string.Empty : string.Empty;
            var id = httpContext.Items.TryGetValue("MCP_REQ_ID", out var idObj) ? idObj : null;

            // 1. Direct Streamable HTTP JSON-RPC POST handling
            if (httpContext.Request.Method == "POST" && !string.IsNullOrEmpty(method))
            {
                if (isNotification || method.StartsWith("notifications/", StringComparison.OrdinalIgnoreCase))
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

                    var result = await adminMcpServer.ProcessRequestAsync(jsonRpcReq, callerUsername);
                    httpContext.Response.Headers.ContentType = "application/json";
                    await httpContext.Response.WriteAsJsonAsync(result, _adminSerializerOptions);
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing direct JSON-RPC admin request: {Method}", method);
                    httpContext.Response.Headers.ContentType = "application/json";
                    await httpContext.Response.WriteAsJsonAsync(new
                    {
                        jsonrpc = "2.0",
                        id = id,
                        error = new { code = -32603, message = "An unexpected error occurred." }
                    }, _adminSerializerOptions);
                    return;
                }
            }

            // 2. Stateful SSE Stream Setup (GET /admin/sse)
            var sessionId = Guid.NewGuid().ToString("N");
            logger.LogInformation("New Admin SSE connection ({Method}). SessionId: {SessionId}, User: {User}",
                httpContext.Request.Method, sessionId, System.Net.WebUtility.UrlEncode(callerUsername));

            var scheme = httpContext.Request.Headers["X-Forwarded-Proto"].ToString();
            if (string.IsNullOrEmpty(scheme))
            {
                scheme = httpContext.Request.Scheme;
            }

            var host = httpContext.Request.Headers["X-Forwarded-Host"].ToString();
            if (string.IsNullOrEmpty(host))
            {
                host = httpContext.Request.Host.Value;
            }
            var endpointPrefix = httpContext.Request.Path.Value?.StartsWith("/mcg-admin", StringComparison.OrdinalIgnoreCase) == true ? "/mcg-admin" : "/admin";
            var absoluteUrl = $"{scheme}://{host}{endpointPrefix}/message?sessionId={sessionId}";

            httpContext.Response.Headers.ContentType = "text/event-stream";
            httpContext.Response.Headers.CacheControl = "no-cache";
            httpContext.Response.Headers.Connection = "keep-alive";

            await httpContext.Response.WriteAsync($"event: endpoint\ndata: {absoluteUrl}\n\n");
            await httpContext.Response.Body.FlushAsync();

            var sseSession = new AdminSseSession(sessionId, httpContext.Response, callerUsername ?? "anonymous");
            RegisterSession(sseSession);

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
                logger.LogInformation("Admin SSE connection closed for SessionId: {SessionId}", sessionId);
            }
            finally
            {
                UnregisterSession(sessionId);
            }
        }

        private static async Task<IResult> HandleAdminMessage(
            HttpContext httpContext,
            [FromQuery] string sessionId,
            [FromServices] AdminMcpServer adminMcpServer,
            [FromServices] CompositeIdentityProvider identityProvider,
            ILogger<Program> logger)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return Results.BadRequest(new { error = "Missing required 'sessionId' query parameter." });
            }

            var session = GetSession(sessionId);
            if (session == null)
            {
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(50);
                    session = GetSession(sessionId);
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

            var method = httpContext.Items.TryGetValue("MCP_METHOD", out var mObj) ? mObj as string : null;
            var isNotification = httpContext.Items.TryGetValue("MCP_IS_NOTIFICATION", out var notifObj) && notifObj is bool b && b;
            var rawBody = httpContext.Items.TryGetValue("MCP_RAW_BODY", out var rawObj) ? rawObj as string ?? string.Empty : string.Empty;
            var id = httpContext.Items.TryGetValue("MCP_REQ_ID", out var idObj) ? idObj : null;

            if (string.IsNullOrWhiteSpace(rawBody) && string.IsNullOrEmpty(method))
            {
                using var reader = new StreamReader(httpContext.Request.Body);
                rawBody = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(rawBody) && string.IsNullOrEmpty(method))
            {
                return Results.BadRequest(new { error = "Request body cannot be empty." });
            }

            try
            {
                if (isNotification || method?.StartsWith("notifications/", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return Results.Accepted();
                }

                var identity = await identityProvider.ResolveIdentityAsync(httpContext);
                var callerUsername = identity?.Username ?? session.CallerUsername ?? "admin";

                var jsonRpcReq = !string.IsNullOrWhiteSpace(rawBody)
                    ? JsonSerializer.Deserialize<JsonRpcRequest>(rawBody)
                    : null;

                if (jsonRpcReq?.Method?.StartsWith("notifications/", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return Results.Accepted();
                }

                jsonRpcReq ??= new JsonRpcRequest
                {
                    Method = method ?? string.Empty,
                    Id = id
                };

                var response = await adminMcpServer.ProcessRequestAsync(jsonRpcReq, callerUsername);
                await session.WriteMessageAsync(response);

                return Results.Accepted();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing admin message for sessionId {SessionId}", sessionId);
                return Results.Problem("An unexpected error occurred.");
            }
        }

        private static async Task<IResult> HandleSetMasterKey(
            HttpContext httpContext,
            [FromBody] SetMasterKeyRequest request,
            [FromServices] IMasterKeyManager masterKeyManager,
            [FromServices] IAuditLogger auditLogger,
            [FromServices] CompositeIdentityProvider identityProvider,
            ILogger<Program> logger)
        {
            var identity = await identityProvider.ResolveIdentityAsync(httpContext);
            var callerUsername = identity?.Username ?? httpContext.User?.Identity?.Name ?? "admin";

            var newKey = request?.NewKey ?? request?.MasterKey ?? request?.Key;

            if (string.IsNullOrWhiteSpace(newKey))
            {
                return Results.BadRequest(new { error = "New master key cannot be empty." });
            }

            var trimmedKey = newKey.Trim();

            if (trimmedKey.Length < 16)
            {
                return Results.BadRequest(new { error = "Master key must be at least 16 characters long." });
            }

            if (DbKeyHelper.ActiveKeySource == MasterKeySource.External || DbKeyHelper.ActiveKeySource == MasterKeySource.Vault)
            {
                return Results.BadRequest(new { error = $"Cannot update master key when key source is managed externally ({DbKeyHelper.ActiveKeySource})." });
            }

            try
            {
                await masterKeyManager.ReencryptDatabaseSecretsAsync(trimmedKey);

                await auditLogger.LogAdminActionAsync(
                    callerUsername,
                    "masterkey.reencrypt",
                    "MasterKey",
                    "Re-encrypted database secrets and updated master key.",
                    true);

                return Results.Ok(new
                {
                    success = true,
                    message = "Master encryption key updated and database secrets successfully re-encrypted.",
                    keySource = DbKeyHelper.ActiveKeySource.ToString()
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to re-encrypt database secrets with new master key.");
                await auditLogger.LogAdminActionAsync(
                    callerUsername,
                    "masterkey.reencrypt",
                    "MasterKey",
                    "Failed to re-encrypt database secrets.",
                    false,
                    ex.Message);

                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Database Re-encryption Failed");
            }
        }
    }
}
