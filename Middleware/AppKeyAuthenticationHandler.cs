using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ModelContextGateway.Components.Activity;

namespace ModelContextGateway.Middleware
{
    public class AppKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IDbConnectionFactory _dbFactory;
        private readonly IActivityTracker? _activityTracker;

        [ActivatorUtilitiesConstructor]
        public AppKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IDbConnectionFactory dbFactory,
            IActivityTracker? activityTracker,
            IConfiguration? config)
            : base(options, logger, encoder)
        {
            _dbFactory = dbFactory;
            _activityTracker = activityTracker;
        }

        public AppKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IDbConnectionFactory dbFactory,
            IActivityTracker? activityTracker)
            : this(options, logger, encoder, dbFactory, activityTracker, null)
        {
        }

        public AppKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IDbConnectionFactory dbFactory,
            IConfiguration? config)
            : this(options, logger, encoder, dbFactory, null, config)
        {
        }

        public AppKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IDbConnectionFactory dbFactory)
            : this(options, logger, encoder, dbFactory, null, null)
        {
        }

        private string? ExtractTokenFromHeaders()
        {
            string authHeader = Request.Headers.Authorization.ToString();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authHeader.Substring("Bearer ".Length).Trim();
            }

            var xAppKey = Request.Headers["X-App-Key"].ToString();
            if (!string.IsNullOrEmpty(xAppKey))
            {
                return xAppKey.Trim();
            }

            var xApiKey = Request.Headers["X-Api-Key"].ToString();
            if (!string.IsNullOrEmpty(xApiKey))
            {
                return xApiKey.Trim();
            }

            return null;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var token = ExtractTokenFromHeaders();
            if (string.IsNullOrEmpty(token) || !token.StartsWith("mcp-"))
            {
                return AuthenticateResult.NoResult();
            }

            try
            {
                string prefix = ExtractKeyPrefix(token);

                using var conn = _dbFactory.CreateConnection();
                AppKey? appKey = null;

                if (_dbFactory.ProviderName == "sqlite")
                {
                    const string sql = "SELECT * FROM AppKeys WHERE KeyPrefix = @KeyPrefix;";
                    appKey = await conn.QueryFirstOrDefaultAsync<AppKey>(sql, new { KeyPrefix = prefix });
                }
                else
                {
                    const string sql = "SELECT Id, Name, Username, OwnerSid, KeyPrefix, EncryptedKey, ScopesJson, ExpiresAt, CreatedAt FROM AppKeys WHERE KeyPrefix = @KeyPrefix;";
                    appKey = await conn.QueryFirstOrDefaultAsync<AppKey>(sql, new { KeyPrefix = prefix });
                }

                if (appKey == null)
                {
                    return AuthenticateResult.Fail("Invalid App Key prefix.");
                }

                // Verify the key using constant-time comparison on SHA-256 hash
                if (appKey.EncryptedKey.Length != 64 || appKey.EncryptedKey.Contains("/") || appKey.EncryptedKey.Contains("+") || appKey.EncryptedKey.Contains("="))
                {
                    return AuthenticateResult.Fail("Invalid App Key hash format.");
                }

                using var sha = SHA256.Create();
                var computedBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token));
                var computedHash = Convert.ToHexString(computedBytes).ToLowerInvariant();

                bool isValid = CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(appKey.EncryptedKey.ToLowerInvariant()),
                    System.Text.Encoding.UTF8.GetBytes(computedHash)
                );

                if (!isValid)
                {
                    return AuthenticateResult.Fail("Invalid App Key.");
                }

                // Check expiration
                if (appKey.ExpiresAt.HasValue && appKey.ExpiresAt.Value < DateTime.UtcNow)
                {
                    return AuthenticateResult.Fail("App Key has expired.");
                }

                _ = _activityTracker?.RecordAppKeyUsageAsync(appKey.Id);

                // Successfully authenticated!
                var claims = new System.Collections.Generic.List<Claim>
                {
                    new Claim(ClaimTypes.Name, appKey.Username),
                    new Claim(ClaimTypes.Role, "McpClient")
                };

                if (IsAdminKey(appKey))
                {
                    claims.Add(new Claim(ClaimTypes.Role, "Administrator"));
                    claims.Add(new Claim("Scope", "admin"));
                }

                if (!string.IsNullOrEmpty(appKey.OwnerSid))
                {
                    claims.Add(new Claim("Sid", appKey.OwnerSid));
                }

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                // Store AppKey details in HttpContext for authorization checks
                Context.Items["AppKeyUsed"] = true;
                Context.Items["AppKeyScopes"] = appKey.ScopesJson;
                Context.Items["AppKeyOwner"] = appKey.Username;
                Context.Items["AppKeyOwnerSid"] = appKey.OwnerSid;
                Context.Items["AppKeyType"] = appKey.KeyType;

                return AuthenticateResult.Success(ticket);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error validating App Key.");
                return AuthenticateResult.Fail("Error validating App Key.");
            }
        }

        private static bool IsAdminKey(AppKey appKey)
        {
            if (!string.Equals(appKey.KeyType, "system", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(appKey.ScopesJson))
            {
                return false;
            }

            try
            {
                var parsedScopes = JsonSerializer.Deserialize<List<string>>(appKey.ScopesJson);
                if (parsedScopes != null && parsedScopes.Any(s =>
                    string.Equals(s, "admin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s, "all", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s, "*", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            catch
            {
                var scopeParts = appKey.ScopesJson.Split(new[] { ',', '[', ']', '"', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (scopeParts.Any(s =>
                    string.Equals(s, "admin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s, "all", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s, "*", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        public static string ExtractKeyPrefix(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return string.Empty;
            }

            var parts = token.Split('-');
            if (parts.Length >= 4 && parts[0] == "mcp")
            {
                return $"{parts[0]}-{parts[1]}-{parts[2]}";
            }
            else if (parts.Length == 3 && parts[0] == "mcp" && parts[2].Length >= 32)
            {
                return $"{parts[0]}-{parts[1]}-{parts[2].Substring(0, 32)}";
            }
            else
            {
                return token.Length > 16 ? token.Substring(0, 16) : token;
            }
        }
    }
}
