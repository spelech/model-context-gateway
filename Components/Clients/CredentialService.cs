using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Dapper;

namespace ModelContextGateway.Components.Clients
{
    public interface ICredentialService
    {
        Task<(AppKey AppKey, string PlaintextKey)> CreateCredentialAsync(
            string name,
            string username,
            string ownerSid,
            List<string> scopes,
            int? expiresInDays,
            string keyType = "personal");

        Task<(AppKey AppKey, string PlaintextKey)> RotateCredentialAsync(string id);

        Task<bool> RevokeCredentialAsync(string id);
    }

    public class CredentialService : ICredentialService
    {
        private const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        private readonly IDbConnectionFactory _dbFactory;

        public CredentialService(IDbConnectionFactory dbFactory)
        {
            _dbFactory = dbFactory;
        }

        private static string GenerateBase62String(int length)
        {
            return RandomNumberGenerator.GetString(Base62Chars, length);
        }

        public async Task<(AppKey AppKey, string PlaintextKey)> CreateCredentialAsync(
            string name,
            string username,
            string ownerSid,
            List<string> scopes,
            int? expiresInDays,
            string keyType = "personal")
        {
            var scopesList = scopes ?? new List<string> { "all" };
            string prefix;

            bool isAdmin = string.Equals(keyType, "system", StringComparison.OrdinalIgnoreCase) ||
                           scopesList.Any(s => string.Equals(s, "admin", StringComparison.OrdinalIgnoreCase));

            if (isAdmin)
            {
                prefix = "mcp-adm-";
            }
            else if (scopesList.Any(s => string.Equals(s, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "*", StringComparison.OrdinalIgnoreCase)))
            {
                prefix = "mcp-glb-";
            }
            else if (scopesList.Any(s => s.StartsWith("server:", StringComparison.OrdinalIgnoreCase)))
            {
                prefix = "mcp-srv-";
            }
            else if (scopesList.Any(s => s.StartsWith("group:", StringComparison.OrdinalIgnoreCase)))
            {
                var groupScope = scopesList.First(s => s.StartsWith("group:", StringComparison.OrdinalIgnoreCase));
                var domain = groupScope.Substring("group:".Length).Trim().ToLowerInvariant();
                prefix = !string.IsNullOrEmpty(domain) ? $"mcp-{domain}-" : "mcp-grp-";
            }
            else if (scopesList.Any(s => s.StartsWith("category:", StringComparison.OrdinalIgnoreCase)))
            {
                var catScope = scopesList.First(s => s.StartsWith("category:", StringComparison.OrdinalIgnoreCase));
                var cat = catScope.Substring("category:".Length).Trim().ToLowerInvariant();
                prefix = !string.IsNullOrEmpty(cat) ? $"mcp-{cat}-" : "mcp-grp-";
            }
            else
            {
                prefix = "mcp-usr-";
            }

            const int maxRetries = 3;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                // Generate compact ~32-character Base62 key: 8-char selector (~48 bits entropy) + 16-char secret (~95 bits entropy)
                var selector = GenerateBase62String(8);
                var secret = GenerateBase62String(16);

                var keyPrefix = $"{prefix}{selector}";
                var plaintextKey = $"{keyPrefix}-{secret}";

                // Store a secure one-way SHA-256 hash of the full plaintext key
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(plaintextKey));
                var encryptedKey = Convert.ToHexString(hashBytes).ToLowerInvariant();

                var appKey = new AppKey
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = name,
                    Username = username,
                    OwnerSid = ownerSid ?? string.Empty,
                    KeyType = string.IsNullOrEmpty(keyType) ? "personal" : keyType,
                    KeyPrefix = keyPrefix,
                    EncryptedKey = encryptedKey,
                    ScopesJson = JsonSerializer.Serialize(scopesList),
                    ExpiresAt = expiresInDays.HasValue ? DateTime.UtcNow.AddDays(expiresInDays.Value) : null,
                    CreatedAt = DateTime.UtcNow
                };

                using var conn = _dbFactory.CreateConnection();

                // Check for selector collision before insertion
                var collision = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM AppKeys WHERE KeyPrefix = @KeyPrefix;",
                    new { KeyPrefix = keyPrefix });
                if (collision > 0)
                {
                    continue; // Retry with fresh random Base62 tokens
                }

                try
                {
                    if (_dbFactory.ProviderName == "sqlite")
                    {
                        const string insertSql = @"
                            INSERT INTO AppKeys (Id, Name, Username, OwnerSid, KeyType, KeyPrefix, EncryptedKey, ScopesJson, ExpiresAt, CreatedAt)
                            VALUES (@Id, @Name, @Username, @OwnerSid, @KeyType, @KeyPrefix, @EncryptedKey, @ScopesJson, @ExpiresAt, @CreatedAt);";
                        await conn.ExecuteAsync(insertSql, appKey);
                    }
                    else if (_dbFactory.ProviderName == "mysql")
                    {
                        await conn.ExecuteAsync(
                            "sp_SaveAppKey",
                            new
                            {
                                p_Id = appKey.Id,
                                p_Name = appKey.Name,
                                p_Username = appKey.Username,
                                p_KeyPrefix = appKey.KeyPrefix,
                                p_EncryptedKey = appKey.EncryptedKey,
                                p_ScopesJson = appKey.ScopesJson,
                                p_OwnerSid = appKey.OwnerSid,
                                p_KeyType = string.IsNullOrEmpty(appKey.KeyType) ? "personal" : appKey.KeyType,
                                p_ExpiresAt = appKey.ExpiresAt
                            },
                            commandType: CommandType.StoredProcedure
                        );
                    }
                    else
                    {
                        await conn.ExecuteAsync(
                            "sp_SaveAppKey",
                            new
                            {
                                appKey.Id,
                                appKey.Name,
                                appKey.Username,
                                appKey.OwnerSid,
                                KeyType = string.IsNullOrEmpty(appKey.KeyType) ? "personal" : appKey.KeyType,
                                appKey.KeyPrefix,
                                appKey.EncryptedKey,
                                appKey.ScopesJson,
                                appKey.ExpiresAt
                            },
                            commandType: CommandType.StoredProcedure
                        );
                    }

                    return (appKey, plaintextKey);
                }
                catch (Exception) when (attempt < maxRetries - 1)
                {
                    // Retry on race-condition duplicate key conflict
                    continue;
                }
            }

            throw new InvalidOperationException("Failed to generate a unique credential after multiple attempts.");
        }

        public async Task<(AppKey AppKey, string PlaintextKey)> RotateCredentialAsync(string id)
        {
            using var conn = _dbFactory.CreateConnection();

            var existingKey = await conn.QueryFirstOrDefaultAsync<AppKey>(
                "SELECT * FROM AppKeys WHERE Id = @Id;",
                new { Id = id });

            if (existingKey == null)
            {
                throw new KeyNotFoundException($"AppKey with ID '{id}' not found.");
            }

            string prefix = "mcp-usr-";
            if (!string.IsNullOrEmpty(existingKey.KeyPrefix))
            {
                var lastDash = existingKey.KeyPrefix.LastIndexOf('-');
                if (lastDash >= 0)
                {
                    prefix = existingKey.KeyPrefix.Substring(0, lastDash + 1);
                }
            }

            const int maxRetries = 3;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var selector = GenerateBase62String(8);
                var secret = GenerateBase62String(16);

                var keyPrefix = $"{prefix}{selector}";
                var plaintextKey = $"{keyPrefix}-{secret}";

                var collision = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM AppKeys WHERE KeyPrefix = @KeyPrefix;",
                    new { KeyPrefix = keyPrefix });
                if (collision > 0)
                {
                    continue;
                }

                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(plaintextKey));
                var encryptedKey = Convert.ToHexString(hashBytes).ToLowerInvariant();

                try
                {
                    await conn.ExecuteAsync(
                        "UPDATE AppKeys SET KeyPrefix = @KeyPrefix, EncryptedKey = @EncryptedKey, LastUsedAt = NULL WHERE Id = @Id;",
                        new { Id = id, KeyPrefix = keyPrefix, EncryptedKey = encryptedKey });

                    existingKey.KeyPrefix = keyPrefix;
                    existingKey.EncryptedKey = encryptedKey;
                    existingKey.LastUsedAt = null;

                    return (existingKey, plaintextKey);
                }
                catch (Exception) when (attempt < maxRetries - 1)
                {
                    continue;
                }
            }

            throw new InvalidOperationException("Failed to generate a unique credential after multiple attempts.");
        }

        public async Task<bool> RevokeCredentialAsync(string id)
        {
            using var conn = _dbFactory.CreateConnection();

            var exists = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM AppKeys WHERE Id = @Id;",
                new { Id = id });
            if (exists == 0)
            {
                return false;
            }

            if (_dbFactory.ProviderName == "sqlite")
            {
                const string deleteSql = "DELETE FROM AppKeys WHERE Id = @Id;";
                await conn.ExecuteAsync(deleteSql, new { Id = id });
            }
            else if (_dbFactory.ProviderName == "mysql")
            {
                await conn.ExecuteAsync(
                    "sp_DeleteAppKey",
                    new { p_Id = id },
                    commandType: CommandType.StoredProcedure
                );
            }
            else
            {
                // For SQL Server, sp_DeleteAppKey uses SET NOCOUNT ON (returning -1 from ExecuteAsync).
                // Verified existence above guarantees accurate deletion status.
                await conn.ExecuteAsync(
                    "sp_DeleteAppKey",
                    new { Id = id },
                    commandType: CommandType.StoredProcedure
                );
            }

            return true;
        }
    }
}

