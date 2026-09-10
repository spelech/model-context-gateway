using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using VaultSharp;
using VaultSharp.V1.AuthMethods.AppRole;
using VaultSharp.V1.AuthMethods.Token;

namespace ModelContextGateway.Infrastructure.Secrets
{
    public class VaultSecretRetriever : ISecretRetriever
    {
        private readonly IConfiguration _config;
        private readonly IMemoryCache _cache;
        private readonly ISecretProviderRepository? _secretRepo;
        private readonly Func<IVaultClient>? _vaultClientFactory;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private IVaultClient? _vaultClient;
        private string _defaultMountPoint = "secret";

        public string ProviderName => "HashiCorpVault";
        public string DefaultMountPoint => _defaultMountPoint;

        public VaultSecretRetriever(IConfiguration config, IMemoryCache cache)
            : this(config, cache, null, null)
        {
        }

        public VaultSecretRetriever(IConfiguration config, IMemoryCache cache, Func<IVaultClient>? vaultClientFactory)
            : this(config, cache, null, vaultClientFactory)
        {
        }

        public VaultSecretRetriever(IConfiguration config, IMemoryCache cache, ISecretProviderRepository? secretRepo, Func<IVaultClient>? vaultClientFactory = null)
        {
            _config = config;
            _cache = cache;
            _secretRepo = secretRepo;
            _vaultClientFactory = vaultClientFactory;
        }

        private record VaultDbConfig(
            bool IsEnabled,
            string? Address,
            string? Token,
            string? RoleId,
            string? SecretId,
            string? MountPath);

        private async Task<VaultDbConfig?> TryLoadDbConfigAsync()
        {
            if (_secretRepo == null)
            {
                return null;
            }

            try
            {
                var dbProviders = await _secretRepo.GetSecretProvidersAsync();
                var vaultDb = dbProviders?.FirstOrDefault(p =>
                    string.Equals(p.ProviderName, "Vault", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.ProviderName, "HashiCorpVault", StringComparison.OrdinalIgnoreCase));

                if (vaultDb != null)
                {
                    if (!vaultDb.IsEnabled)
                    {
                        return new VaultDbConfig(false, null, null, null, null, null);
                    }

                    return ParseConfigJson(vaultDb.ConfigJson);
                }
            }
            catch
            {
                // Fall back to static configuration if DB read encounters transient issues
            }

            return null;
        }

        private VaultDbConfig ParseConfigJson(string? configJson)
        {
            string? address = null;
            string? token = null;
            string? roleId = null;
            string? secretId = null;
            string? mountPath = null;

            if (!string.IsNullOrWhiteSpace(configJson))
            {
                using var doc = JsonDocument.Parse(configJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("address", out var aProp) ||
                    root.TryGetProperty("url", out aProp) ||
                    root.TryGetProperty("vault_addr", out aProp))
                {
                    address = aProp.GetString();
                }
                if (root.TryGetProperty("token", out var tProp) ||
                    root.TryGetProperty("vault_token", out tProp))
                {
                    token = tProp.GetString();
                }
                if (root.TryGetProperty("roleId", out var rProp) ||
                    root.TryGetProperty("role_id", out rProp))
                {
                    roleId = rProp.GetString();
                }
                if (root.TryGetProperty("secretId", out var sProp) ||
                    root.TryGetProperty("secret_id", out sProp))
                {
                    secretId = sProp.GetString();
                }
                if (root.TryGetProperty("mountPath", out var mProp) ||
                    root.TryGetProperty("mount", out mProp))
                {
                    var m = mProp.GetString();
                    if (!string.IsNullOrWhiteSpace(m))
                    {
                        mountPath = m;
                    }
                }
            }

            return new VaultDbConfig(true, address, token, roleId, secretId, mountPath);
        }

        public async Task ReloadConfigAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                _vaultClient = null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<IVaultClient?> EnsureVaultClientAsync(bool forceRecreate = false)
        {
            if (_vaultClient != null && !forceRecreate)
            {
                return _vaultClient;
            }

            await _semaphore.WaitAsync();
            try
            {
                if (_vaultClient != null && !forceRecreate)
                {
                    return _vaultClient;
                }

                if (_vaultClientFactory != null)
                {
                    _vaultClient = _vaultClientFactory();
                    return _vaultClient;
                }

                // 1. Try to load enabled provider configuration from database repository
                var dbConfig = await TryLoadDbConfigAsync();

                if (dbConfig != null && !dbConfig.IsEnabled)
                {
                    _vaultClient = null;
                    return null;
                }

                string? address = dbConfig?.Address;
                string? roleId = dbConfig?.RoleId;
                string? secretId = dbConfig?.SecretId;
                string? token = dbConfig?.Token;
                string mountPath = dbConfig?.MountPath ?? "secret";

                // 2. Fall back to static IConfiguration / Environment variables
                address ??= _config["MCG_VAULT_ADDR"] ?? _config["Vault:Address"] ?? _config["VAULT_ADDR"] ?? Environment.GetEnvironmentVariable("MCG_VAULT_ADDR") ?? Environment.GetEnvironmentVariable("VAULT_ADDR");
                if (string.IsNullOrEmpty(address))
                {
                    _vaultClient = null;
                    return null;
                }

                if (!address.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("Vault Address must use the HTTP or HTTPS scheme.");
                }

                roleId ??= _config["MCG_VAULT_ROLE_ID"] ?? _config["Vault:RoleId"] ?? _config["VAULT_ROLE_ID"] ?? Environment.GetEnvironmentVariable("MCG_VAULT_ROLE_ID") ?? Environment.GetEnvironmentVariable("VAULT_ROLE_ID");
                secretId ??= _config["MCG_VAULT_SECRET_ID"] ?? _config["Vault:SecretId"] ?? _config["VAULT_SECRET_ID"] ?? Environment.GetEnvironmentVariable("MCG_VAULT_SECRET_ID") ?? Environment.GetEnvironmentVariable("VAULT_SECRET_ID");
                token ??= _config["MCG_VAULT_TOKEN"] ?? _config["Vault:Token"] ?? _config["VAULT_TOKEN"] ?? Environment.GetEnvironmentVariable("MCG_VAULT_TOKEN") ?? Environment.GetEnvironmentVariable("VAULT_TOKEN");

                _defaultMountPoint = mountPath;

                VaultSharp.V1.AuthMethods.IAuthMethodInfo authMethod;

                if (!string.IsNullOrEmpty(roleId) && !string.IsNullOrEmpty(secretId))
                {
                    authMethod = new AppRoleAuthMethodInfo(roleId, secretId);
                }
                else if (!string.IsNullOrEmpty(token))
                {
                    authMethod = new TokenAuthMethodInfo(token);
                }
                else
                {
                    _vaultClient = null;
                    return null;
                }

                var settings = new VaultClientSettings(address, authMethod);
                _vaultClient = new VaultClient(settings);

                return _vaultClient;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<string?> GetSecretAsync(string secretPath, string keyName)
        {
            string mountPoint = _defaultMountPoint;
            string path = secretPath;

            if (secretPath.Contains(':'))
            {
                var parts = secretPath.Split(':', 2);
                mountPoint = parts[0];
                path = parts[1];
            }

            // 1. Check cache FIRST before client creation or network operations
            string cacheKey = $"vault:{mountPoint}:{path}:{keyName}";
            if (_cache.TryGetValue(cacheKey, out string? cached) && cached != null)
            {
                return cached;
            }

            var client = await EnsureVaultClientAsync();
            if (client == null)
            {
                return null;
            }

            // 2. Perform JIT renewal check on token TTL
            long ttlSeconds;
            try
            {
                var tokenInfoSecret = await client.V1.Auth.Token.LookupSelfAsync();
                if (tokenInfoSecret?.Data != null)
                {
                    ttlSeconds = tokenInfoSecret.Data.TimeToLive;
                }
                else
                {
                    ttlSeconds = 0; // Null token data -> force re-login / client recreation
                }
            }
            catch
            {
                ttlSeconds = 0; // Exception -> force re-login / client recreation
            }

            if (ttlSeconds < 300) // Under 5 minutes remaining (or force re-login on exception/0)
            {
                client = await EnsureVaultClientAsync(forceRecreate: true);
                if (client == null)
                {
                    return null;
                }
            }

            try
            {
                var secretData = await client.V1.Secrets.KeyValue.V2.ReadSecretAsync(path: path, mountPoint: mountPoint);
                var value = secretData.Data.Data[keyName]?.ToString();
                if (value != null)
                {
                    _cache.Set(cacheKey, value, TimeSpan.FromMinutes(10));
                }
                return value;
            }
            catch (Exception ex)
            {
                throw new System.Security.SecurityException($"Vault secret read failed for path '{secretPath}', key '{keyName}'.", ex);
            }
        }
    }
}
