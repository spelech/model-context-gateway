using System.Text.Json.Nodes;

namespace ModelContextGateway.Core.Routing
{
    /// <summary>
    /// Handles extraction, validation, and automated background refresh of third-party personal OAuth2 3LO access tokens.
    /// </summary>
    public static class OAuthEgressTokenManager
    {
        public static async Task<string?> ExtractOrRefreshTokenAsync(
            McpServer server,
            string? tokenOrSecret,
            string? username,
            IUserSecretStore? userSecretStore,
            HttpClient httpClient,
            ILogger logger,
            Action<string>? onTokenUpdated = null)
        {
            if (string.IsNullOrWhiteSpace(tokenOrSecret))
            {
                if ((server.EnableOAuth3Lo || server.SecretProvider == "UserProvided") && userSecretStore != null && !string.IsNullOrEmpty(username))
                {
                    try
                    {
                        tokenOrSecret = await userSecretStore.GetSecretAsync(username, server.Id);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to retrieve stored user secret for server {ServerId} and user {Username}", server.Id, username);
                    }
                }

                if (string.IsNullOrWhiteSpace(tokenOrSecret))
                {
                    return null;
                }
            }

            var trimmed = tokenOrSecret.Trim();
            if (!trimmed.StartsWith('{'))
            {
                // Raw token string
                return tokenOrSecret;
            }

            JsonNode? rootNode;
            try
            {
                rootNode = JsonNode.Parse(trimmed);
            }
            catch
            {
                return tokenOrSecret;
            }

            if (rootNode is not JsonObject jsonObj)
            {
                return tokenOrSecret;
            }

            // Check if this is an OAuth token dictionary with access_token
            var accessToken = jsonObj["access_token"]?.GetValue<string>();
            if (string.IsNullOrEmpty(accessToken))
            {
                if (jsonObj["apiKey"] != null)
                {
                    return jsonObj["apiKey"]!.GetValue<string>();
                }
                if (jsonObj["token"] != null)
                {
                    return jsonObj["token"]!.GetValue<string>();
                }
                return tokenOrSecret;
            }

            // Check token expiry
            bool isExpired = false;
            long expiresAt = 0;
            if (jsonObj["expires_at"] != null)
            {
                var expiresAtNode = jsonObj["expires_at"];
                if (expiresAtNode is JsonValue val && val.TryGetValue<long>(out var expVal))
                {
                    expiresAt = expVal;
                    if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expiresAt - 10)
                    {
                        isExpired = true;
                    }
                }
                else if (DateTimeOffset.TryParse(expiresAtNode?.ToString(), out var parsedDto))
                {
                    expiresAt = parsedDto.ToUnixTimeSeconds();
                    if (DateTimeOffset.UtcNow >= parsedDto.AddSeconds(-10))
                    {
                        isExpired = true;
                    }
                }
            }

            var refreshToken = jsonObj["refresh_token"]?.GetValue<string>();

            // If token is expired and refresh token exists with token URL, perform refresh
            if (isExpired && !string.IsNullOrEmpty(refreshToken) && !string.IsNullOrEmpty(server.OAuthTokenUrl))
            {
                logger.LogInformation("Outbound OAuth token for server {ServerId} expired at {ExpiresAt}. Refreshing via {OAuthTokenUrl}...", server.Id, expiresAt, server.OAuthTokenUrl);

                try
                {
                    var refreshParams = new Dictionary<string, string>
                    {
                        ["grant_type"] = "refresh_token",
                        ["refresh_token"] = refreshToken
                    };

                    if (!string.IsNullOrEmpty(server.OAuthClientId))
                    {
                        refreshParams["client_id"] = server.OAuthClientId;
                    }
                    if (!string.IsNullOrEmpty(server.OAuthClientSecret))
                    {
                        refreshParams["client_secret"] = server.OAuthClientSecret;
                    }

                    using var refreshReq = new HttpRequestMessage(HttpMethod.Post, server.OAuthTokenUrl)
                    {
                        Content = new FormUrlEncodedContent(refreshParams)
                    };

                    var refreshResp = await httpClient.SendAsync(refreshReq);
                    if (refreshResp.IsSuccessStatusCode)
                    {
                        var respContent = await refreshResp.Content.ReadAsStringAsync();
                        var freshNode = JsonNode.Parse(respContent);
                        if (freshNode is JsonObject freshObj && freshObj["access_token"] != null)
                        {
                            var newAccessToken = freshObj["access_token"]!.GetValue<string>();
                            var newRefreshToken = freshObj["refresh_token"]?.GetValue<string>() ?? refreshToken;
                            var newTokenType = freshObj["token_type"]?.GetValue<string>() ?? jsonObj["token_type"]?.GetValue<string>() ?? "Bearer";

                            int newExpiresIn = 3600;
                            if (freshObj["expires_in"] is JsonValue expValNode && expValNode.TryGetValue<int>(out var expNum))
                            {
                                newExpiresIn = expNum;
                            }
                            else if (int.TryParse(freshObj["expires_in"]?.ToString(), out var parsedExp))
                            {
                                newExpiresIn = parsedExp;
                            }

                            long newExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + newExpiresIn;

                            var updatedObj = new JsonObject
                            {
                                ["access_token"] = newAccessToken,
                                ["token_type"] = newTokenType,
                                ["refresh_token"] = newRefreshToken,
                                ["expires_in"] = newExpiresIn,
                                ["expires_at"] = newExpiresAt
                            };

                            var updatedJson = updatedObj.ToJsonString();

                            if (userSecretStore != null && !string.IsNullOrEmpty(username))
                            {
                                await userSecretStore.SaveSecretAsync(username, server.Id, updatedJson);
                            }

                            onTokenUpdated?.Invoke(updatedJson);
                            logger.LogInformation("Successfully refreshed outbound OAuth token for server {ServerId}. New expiry: {NewExpiresAt}", server.Id, newExpiresAt);
                            return newAccessToken;
                        }
                    }
                    else
                    {
                        var errText = await refreshResp.Content.ReadAsStringAsync();
                        logger.LogWarning("Failed to refresh OAuth token for server {ServerId}: HTTP {Status} - {Details}", server.Id, (int)refreshResp.StatusCode, errText);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unexpected error refreshing OAuth token for server {ServerId}", server.Id);
                }
            }

            return accessToken;
        }
    }
}
