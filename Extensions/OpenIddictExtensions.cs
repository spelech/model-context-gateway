using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using OpenIddict.Validation.AspNetCore;

namespace ModelContextGateway.Extensions
{
    public static class OpenIddictExtensions
    {
        public static IServiceCollection AddMcpOpenIddict(
            this IServiceCollection services,
            Microsoft.Extensions.Hosting.IHostEnvironment env,
            Microsoft.Extensions.Configuration.IConfiguration config)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, AppKeyAuthenticationHandler>("AppKey", null)
            .AddScheme<AuthenticationSchemeOptions, OidcHeaderAuthenticationHandler>("OidcHeader", null)
            .AddScheme<AuthenticationSchemeOptions, ModelContextGateway.Middleware.ExternalJwtAuthenticationHandler>("ExternalJwt", null);

            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, "AppKey", "OidcHeader", "ExternalJwt")
                    .Build();

                options.AddPolicy("AdminPolicy", policy =>
                {
                    policy.RequireAssertion(ctx =>
                    {
                        var httpContext = ctx.Resource as Microsoft.AspNetCore.Http.HttpContext;
                        var cfg = httpContext?.RequestServices?.GetService<Microsoft.Extensions.Configuration.IConfiguration>();

                        // Standalone network authorization (when no external IDP is configured)
                        if (httpContext != null && SecurityValidationHelper.IsStandaloneAdminNetwork(httpContext.Connection.RemoteIpAddress, cfg))
                        {
                            if (!SecurityValidationHelper.HasExternalIdp(cfg, httpContext))
                            {
                                return true;
                            }
                        }

                        if (ctx.User?.Identity?.IsAuthenticated != true)
                        {
                            return false;
                        }

                        if (ctx.User.IsInRole("Administrator") || ctx.User.HasClaim("Scope", "admin"))
                        {
                            return true;
                        }

                        var adminSid = cfg?["Admin:GroupSid"] ?? "S-1-5-32-544";

                        var configuredAdminGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var singleGroupName = cfg?["Admin:GroupName"];
                        if (!string.IsNullOrWhiteSpace(singleGroupName))
                        {
                            configuredAdminGroups.Add(singleGroupName.Trim());
                        }
                        else
                        {
                            configuredAdminGroups.Add("full_admin");
                            configuredAdminGroups.Add("Administrator");
                            configuredAdminGroups.Add("Administrators");
                        }

                        var adminGroupsSection = cfg?.GetSection("Admin:Groups")?.Get<string[]>();
                        if (adminGroupsSection != null)
                        {
                            foreach (var g in adminGroupsSection)
                            {
                                if (!string.IsNullOrWhiteSpace(g))
                                {
                                    configuredAdminGroups.Add(g.Trim());
                                }
                            }
                        }

                        return ctx.User.HasClaim("Sid", adminSid) ||
                               ctx.User.Claims.Any(c => c.Type == ClaimTypes.Role && configuredAdminGroups.Contains(c.Value));
                    })
                    .AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, "AppKey", "OidcHeader", "ExternalJwt");
                });
            });

            services.AddOpenIddict()
                .AddServer(options =>
                {
                    options.SetTokenEndpointUris("/connect/token", "/oauth/token");
                    options.SetAuthorizationEndpointUris("/connect/authorize", "/oauth/authorize");
                    options.AllowClientCredentialsFlow();
                    options.AllowAuthorizationCodeFlow();
                    options.AllowRefreshTokenFlow();
                    options.EnableDegradedMode();
                    options.RegisterScopes(
                        OpenIddict.Abstractions.OpenIddictConstants.Scopes.OpenId,
                        OpenIddict.Abstractions.OpenIddictConstants.Scopes.Profile,
                        OpenIddict.Abstractions.OpenIddictConstants.Scopes.Email,
                        OpenIddict.Abstractions.OpenIddictConstants.Scopes.OfflineAccess,
                        "api",
                        "mcp_client",
                        "tools:execute",
                        "resources:read",
                        "prompts:read"
                    );
                    options.DisableScopeValidation();
                    options.DisableResourceValidation();

                    var certPath = config["MCG_JWT_CERT_PATH"]
                        ?? config["MCG_OPENIDDICT_CERT_PATH"]
                        ?? config["OpenIddict:CertificatePath"]
                        ?? Environment.GetEnvironmentVariable("MCG_JWT_CERT_PATH")
                        ?? Environment.GetEnvironmentVariable("MCG_OPENIDDICT_CERT_PATH")
                        ?? Environment.GetEnvironmentVariable("OPENIDDICT_CERT_PATH");
                    var certPass = config["MCG_JWT_CERT_PASSWORD"]
                        ?? config["MCG_OPENIDDICT_CERT_PASSWORD"]
                        ?? config["OpenIddict:CertificatePassword"]
                        ?? Environment.GetEnvironmentVariable("MCG_JWT_CERT_PASSWORD")
                        ?? Environment.GetEnvironmentVariable("MCG_OPENIDDICT_CERT_PASSWORD")
                        ?? Environment.GetEnvironmentVariable("OPENIDDICT_CERT_PASSWORD");

                    var autoCert = config["OpenIddict:AutoGenerateCertificate"]
                        ?? config["MCG_AUTO_CERT"]
                        ?? Environment.GetEnvironmentVariable("OPENIDDICT_AUTO_CERT")
                        ?? Environment.GetEnvironmentVariable("MCG_AUTO_CERT");
                    var useDevCerts = config["OpenIddict:UseDevelopmentCertificate"]
                        ?? Environment.GetEnvironmentVariable("OPENIDDICT_DEV_CERTS");

                    if (!string.IsNullOrEmpty(certPath) && System.IO.File.Exists(certPath))
                    {
                        var cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(
                            certPath, certPass, System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.MachineKeySet);
                        options.AddSigningCertificate(cert).AddEncryptionCertificate(cert);
                    }
                    else if (string.Equals(autoCert, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        string dataDir = DbKeyHelper.ResolveDataDirectory(config);
                        string autoPfxPath = System.IO.Path.Combine(dataDir, ".openiddict.pfx");

                        if (System.IO.File.Exists(autoPfxPath))
                        {
                            try
                            {
                                var cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(
                                    autoPfxPath, null, System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.MachineKeySet);
                                options.AddSigningCertificate(cert).AddEncryptionCertificate(cert);
                            }
                            catch
                            {
                                options.AddDevelopmentEncryptionCertificate()
                                       .AddDevelopmentSigningCertificate();
                            }
                        }
                        else
                        {
                            try
                            {
                                System.IO.Directory.CreateDirectory(dataDir);
                                using var rsa = System.Security.Cryptography.RSA.Create(2048);
                                var certReq = new System.Security.Cryptography.X509Certificates.CertificateRequest(
                                    "CN=mcg-standalone", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
                                var selfSignedCert = certReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
                                var pfxBytes = selfSignedCert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx);
                                System.IO.File.WriteAllBytes(autoPfxPath, pfxBytes);
                                if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                                {
                                    try { System.IO.File.SetUnixFileMode(autoPfxPath, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch { }
                                }
                                options.AddSigningCertificate(selfSignedCert).AddEncryptionCertificate(selfSignedCert);
                            }
                            catch
                            {
                                options.AddDevelopmentEncryptionCertificate()
                                       .AddDevelopmentSigningCertificate();
                            }
                        }
                    }
                    else if (env.EnvironmentName == Environments.Production && !string.Equals(useDevCerts, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("OpenIddict:CertificatePath must be configured in Production environment.");
                    }
                    else
                    {
                        options.AddDevelopmentEncryptionCertificate()
                               .AddDevelopmentSigningCertificate();
                    }

                    options.UseAspNetCore()
                           .EnableTokenEndpointPassthrough()
                           .EnableAuthorizationEndpointPassthrough()
                           .DisableTransportSecurityRequirement();

                    options.AddEventHandler<OpenIddict.Server.OpenIddictServerEvents.ValidateAuthorizationRequestContext>(builder =>
                        builder.UseInlineHandler(context => default));

                    options.AddEventHandler<OpenIddict.Server.OpenIddictServerEvents.ValidateTokenRequestContext>(builder =>
                        builder.UseInlineHandler(context => default));

                    options.AddEventHandler<OpenIddict.Server.OpenIddictServerEvents.ApplyConfigurationResponseContext>(builder =>
                        builder.UseInlineHandler(context =>
                        {
                            var issuer = ((string?)context.Response.GetParameter("issuer"))?.TrimEnd('/') ?? "";
                            if (!string.IsNullOrEmpty(issuer))
                            {
                                context.Response.SetParameter("registration_endpoint", $"{issuer}/api/register");
                            }
                            return default;
                        }));
                })
                .AddValidation(options =>
                {
                    options.UseLocalServer();
                    options.UseAspNetCore();
                });

            return services;
        }
    }
}
