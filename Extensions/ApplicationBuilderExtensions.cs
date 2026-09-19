namespace ModelContextGateway.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static void ConfigureModelContextGatewayPipeline(this WebApplication app)
        {
            var config = app.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ModelContextGateway.Startup");

            var requireTrusted = config.GetValue<bool>("Oidc:RequireTrustedProxy", true);
            if (!requireTrusted)
            {
                logger.LogWarning("SECURITY WARNING: Oidc:RequireTrustedProxy is set to false! This is dangerous in production as headers like Remote-User can be spoofed by any internal or external client.");
            }

            var trustedProxies = config["Oidc:TrustedProxies"];
            if (string.IsNullOrWhiteSpace(trustedProxies))
            {
                logger.LogWarning("Notice: Oidc:TrustedProxies is unset. Header-based authentication (reverse proxy auth) will trust loopback-only (127.0.0.1 / ::1).");
            }

            if (!SecurityValidationHelper.HasExternalIdp(config))
            {
                var standaloneNetworks = config.GetSection("Admin:StandaloneAllowedNetworks").Get<string[]>();
                var networksList = standaloneNetworks != null && standaloneNetworks.Length > 0
                    ? string.Join(", ", standaloneNetworks)
                    : (config["Admin:StandaloneAllowedNetworks"] ?? "127.0.0.1, ::1");

            }

            var forwardedHeadersOptions = new ForwardedHeadersOptions
            {
                ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.All
            };
            forwardedHeadersOptions.KnownIPNetworks.Clear();
            forwardedHeadersOptions.KnownProxies.Clear();
            app.UseForwardedHeaders(forwardedHeadersOptions);

            // Comprehensive HTTP Request Logging Middleware (logs method, path, status code, duration, IP, user-agent)
            app.Use(async (context, next) =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var reqLogger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                var ip = context.Request.Headers.TryGetValue("X-Forwarded-For", out var fwd) ? fwd.ToString() : context.Connection.RemoteIpAddress?.ToString();
                var userAgent = context.Request.Headers.UserAgent.ToString();

                try
                {
                    await next();
                }
                finally
                {
                    sw.Stop();
                    var status = context.Response.StatusCode;
                    var authScheme = context.Request.Headers.Authorization.FirstOrDefault()?.Split(' ').FirstOrDefault() ?? "None";
                    reqLogger.LogInformation("HTTP {Method} {Path}{Query} -> {StatusCode} in {ElapsedMs:0.0}ms [IP: {Ip}, Auth: {AuthScheme}, UA: {UserAgent}]",
                        context.Request.Method,
                        context.Request.Path,
                        context.Request.QueryString.HasValue ? context.Request.QueryString.Value : "",
                        status,
                        sw.Elapsed.TotalMilliseconds,
                        ip,
                        authScheme,
                        userAgent);
                }
            });

            app.UseCors();

            // Extract token from query parameters for SSE / WebSocket bypass support
            app.Use(async (context, next) =>
            {
                if (string.IsNullOrEmpty(context.Request.Headers.Authorization))
                {
                    if (context.Request.Query.TryGetValue("access_token", out var accessToken) && !string.IsNullOrEmpty(accessToken))
                    {
                        context.Request.Headers.Authorization = $"Bearer {accessToken}";
                    }
                    else if (context.Request.Query.TryGetValue("token", out var token) && !string.IsNullOrEmpty(token))
                    {
                        context.Request.Headers.Authorization = $"Bearer {token}";
                    }
                }
                await next();
            });

            app.UseMiddleware<ModelContextGateway.Middleware.McpAuthorizationSpecMiddleware>();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseMiddleware<ModelContextGateway.Middleware.McpSpecMiddleware>();
            app.MapControllers();

            app.UseDefaultFiles();
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                    ctx.Context.Response.Headers.Append("Pragma", "no-cache");
                    ctx.Context.Response.Headers.Append("Expires", "0");
                }
            }); // Serves dashboard files from wwwroot with no-cache headers

            app.SeedDatabase();

            // ----------------------------------------------------
            // SYSTEM/HEALTH ENDPOINTS
            // ----------------------------------------------------
            app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = GatewayMetadata.DefaultName, version = GatewayMetadata.Version }));
            app.MapGet("/api/config/branding", async (ModelContextGateway.Infrastructure.Persistence.ISettingRepository settingsRepo) =>
            {
                var settings = await settingsRepo.GetSettingsAsync() ?? new ModelContextGateway.Models.RouterSettings();
                return Results.Ok(new
                {
                    title = settings.DashboardTitle,
                    icon = settings.DashboardIcon
                });
            });
            app.MapGet("/api/config/branding/logo", () =>
            {
                var dir = Path.Combine(AppContext.BaseDirectory, "data", "branding");
                if (!Directory.Exists(dir))
                {
                    return Results.NotFound();
                }

                var file = Directory.GetFiles(dir, "logo.*").FirstOrDefault();
                if (file == null)
                {
                    return Results.NotFound();
                }

                var ext = Path.GetExtension(file).ToLowerInvariant();
                var contentType = ext switch
                {
                    ".svg" => "image/svg+xml",
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".webp" => "image/webp",
                    ".ico" => "image/x-icon",
                    _ => "application/octet-stream"
                };
                return Results.File(file, contentType, enableRangeProcessing: true);
            });

            // ----------------------------------------------------
            // OAUTH & OIDC PROTECTED RESOURCE DISCOVERY (RFC 9728)
            // ----------------------------------------------------
            app.MapGet("/.well-known/oauth-protected-resource", async (HttpContext context, Microsoft.Extensions.Configuration.IConfiguration config, IServiceProvider sp) =>
            {
                var canonicalUrl = ModelContextGateway.Components.Authorization.ProtectedResourceHelper.GetCanonicalUrl(context, config);
                var requestedResource = context.Request.Query["resource"].FirstOrDefault();
                var resourceUri = !string.IsNullOrEmpty(requestedResource)
                    ? requestedResource
                    : canonicalUrl;

                var authRepo = sp.GetService<ModelContextGateway.Infrastructure.Persistence.IAuthProviderRepository>();
                var authServers = await ModelContextGateway.Components.Authorization.ProtectedResourceHelper.GetAuthorizationServersAsync(context, config, authRepo);

                return Results.Json(new ModelContextGateway.Components.Authorization.ProtectedResourceMetadata
                {
                    Resource = resourceUri,
                    AuthorizationServers = authServers,
                    ScopesSupported = ModelContextGateway.Components.Authorization.ProtectedResourceHelper.DefaultScopesSupported,
                    BearerMethodsSupported = ModelContextGateway.Components.Authorization.ProtectedResourceHelper.DefaultBearerMethodsSupported,
                    ResourceDocumentation = $"{canonicalUrl}/docs"
                });
            });

            app.MapGet("/.well-known/oauth-protected-resource/{**path}", async (HttpContext context, string path, Microsoft.Extensions.Configuration.IConfiguration config, IServiceProvider sp) =>
            {
                var canonicalUrl = ModelContextGateway.Components.Authorization.ProtectedResourceHelper.GetCanonicalUrl(context, config);
                var resourceUri = $"{canonicalUrl}/{path.TrimStart('/')}";

                var authRepo = sp.GetService<ModelContextGateway.Infrastructure.Persistence.IAuthProviderRepository>();
                var authServers = await ModelContextGateway.Components.Authorization.ProtectedResourceHelper.GetAuthorizationServersAsync(context, config, authRepo);

                return Results.Json(new ModelContextGateway.Components.Authorization.ProtectedResourceMetadata
                {
                    Resource = resourceUri,
                    AuthorizationServers = authServers,
                    ScopesSupported = ModelContextGateway.Components.Authorization.ProtectedResourceHelper.DefaultScopesSupported,
                    BearerMethodsSupported = ModelContextGateway.Components.Authorization.ProtectedResourceHelper.DefaultBearerMethodsSupported,
                    ResourceDocumentation = $"{canonicalUrl}/docs"
                });
            });

            // Feature-focused endpoint composition
            app.MapProxyEndpoints();
            app.MapAdminMcpEndpoints();
            app.MapCapabilityEndpoints();
            app.MapServerEndpoints();
            app.MapClientEndpoints();
            app.MapAppKeyEndpoints();
            app.MapProviderEndpoints();
            app.MapPolicyEndpoints();
            app.MapFallbackToFile("index.html");
        }

        // Backwards compatibility endpoint forwarder
        public static void MapAdminEndpoints(this WebApplication app)
        {
            app.MapAdminMcpEndpoints();
            app.MapCapabilityEndpoints();
            app.MapServerEndpoints();
            app.MapClientEndpoints();
            app.MapAppKeyEndpoints();
            app.MapProviderEndpoints();
            app.MapPolicyEndpoints();
        }
    }
}
