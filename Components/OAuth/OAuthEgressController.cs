using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;

namespace ModelContextGateway.Components.OAuth
{
    public class OAuthEgressState
    {
        public string Username { get; set; } = string.Empty;
        public string ServerId { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    [ApiController]
    [Route("api/oauth/egress")]
    public class OAuthEgressController : ControllerBase
    {
        private readonly IDbConnectionFactory _dbFactory;
        private readonly IUserSecretStore _userSecretStore;
        private readonly IMemoryCache _memoryCache;
        private readonly HttpClient _httpClient;
        private readonly CompositeIdentityProvider _identityProvider;
        private readonly ILogger<OAuthEgressController> _logger;

        public OAuthEgressController(
            IDbConnectionFactory dbFactory,
            IUserSecretStore userSecretStore,
            IMemoryCache memoryCache,
            IHttpClientFactory httpClientFactory,
            CompositeIdentityProvider identityProvider,
            ILogger<OAuthEgressController> logger)
        {
            _dbFactory = dbFactory;
            _userSecretStore = userSecretStore;
            _memoryCache = memoryCache;
            _httpClient = httpClientFactory.CreateClient("McpClient");
            _identityProvider = identityProvider;
            _logger = logger;
        }

        private async Task<string> GetCurrentUsernameAsync()
        {
            try
            {
                var identity = await _identityProvider.ResolveIdentityAsync(HttpContext);
                if (!string.IsNullOrEmpty(identity?.Username))
                {
                    return identity.Username;
                }
            }
            catch
            {
                // Fallback to ClaimsPrincipal
            }

            return User.Identity?.Name ?? "anonymous";
        }

        [HttpGet("authorize/{serverId}")]
        [Authorize]
        public async Task<IActionResult> Authorize(string serverId)
        {
            if (string.IsNullOrWhiteSpace(serverId))
            {
                return BadRequest(new { error = "ServerId is required." });
            }

            var username = await GetCurrentUsernameAsync();
            using var conn = _dbFactory.CreateConnection();
            DatabaseInitializer.EnsureOAuthColumns(conn);
            var server = await conn.QueryFirstOrDefaultAsync<McpServer>("SELECT * FROM Servers WHERE Id = @Id", new { Id = serverId });

            if (server == null)
            {
                return NotFound(new { error = $"Server '{serverId}' not found." });
            }

            if (!server.EnableOAuth3Lo || string.IsNullOrWhiteSpace(server.OAuthAuthorizationUrl) || string.IsNullOrWhiteSpace(server.OAuthClientId))
            {
                return BadRequest(new { error = $"OAuth 3LO is not configured for server '{serverId}'." });
            }

            // Generate cryptographic state parameter
            var stateBytes = new byte[32];
            RandomNumberGenerator.Fill(stateBytes);
            var state = Convert.ToHexString(stateBytes).ToLowerInvariant();

            var stateData = new OAuthEgressState
            {
                Username = username,
                ServerId = server.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _memoryCache.Set($"oauth_egress_state:{state}", stateData, TimeSpan.FromMinutes(15));

            var redirectUri = !string.IsNullOrWhiteSpace(server.OAuthRedirectUri)
                ? server.OAuthRedirectUri
                : $"{Request.Scheme}://{Request.Host}/api/oauth/egress/callback";

            var queryParams = new Dictionary<string, string?>
            {
                ["response_type"] = "code",
                ["client_id"] = server.OAuthClientId,
                ["redirect_uri"] = redirectUri,
                ["state"] = state
            };

            if (!string.IsNullOrWhiteSpace(server.OAuthScopes))
            {
                queryParams["scope"] = server.OAuthScopes;
            }

            var targetUrl = QueryHelpers.AddQueryString(server.OAuthAuthorizationUrl, queryParams);
            _logger.LogInformation("Initiating OAuth 3LO egress authorization for user '{Username}' to server '{ServerId}' at {AuthUrl}", username, server.Id, targetUrl);

            return Redirect(targetUrl);
        }

        [HttpGet("callback")]
        public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, [FromQuery] string? error_description)
        {
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("OAuth egress callback returned error: {Error} - {Description}", error, error_description);
                return BadRequest(new { error = $"OAuth authorization failed: {error}", description = error_description });
            }

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                return BadRequest(new { error = "Missing 'code' or 'state' parameter in OAuth callback." });
            }

            var cacheKey = $"oauth_egress_state:{state}";
            if (!_memoryCache.TryGetValue<OAuthEgressState>(cacheKey, out var stateData) || stateData == null)
            {
                return BadRequest(new { error = "Invalid or expired OAuth state parameter." });
            }

            _memoryCache.Remove(cacheKey);

            using var conn = _dbFactory.CreateConnection();
            DatabaseInitializer.EnsureOAuthColumns(conn);
            var server = await conn.QueryFirstOrDefaultAsync<McpServer>("SELECT * FROM Servers WHERE Id = @Id", new { Id = stateData.ServerId });

            if (server == null)
            {
                return NotFound(new { error = $"Server '{stateData.ServerId}' not found." });
            }

            if (string.IsNullOrWhiteSpace(server.OAuthTokenUrl))
            {
                return BadRequest(new { error = $"OAuth token URL not configured for server '{server.Id}'." });
            }

            var redirectUri = !string.IsNullOrWhiteSpace(server.OAuthRedirectUri)
                ? server.OAuthRedirectUri
                : $"{Request.Scheme}://{Request.Host}/api/oauth/egress/callback";

            var tokenParams = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = server.OAuthClientId ?? string.Empty,
                ["client_secret"] = server.OAuthClientSecret ?? string.Empty
            };

            using var tokenReq = new HttpRequestMessage(HttpMethod.Post, server.OAuthTokenUrl)
            {
                Content = new FormUrlEncodedContent(tokenParams)
            };

            var tokenResp = await _httpClient.SendAsync(tokenReq);
            if (!tokenResp.IsSuccessStatusCode)
            {
                var errContent = await tokenResp.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to exchange OAuth code for server {ServerId}: HTTP {Status} - {Details}", server.Id, (int)tokenResp.StatusCode, errContent);
                return StatusCode((int)tokenResp.StatusCode, new { error = "Failed to exchange authorization code for tokens.", details = errContent });
            }

            var responseBody = await tokenResp.Content.ReadAsStringAsync();
            JsonNode? parsedNode;
            try
            {
                parsedNode = JsonNode.Parse(responseBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse OAuth token response for server {ServerId}", server.Id);
                return BadRequest(new { error = "Invalid JSON response received from OAuth token endpoint." });
            }

            if (parsedNode is not JsonObject tokenObj)
            {
                return BadRequest(new { error = "OAuth provider returned invalid token response shape." });
            }

            var accessToken = tokenObj["access_token"]?.GetValue<string>();
            if (string.IsNullOrEmpty(accessToken))
            {
                return BadRequest(new { error = "OAuth token response did not contain an access_token." });
            }

            var refreshToken = tokenObj["refresh_token"]?.GetValue<string>();
            var tokenType = tokenObj["token_type"]?.GetValue<string>() ?? "Bearer";

            int expiresIn = 3600;
            if (tokenObj["expires_in"] is JsonValue expVal && expVal.TryGetValue<int>(out var expNum))
            {
                expiresIn = expNum;
            }
            else if (int.TryParse(tokenObj["expires_in"]?.ToString(), out var parsedExp))
            {
                expiresIn = parsedExp;
            }

            long expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn;

            var persistedObj = new JsonObject
            {
                ["access_token"] = accessToken,
                ["token_type"] = tokenType,
                ["expires_in"] = expiresIn,
                ["expires_at"] = expiresAt
            };

            if (!string.IsNullOrEmpty(refreshToken))
            {
                persistedObj["refresh_token"] = refreshToken;
            }

            var tokenJson = persistedObj.ToJsonString();
            await _userSecretStore.SaveSecretAsync(stateData.Username, stateData.ServerId, tokenJson);
            _logger.LogInformation("Successfully persisted egress OAuth credentials for user '{Username}' and server '{ServerId}'", stateData.Username, stateData.ServerId);

            return Redirect($"/?connected={stateData.ServerId}");
        }

        [HttpPost("disconnect/{serverId}")]
        [Authorize]
        public async Task<IActionResult> Disconnect(string serverId)
        {
            if (string.IsNullOrWhiteSpace(serverId))
            {
                return BadRequest(new { error = "ServerId is required." });
            }

            var username = await GetCurrentUsernameAsync();
            await _userSecretStore.DeleteSecretAsync(username, serverId);
            _logger.LogInformation("Disconnected egress OAuth account for user '{Username}' and server '{ServerId}'", username, serverId);

            return Ok(new { success = true, serverId });
        }

        [HttpGet("servers")]
        [Authorize]
        public async Task<IActionResult> GetOAuthServers()
        {
            var username = await GetCurrentUsernameAsync();
            using var conn = _dbFactory.CreateConnection();
            DatabaseInitializer.EnsureOAuthColumns(conn);
            var servers = (await conn.QueryAsync<McpServer>("SELECT * FROM Servers WHERE EnableOAuth3Lo = 1 AND Enabled = 1")).ToList();
            var configuredServers = (await _userSecretStore.GetServerIdsAsync(username)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var result = servers.Select(s => new
            {
                id = s.Id,
                alias = s.Alias,
                displayName = string.IsNullOrWhiteSpace(s.DisplayName) ? s.Id : s.DisplayName,
                enableOAuth3Lo = true,
                isConnected = configuredServers.Contains(s.Id)
            });

            return Ok(result);
        }
    }
}
