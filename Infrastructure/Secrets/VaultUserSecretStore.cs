using System.Text.Json;

namespace ModelContextGateway.Infrastructure.Secrets
{
    public class VaultUserSecretStore : IUserSecretStore
    {
        private readonly VaultSecretRetriever _retriever;
        private readonly IConfiguration? _config;
        private readonly string _pathTemplate;
        private readonly string _company;
        private readonly string _mountPoint;
        private readonly string _keyName;

        public VaultUserSecretStore(VaultSecretRetriever retriever, IConfiguration? config = null)
        {
            _retriever = retriever;
            _config = config;

            _pathTemplate = _config?["Vault:UserSecretPathTemplate"]
                ?? _config?["MCG_VAULT_USER_SECRET_PATH_TEMPLATE"]
                ?? "users/{username}/{serverId}";

            _company = _config?["Vault:Company"]
                ?? _config?["Company"]
                ?? _config?["Enterprise:Company"]
                ?? "default";

            _mountPoint = _config?["Vault:MountPath"]
                ?? _config?["Vault:Mount"]
                ?? _retriever.DefaultMountPoint
                ?? "secret";

            _keyName = _config?["Vault:UserSecretKeyName"]
                ?? _config?["MCG_VAULT_USER_SECRET_KEY_NAME"]
                ?? "secret";
        }

        private string ResolvePath(string username, string serverId)
        {
            var template = _pathTemplate;
            return template
                .Replace("{company}", _company, StringComparison.OrdinalIgnoreCase)
                .Replace("{username}", username, StringComparison.OrdinalIgnoreCase)
                .Replace("{user}", username, StringComparison.OrdinalIgnoreCase)
                .Replace("{serverId}", serverId, StringComparison.OrdinalIgnoreCase)
                .Replace("{server}", serverId, StringComparison.OrdinalIgnoreCase)
                .Replace("{app}", serverId, StringComparison.OrdinalIgnoreCase)
                .Trim('/');
        }

        private string ResolveUserParentPath(string username)
        {
            var sample = ResolvePath(username, "PLACEHOLDER_APP");
            var lastSlash = sample.LastIndexOf('/');
            return lastSlash > 0 ? sample.Substring(0, lastSlash) : $"users/{username}";
        }

        public async Task<string?> GetSecretAsync(string username, string serverId)
        {
            var path = ResolvePath(username, serverId);

            // 1. First try direct single-key retrieval via retriever cache
            try
            {
                var cachedOrDirect = await _retriever.GetSecretAsync(path, _keyName);
                if (!string.IsNullOrEmpty(cachedOrDirect))
                {
                    return cachedOrDirect;
                }
            }
            catch
            {
                // Fall through to full data inspection
            }

            var client = await _retriever.EnsureVaultClientAsync();
            if (client == null)
            {
                return null;
            }

            try
            {
                var secretData = await client.V1.Secrets.KeyValue.V2.ReadSecretAsync(path: path, mountPoint: _mountPoint);
                var data = secretData?.Data?.Data;
                if (data == null || data.Count == 0)
                {
                    return null;
                }

                // Try configured key name
                if (data.TryGetValue(_keyName, out var keyVal) && keyVal != null && !string.IsNullOrWhiteSpace(keyVal.ToString()))
                {
                    return keyVal.ToString();
                }

                // Try access_token or token
                if (data.TryGetValue("access_token", out var tokenVal) && tokenVal != null && !string.IsNullOrWhiteSpace(tokenVal.ToString()))
                {
                    return tokenVal.ToString();
                }

                if (data.TryGetValue("token", out var tVal) && tVal != null && !string.IsNullOrWhiteSpace(tVal.ToString()))
                {
                    return tVal.ToString();
                }

                if (data.TryGetValue("auth_blob", out var blobVal) && blobVal != null && !string.IsNullOrWhiteSpace(blobVal.ToString()))
                {
                    return blobVal.ToString();
                }

                // Multiple discrete fields (e.g. client_id, client_secret, etc.) -> serialize to JSON blob
                return JsonSerializer.Serialize(data);
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveSecretAsync(string username, string serverId, string secretJson)
        {
            var path = ResolvePath(username, serverId);
            var client = await _retriever.EnsureVaultClientAsync();
            if (client == null)
            {
                throw new InvalidOperationException("Vault client is not configured or unavailable.");
            }

            var dict = new Dictionary<string, object>();
            try
            {
                using var doc = JsonDocument.Parse(secretJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        dict[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString() ?? "",
                            JsonValueKind.Number => prop.Value.GetRawText(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => prop.Value.GetRawText()
                        };
                    }
                }
            }
            catch
            {
                // Not a JSON object - treat as plain text secret string
            }

            if (!dict.ContainsKey(_keyName))
            {
                dict[_keyName] = secretJson;
            }

            await client.V1.Secrets.KeyValue.V2.WriteSecretAsync(path: path, data: dict, mountPoint: _mountPoint);
        }

        public async Task DeleteSecretAsync(string username, string serverId)
        {
            var path = ResolvePath(username, serverId);
            var client = await _retriever.EnsureVaultClientAsync();
            if (client != null)
            {
                try
                {
                    await client.V1.Secrets.KeyValue.V2.DeleteSecretAsync(path: path, mountPoint: _mountPoint);
                }
                catch
                {
                    // Best-effort delete
                }
            }
        }

        public async Task<IEnumerable<string>> GetServerIdsAsync(string username)
        {
            var parentPath = ResolveUserParentPath(username);
            var client = await _retriever.EnsureVaultClientAsync();
            if (client == null)
            {
                return Enumerable.Empty<string>();
            }

            try
            {
                var paths = await client.V1.Secrets.KeyValue.V2.ReadSecretPathsAsync(path: parentPath, mountPoint: _mountPoint);
                return paths?.Data?.Keys?.Select(k => k.TrimEnd('/')) ?? Enumerable.Empty<string>();
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }
    }
}
