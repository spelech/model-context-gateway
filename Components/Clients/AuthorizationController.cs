using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelContextGateway.Components.Activity;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace ModelContextGateway.Components.Clients
{
    public class AuthorizationController : Controller
    {
        private readonly IOpenIddictApplicationManager? _applicationManager;
        private readonly IAuditLogger _auditLogger;
        private readonly IOAuthClientRepository? _oauthClientRepo;
        private readonly IActivityTracker? _activityTracker;

        [ActivatorUtilitiesConstructor]
        public AuthorizationController(
            IOAuthClientRepository oauthClientRepo,
            IAuditLogger auditLogger,
            IOpenIddictApplicationManager? applicationManager = null,
            IActivityTracker? activityTracker = null)
        {
            _oauthClientRepo = oauthClientRepo;
            _auditLogger = auditLogger;
            _applicationManager = applicationManager;
            _activityTracker = activityTracker;
        }

        public AuthorizationController(IOpenIddictApplicationManager applicationManager, IAuditLogger auditLogger)
            : this(null!, auditLogger, applicationManager, null)
        {
        }

        private async Task<object?> FindApplicationAsync(string clientId)
        {
            if (_applicationManager != null)
            {
                return await _applicationManager.FindByClientIdAsync(clientId);
            }
            if (_oauthClientRepo != null)
            {
                return await _oauthClientRepo.GetOAuthClientByIdAsync(clientId);
            }
            return null;
        }

        private async Task<string?> GetDisplayNameAsync(object application)
        {
            if (_applicationManager != null && !(application is OAuthClient))
            {
                return await _applicationManager.GetDisplayNameAsync(application);
            }
            if (application is OAuthClient client)
            {
                return client.ClientName;
            }
            return null;
        }

        [HttpPost("~/connect/token")]
        [HttpPost("~/oauth/token")]
        [Produces("application/json")]
        public async Task<IActionResult> Exchange()
        {
            var request = HttpContext.GetOpenIddictServerRequest() ??
                throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

            if (request.IsClientCredentialsGrantType())
            {
                var application = await FindApplicationAsync(request.ClientId!);
                if (application == null)
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new Microsoft.AspNetCore.Authentication.AuthenticationProperties(new Dictionary<string, string?>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidClient,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The client application was not found in the directory."
                        }));
                }

                if (application is OAuthClient client)
                {
                    var isPublic = string.Equals(client.ClientType, "public", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(client.ClientSecretHash);
                    if (isPublic)
                    {
                        return Forbid(
                            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                            properties: new Microsoft.AspNetCore.Authentication.AuthenticationProperties(new Dictionary<string, string?>
                            {
                                [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.UnauthorizedClient,
                                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "Public clients are not allowed to use the client_credentials grant type."
                            }));
                    }

                    if (client.ExpiresAt.HasValue && client.ExpiresAt.Value < DateTime.UtcNow)
                    {
                        return Forbid(
                            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                            properties: new Microsoft.AspNetCore.Authentication.AuthenticationProperties(new Dictionary<string, string?>
                            {
                                [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidClient,
                                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The client application credentials have expired."
                            }));
                    }

                    if (!string.IsNullOrEmpty(client.ClientSecretHash))
                    {
                        var providedSecret = request.ClientSecret;
                        if (string.IsNullOrEmpty(providedSecret))
                        {
                            return Forbid(
                                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                                properties: new Microsoft.AspNetCore.Authentication.AuthenticationProperties(new Dictionary<string, string?>
                                {
                                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidClient,
                                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The client secret is invalid."
                                }));
                        }

                        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(providedSecret))).ToLowerInvariant();
                        if (!CryptographicOperations.FixedTimeEquals(
                            Encoding.UTF8.GetBytes(hash),
                            Encoding.UTF8.GetBytes(client.ClientSecretHash.ToLowerInvariant())))
                        {
                            return Forbid(
                                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                                properties: new Microsoft.AspNetCore.Authentication.AuthenticationProperties(new Dictionary<string, string?>
                                {
                                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidClient,
                                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The client secret is invalid."
                                }));
                        }
                    }

                    _ = _activityTracker?.RecordClientUsageAsync(client.ClientId);
                }

                var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

                // Subject (sub) is a required claim
                identity.AddClaim(OpenIddictConstants.Claims.Subject, request.ClientId!);
                identity.AddClaim(OpenIddictConstants.Claims.Name, await GetDisplayNameAsync(application) ?? request.ClientId!);

                var requestedScopes = request.GetScopes();
                if (requestedScopes.Any())
                {
                    identity.SetScopes(requestedScopes);
                }

                identity.SetDestinations(static claim => new[] { OpenIddictConstants.Destinations.AccessToken });

                return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
            {
                var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                if (!result.Succeeded || result.Principal == null)
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new Microsoft.AspNetCore.Authentication.AuthenticationProperties(new Dictionary<string, string?>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The token is no longer valid."
                        }));
                }

                var identity = new ClaimsIdentity(result.Principal.Claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                var scopes = result.Principal.GetScopes();
                if (scopes.Any())
                {
                    identity.SetScopes(scopes);
                }
                var resources = result.Principal.GetResources();
                if (resources.Any())
                {
                    identity.SetResources(resources);
                }

                identity.SetDestinations(static claim => claim.Type switch
                {
                    OpenIddictConstants.Claims.Subject or OpenIddictConstants.Claims.Name or OpenIddictConstants.Claims.Email or ClaimTypes.Name or ClaimTypes.Email =>
                        new[] { OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken },
                    _ => new[] { OpenIddictConstants.Destinations.AccessToken }
                });

                return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            throw new NotImplementedException("The specified grant type is not implemented.");
        }

        [HttpGet("~/connect/authorize")]
        [HttpPost("~/connect/authorize")]
        [HttpGet("~/oauth/authorize")]
        [HttpPost("~/oauth/authorize")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Authorize()
        {
            var request = HttpContext.GetOpenIddictServerRequest() ??
                throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

            // Attempt to authenticate the user using either OIDC Headers, AppKey, or standard auth
            var result = await HttpContext.AuthenticateAsync("OidcHeader");
            if (!result.Succeeded || result.Principal == null)
            {
                result = await HttpContext.AuthenticateAsync("AppKey");
            }
            if (!result.Succeeded || result.Principal == null)
            {
                result = await HttpContext.AuthenticateAsync();
            }

            var principal = (result.Succeeded && result.Principal != null) ? result.Principal : User;
            var config = HttpContext.RequestServices.GetService<Microsoft.Extensions.Configuration.IConfiguration>();

            if (principal?.Identity?.IsAuthenticated != true)
            {
                if (config != null && SecurityValidationHelper.IsStandaloneAdminNetwork(HttpContext.Connection.RemoteIpAddress, config))
                {
                    if (!SecurityValidationHelper.HasExternalIdp(config, HttpContext))
                    {
                        var standaloneIdentity = new ClaimsIdentity("Standalone");
                        standaloneIdentity.AddClaim(new Claim(ClaimTypes.Name, "admin"));
                        standaloneIdentity.AddClaim(new Claim(ClaimTypes.Role, "Administrator"));
                        principal = new ClaimsPrincipal(standaloneIdentity);
                    }
                }
            }

            if (principal?.Identity?.IsAuthenticated != true)
            {
                return Content("<html><body><div style='font-family:sans-serif; text-align:center; padding-top: 50px;'><h1>Access Denied</h1><p>Please authenticate through your SSO portal or provide valid proxy headers to access this consent screen.</p></div></body></html>", "text/html");
            }

            var application = await FindApplicationAsync(request.ClientId!);
            if (application == null)
            {
                return BadRequest(new { error = "invalid_client", error_description = "The client application was not found." });
            }

            var clientName = await GetDisplayNameAsync(application) ?? request.ClientId!;
            var username = principal.Identity?.Name ?? "Unknown";

            // Handle Form Post (Accept/Deny)
            if (HttpMethods.IsPost(HttpContext.Request.Method))
            {
                var form = await HttpContext.Request.ReadFormAsync();
                if (form.ContainsKey("submit.Deny"))
                {
                    return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                }

                // Accept
                var claims = principal.Claims.ToList();
                if (!claims.Any(c => c.Type == OpenIddictConstants.Claims.Subject))
                {
                    claims.Add(new Claim(OpenIddictConstants.Claims.Subject, username));
                }
                var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                var requestedScopes = request.GetScopes();
                if (requestedScopes.Any())
                {
                    identity.SetScopes(requestedScopes);
                }
                var requestedResources = request.GetResources();
                if (requestedResources.Any())
                {
                    identity.SetResources(requestedResources);
                }

                identity.SetDestinations(static claim => claim.Type switch
                {
                    OpenIddictConstants.Claims.Subject or OpenIddictConstants.Claims.Name or OpenIddictConstants.Claims.Email or ClaimTypes.Name or ClaimTypes.Email =>
                        new[] { OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken },
                    _ => new[] { OpenIddictConstants.Destinations.AccessToken }
                });

                return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            // Redirect to React App (GET)
            var qs = HttpContext.Request.QueryString.HasValue ? HttpContext.Request.QueryString.Value : "";
            if (!qs.Contains("client_name="))
            {
                var encodedName = Uri.EscapeDataString(clientName);
                qs = string.IsNullOrEmpty(qs) ? $"?client_name={encodedName}" : $"{qs}&client_name={encodedName}";
            }
            return Redirect($"/consent{qs}");
        }

        [HttpPost("~/api/register")]
        [HttpPost("~/connect/register")]
        [HttpPost("~/oauth/register")]
        [HttpPost("~/register")]
        [Produces("application/json")]
        public async Task<IActionResult> RegisterClient([FromBody] JsonElement metadata)
        {
            var embeddingService = HttpContext.RequestServices.GetService<ModelContextGateway.Core.Routing.DynamicEmbeddingService>();
            var settings = embeddingService?.GetSettings();

            if (settings != null && !settings.AllowOpenClientRegistration)
            {
                var authService = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
                var authResult = await authService.AuthorizeAsync(User, "AdminPolicy");
                if (!authResult.Succeeded)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        error = "access_denied",
                        error_description = "Dynamic client registration is restricted to administrators."
                    });
                }
            }

            var clientName = metadata.TryGetProperty("client_name", out var cn) ? cn.GetString() : "Unknown Client";

            var redirectUrisList = new List<string>();
            if (metadata.TryGetProperty("redirect_uris", out var rUris) && rUris.ValueKind == JsonValueKind.Array)
            {
                foreach (var uri in rUris.EnumerateArray())
                {
                    var uriStr = uri.GetString();
                    if (!string.IsNullOrEmpty(uriStr))
                    {
                        if (!Uri.TryCreate(uriStr, UriKind.Absolute, out var parsedUri) ||
                            (!string.Equals(parsedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                             !string.Equals(parsedUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                             !parsedUri.IsLoopback))
                        {
                            return BadRequest(new
                            {
                                error = "invalid_redirect_uri",
                                error_description = $"The redirect URI '{uriStr}' is invalid or not an absolute HTTP/HTTPS URI."
                            });
                        }
                        redirectUrisList.Add(uriStr);
                    }
                }
            }

            var grantTypesList = new List<string> { "authorization_code", "refresh_token" };
            if (metadata.TryGetProperty("grant_types", out var gTypes) && gTypes.ValueKind == JsonValueKind.Array)
            {
                grantTypesList.Clear();
                foreach (var gt in gTypes.EnumerateArray())
                {
                    var gtStr = gt.GetString();
                    if (!string.IsNullOrEmpty(gtStr))
                    {
                        grantTypesList.Add(gtStr);
                    }
                }
            }

            var responseTypesList = new List<string> { "code" };
            if (metadata.TryGetProperty("response_types", out var respTypes) && respTypes.ValueKind == JsonValueKind.Array)
            {
                responseTypesList.Clear();
                foreach (var rt in respTypes.EnumerateArray())
                {
                    var rtStr = rt.GetString();
                    if (!string.IsNullOrEmpty(rtStr))
                    {
                        responseTypesList.Add(rtStr);
                    }
                }
            }

            var scopesList = new List<string> { "api", "mcp_client", "openid", "offline_access" };
            if (metadata.TryGetProperty("scope", out var scopeProp) && scopeProp.ValueKind == JsonValueKind.String)
            {
                var parsed = scopeProp.GetString()?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parsed != null && parsed.Length > 0)
                {
                    scopesList = parsed.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                }
            }
            else if (metadata.TryGetProperty("scopes", out var scopesProp) && scopesProp.ValueKind == JsonValueKind.Array)
            {
                scopesList.Clear();
                foreach (var s in scopesProp.EnumerateArray())
                {
                    var sStr = s.GetString();
                    if (!string.IsNullOrEmpty(sStr))
                    {
                        scopesList.Add(sStr);
                    }
                }
                scopesList = scopesList.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            var authMethod = metadata.TryGetProperty("token_endpoint_auth_method", out var team) ? team.GetString() : null;
            var isNativeApp = metadata.TryGetProperty("application_type", out var appType) && string.Equals(appType.GetString(), "native", StringComparison.OrdinalIgnoreCase);
            var isPublic = string.Equals(authMethod, "none", StringComparison.OrdinalIgnoreCase) || (isNativeApp && (authMethod == null || string.Equals(authMethod, "none", StringComparison.OrdinalIgnoreCase)));

            var clientType = isPublic ? "public" : "confidential";
            string? clientSecret = isPublic ? null : Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            string clientSecretHash = clientSecret != null ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(clientSecret))).ToLowerInvariant() : "";

            // DCR Idempotency / Reuse: If a dynamic client registration already exists for this client name and type,
            // reuse the existing client ID and update metadata instead of accumulating unbounded duplicate records.
            OAuthClient? existingDcrClient = null;
            if (_oauthClientRepo != null && !string.IsNullOrWhiteSpace(clientName))
            {
                existingDcrClient = await _oauthClientRepo.FindDcrClientAsync(clientName, clientType);
            }

            var clientId = existingDcrClient?.ClientId ?? Guid.NewGuid().ToString("N");

            try
            {
                if (_applicationManager != null)
                {
                    var existingApp = await _applicationManager.FindByClientIdAsync(clientId);
                    if (existingApp == null)
                    {
                        var descriptor = new OpenIddictApplicationDescriptor
                        {
                            ClientId = clientId,
                            ClientSecret = clientSecret,
                            DisplayName = clientName,
                            ClientType = isPublic ? OpenIddictConstants.ClientTypes.Public : OpenIddictConstants.ClientTypes.Confidential,
                            Permissions =
                            {
                                OpenIddictConstants.Permissions.Endpoints.Token,
                                OpenIddictConstants.Permissions.Endpoints.Authorization,
                                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                                OpenIddictConstants.Permissions.ResponseTypes.Code
                            }
                        };

                        if (isPublic)
                        {
                            descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
                        }
                        else
                        {
                            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
                        }

                        // Dynamically register all requested scopes in OpenIddict application permissions
                        foreach (var s in scopesList)
                        {
                            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + s);
                        }

                        foreach (var uriStr in redirectUrisList)
                        {
                            if (Uri.TryCreate(uriStr, UriKind.Absolute, out var parsedUri))
                            {
                                descriptor.RedirectUris.Add(parsedUri);
                            }
                        }
                        await _applicationManager.CreateAsync(descriptor);
                    }
                }

                if (_oauthClientRepo != null)
                {
                    var oauthClient = existingDcrClient ?? new OAuthClient
                    {
                        ClientId = clientId,
                        CreatedBy = User?.Identity?.Name ?? "dcr"
                    };

                    oauthClient.ClientSecretHash = clientSecretHash;
                    oauthClient.ClientName = clientName ?? "Unknown Client";
                    oauthClient.ClientType = clientType;
                    oauthClient.RedirectUrisJson = JsonSerializer.Serialize(redirectUrisList);
                    oauthClient.GrantTypesJson = JsonSerializer.Serialize(grantTypesList);
                    oauthClient.ScopesJson = JsonSerializer.Serialize(scopesList);
                    oauthClient.CreatedAt = DateTime.UtcNow;

                    await _oauthClientRepo.SaveOAuthClientAsync(oauthClient);

                    // Proactively trigger background pruning of historical duplicate DCR records
                    _ = Task.Run(async () =>
                    {
                        try { await _oauthClientRepo.CleanupDcrClientsAsync(); } catch { }
                    });
                }

                var username = User?.Identity?.Name ?? "unknown";
                await _auditLogger.LogAdminActionAsync(username, "oauth.client.register", clientId, JsonSerializer.Serialize(new { clientName, redirectUris = redirectUrisList, clientType }), true);

                if (isPublic)
                {
                    return StatusCode(StatusCodes.Status201Created, new
                    {
                        client_id = clientId,
                        client_name = clientName,
                        client_id_issued_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        redirect_uris = redirectUrisList,
                        grant_types = grantTypesList,
                        response_types = responseTypesList,
                        token_endpoint_auth_method = "none",
                        scope = string.Join(" ", scopesList)
                    });
                }

                return StatusCode(StatusCodes.Status201Created, new
                {
                    client_id = clientId,
                    client_secret = clientSecret,
                    client_name = clientName,
                    client_id_issued_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    client_secret_expires_at = 0,
                    redirect_uris = redirectUrisList,
                    grant_types = grantTypesList,
                    response_types = responseTypesList,
                    token_endpoint_auth_method = authMethod ?? "client_secret_post",
                    scope = string.Join(" ", scopesList)
                });
            }
            catch (Exception ex)
            {
                var username = User?.Identity?.Name ?? "unknown";
                await _auditLogger.LogAdminActionAsync(username, "oauth.client.register", clientId, JsonSerializer.Serialize(new { clientName, redirectUris = redirectUrisList }), false, ex.Message);
                throw;
            }
        }
    }
}


