using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ModelContextGateway.Middleware
{
    public class ExternalJwtAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _memoryCache;

        public ExternalJwtAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IMemoryCache memoryCache)
            : base(options, logger, encoder)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var authority = _configuration["Identity:Jwt:Authority"]
                ?? _configuration["MCG_JWT_AUTHORITY"]
                ?? _configuration["Jwt:Authority"];

            var jwksUri = _configuration["Identity:Jwt:JwksUri"]
                ?? _configuration["MCG_JWT_JWKS_URI"]
                ?? _configuration["Jwt:JwksUri"];

            var audience = _configuration["Identity:Jwt:Audience"]
                ?? _configuration["MCG_JWT_AUDIENCE"]
                ?? _configuration["Jwt:Audience"];

            var issuer = _configuration["Identity:Jwt:Issuer"]
                ?? _configuration["MCG_JWT_ISSUER"]
                ?? _configuration["Jwt:Issuer"]
                ?? authority;

            // If no external JWT validation is configured, pass through
            if (string.IsNullOrEmpty(authority) && string.IsNullOrEmpty(jwksUri))
            {
                return AuthenticateResult.NoResult();
            }

            if (string.IsNullOrEmpty(jwksUri) && !string.IsNullOrEmpty(authority))
            {
                jwksUri = $"{authority.TrimEnd('/')}/.well-known/jwks.json";
            }

            string? authHeader = Request.Headers.Authorization;
            if (string.IsNullOrEmpty(authHeader) && Request.Query.TryGetValue("access_token", out var qToken) && !string.IsNullOrEmpty(qToken))
            {
                authHeader = $"Bearer {qToken}";
            }

            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return AuthenticateResult.NoResult();
            }

            var token = authHeader.Substring(7).Trim();
            if (string.IsNullOrEmpty(token) || token.Count(c => c == '.') != 2)
            {
                return AuthenticateResult.NoResult();
            }

            try
            {
                var cacheKey = $"jwks:{jwksUri}";
                if (!_memoryCache.TryGetValue(cacheKey, out JsonWebKeySet? keySet) || keySet == null)
                {
                    var httpClient = _httpClientFactory.CreateClient("McpClient");
                    var json = await httpClient.GetStringAsync(jwksUri);
                    keySet = new JsonWebKeySet(json);
                    _memoryCache.Set(cacheKey, keySet, TimeSpan.FromHours(1));
                }

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = keySet.GetSigningKeys(),
                    ValidateLifetime = true,
                    ValidateIssuer = !string.IsNullOrEmpty(issuer),
                    ValidIssuer = issuer,
                    ValidateAudience = !string.IsNullOrEmpty(audience),
                    ValidAudience = audience,
                    ClockSkew = TimeSpan.FromMinutes(2)
                };

                var handler = new JsonWebTokenHandler();
                var result = await handler.ValidateTokenAsync(token, validationParameters);
                if (!result.IsValid)
                {
                    Logger.LogDebug("External JWT signature validation returned invalid: {Reason}", result.Exception?.Message);
                    return AuthenticateResult.NoResult();
                }

                var principal = new ClaimsPrincipal(result.ClaimsIdentity);
                var username = principal.FindFirst("preferred_username")?.Value
                    ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? principal.FindFirst(ClaimTypes.Name)?.Value
                    ?? principal.FindFirst("sub")?.Value
                    ?? "unknown";

                var claims = new List<Claim>(result.ClaimsIdentity.Claims);
                if (!claims.Any(c => c.Type == ClaimTypes.Name))
                {
                    claims.Add(new Claim(ClaimTypes.Name, username));
                }
                if (!claims.Any(c => c.Type == ClaimTypes.NameIdentifier))
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, username));
                }

                // Map roles / groups
                foreach (var groupClaim in claims.Where(c => c.Type == "groups" || c.Type == "roles").ToList())
                {
                    if (!claims.Any(c => c.Type == ClaimTypes.Role && c.Value == groupClaim.Value))
                    {
                        claims.Add(new Claim(ClaimTypes.Role, groupClaim.Value));
                    }
                }

                // Map SID if present
                var sidClaim = claims.FirstOrDefault(c => c.Type == "sid" || c.Type == "Sid");
                if (sidClaim != null && !claims.Any(c => c.Type == ClaimTypes.PrimarySid))
                {
                    claims.Add(new Claim(ClaimTypes.PrimarySid, sidClaim.Value));
                }

                // Administrator role check
                var adminSid = _configuration["Admin:GroupSid"] ?? "S-1-5-32-544";
                if (claims.Any(c => c.Type == ClaimTypes.Role && (c.Value.Equals("Administrator", StringComparison.OrdinalIgnoreCase) || c.Value.Equals("full_admin", StringComparison.OrdinalIgnoreCase))) ||
                    claims.Any(c => (c.Type == "Sid" || c.Type == ClaimTypes.PrimarySid || c.Type == ClaimTypes.GroupSid) && c.Value == adminSid))
                {
                    if (!claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "Administrator"))
                    {
                        claims.Add(new Claim(ClaimTypes.Role, "Administrator"));
                    }
                    if (!claims.Any(c => c.Type == "Sid" && c.Value == adminSid))
                    {
                        claims.Add(new Claim("Sid", adminSid));
                    }
                }

                var ticketIdentity = new ClaimsIdentity(claims, Scheme.Name);
                var ticketPrincipal = new ClaimsPrincipal(ticketIdentity);
                var ticket = new AuthenticationTicket(ticketPrincipal, Scheme.Name);

                return AuthenticateResult.Success(ticket);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to validate external JWT.");
                return AuthenticateResult.NoResult();
            }
        }
    }
}
