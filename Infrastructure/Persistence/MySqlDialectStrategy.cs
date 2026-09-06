using System.Data;
using Dapper;

namespace ModelContextGateway.Infrastructure.Persistence
{
    public class MySqlDialectStrategy : ISqlDialectStrategy
    {
        public string ProviderName => "mysql";

        public async Task SaveServerAsync(IDbConnection conn, McpServer server)
        {
            await conn.ExecuteAsync(@"
                INSERT INTO `Servers` (`Id`, `Alias`, `DisplayName`, `Url`, `Enabled`, `Hidden`, `Type`, `SecretProvider`, `SecretItemKey`, `SecretMount`, `SecretPath`, `SecretField`, `AuthShape`, `CustomHeaderName`, `Categories`, `ApiKey`, `HeadersJson`)
                VALUES (@Id, @Alias, @DisplayName, @Url, @Enabled, @Hidden, @Type, @SecretProvider, @SecretItemKey, @SecretMount, @SecretPath, @SecretField, @AuthShape, @CustomHeaderName, @Categories, @ApiKey, @HeadersJson)
                ON DUPLICATE KEY UPDATE
                    `Alias` = @Alias,
                    `DisplayName` = @DisplayName,
                    `Url` = @Url,
                    `Enabled` = @Enabled,
                    `Hidden` = @Hidden,
                    `Type` = @Type,
                    `SecretProvider` = @SecretProvider,
                    `SecretItemKey` = @SecretItemKey,
                    `SecretMount` = @SecretMount,
                    `SecretPath` = @SecretPath,
                    `SecretField` = @SecretField,
                    `AuthShape` = @AuthShape,
                    `CustomHeaderName` = @CustomHeaderName,
                    `Categories` = @Categories,
                    `ApiKey` = @ApiKey,
                    `HeadersJson` = @HeadersJson;
            ", server);
        }

        public async Task<IEnumerable<AppKey>> GetAppKeysAsync(IDbConnection conn, string? usernameFilter, bool isAdmin, string? currentUser, string? keyType = null)
        {
            var sql = "SELECT * FROM `AppKeys` WHERE 1=1";
            var p = new DynamicParameters();
            if (!isAdmin)
            {
                sql += " AND `Username` = @Username";
                p.Add("Username", currentUser);
            }
            else if (!string.IsNullOrEmpty(usernameFilter))
            {
                sql += " AND `Username` = @Username";
                p.Add("Username", usernameFilter);
            }
            if (!string.IsNullOrEmpty(keyType))
            {
                sql += " AND `KeyType` = @KeyType";
                p.Add("KeyType", keyType);
            }
            sql += " ORDER BY `CreatedAt` DESC;";
            return await conn.QueryAsync<AppKey>(sql, p);
        }

        public async Task SaveAppKeyAsync(IDbConnection conn, AppKey key)
        {
            await conn.ExecuteAsync(@"
                INSERT INTO `AppKeys` (`Id`, `Name`, `Username`, `OwnerSid`, `KeyType`, `KeyPrefix`, `EncryptedKey`, `ScopesJson`, `ExpiresAt`, `CreatedAt`)
                VALUES (@Id, @Name, @Username, @OwnerSid, @KeyType, @KeyPrefix, @EncryptedKey, @ScopesJson, @ExpiresAt, @CreatedAt)
                ON DUPLICATE KEY UPDATE
                    `Name` = @Name,
                    `Username` = @Username,
                    `OwnerSid` = @OwnerSid,
                    `KeyType` = @KeyType,
                    `KeyPrefix` = @KeyPrefix,
                    `EncryptedKey` = @EncryptedKey,
                    `ScopesJson` = @ScopesJson,
                    `ExpiresAt` = @ExpiresAt;
            ", key);
        }

        public async Task DeleteAppKeyAsync(IDbConnection conn, string id)
        {
            await conn.ExecuteAsync("DELETE FROM `AppKeys` WHERE `Id` = @Id", new { Id = id });
        }

        public async Task SaveSecretProviderAsync(IDbConnection conn, SecretProviderDto dto, string? encryptedConfig)
        {
            await conn.ExecuteAsync(@"
                INSERT INTO `SecretProviders` (`ProviderName`, `DisplayName`, `EncryptedConfigJson`, `IsEnabled`)
                VALUES (@ProviderName, @DisplayName, @EncryptedConfigJson, @IsEnabled)
                ON DUPLICATE KEY UPDATE
                    `DisplayName` = @DisplayName,
                    `EncryptedConfigJson` = @EncryptedConfigJson,
                    `IsEnabled` = @IsEnabled;
            ", new { dto.ProviderName, dto.DisplayName, EncryptedConfigJson = encryptedConfig, dto.IsEnabled });
        }

        public async Task SaveAuthProviderAsync(IDbConnection conn, AuthProviderDto dto, string? encryptedConfig)
        {
            await conn.ExecuteAsync(@"
                INSERT INTO `AuthProviderConfigs` (`ProviderName`, `DisplayName`, `UserHeader`, `GroupsHeader`, `EncryptedConfigJson`, `IsEnabled`)
                VALUES (@ProviderName, @DisplayName, @UserHeader, @GroupsHeader, @EncryptedConfigJson, @IsEnabled)
                ON DUPLICATE KEY UPDATE
                    `DisplayName` = @DisplayName,
                    `UserHeader` = @UserHeader,
                    `GroupsHeader` = @GroupsHeader,
                    `EncryptedConfigJson` = @EncryptedConfigJson,
                    `IsEnabled` = @IsEnabled;
            ", new { dto.ProviderName, dto.DisplayName, dto.UserHeader, dto.GroupsHeader, EncryptedConfigJson = encryptedConfig, dto.IsEnabled });
        }
    }
}
