using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModelContextGateway.Components.Authorization
{
    public class ProtectedResourceMetadata
    {
        [JsonPropertyName("resource")]
        public string Resource { get; set; } = string.Empty;

        [JsonPropertyName("authorization_servers")]
        public string[] AuthorizationServers { get; set; } = Array.Empty<string>();

        [JsonPropertyName("scopes_supported")]
        public string[] ScopesSupported { get; set; } = Array.Empty<string>();

        [JsonPropertyName("bearer_methods_supported")]
        public string[] BearerMethodsSupported { get; set; } = new[] { "header" };

        [JsonPropertyName("resource_documentation")]
        public string ResourceDocumentation { get; set; } = string.Empty;
    }

    public static class ProtectedResourceHelper
    {
        public static readonly string[] DefaultScopesSupported = new[] { "openid", "profile", "email", "mcp:access" };
        public static readonly string[] DefaultBearerMethodsSupported = new[] { "header" };

        public static string GetCanonicalUrl(HttpContext context, IConfiguration? config)
        {
            var configuredUrl = config?["Gateway:PublicUrl"]
                ?? config?["PublicUrl"]
                ?? config?["CanonicalUrl"]
                ?? config?["MCG_PUBLIC_URL"]
                ?? config?["GatewayUrl"];

            if (!string.IsNullOrWhiteSpace(configuredUrl))
            {
                return configuredUrl.Trim().TrimEnd('/');
            }

            var scheme = context.Request.Headers.TryGetValue("X-Forwarded-Proto", out var proto) && !string.IsNullOrWhiteSpace(proto)
                ? proto.ToString()
                : context.Request.Scheme;

            var host = context.Request.Headers.TryGetValue("X-Forwarded-Host", out var fwdHost) && !string.IsNullOrWhiteSpace(fwdHost)
                ? fwdHost.ToString()
                : context.Request.Host.ToString();

            return $"{scheme}://{host}".TrimEnd('/');
        }

        public static async Task<string[]> GetAuthorizationServersAsync(HttpContext context, IConfiguration? config, IAuthProviderRepository? authRepo)
        {
            var servers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (config != null)
            {
                var authority = config["Identity:Jwt:Authority"]
                    ?? config["MCG_JWT_AUTHORITY"]
                    ?? config["Jwt:Authority"]
                    ?? config["JwtOptions:Authority"]
                    ?? config["Oidc:Authority"];
                if (!string.IsNullOrWhiteSpace(authority))
                {
                    servers.Add(authority.Trim().TrimEnd('/'));
                }

                var issuer = config["Identity:Jwt:Issuer"]
                    ?? config["MCG_JWT_ISSUER"]
                    ?? config["Jwt:Issuer"]
                    ?? config["JwtOptions:Issuer"]
                    ?? config["Oidc:Issuer"];
                if (!string.IsNullOrWhiteSpace(issuer) && (issuer.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || issuer.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    servers.Add(issuer.Trim().TrimEnd('/'));
                }

                var tokenExchangeEndpoint = config["Identity:TokenExchange:TokenEndpoint"];
                if (!string.IsNullOrWhiteSpace(tokenExchangeEndpoint) && Uri.TryCreate(tokenExchangeEndpoint, UriKind.Absolute, out var teUri))
                {
                    servers.Add($"{teUri.Scheme}://{teUri.Authority}".TrimEnd('/'));
                }
            }

            if (authRepo != null)
            {
                try
                {
                    var providers = await authRepo.GetAuthProvidersAsync();
                    if (providers != null)
                    {
                        foreach (var provider in providers.Where(p => p.IsEnabled && !string.IsNullOrWhiteSpace(p.ConfigJson)))
                        {
                            try
                            {
                                using var doc = JsonDocument.Parse(provider.ConfigJson!);
                                var root = doc.RootElement;
                                if (root.ValueKind == JsonValueKind.Object)
                                {
                                    if (root.TryGetProperty("authority", out var authProp) && authProp.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(authProp.GetString()))
                                    {
                                        servers.Add(authProp.GetString()!.Trim().TrimEnd('/'));
                                    }
                                    else if (root.TryGetProperty("issuer", out var issProp) && issProp.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(issProp.GetString()))
                                    {
                                        var iss = issProp.GetString()!.Trim().TrimEnd('/');
                                        if (iss.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || iss.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                        {
                                            servers.Add(iss);
                                        }
                                    }
                                    else if (root.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(urlProp.GetString()))
                                    {
                                        var u = urlProp.GetString()!.Trim().TrimEnd('/');
                                        if (u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                        {
                                            servers.Add(u);
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // Ignore malformed JSON in DB
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore DB errors
                }
            }

            if (servers.Count == 0)
            {
                servers.Add(GetCanonicalUrl(context, config));
            }

            return servers.ToArray();
        }

        public static string GetResourceMetadataUrl(HttpContext context, IConfiguration? config, string? targetServerId = null)
        {
            var canonicalUrl = GetCanonicalUrl(context, config);
            if (string.IsNullOrWhiteSpace(targetServerId))
            {
                return $"{canonicalUrl}/.well-known/oauth-protected-resource";
            }

            return $"{canonicalUrl}/.well-known/oauth-protected-resource/{targetServerId.TrimStart('/')}";
        }
    }
}
