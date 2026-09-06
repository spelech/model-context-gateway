using System.Data;
using Dapper;

namespace ModelContextGateway.Infrastructure.Persistence
{
    public interface IMasterKeyManager
    {
        Task ReencryptDatabaseSecretsAsync(string newMasterKey);
    }

    public interface ISettingRepository
    {
        Task<RouterSettings?> GetSettingsAsync();
        Task SaveSettingsAsync(RouterSettings settings);
    }

    public interface IServerRepository
    {
        Task<IEnumerable<McpServer>> GetServersAsync();
        Task<IEnumerable<McpServer>> GetEnabledServersAsync();
        Task<McpServer?> GetServerByIdAsync(string id);
        Task SaveServerAsync(McpServer server);
        Task DeleteServerAsync(string id);
    }

    public interface IAppKeyRepository
    {
        Task<IEnumerable<AppKey>> GetAppKeysAsync(string? usernameFilter = null, bool isAdmin = false, string? currentUser = null, string? keyType = null);
        Task<AppKey?> GetAppKeyByIdAsync(string id);
        Task SaveAppKeyAsync(AppKey key);
        Task DeleteAppKeyAsync(string id);
        Task<int> GetTotalActiveKeysAsync();
        Task<int> GetUserActiveKeysAsync(string username);
    }

    public interface IUserQuotaRepository
    {
        Task<UserQuota?> GetUserQuotaAsync(string username);
        Task<IEnumerable<UserQuota>> GetAllUserQuotasAsync();
        Task SetUserQuotaAsync(string username, int maxKeys);
        Task DeleteUserQuotaAsync(string username);
    }

    public interface IOAuthClientRepository
    {
        Task<IEnumerable<OAuthClient>> GetOAuthClientsAsync();
        Task<OAuthClient?> GetOAuthClientByIdAsync(string clientId);
        Task<OAuthClient?> FindDcrClientAsync(string clientName, string clientType);
        Task SaveOAuthClientAsync(OAuthClient client);
        Task<bool> DeleteOAuthClientAsync(string clientId);
        Task<int> CleanupDcrClientsAsync(int retentionDays = 30);
    }

    public interface ISecretProviderRepository
    {
        Task<IEnumerable<SecretProviderDto>> GetSecretProvidersAsync();
        Task SaveSecretProviderAsync(SecretProviderDto dto);
    }

    public interface IAuthProviderRepository
    {
        Task<IEnumerable<AuthProviderDto>> GetAuthProvidersAsync();
        Task SaveAuthProviderAsync(AuthProviderDto dto);
    }
    public class UserCredentialDto
    {
        public string Id { get; set; } = "";
        public string Username { get; set; } = "";
        public string ServerId { get; set; } = "";
        public string EncryptedSecretJson { get; set; } = "";
    }

    public interface IUserCredentialRepository
    {
        Task<UserCredentialDto?> GetCredentialAsync(string username, string serverId);
        Task SaveCredentialAsync(UserCredentialDto dto);
        Task DeleteCredentialAsync(string username, string serverId);
        Task<IEnumerable<string>> GetServerIdsAsync(string username);
    }


    public class DatabaseRepository :
        ISettingRepository,
        IServerRepository,
        IAppKeyRepository,
        ISecretProviderRepository,
        IAuthProviderRepository,
        IUserCredentialRepository,
        IUserQuotaRepository,
        IOAuthClientRepository,
        IMasterKeyManager
    {
        private readonly IDbConnectionFactory _dbFactory;
        private readonly IConfiguration? _config;

        public DatabaseRepository(IDbConnectionFactory dbFactory, IConfiguration? config = null)
        {
            _dbFactory = dbFactory;
            _config = config;
        }

        // ==========================================
        // ISettingRepository
        // ==========================================
        public async Task<RouterSettings?> GetSettingsAsync()
        {
            using var conn = _dbFactory.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<RouterSettings>("SELECT * FROM Settings WHERE Id = 'default';");
        }

        public async Task SaveSettingsAsync(RouterSettings settings)
        {
            using var conn = _dbFactory.CreateConnection();
            var exists = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Settings WHERE Id = 'default';");
            if (exists == 0)
            {
                const string insertSql = @"
                    INSERT INTO Settings (Id, DashboardTitle, DashboardIcon, EmbeddingProvider, EmbeddingApiUrl, EmbeddingApiKey, EmbeddingApiModel, EmbeddingModelDir, GlobalMaxKeys, UserMaxKeys)
                    VALUES ('default', @DashboardTitle, @DashboardIcon, @EmbeddingProvider, @EmbeddingApiUrl, @EmbeddingApiKey, @EmbeddingApiModel, @EmbeddingModelDir, @GlobalMaxKeys, @UserMaxKeys);";
                await conn.ExecuteAsync(insertSql, settings);
            }
            else
            {
                const string updateSql = @"
                    UPDATE Settings
                    SET DashboardTitle = @DashboardTitle,
                        DashboardIcon = @DashboardIcon,
                        EmbeddingProvider = @EmbeddingProvider,
                        EmbeddingApiUrl = @EmbeddingApiUrl,
                        EmbeddingApiKey = @EmbeddingApiKey,
                        EmbeddingApiModel = @EmbeddingApiModel,
                        EmbeddingModelDir = @EmbeddingModelDir,
                        GlobalMaxKeys = @GlobalMaxKeys,
                        UserMaxKeys = @UserMaxKeys
                    WHERE Id = 'default';";
                await conn.ExecuteAsync(updateSql, settings);
            }
        }

        // ==========================================
        // IServerRepository
        // ==========================================
        public async Task<IEnumerable<McpServer>> GetServersAsync()
        {
            using var conn = _dbFactory.CreateConnection();
            DatabaseInitializer.EnsureAliasColumn(conn);
            return await conn.QueryAsync<McpServer>(@"
                SELECT Id, Alias, DisplayName, Url, Enabled, Hidden, Type, Categories, SecretProvider,
                       SecretItemKey, SecretMount, SecretPath, SecretField, AuthShape,
                       CustomHeaderName, ApiKey, HeadersJson, AutoDiscovered
                FROM Servers;");
        }

        public async Task<IEnumerable<McpServer>> GetEnabledServersAsync()
        {
            using var conn = _dbFactory.CreateConnection();
            return await conn.QueryAsync<McpServer>("SELECT * FROM Servers WHERE Enabled = @Enabled;", new { Enabled = 1 });
        }

        public async Task<McpServer?> GetServerByIdAsync(string id)
        {
            using var conn = _dbFactory.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<McpServer>("SELECT * FROM Servers WHERE Id = @Id;", new { Id = id });
        }

        public async Task SaveServerAsync(McpServer server)
        {
            using var conn = _dbFactory.CreateConnection();
            DatabaseInitializer.EnsureAliasColumn(conn);
            var provider = _dbFactory.ProviderName.ToLower();

            if (provider == "sqlite")
            {
                const string sql = @"
                    INSERT INTO Servers (Id, Alias, DisplayName, Url, Enabled, Hidden, Type, SecretProvider, SecretItemKey, SecretMount, SecretPath, SecretField, AuthShape, CustomHeaderName, Categories, ApiKey, HeadersJson, AutoDiscovered)
                    VALUES (@Id, @Alias, @DisplayName, @Url, @Enabled, @Hidden, @Type, @SecretProvider, @SecretItemKey, @SecretMount, @SecretPath, @SecretField, @AuthShape, @CustomHeaderName, @Categories, @ApiKey, @HeadersJson, @AutoDiscovered)
                    ON CONFLICT(Id) DO UPDATE SET
                        Alias = @Alias, DisplayName = @DisplayName, Url = @Url, Enabled = @Enabled, Hidden = @Hidden, Type = @Type,
                        SecretProvider = @SecretProvider, SecretItemKey = @SecretItemKey, SecretMount = @SecretMount,
                        SecretPath = @SecretPath, SecretField = @SecretField, AuthShape = @AuthShape,
                        CustomHeaderName = @CustomHeaderName, Categories = @Categories, ApiKey = @ApiKey,
                        HeadersJson = @HeadersJson, AutoDiscovered = @AutoDiscovered;";
                await conn.ExecuteAsync(sql, server);
            }
            else if (provider == "mssql")
            {
                const string sql = @"
                    IF EXISTS (SELECT 1 FROM Servers WHERE Id = @Id)
                    BEGIN
                        UPDATE Servers SET Alias = @Alias, DisplayName = @DisplayName, Url = @Url, Enabled = @Enabled, Hidden = @Hidden, Type = @Type,
                            SecretProvider = @SecretProvider, SecretItemKey = @SecretItemKey, SecretMount = @SecretMount,
                            SecretPath = @SecretPath, SecretField = @SecretField, AuthShape = @AuthShape,
                            CustomHeaderName = @CustomHeaderName, Categories = @Categories, ApiKey = @ApiKey,
                            HeadersJson = @HeadersJson, AutoDiscovered = @AutoDiscovered WHERE Id = @Id;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO Servers (Id, Alias, DisplayName, Url, Enabled, Hidden, Type, SecretProvider, SecretItemKey, SecretMount, SecretPath, SecretField, AuthShape, CustomHeaderName, Categories, ApiKey, HeadersJson, AutoDiscovered)
                        VALUES (@Id, @Alias, @DisplayName, @Url, @Enabled, @Hidden, @Type, @SecretProvider, @SecretItemKey, @SecretMount, @SecretPath, @SecretField, @AuthShape, @CustomHeaderName, @Categories, @ApiKey, @HeadersJson, @AutoDiscovered);
                    END;";
                await conn.ExecuteAsync(sql, server);
            }
            else if (provider == "mysql")
            {
                const string sql = @"
                    INSERT INTO Servers (Id, Alias, DisplayName, Url, Enabled, Hidden, Type, SecretProvider, SecretItemKey, SecretMount, SecretPath, SecretField, AuthShape, CustomHeaderName, Categories, ApiKey, HeadersJson, AutoDiscovered)
                    VALUES (@Id, @Alias, @DisplayName, @Url, @Enabled, @Hidden, @Type, @SecretProvider, @SecretItemKey, @SecretMount, @SecretPath, @SecretField, @AuthShape, @CustomHeaderName, @Categories, @ApiKey, @HeadersJson, @AutoDiscovered)
                    ON DUPLICATE KEY UPDATE
                        Alias = @Alias, DisplayName = @DisplayName, Url = @Url, Enabled = @Enabled, Hidden = @Hidden, Type = @Type,
                        SecretProvider = @SecretProvider, SecretItemKey = @SecretItemKey, SecretMount = @SecretMount,
                        SecretPath = @SecretPath, SecretField = @SecretField, AuthShape = @AuthShape,
                        CustomHeaderName = @CustomHeaderName, Categories = @Categories, ApiKey = @ApiKey,
                        HeadersJson = @HeadersJson, AutoDiscovered = @AutoDiscovered;";
                await conn.ExecuteAsync(sql, server);
            }
        }

        public async Task DeleteServerAsync(string id)
        {
            using var conn = _dbFactory.CreateConnection();
            await conn.ExecuteAsync("DELETE FROM Servers WHERE Id = @Id;", new { Id = id });
        }

        // ==========================================
        // IAppKeyRepository
        // ==========================================
        public async Task<IEnumerable<AppKey>> GetAppKeysAsync(string? usernameFilter = null, bool isAdmin = false, string? currentUser = null, string? keyType = null)
        {
            using var conn = _dbFactory.CreateConnection();
            var provider = _dbFactory.ProviderName.ToLower();

            if (provider == "sqlite")
            {
                var sql = "SELECT * FROM AppKeys WHERE 1=1";
                var p = new DynamicParameters();

                if (!isAdmin)
                {
                    sql += " AND Username = @Username";
                    p.Add("Username", currentUser);
                }
                else if (!string.IsNullOrEmpty(usernameFilter))
                {
                    sql += " AND Username = @Username";
                    p.Add("Username", usernameFilter);
                }

                if (!string.IsNullOrEmpty(keyType))
                {
                    sql += " AND KeyType = @KeyType";
                    p.Add("KeyType", keyType);
                }

                sql += " ORDER BY CreatedAt DESC;";
                return await conn.QueryAsync<AppKey>(sql, p);
            }
            else if (provider == "mysql")
            {
                var parameters = new
                {
                    p_Username = isAdmin ? usernameFilter : currentUser,
                    p_KeyType = keyType
                };
                return await conn.QueryAsync<AppKey>(
                    "sp_GetAppKeys",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );
            }
            else
            {
                var parameters = new
                {
                    Username = isAdmin ? usernameFilter : currentUser,
                    KeyType = keyType
                };
                return await conn.QueryAsync<AppKey>(
                    "sp_GetAppKeys",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );
            }
        }

        public async Task<AppKey?> GetAppKeyByIdAsync(string id)
        {
            using var conn = _dbFactory.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<AppKey>("SELECT * FROM AppKeys WHERE Id = @Id;", new { Id = id });
        }

        public async Task SaveAppKeyAsync(AppKey key)
        {
            using var conn = _dbFactory.CreateConnection();
            var provider = _dbFactory.ProviderName.ToLower();

            if (provider == "sqlite")
            {
                const string sql = @"
                    INSERT INTO AppKeys (Id, Name, Username, OwnerSid, KeyType, KeyPrefix, EncryptedKey, ScopesJson, ExpiresAt, CreatedAt)
                    VALUES (@Id, @Name, @Username, @OwnerSid, @KeyType, @KeyPrefix, @EncryptedKey, @ScopesJson, @ExpiresAt, @CreatedAt)
                    ON CONFLICT(Id) DO UPDATE SET
                        Name = @Name, Username = @Username, OwnerSid = @OwnerSid, KeyType = @KeyType, KeyPrefix = @KeyPrefix,
                        EncryptedKey = @EncryptedKey, ScopesJson = @ScopesJson, ExpiresAt = @ExpiresAt;";
                await conn.ExecuteAsync(sql, key);
            }
            else if (provider == "mysql")
            {
                await conn.ExecuteAsync(
                    "sp_SaveAppKey",
                    new
                    {
                        p_Id = key.Id,
                        p_Name = key.Name,
                        p_Username = key.Username,
                        p_KeyPrefix = key.KeyPrefix,
                        p_EncryptedKey = key.EncryptedKey,
                        p_ScopesJson = key.ScopesJson,
                        p_OwnerSid = key.OwnerSid ?? "",
                        p_KeyType = string.IsNullOrEmpty(key.KeyType) ? "personal" : key.KeyType,
                        p_ExpiresAt = key.ExpiresAt
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
                        key.Id,
                        key.Name,
                        key.Username,
                        key.OwnerSid,
                        KeyType = string.IsNullOrEmpty(key.KeyType) ? "personal" : key.KeyType,
                        key.KeyPrefix,
                        key.EncryptedKey,
                        key.ScopesJson,
                        key.ExpiresAt
                    },
                    commandType: CommandType.StoredProcedure
                );
            }
        }

        public async Task DeleteAppKeyAsync(string id)
        {
            using var conn = _dbFactory.CreateConnection();
            var provider = _dbFactory.ProviderName.ToLower();

            if (provider == "sqlite")
            {
                await conn.ExecuteAsync("DELETE FROM AppKeys WHERE Id = @Id;", new { Id = id });
            }
            else if (provider == "mysql")
            {
                await conn.ExecuteAsync(
                    "sp_DeleteAppKey",
                    new { p_Id = id },
                    commandType: CommandType.StoredProcedure
                );
            }
            else
            {
                await conn.ExecuteAsync(
                    "sp_DeleteAppKey",
                    new { Id = id },
                    commandType: CommandType.StoredProcedure
                );
            }
        }

        public async Task<int> GetTotalActiveKeysAsync()
        {
            using var conn = _dbFactory.CreateConnection();
            return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AppKeys;");
        }

        public async Task<int> GetUserActiveKeysAsync(string username)
        {
            using var conn = _dbFactory.CreateConnection();
            return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AppKeys WHERE Username = @Username AND (KeyType IS NULL OR KeyType != 'system');", new { Username = username });
        }

        // ==========================================
        // ISecretProviderRepository
        // ==========================================
        public async Task<IEnumerable<SecretProviderDto>> GetSecretProvidersAsync()
        {
            using var conn = _dbFactory.CreateConnection();
            var list = (await conn.QueryAsync<SecretProviderDto>("SELECT ProviderName, DisplayName, EncryptedConfigJson AS ConfigJson, IsEnabled FROM SecretProviders;")).ToList();
            if (_config != null)
            {
                foreach (var item in list)
                {
                    if (!string.IsNullOrEmpty(item.ConfigJson))
                    {
                        if (SymmetricEncryptionHelper.TryDecrypt(item.ConfigJson, _config, out var decrypted))
                        {
                            item.ConfigJson = decrypted;
                        }
                        else
                        {
                            item.IsDecryptionFailed = true;
                        }
                    }
                }
            }
            return list;
        }

        public async Task SaveSecretProviderAsync(SecretProviderDto dto)
        {
            using var conn = _dbFactory.CreateConnection();
            var provider = _dbFactory.ProviderName.ToLower();

            string? configToSave;
            if (dto.IsDecryptionFailed)
            {
                // Preserve the existing encrypted payload to avoid data loss
                configToSave = await conn.ExecuteScalarAsync<string>("SELECT EncryptedConfigJson FROM SecretProviders WHERE ProviderName = @ProviderName;", new { dto.ProviderName });
            }
            else
            {
                configToSave = dto.ConfigJson;
                if (!string.IsNullOrEmpty(configToSave) && _config != null)
                {
                    configToSave = SymmetricEncryptionHelper.Encrypt(configToSave, _config);
                }
            }

            var param = new
            {
                dto.ProviderName,
                dto.DisplayName,
                ConfigJson = configToSave,
                dto.IsEnabled
            };

            if (provider == "sqlite")
            {
                const string sql = @"
                    INSERT INTO SecretProviders (ProviderName, DisplayName, EncryptedConfigJson, IsEnabled)
                    VALUES (@ProviderName, @DisplayName, @ConfigJson, @IsEnabled)
                    ON CONFLICT(ProviderName) DO UPDATE SET DisplayName = @DisplayName, EncryptedConfigJson = @ConfigJson, IsEnabled = @IsEnabled;";
                await conn.ExecuteAsync(sql, param);
            }
            else if (provider == "mysql")
            {
                await conn.ExecuteAsync("sp_SaveSecretProvider", new
                {
                    p_ProviderName = dto.ProviderName,
                    p_DisplayName = dto.DisplayName,
                    p_EncryptedConfigJson = configToSave,
                    p_IsEnabled = dto.IsEnabled ? 1 : 0
                }, commandType: CommandType.StoredProcedure);
            }
            else
            {
                await conn.ExecuteAsync("sp_SaveSecretProvider", new
                {
                    dto.ProviderName,
                    dto.DisplayName,
                    EncryptedConfigJson = configToSave,
                    dto.IsEnabled
                }, commandType: CommandType.StoredProcedure);
            }
        }

        // ==========================================
        // IAuthProviderRepository
        // ==========================================
        public async Task<IEnumerable<AuthProviderDto>> GetAuthProvidersAsync()
        {
            using var conn = _dbFactory.CreateConnection();
            var list = (await conn.QueryAsync<AuthProviderDto>("SELECT ProviderName, DisplayName, UserHeader, GroupsHeader, EncryptedConfigJson AS ConfigJson, IsEnabled FROM AuthProviderConfigs;")).ToList();
            if (_config != null)
            {
                foreach (var item in list)
                {
                    if (!string.IsNullOrEmpty(item.ConfigJson))
                    {
                        if (SymmetricEncryptionHelper.TryDecrypt(item.ConfigJson, _config, out var decrypted))
                        {
                            item.ConfigJson = decrypted;
                        }
                        else
                        {
                            item.IsDecryptionFailed = true;
                        }
                    }
                }
            }
            return list;
        }

        public async Task SaveAuthProviderAsync(AuthProviderDto dto)
        {
            using var conn = _dbFactory.CreateConnection();
            var provider = _dbFactory.ProviderName.ToLower();

            string? configToSave;
            if (dto.IsDecryptionFailed)
            {
                // Preserve the existing encrypted payload to avoid data loss
                configToSave = await conn.ExecuteScalarAsync<string>("SELECT EncryptedConfigJson FROM AuthProviderConfigs WHERE ProviderName = @ProviderName;", new { dto.ProviderName });
            }
            else
            {
                configToSave = dto.ConfigJson;
                if (!string.IsNullOrEmpty(configToSave) && _config != null)
                {
                    configToSave = SymmetricEncryptionHelper.Encrypt(configToSave, _config);
                }
            }

            var param = new
            {
                dto.ProviderName,
                dto.DisplayName,
                dto.UserHeader,
                dto.GroupsHeader,
                ConfigJson = configToSave,
                dto.IsEnabled
            };

            if (provider == "sqlite")
            {
                const string sql = @"
                    INSERT INTO AuthProviderConfigs (ProviderName, DisplayName, UserHeader, GroupsHeader, EncryptedConfigJson, IsEnabled)
                    VALUES (@ProviderName, @DisplayName, @UserHeader, @GroupsHeader, @ConfigJson, @IsEnabled)
                    ON CONFLICT(ProviderName) DO UPDATE SET DisplayName = @DisplayName, UserHeader = @UserHeader, GroupsHeader = @GroupsHeader, EncryptedConfigJson = @ConfigJson, IsEnabled = @IsEnabled;";
                await conn.ExecuteAsync(sql, param);
            }
            else if (provider == "mysql")
            {
                await conn.ExecuteAsync("sp_SaveAuthProvider", new
                {
                    p_ProviderName = dto.ProviderName,
                    p_DisplayName = dto.DisplayName,
                    p_UserHeader = dto.UserHeader,
                    p_GroupsHeader = dto.GroupsHeader,
                    p_EncryptedConfigJson = configToSave,
                    p_IsEnabled = dto.IsEnabled ? 1 : 0
                }, commandType: CommandType.StoredProcedure);
            }
            else
            {
                await conn.ExecuteAsync("sp_SaveAuthProvider", new
                {
                    dto.ProviderName,
                    dto.DisplayName,
                    dto.UserHeader,
                    dto.GroupsHeader,
                    EncryptedConfigJson = configToSave,
                    dto.IsEnabled
                }, commandType: CommandType.StoredProcedure);
            }
        }

        public async Task SaveAuthProvidersBatchAsync(IEnumerable<AuthProviderDto> dtos)
        {
            using var conn = _dbFactory.CreateConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                var provider = _dbFactory.ProviderName.ToLower();

                foreach (var dto in dtos)
                {
                    var configToSave = dto.ConfigJson;
                    if (!string.IsNullOrEmpty(configToSave) && _config != null)
                    {
                        configToSave = SymmetricEncryptionHelper.Encrypt(configToSave, _config);
                    }

                    var param = new
                    {
                        dto.ProviderName,
                        dto.DisplayName,
                        dto.UserHeader,
                        dto.GroupsHeader,
                        ConfigJson = configToSave,
                        dto.IsEnabled
                    };

                    if (provider == "sqlite")
                    {
                        const string sql = @"
                            INSERT INTO AuthProviderConfigs (ProviderName, DisplayName, UserHeader, GroupsHeader, EncryptedConfigJson, IsEnabled)
                            VALUES (@ProviderName, @DisplayName, @UserHeader, @GroupsHeader, @ConfigJson, @IsEnabled)
                            ON CONFLICT(ProviderName) DO UPDATE SET DisplayName = @DisplayName, UserHeader = @UserHeader, GroupsHeader = @GroupsHeader, EncryptedConfigJson = @ConfigJson, IsEnabled = @IsEnabled;";
                        await conn.ExecuteAsync(sql, param, transaction: tx);
                    }
                    else if (provider == "mysql")
                    {
                        await conn.ExecuteAsync("sp_SaveAuthProvider", new
                        {
                            p_ProviderName = dto.ProviderName,
                            p_DisplayName = dto.DisplayName,
                            p_UserHeader = dto.UserHeader,
                            p_GroupsHeader = dto.GroupsHeader,
                            p_EncryptedConfigJson = configToSave,
                            p_IsEnabled = dto.IsEnabled ? 1 : 0
                        }, commandType: System.Data.CommandType.StoredProcedure, transaction: tx);
                    }
                    else
                    {
                        await conn.ExecuteAsync("sp_SaveAuthProvider", new
                        {
                            dto.ProviderName,
                            dto.DisplayName,
                            dto.UserHeader,
                            dto.GroupsHeader,
                            EncryptedConfigJson = configToSave,
                            dto.IsEnabled
                        }, commandType: System.Data.CommandType.StoredProcedure, transaction: tx);
                    }
                }
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
        // ==========================================
        // IUserCredentialRepository
        // ==========================================
        public async Task<UserCredentialDto?> GetCredentialAsync(string username, string serverId)
        {
            using var conn = _dbFactory.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<UserCredentialDto>(
                "SELECT * FROM UserServerCredentials WHERE Username = @Username AND ServerId = @ServerId;",
                new { Username = username, ServerId = serverId });
        }

        public async Task SaveCredentialAsync(UserCredentialDto dto)
        {
            using var conn = _dbFactory.CreateConnection();
            var provider = _dbFactory.ProviderName.ToLower();

            if (provider == "sqlite")
            {
                const string sql = @"
                    INSERT INTO UserServerCredentials (Id, Username, ServerId, EncryptedSecretJson)
                    VALUES (@Id, @Username, @ServerId, @EncryptedSecretJson)
                    ON CONFLICT(Id) DO UPDATE SET
                        EncryptedSecretJson = @EncryptedSecretJson;";
                await conn.ExecuteAsync(sql, dto);
            }
            else if (provider == "mysql")
            {
                const string sql = @"
                    INSERT INTO UserServerCredentials (Id, Username, ServerId, EncryptedSecretJson)
                    VALUES (@Id, @Username, @ServerId, @EncryptedSecretJson)
                    ON DUPLICATE KEY UPDATE
                        EncryptedSecretJson = @EncryptedSecretJson;";
                await conn.ExecuteAsync(sql, dto);
            }
            else
            {
                const string sql = @"
                    IF EXISTS (SELECT 1 FROM UserServerCredentials WHERE Id = @Id)
                    BEGIN
                        UPDATE UserServerCredentials SET EncryptedSecretJson = @EncryptedSecretJson WHERE Id = @Id;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO UserServerCredentials (Id, Username, ServerId, EncryptedSecretJson)
                        VALUES (@Id, @Username, @ServerId, @EncryptedSecretJson);
                    END;";
                await conn.ExecuteAsync(sql, dto);
            }
        }

        public async Task DeleteCredentialAsync(string username, string serverId)
        {
            using var conn = _dbFactory.CreateConnection();
            await conn.ExecuteAsync(
                "DELETE FROM UserServerCredentials WHERE Username = @Username AND ServerId = @ServerId;",
                new { Username = username, ServerId = serverId });
        }

        public async Task<IEnumerable<string>> GetServerIdsAsync(string username)
        {
            using var conn = _dbFactory.CreateConnection();
            return await conn.QueryAsync<string>(
                "SELECT ServerId FROM UserServerCredentials WHERE Username = @Username;",
                new { Username = username });
        }

        // ==========================================
        // IUserQuotaRepository
        // ==========================================
        public async Task<UserQuota?> GetUserQuotaAsync(string username)
        {
            using var conn = _dbFactory.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<UserQuota>(
                "SELECT * FROM UserQuotas WHERE Username = @Username;",
                new { Username = username });
        }

        public async Task<IEnumerable<UserQuota>> GetAllUserQuotasAsync()
        {
            using var conn = _dbFactory.CreateConnection();
            return await conn.QueryAsync<UserQuota>("SELECT * FROM UserQuotas ORDER BY Username ASC;");
        }

        public async Task SetUserQuotaAsync(string username, int maxKeys)
        {
            using var conn = _dbFactory.CreateConnection();
            var provider = _dbFactory.ProviderName.ToLower();

            if (provider == "sqlite")
            {
                const string sql = @"
                    INSERT INTO UserQuotas (Username, MaxKeys, CreatedAt, UpdatedAt)
                    VALUES (@Username, @MaxKeys, @Now, @Now)
                    ON CONFLICT(Username) DO UPDATE SET
                        MaxKeys = @MaxKeys,
                        UpdatedAt = @Now;";
                await conn.ExecuteAsync(sql, new { Username = username, MaxKeys = maxKeys, Now = DateTime.UtcNow });
            }
            else if (provider == "mysql")
            {
                const string sql = @"
                    INSERT INTO `UserQuotas` (`Username`, `MaxKeys`, `CreatedAt`, `UpdatedAt`)
                    VALUES (@Username, @MaxKeys, NOW(), NOW())
                    ON DUPLICATE KEY UPDATE
                        `MaxKeys` = @MaxKeys,
                        `UpdatedAt` = NOW();";
                await conn.ExecuteAsync(sql, new { Username = username, MaxKeys = maxKeys });
            }
            else
            {
                const string sql = @"
                    IF EXISTS (SELECT 1 FROM [dbo].[UserQuotas] WHERE [Username] = @Username)
                    BEGIN
                        UPDATE [dbo].[UserQuotas]
                        SET [MaxKeys] = @MaxKeys,
                            [UpdatedAt] = SYSUTCDATETIME()
                        WHERE [Username] = @Username;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO [dbo].[UserQuotas] ([Username], [MaxKeys], [CreatedAt], [UpdatedAt])
                        VALUES (@Username, @MaxKeys, SYSUTCDATETIME(), SYSUTCDATETIME());
                    END;";
                await conn.ExecuteAsync(sql, new { Username = username, MaxKeys = maxKeys });
            }
        }

        public async Task DeleteUserQuotaAsync(string username)
        {
            using var conn = _dbFactory.CreateConnection();
            await conn.ExecuteAsync(
                "DELETE FROM UserQuotas WHERE Username = @Username;",
                new { Username = username });
        }

        // ==========================================
        // IOAuthClientRepository
        // ==========================================
        public async Task<IEnumerable<OAuthClient>> GetOAuthClientsAsync()
        {
            using var conn = _dbFactory.CreateConnection();
            var provider = _dbFactory.ProviderName.ToLowerInvariant();

            if (provider == "sqlite")
            {
                return await conn.QueryAsync<OAuthClient>(@"
                    SELECT ClientId, ClientSecretHash, ClientName, ClientType,
                           RedirectUrisJson, GrantTypesJson, ScopesJson,
                           OwnerSid, CreatedBy, ExpiresAt, CreatedAt
                    FROM OAuthClients
                    ORDER BY CreatedAt DESC;");
            }
            else if (provider == "mysql")
            {
                return await conn.QueryAsync<OAuthClient>(
                    "sp_GetOAuthClients",
                    commandType: CommandType.StoredProcedure
                );
            }
            else
            {
                return await conn.QueryAsync<OAuthClient>(
                    "sp_GetOAuthClients",
                    commandType: CommandType.StoredProcedure
                );
            }
        }

        public async Task<OAuthClient?> GetOAuthClientByIdAsync(string clientId)
        {
            using var conn = _dbFactory.CreateConnection();
            var provider = _dbFactory.ProviderName.ToLowerInvariant();

            if (provider == "sqlite")
            {
                return await conn.QueryFirstOrDefaultAsync<OAuthClient>(@"
                    SELECT ClientId, ClientSecretHash, ClientName, ClientType,
                           RedirectUrisJson, GrantTypesJson, ScopesJson,
                           OwnerSid, CreatedBy, ExpiresAt, CreatedAt
                    FROM OAuthClients
                    WHERE ClientId = @ClientId;",
                    new { ClientId = clientId });
            }
            else if (provider == "mysql")
            {
                return await conn.QueryFirstOrDefaultAsync<OAuthClient>(
                    "sp_GetOAuthClientById",
                    new { p_ClientId = clientId },
                    commandType: CommandType.StoredProcedure
                );
            }
            else
            {
                return await conn.QueryFirstOrDefaultAsync<OAuthClient>(
                    "sp_GetOAuthClientById",
                    new { ClientId = clientId },
                    commandType: CommandType.StoredProcedure
                );
            }
        }

        public async Task SaveOAuthClientAsync(OAuthClient client)
        {
            using var conn = _dbFactory.CreateConnection();
            var provider = _dbFactory.ProviderName.ToLowerInvariant();

            if (provider == "sqlite")
            {
                const string sql = @"
                    INSERT INTO OAuthClients (ClientId, ClientSecretHash, ClientName, ClientType, RedirectUrisJson, GrantTypesJson, ScopesJson, OwnerSid, CreatedBy, ExpiresAt, CreatedAt)
                    VALUES (@ClientId, @ClientSecretHash, @ClientName, @ClientType, @RedirectUrisJson, @GrantTypesJson, @ScopesJson, @OwnerSid, @CreatedBy, @ExpiresAt, @CreatedAt)
                    ON CONFLICT(ClientId) DO UPDATE SET
                        ClientSecretHash = @ClientSecretHash,
                        ClientName = @ClientName,
                        ClientType = @ClientType,
                        RedirectUrisJson = @RedirectUrisJson,
                        GrantTypesJson = @GrantTypesJson,
                        ScopesJson = @ScopesJson,
                        OwnerSid = @OwnerSid,
                        CreatedBy = @CreatedBy,
                        ExpiresAt = @ExpiresAt;";
                await conn.ExecuteAsync(sql, new
                {
                    client.ClientId,
                    ClientSecretHash = client.ClientSecretHash ?? "",
                    client.ClientName,
                    ClientType = string.IsNullOrEmpty(client.ClientType) ? "confidential" : client.ClientType,
                    RedirectUrisJson = client.RedirectUrisJson ?? "[]",
                    GrantTypesJson = client.GrantTypesJson ?? "[]",
                    ScopesJson = client.ScopesJson ?? "[]",
                    OwnerSid = client.OwnerSid ?? "",
                    CreatedBy = client.CreatedBy ?? "",
                    client.ExpiresAt,
                    client.CreatedAt
                });
            }
            else if (provider == "mysql")
            {
                await conn.ExecuteAsync(
                    "sp_SaveOAuthClient",
                    new
                    {
                        p_ClientId = client.ClientId,
                        p_ClientSecretHash = client.ClientSecretHash ?? "",
                        p_ClientName = client.ClientName,
                        p_ClientType = string.IsNullOrEmpty(client.ClientType) ? "confidential" : client.ClientType,
                        p_RedirectUrisJson = client.RedirectUrisJson ?? "[]",
                        p_GrantTypesJson = client.GrantTypesJson ?? "[]",
                        p_ScopesJson = client.ScopesJson ?? "[]",
                        p_OwnerSid = client.OwnerSid ?? "",
                        p_CreatedBy = client.CreatedBy ?? "",
                        p_ExpiresAt = client.ExpiresAt
                    },
                    commandType: CommandType.StoredProcedure
                );
            }
            else
            {
                await conn.ExecuteAsync(
                    "sp_SaveOAuthClient",
                    new
                    {
                        client.ClientId,
                        ClientSecretHash = client.ClientSecretHash ?? "",
                        client.ClientName,
                        ClientType = string.IsNullOrEmpty(client.ClientType) ? "confidential" : client.ClientType,
                        RedirectUrisJson = client.RedirectUrisJson ?? "[]",
                        GrantTypesJson = client.GrantTypesJson ?? "[]",
                        ScopesJson = client.ScopesJson ?? "[]",
                        OwnerSid = client.OwnerSid ?? "",
                        CreatedBy = client.CreatedBy ?? "",
                        client.ExpiresAt
                    },
                    commandType: CommandType.StoredProcedure
                );
            }
        }

        public async Task<bool> DeleteOAuthClientAsync(string clientId)
        {
            using var conn = _dbFactory.CreateConnection();
            var provider = _dbFactory.ProviderName.ToLowerInvariant();

            if (provider == "sqlite")
            {
                var affected = await conn.ExecuteAsync("DELETE FROM OAuthClients WHERE ClientId = @ClientId;", new { ClientId = clientId });
                return affected > 0;
            }
            else if (provider == "mysql")
            {
                var affected = await conn.ExecuteAsync(
                    "sp_DeleteOAuthClient",
                    new { p_ClientId = clientId },
                    commandType: CommandType.StoredProcedure
                );
                return affected > 0;
            }
            else
            {
                var affected = await conn.ExecuteAsync(
                    "sp_DeleteOAuthClient",
                    new { ClientId = clientId },
                    commandType: CommandType.StoredProcedure
                );
                return affected > 0 || affected == -1;
            }
        }

        public async Task<OAuthClient?> FindDcrClientAsync(string clientName, string clientType)
        {
            using var conn = _dbFactory.CreateConnection();
            var provider = _dbFactory.ProviderName.ToLowerInvariant();

            if (provider == "sqlite" || provider == "mysql")
            {
                return await conn.QueryFirstOrDefaultAsync<OAuthClient>(@"
                    SELECT ClientId, ClientSecretHash, ClientName, ClientType,
                           RedirectUrisJson, GrantTypesJson, ScopesJson,
                           OwnerSid, CreatedBy, ExpiresAt, CreatedAt
                    FROM OAuthClients
                    WHERE (CreatedBy = 'dcr' OR CreatedBy = '' OR CreatedBy IS NULL)
                      AND ClientName = @ClientName
                      AND ClientType = @ClientType
                    ORDER BY CreatedAt DESC
                    LIMIT 1;",
                    new { ClientName = clientName, ClientType = clientType });
            }
            else
            {
                return await conn.QueryFirstOrDefaultAsync<OAuthClient>(@"
                    SELECT TOP 1 ClientId, ClientSecretHash, ClientName, ClientType,
                           RedirectUrisJson, GrantTypesJson, ScopesJson,
                           OwnerSid, CreatedBy, ExpiresAt, CreatedAt
                    FROM [dbo].[OAuthClients]
                    WHERE (CreatedBy = 'dcr' OR CreatedBy = '' OR CreatedBy IS NULL)
                      AND ClientName = @ClientName
                      AND ClientType = @ClientType
                    ORDER BY CreatedAt DESC;",
                    new { ClientName = clientName, ClientType = clientType });
            }
        }

        public async Task<int> CleanupDcrClientsAsync(int retentionDays = 30)
        {
            using var conn = _dbFactory.CreateConnection();
            var provider = _dbFactory.ProviderName.ToLowerInvariant();
            int cleaned = 0;

            if (provider == "sqlite")
            {
                // 1. Prune duplicate DCR registrations keeping the newest record per (ClientName, ClientType)
                cleaned += await conn.ExecuteAsync(@"
                    DELETE FROM OAuthClients
                    WHERE (CreatedBy = 'dcr' OR CreatedBy = '' OR CreatedBy IS NULL)
                      AND ClientId IN (
                          SELECT ClientId FROM (
                              SELECT ClientId, ROW_NUMBER() OVER (PARTITION BY ClientName, ClientType ORDER BY CreatedAt DESC) as rn
                              FROM OAuthClients
                              WHERE (CreatedBy = 'dcr' OR CreatedBy = '' OR CreatedBy IS NULL)
                          ) WHERE rn > 1
                      );");

                // 2. Delete expired DCR clients
                cleaned += await conn.ExecuteAsync(@"
                    DELETE FROM OAuthClients
                    WHERE (CreatedBy = 'dcr' OR CreatedBy = '' OR CreatedBy IS NULL)
                      AND ExpiresAt IS NOT NULL
                      AND ExpiresAt != ''
                      AND datetime(ExpiresAt) < datetime('now');");
            }
            else if (provider == "mysql")
            {
                cleaned += await conn.ExecuteAsync(@"
                    DELETE c FROM OAuthClients c
                    INNER JOIN (
                        SELECT ClientId, ROW_NUMBER() OVER (PARTITION BY ClientName, ClientType ORDER BY CreatedAt DESC) as rn
                        FROM OAuthClients
                        WHERE (CreatedBy = 'dcr' OR CreatedBy = '' OR CreatedBy IS NULL)
                    ) dupes ON c.ClientId = dupes.ClientId
                    WHERE dupes.rn > 1;");

                cleaned += await conn.ExecuteAsync(@"
                    DELETE FROM OAuthClients
                    WHERE (CreatedBy = 'dcr' OR CreatedBy = '' OR CreatedBy IS NULL)
                      AND ExpiresAt IS NOT NULL
                      AND ExpiresAt < NOW();");
            }
            else // mssql
            {
                cleaned += await conn.ExecuteAsync(@"
                    WITH CTE AS (
                        SELECT ClientId, ROW_NUMBER() OVER (PARTITION BY ClientName, ClientType ORDER BY CreatedAt DESC) as rn
                        FROM [dbo].[OAuthClients]
                        WHERE (CreatedBy = 'dcr' OR CreatedBy = '' OR CreatedBy IS NULL)
                    )
                    DELETE FROM [dbo].[OAuthClients] WHERE ClientId IN (SELECT ClientId FROM CTE WHERE rn > 1);");

                cleaned += await conn.ExecuteAsync(@"
                    DELETE FROM [dbo].[OAuthClients]
                    WHERE (CreatedBy = 'dcr' OR CreatedBy = '' OR CreatedBy IS NULL)
                      AND ExpiresAt IS NOT NULL
                      AND ExpiresAt < SYSUTCDATETIME();");
            }

            return cleaned;
        }

        // ==========================================
        // IMasterKeyManager
        // ==========================================
        public async Task ReencryptDatabaseSecretsAsync(string newMasterKey)
        {
            if (string.IsNullOrWhiteSpace(newMasterKey))
            {
                throw new ArgumentException("New master key cannot be null or empty.", nameof(newMasterKey));
            }

            var trimmedNewKey = newMasterKey.Trim();
            if (trimmedNewKey.Length < 16)
            {
                throw new ArgumentException("New master key must be at least 16 characters long.", nameof(newMasterKey));
            }

            if (DbKeyHelper.ActiveKeySource == MasterKeySource.External || DbKeyHelper.ActiveKeySource == MasterKeySource.Vault)
            {
                throw new InvalidOperationException($"Cannot set custom master key when key source is managed externally ({DbKeyHelper.ActiveKeySource}).");
            }

            var config = _config ?? new ConfigurationBuilder().Build();
            var currentKey = DbKeyHelper.ResolveDbEncryptionKey(config);

            var oldKeyBytes = SymmetricEncryptionHelper.DeriveKey(currentKey);
            var newKeyBytes = SymmetricEncryptionHelper.DeriveKey(trimmedNewKey);

            using var conn = _dbFactory.CreateConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                // 1. SecretProviders
                if (await TableExistsAsync(conn, tx, "SecretProviders"))
                {
                    var secretProviders = (await conn.QueryAsync<SecretProviderReencryptRow>(
                        "SELECT ProviderName, EncryptedConfigJson FROM SecretProviders;",
                        transaction: tx)).ToList();

                    foreach (var sp in secretProviders)
                    {
                        if (!string.IsNullOrEmpty(sp.EncryptedConfigJson))
                        {
                            if (SymmetricEncryptionHelper.TryDecryptWithKeyBytes(sp.EncryptedConfigJson, oldKeyBytes, out var plainConfig))
                            {
                                var reEncrypted = SymmetricEncryptionHelper.EncryptWithKeyBytes(plainConfig, newKeyBytes);
                                await conn.ExecuteAsync(
                                    "UPDATE SecretProviders SET EncryptedConfigJson = @EncryptedConfigJson WHERE ProviderName = @ProviderName;",
                                    new { EncryptedConfigJson = reEncrypted, sp.ProviderName },
                                    transaction: tx);
                            }
                        }
                    }
                }

                // 2. AuthProviderConfigs
                if (await TableExistsAsync(conn, tx, "AuthProviderConfigs"))
                {
                    var authProviders = (await conn.QueryAsync<AuthProviderReencryptRow>(
                        "SELECT ProviderName, EncryptedConfigJson FROM AuthProviderConfigs;",
                        transaction: tx)).ToList();

                    foreach (var ap in authProviders)
                    {
                        if (!string.IsNullOrEmpty(ap.EncryptedConfigJson))
                        {
                            if (SymmetricEncryptionHelper.TryDecryptWithKeyBytes(ap.EncryptedConfigJson, oldKeyBytes, out var plainConfig))
                            {
                                var reEncrypted = SymmetricEncryptionHelper.EncryptWithKeyBytes(plainConfig, newKeyBytes);
                                await conn.ExecuteAsync(
                                    "UPDATE AuthProviderConfigs SET EncryptedConfigJson = @EncryptedConfigJson WHERE ProviderName = @ProviderName;",
                                    new { EncryptedConfigJson = reEncrypted, ap.ProviderName },
                                    transaction: tx);
                            }
                        }
                    }
                }

                // 3. UserServerCredentials (UserSecrets)
                if (await TableExistsAsync(conn, tx, "UserServerCredentials"))
                {
                    var userSecrets = (await conn.QueryAsync<UserCredentialReencryptRow>(
                        "SELECT Id, EncryptedSecretJson FROM UserServerCredentials;",
                        transaction: tx)).ToList();

                    foreach (var us in userSecrets)
                    {
                        if (!string.IsNullOrEmpty(us.EncryptedSecretJson))
                        {
                            if (SymmetricEncryptionHelper.TryDecryptWithKeyBytes(us.EncryptedSecretJson, oldKeyBytes, out var plainSecret))
                            {
                                var reEncrypted = SymmetricEncryptionHelper.EncryptWithKeyBytes(plainSecret, newKeyBytes);
                                await conn.ExecuteAsync(
                                    "UPDATE UserServerCredentials SET EncryptedSecretJson = @EncryptedSecretJson WHERE Id = @Id;",
                                    new { EncryptedSecretJson = reEncrypted, us.Id },
                                    transaction: tx);
                            }
                        }
                    }
                }

                // 4. Update ./data/.master.key file
                string dataDir = DbKeyHelper.ResolveDataDirectory(config);
                Directory.CreateDirectory(dataDir);
                var keyFilePath = Path.Combine(dataDir, ".master.key");
                File.WriteAllText(keyFilePath, trimmedNewKey);

                if (!OperatingSystem.IsWindows())
                {
                    try
                    {
                        File.SetUnixFileMode(keyFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    }
                    catch
                    {
                        // Ignored on file systems without POSIX permissions support
                    }
                }

                tx.Commit();

                // 5. Update in-memory cache and key source
                DbKeyHelper.SetCachedKey(trimmedNewKey, MasterKeySource.Configured);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private async Task<bool> TableExistsAsync(IDbConnection conn, IDbTransaction tx, string tableName)
        {
            var provider = _dbFactory.ProviderName.ToLowerInvariant();
            if (provider == "sqlite")
            {
                return await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tableName;",
                    new { tableName }, transaction: tx) > 0;
            }
            else if (provider == "mssql")
            {
                return await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM sys.tables WHERE name=@tableName;",
                    new { tableName }, transaction: tx) > 0;
            }
            else if (provider == "mysql")
            {
                return await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = @tableName;",
                    new { tableName }, transaction: tx) > 0;
            }
            return true;
        }

        private class SecretProviderReencryptRow
        {
            public string ProviderName { get; set; } = "";
            public string? EncryptedConfigJson { get; set; }
        }

        private class AuthProviderReencryptRow
        {
            public string ProviderName { get; set; } = "";
            public string? EncryptedConfigJson { get; set; }
        }

        private class UserCredentialReencryptRow
        {
            public string Id { get; set; } = "";
            public string? EncryptedSecretJson { get; set; }
        }
    }
}
