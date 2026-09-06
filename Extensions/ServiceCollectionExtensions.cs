namespace ModelContextGateway.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static void AddModelContextGatewayServices(this WebApplicationBuilder builder)
        {
            // Add logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();
            builder.Logging.AddProvider(new InMemoryLoggerProvider());

            var logLevelStr = builder.Configuration["MCG_LOG_LEVEL"]
                ?? builder.Configuration["LOG_LEVEL"]
                ?? Environment.GetEnvironmentVariable("MCG_LOG_LEVEL")
                ?? Environment.GetEnvironmentVariable("LOG_LEVEL");
            if (!string.IsNullOrEmpty(logLevelStr) && Enum.TryParse<LogLevel>(logLevelStr, true, out var minLevel))
            {
                builder.Logging.SetMinimumLevel(minLevel);
            }

            // Decorate Console and Debug providers with SanitizingLoggerProvider to sanitize all logs
            for (int i = 0; i < builder.Services.Count; i++)
            {
                var sd = builder.Services[i];
                if (sd.ServiceType == typeof(ILoggerProvider))
                {
                    var implType = sd.ImplementationType;
                    var implInstance = sd.ImplementationInstance;
                    var implFactory = sd.ImplementationFactory;

                    bool isInMemory = false;
                    if (implType != null && implType.Name.Contains("InMemoryLoggerProvider"))
                    {
                        isInMemory = true;
                    }

                    if (implInstance != null && implInstance.GetType().Name.Contains("InMemoryLoggerProvider"))
                    {
                        isInMemory = true;
                    }

                    if (isInMemory)
                    {
                        continue;
                    }

                    if (implInstance != null)
                    {
                        var provider = (ILoggerProvider)implInstance;
                        builder.Services[i] = ServiceDescriptor.Singleton<ILoggerProvider>(sp => new SanitizingLoggerProvider(provider));
                    }
                    else if (implType != null)
                    {
                        builder.Services[i] = ServiceDescriptor.Singleton<ILoggerProvider>(sp =>
                        {
                            var original = (ILoggerProvider)ActivatorUtilities.CreateInstance(sp, implType);
                            return new SanitizingLoggerProvider(original);
                        });
                    }
                    else if (implFactory != null)
                    {
                        builder.Services[i] = ServiceDescriptor.Singleton<ILoggerProvider>(sp =>
                        {
                            var original = (ILoggerProvider)implFactory(sp);
                            return new SanitizingLoggerProvider(original);
                        });
                    }
                }
            }

            // Register Multi-Database Provider Factory (Pure Dapper)
            builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();

            // Register Aligned Repositories (ISettingRepository, IServerRepository, IAppKeyRepository, ISecretProviderRepository, IAuthProviderRepository)
            builder.Services.AddSingleton<DatabaseRepository>(sp =>
                new DatabaseRepository(
                    sp.GetRequiredService<IDbConnectionFactory>(),
                    sp.GetService<IConfiguration>()
                ));
            builder.Services.AddSingleton<ISettingRepository>(sp => sp.GetRequiredService<DatabaseRepository>());
            builder.Services.AddSingleton<IServerRepository>(sp => sp.GetRequiredService<DatabaseRepository>());
            builder.Services.AddSingleton<IAppKeyRepository>(sp => sp.GetRequiredService<DatabaseRepository>());
            builder.Services.AddSingleton<ISecretProviderRepository>(sp => sp.GetRequiredService<DatabaseRepository>());
            builder.Services.AddSingleton<IAuthProviderRepository>(sp => sp.GetRequiredService<DatabaseRepository>());
            builder.Services.AddSingleton<IUserCredentialRepository>(sp => sp.GetRequiredService<DatabaseRepository>());
            builder.Services.AddSingleton<IUserQuotaRepository>(sp => sp.GetRequiredService<DatabaseRepository>());
            builder.Services.AddSingleton<IOAuthClientRepository>(sp => sp.GetRequiredService<DatabaseRepository>());
            builder.Services.AddSingleton<IMasterKeyManager>(sp => sp.GetRequiredService<DatabaseRepository>());

            // Register Credential Service
            builder.Services.AddSingleton<ICredentialService, CredentialService>();

            // Register Pluggable Identity Providers (Active Directory & Configurable Header Auth)
            builder.Services.AddSingleton<ILdapService>(sp =>
                new LdapActiveDirectoryService(
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<ILogger<LdapActiveDirectoryService>>(),
                    sp.GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
                    sp.GetService<IAuthProviderRepository>()
                ));
            // App-key requests carry no AD/OIDC headers; resolve their owner+SID first so audit rows are attributable.
            builder.Services.AddSingleton<IIdentityProvider, AppKeyIdentityProvider>();
            builder.Services.AddSingleton<IIdentityProvider>(sp =>
                new ActiveDirectoryIdentityProvider(
                    sp.GetService<IConfiguration>(),
                    sp.GetService<ILdapService>(),
                    sp.GetService<IAuthProviderRepository>()
                ));
            builder.Services.AddSingleton<IIdentityProvider>(sp =>
                new HeaderIdentityProvider(
                    sp.GetService<IConfiguration>(),
                    sp.GetService<IAuthProviderRepository>()
                ));
            builder.Services.AddSingleton<IIdentityProvider>(sp =>
                new OidcIdentityProvider(
                    sp.GetService<IConfiguration>(),
                    sp.GetService<IAuthProviderRepository>()
                ));
            builder.Services.AddSingleton<CompositeIdentityProvider>();

            // Register HttpContextAccessor and Secret Retrievers (HashiCorp Vault, Windows Registry, Environment & OAuth2 Token Exchange)
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<ISecretRetriever>(sp =>
                new VaultSecretRetriever(
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
                    sp.GetService<ISecretProviderRepository>()
                ));
            builder.Services.AddSingleton<ISecretRetriever, WindowsRegistrySecretRetriever>();
            builder.Services.AddSingleton<ISecretRetriever, EnvironmentSecretRetriever>();
            builder.Services.AddSingleton<ISecretRetriever>(sp =>
                new TokenExchangeSecretRetriever(
                    sp.GetRequiredService<IHttpClientFactory>(),
                    sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
                    sp.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
                    sp.GetService<ISecretProviderRepository>(),
                    sp.GetService<IAuthProviderRepository>(),
                    sp.GetService<IConfiguration>(),
                    sp.GetService<ILogger<TokenExchangeSecretRetriever>>()
                ));
            builder.Services.AddSingleton<CompositeSecretRetriever>();
            builder.Services.AddSingleton<ModelContextGateway.Infrastructure.Secrets.IUserSecretStore, ModelContextGateway.Infrastructure.Secrets.DatabaseUserSecretStore>();

            // Register Observability & Audit Logger
            builder.Services.AddSingleton<IAuditLogger, AuditLogger>();

            // Register OpenIddict & Controllers
            builder.Services.AddMcpOpenIddict(builder.Environment, builder.Configuration);
            builder.Services.AddControllers();

            builder.Services.AddHttpClient("McpClient");
            builder.Services.ConfigureAll<Microsoft.Extensions.Http.HttpClientFactoryOptions>(options =>
            {
                options.HttpMessageHandlerBuilderActions.Add(b =>
                {
                    var configuration = b.Services.GetRequiredService<IConfiguration>();
                    var allowedIpRanges = configuration.GetSection("Security:AllowedIpRanges").Get<string[]>() ?? Array.Empty<string>();

                    b.PrimaryHandler = new SocketsHttpHandler
                    {
                        AllowAutoRedirect = false,
                        ConnectCallback = (context, cancellationToken) => SecurityValidationHelper.ValidatingConnectCallback(context, allowedIpRanges, cancellationToken)
                    };
                });
            });
            builder.Services.AddSingleton<SessionManager>();

            // Register Docker Auto-Discovery Service
            builder.Services.AddHostedService<DockerAutoDiscoveryService>();

            // Register Backend Health Check Service
            builder.Services.AddSingleton<BackendHealthCheckService>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<BackendHealthCheckService>());

            // Register Dynamic Embedding Service (handles settings in encrypted DB)
            builder.Services.AddSingleton<DynamicEmbeddingService>();
            builder.Services.AddSingleton<IEmbeddingService>(sp => sp.GetRequiredService<DynamicEmbeddingService>());

            // Register In-Process Virtual Admin MCP Server
            builder.Services.AddSingleton<AdminMcpServer>(sp => new AdminMcpServer(
                sp.GetRequiredService<IServerRepository>(),
                sp.GetRequiredService<IAppKeyRepository>(),
                sp.GetRequiredService<ISecretProviderRepository>(),
                sp.GetRequiredService<IAuthProviderRepository>(),
                sp.GetRequiredService<ISettingRepository>(),
                sp.GetRequiredService<IDbConnectionFactory>(),
                sp.GetRequiredService<IAuditLogger>(),
                sp.GetRequiredService<ICredentialService>(),
                sp.GetRequiredService<BackendHealthCheckService>(),
                sp.GetRequiredService<DynamicEmbeddingService>(),
                sp.GetRequiredService<SessionManager>(),
                sp.GetService<ILdapService>(),
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("McpClient"),
                sp.GetService<IConfiguration>(),
                sp.GetService<ILogger<AdminMcpServer>>(),
                sp.GetService<IMasterKeyManager>(),
                sp.GetService<CompositeSecretRetriever>() ?? (ISecretRetriever?)sp.GetService<ISecretRetriever>()
            ));

            // Configure CORS
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    var allowedOriginsValue = builder.Configuration["MCG_CORS_ALLOWED_ORIGINS"]
                        ?? builder.Configuration["CORS_ALLOWED_ORIGINS"]
                        ?? builder.Configuration["AllowedOrigins"]
                        ?? Environment.GetEnvironmentVariable("MCG_CORS_ALLOWED_ORIGINS")
                        ?? Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");

                    var validOrigins = new List<string>();

                    if (!string.IsNullOrWhiteSpace(allowedOriginsValue))
                    {
                        var rawOrigins = allowedOriginsValue
                            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => o.Trim())
                            .Where(o => !string.IsNullOrEmpty(o));

                        foreach (var rawOrigin in rawOrigins)
                        {
                            if (rawOrigin == "*")
                            {
                                LogBuffer.Add(LogLevel.Warning, "ModelContextGateway.CORS", "Wildcard origin '*' is not allowed in MCG_CORS_ALLOWED_ORIGINS and was rejected.", null);
                                continue;
                            }

                            if (Uri.TryCreate(rawOrigin, UriKind.Absolute, out var uri)
                                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                                && uri.GetLeftPart(UriPartial.Authority) == rawOrigin)
                            {
                                validOrigins.Add(rawOrigin);
                            }
                            else
                            {
                                LogBuffer.Add(LogLevel.Warning, "ModelContextGateway.CORS", $"Invalid CORS origin '{rawOrigin}' ignored. Origins must be well-formed HTTP/HTTPS URIs without paths or wildcards.", null);
                            }
                        }
                    }

                    if (validOrigins.Count > 0)
                    {
                        policy.WithOrigins(validOrigins.ToArray())
                              .AllowAnyMethod()
                              .AllowAnyHeader()
                              .AllowCredentials();
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(allowedOriginsValue))
                        {
                            LogBuffer.Add(LogLevel.Warning, "ModelContextGateway.CORS", "No valid CORS origins supplied in configuration. Falling back to default environment CORS configuration.", null);
                        }

                        if (builder.Environment.EnvironmentName == "Development" || builder.Environment.EnvironmentName == "Dev")
                        {
                            policy.WithOrigins("http://localhost:3000", "http://localhost:5000", "https://localhost:5001")
                                  .AllowAnyMethod()
                                  .AllowAnyHeader()
                                  .AllowCredentials();
                        }
                        else
                        {
                            policy.WithOrigins("https://invalid-origin.local");
                        }
                    }
                });
            });
        }
    }
}
