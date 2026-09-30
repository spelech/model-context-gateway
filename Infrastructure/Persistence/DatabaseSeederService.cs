using System.Data;
using Dapper;
using ModelContextGateway.Infrastructure.Persistence.DatabaseSeeders;

namespace ModelContextGateway.Infrastructure.Persistence
{
    public static class DatabaseSeederService
    {
        public static void SeedDatabase(this WebApplication app)
        {
            SeedDatabase(app.Services, app.Configuration);
        }

        public static void SeedDatabase(IServiceProvider services, IConfiguration configuration)
        {
            using var scope = services.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            var provider = dbFactory.ProviderName.ToLowerInvariant();

            var legacyDbPath = Path.Combine(AppContext.BaseDirectory, "data", "mcp_router.db");
            var newDbPath = Path.Combine(AppContext.BaseDirectory, "data", "mcg.db");
            if (File.Exists(legacyDbPath) && !File.Exists(newDbPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(newDbPath)!);
                File.Copy(legacyDbPath, newDbPath);
                logger.LogInformation("Migrated legacy database '{OldPath}' -> '{NewPath}'", legacyDbPath, newDbPath);
            }

            logger.LogInformation("Initializing database via Dapper ({Provider})...", provider);

            var encryptionKey = DbKeyHelper.ResolveDbEncryptionKey(configuration, logger);
            if (string.IsNullOrEmpty(configuration["MCG_MASTER_KEY"])
                && string.IsNullOrEmpty(configuration["MCG_SECRET"])
                && string.IsNullOrEmpty(configuration["DB_ENCRYPTION_KEY"]))
            {
                logger.LogInformation("Master encryption key resolved from persistent keyfile or auto-generated key.");
            }
            else if (encryptionKey.Length < 16)
            {
                logger.LogCritical("SECURITY WARNING: The configured master encryption key is too short (< 16 characters).");
            }

            using var conn = dbFactory.CreateConnection();

            // 1. Run Data-Preserving Upgrade Migrations for existing databases
            ApplyUpgradeMigrations(conn, provider, logger);

            // 2. Ensure baseline tables exist
            EnsureBaselineTables(conn, provider);

            // 3. Populate default settings & seed values
            EnsureDefaultRows(conn, provider);

            // 4. Validate schema compatibility (procedures, columns, foreign keys, types) - fails closed (throws on error)
            ValidateSchemaCompatibility(conn, provider, logger);

            // 5. Seed default client keys and catalog
            ClientAppKeySeeder.SeedDefaultClientsAndKeys(dbFactory, logger, configuration);
            CatalogDatabaseSeeder.SeedCatalogServers(dbFactory, logger);

            // 6. Automatically prune duplicate/stale DCR client registrations
            try
            {
                var oauthRepo = scope.ServiceProvider.GetService<IOAuthClientRepository>();
                if (oauthRepo != null)
                {
                    var cleanedCount = oauthRepo.CleanupDcrClientsAsync().GetAwaiter().GetResult();
                    if (cleanedCount > 0)
                    {
                        logger.LogInformation("Cleaned up {Count} duplicate/stale dynamic OAuth client registrations during initialization.", cleanedCount);
                    }
                }
            }
            catch (Exception exCleanup)
            {
                logger.LogWarning(exCleanup, "Dynamic client cleanup during seeding encountered a non-fatal warning.");
            }

            try
            {
                var embeddingSvc = scope.ServiceProvider.GetService<DynamicEmbeddingService>();
                if (embeddingSvc != null)
                {
                    Task.Run(async () => await embeddingSvc.PreWarmAsync());
                }
            }
            catch (Exception exPrewarm)
            {
                logger.LogWarning(exPrewarm, "Pre-warm background trigger warning");
            }
        }

        public static void ApplyUpgradeMigrations(IDbConnection conn, string provider, ILogger logger)
        {
            logger.LogInformation("Applying upgrade migrations if necessary ({Provider})...", provider);

            if (provider == "sqlite")
            {
                ApplySqliteMigrations(conn, logger);
            }
            else if (provider == "mssql")
            {
                ApplyMssqlMigrations(conn, logger);
            }
            else if (provider == "mysql")
            {
                ApplyMySqlMigrations(conn, logger);
            }
        }

        private static void ApplySqliteMigrations(IDbConnection conn, ILogger logger)
        {
            // 1. SecretProviders.ConfigJson -> EncryptedConfigJson
            var spTableExists = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SecretProviders';") > 0;
            if (spTableExists)
            {
                var cols = conn.Query<string>("SELECT name FROM pragma_table_info('SecretProviders');").ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!cols.Contains("EncryptedConfigJson"))
                {
                    if (cols.Contains("ConfigJson"))
                    {
                        logger.LogInformation("Migrating SQLite SecretProviders.ConfigJson to EncryptedConfigJson...");
                        conn.Execute("ALTER TABLE SecretProviders ADD COLUMN EncryptedConfigJson TEXT;");
                        conn.Execute("UPDATE SecretProviders SET EncryptedConfigJson = ConfigJson WHERE EncryptedConfigJson IS NULL;");
                    }
                    else
                    {
                        conn.Execute("ALTER TABLE SecretProviders ADD COLUMN EncryptedConfigJson TEXT;");
                    }
                }
            }

            // 2. AuthProviderConfigs.ConfigJson -> EncryptedConfigJson
            var apTableExists = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='AuthProviderConfigs';") > 0;
            if (apTableExists)
            {
                var cols = conn.Query<string>("SELECT name FROM pragma_table_info('AuthProviderConfigs');").ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!cols.Contains("EncryptedConfigJson"))
                {
                    if (cols.Contains("ConfigJson"))
                    {
                        logger.LogInformation("Migrating SQLite AuthProviderConfigs.ConfigJson to EncryptedConfigJson...");
                        conn.Execute("ALTER TABLE AuthProviderConfigs ADD COLUMN EncryptedConfigJson TEXT;");
                        conn.Execute("UPDATE AuthProviderConfigs SET EncryptedConfigJson = ConfigJson WHERE EncryptedConfigJson IS NULL;");
                    }
                    else
                    {
                        conn.Execute("ALTER TABLE AuthProviderConfigs ADD COLUMN EncryptedConfigJson TEXT;");
                    }
                }
            }

            // 3. Settings columns
            var settingsExists = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Settings';") > 0;
            if (settingsExists)
            {
                var cols = conn.Query<string>("SELECT name FROM pragma_table_info('Settings');").ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!cols.Contains("DashboardTitle"))
                {
                    conn.Execute("ALTER TABLE Settings ADD COLUMN DashboardTitle TEXT DEFAULT 'MCP Gateway';");
                }

                if (!cols.Contains("DashboardIcon"))
                {
                    conn.Execute("ALTER TABLE Settings ADD COLUMN DashboardIcon TEXT DEFAULT 'fa-solid fa-network-wired';");
                }

                if (!cols.Contains("EmbeddingProvider"))
                {
                    conn.Execute("ALTER TABLE Settings ADD COLUMN EmbeddingProvider TEXT;");
                }

                if (!cols.Contains("EmbeddingApiUrl"))
                {
                    conn.Execute("ALTER TABLE Settings ADD COLUMN EmbeddingApiUrl TEXT;");
                }

                if (!cols.Contains("EmbeddingApiKey"))
                {
                    conn.Execute("ALTER TABLE Settings ADD COLUMN EmbeddingApiKey TEXT;");
                }

                if (!cols.Contains("EmbeddingApiModel"))
                {
                    conn.Execute("ALTER TABLE Settings ADD COLUMN EmbeddingApiModel TEXT;");
                }

                if (!cols.Contains("EmbeddingModelDir"))
                {
                    conn.Execute("ALTER TABLE Settings ADD COLUMN EmbeddingModelDir TEXT;");
                }

                if (!cols.Contains("GlobalMaxKeys"))
                {
                    conn.Execute("ALTER TABLE Settings ADD COLUMN GlobalMaxKeys INTEGER DEFAULT 100;");
                }

                if (!cols.Contains("UserMaxKeys"))
                {
                    conn.Execute("ALTER TABLE Settings ADD COLUMN UserMaxKeys INTEGER DEFAULT 5;");
                }

                if (!cols.Contains("UserSecretStorage"))
                {
                    conn.Execute("ALTER TABLE Settings ADD COLUMN UserSecretStorage TEXT DEFAULT 'Database';");
                }
            }

            // 4. AppKeys.OwnerSid and AppKeys.KeyType
            var appKeysExists = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='AppKeys';") > 0;
            if (appKeysExists)
            {
                var cols = conn.Query<string>("SELECT name FROM pragma_table_info('AppKeys');").ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!cols.Contains("OwnerSid"))
                {
                    conn.Execute("ALTER TABLE AppKeys ADD COLUMN OwnerSid TEXT DEFAULT '';");
                }

                if (!cols.Contains("KeyType"))
                {
                    conn.Execute("ALTER TABLE AppKeys ADD COLUMN KeyType TEXT DEFAULT 'personal';");
                }

                if (!cols.Contains("LastUsedAt"))
                {
                    conn.Execute("ALTER TABLE AppKeys ADD COLUMN LastUsedAt DATETIME NULL;");
                }
            }

            // 5. Legacy McpServers -> Servers
            var mcpServersExists = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='McpServers';") > 0;
            var serversExists = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Servers';") > 0;
            if (mcpServersExists && !serversExists)
            {
                logger.LogInformation("Migrating SQLite McpServers to Servers table...");
                conn.Execute("ALTER TABLE McpServers RENAME TO Servers;");
                serversExists = true;
            }

            // Ensure all columns on Servers table exist
            if (serversExists)
            {
                var serversCols = conn.Query<string>("SELECT name FROM pragma_table_info('Servers');").ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!serversCols.Contains("Enabled"))
                {
                    conn.Execute("ALTER TABLE Servers ADD COLUMN Enabled INTEGER DEFAULT 1;");
                }

                if (!serversCols.Contains("Hidden"))
                {
                    conn.Execute("ALTER TABLE Servers ADD COLUMN Hidden INTEGER DEFAULT 0;");
                }

                if (!serversCols.Contains("Type"))
                {
                    conn.Execute("ALTER TABLE Servers ADD COLUMN Type TEXT DEFAULT 'sse';");
                }

                if (!serversCols.Contains("SecretProvider"))
                {
                    conn.Execute("ALTER TABLE Servers ADD COLUMN SecretProvider TEXT DEFAULT 'None';");
                }

                if (!serversCols.Contains("SecretItemKey"))
                {
                    conn.Execute("ALTER TABLE Servers ADD COLUMN SecretItemKey TEXT NULL;");
                }

                if (!serversCols.Contains("SecretMount"))
                {
                    conn.Execute("ALTER TABLE Servers ADD COLUMN SecretMount TEXT NULL;");
                }

                if (!serversCols.Contains("SecretPath"))
                {
                    conn.Execute("ALTER TABLE Servers ADD COLUMN SecretPath TEXT NULL;");
                }

                if (!serversCols.Contains("SecretField"))
                {
                    conn.Execute("ALTER TABLE Servers ADD COLUMN SecretField TEXT NULL;");
                }

                if (!serversCols.Contains("AuthShape"))
                {
                    conn.Execute("ALTER TABLE Servers ADD COLUMN AuthShape TEXT DEFAULT 'bearer';");
                }

                if (!serversCols.Contains("CustomHeaderName"))
                {
                    conn.Execute("ALTER TABLE Servers ADD COLUMN CustomHeaderName TEXT NULL;");
                }

                if (!serversCols.Contains("Categories"))
                {
                    conn.Execute("ALTER TABLE Servers ADD COLUMN Categories TEXT DEFAULT '[]';");
                }

                if (!serversCols.Contains("ApiKey"))
                {
                    conn.Execute("ALTER TABLE Servers ADD COLUMN ApiKey TEXT NULL;");
                }

                if (!serversCols.Contains("HeadersJson"))
                {
                    conn.Execute("ALTER TABLE Servers ADD COLUMN HeadersJson TEXT NULL;");
                }

                if (!serversCols.Contains("AutoDiscovered"))
                {
                    conn.Execute("ALTER TABLE Servers ADD COLUMN AutoDiscovered INTEGER DEFAULT 0;");
                }

                if (!serversCols.Contains("AllowPassThroughAuth"))
                {
                    conn.Execute("ALTER TABLE Servers ADD COLUMN AllowPassThroughAuth INTEGER DEFAULT 0;");
                }

                if (!serversCols.Contains("DynamicAuthPrompt"))
                {
                    conn.Execute("ALTER TABLE Servers ADD COLUMN DynamicAuthPrompt TEXT NULL;");
                }
            }

            // 6. OAuthClients table
            var oauthClientsExists = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='OAuthClients';") > 0;
            if (!oauthClientsExists)
            {
                logger.LogInformation("Creating SQLite OAuthClients table...");
                conn.Execute(@"
                    CREATE TABLE IF NOT EXISTS OAuthClients (
                        ClientId TEXT PRIMARY KEY,
                        ClientSecretHash TEXT DEFAULT '',
                        ClientName TEXT NOT NULL,
                        ClientType TEXT DEFAULT 'confidential',
                        RedirectUrisJson TEXT DEFAULT '[]',
                        GrantTypesJson TEXT DEFAULT '[]',
                        ScopesJson TEXT DEFAULT '[]',
                        OwnerSid TEXT DEFAULT '',
                        CreatedBy TEXT DEFAULT '',
                        ExpiresAt TEXT NULL,
                        CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                        LastUsedAt DATETIME NULL
                    );
                ");
            }
            else
            {
                var cols = conn.Query<string>("SELECT name FROM pragma_table_info('OAuthClients');").ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!cols.Contains("LastUsedAt"))
                {
                    conn.Execute("ALTER TABLE OAuthClients ADD COLUMN LastUsedAt DATETIME NULL;");
                }
            }
        }

        private static void ApplyMssqlMigrations(IDbConnection conn, ILogger logger)
        {
            // 1. Migrate McpServers to Servers if McpServers exists
            conn.Execute(@"
                IF OBJECT_ID('dbo.McpServers', 'U') IS NOT NULL
                BEGIN
                    IF OBJECT_ID('dbo.Servers', 'U') IS NULL
                    BEGIN
                        CREATE TABLE [dbo].[Servers] (
                            [Id]                VARCHAR(100) PRIMARY KEY,
                            [DisplayName]       NVARCHAR(200) NOT NULL,
                            [Url]               VARCHAR(500) NOT NULL,
                            [Enabled]           BIT NOT NULL DEFAULT 1,
                            [Hidden]            BIT NOT NULL DEFAULT 0,
                            [Type]              VARCHAR(20) NOT NULL DEFAULT 'sse',
                            [SecretProvider]    VARCHAR(50) NOT NULL DEFAULT 'None',
                            [SecretItemKey]     VARCHAR(100) NULL,
                            [SecretMount]       VARCHAR(100) NULL,
                            [SecretPath]        VARCHAR(250) NULL,
                            [SecretField]       VARCHAR(100) NULL,
                            [AuthShape]         VARCHAR(20) NOT NULL DEFAULT 'bearer',
                            [CustomHeaderName]  VARCHAR(100) NULL,
                            [Categories]        NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                            [ApiKey]            NVARCHAR(MAX) NULL,
                            [HeadersJson]       NVARCHAR(MAX) NULL,
                            [AutoDiscovered]    BIT NOT NULL DEFAULT 0,
                            [AllowPassThroughAuth] BIT NOT NULL DEFAULT 0,
                            [DynamicAuthPrompt] NVARCHAR(MAX) NULL
                        );
                    END;

                    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.McpServers') AND name = 'Id')
                    BEGIN
                        INSERT INTO [dbo].[Servers] ([Id], [DisplayName], [Url], [Enabled], [Hidden], [Type], [SecretProvider], [SecretItemKey], [SecretMount], [SecretPath], [SecretField], [AuthShape], [CustomHeaderName], [Categories], [ApiKey], [HeadersJson], [AutoDiscovered])
                        SELECT [Id], [DisplayName], [Url], [Enabled], [Hidden], [Type], [SecretProvider], [SecretItemKey], [SecretMount], [SecretPath], [SecretField], [AuthShape], [CustomHeaderName], [Categories], [ApiKey], [HeadersJson], [AutoDiscovered]
                        FROM [dbo].[McpServers]
                        WHERE [Id] NOT IN (SELECT [Id] FROM [dbo].[Servers]);
                    END
                    ELSE IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.McpServers') AND name = 'CodeName')
                    BEGIN
                        INSERT INTO [dbo].[Servers] ([Id], [DisplayName], [Url], [Enabled], [Hidden], [Type], [SecretProvider], [Categories])
                        SELECT [CodeName], [DisplayName], [Url], ISNULL([IsActive], 1), 0, 'sse', ISNULL([SecretProvider], 'None'), '[]'
                        FROM [dbo].[McpServers]
                        WHERE [CodeName] NOT IN (SELECT [Id] FROM [dbo].[Servers]);
                    END;
                END;

                IF OBJECT_ID('dbo.Servers', 'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Servers') AND name = 'SecretProvider')
                        ALTER TABLE [dbo].[Servers] ADD [SecretProvider] VARCHAR(50) NOT NULL DEFAULT 'None';
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Servers') AND name = 'SecretItemKey')
                        ALTER TABLE [dbo].[Servers] ADD [SecretItemKey] VARCHAR(100) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Servers') AND name = 'SecretMount')
                        ALTER TABLE [dbo].[Servers] ADD [SecretMount] VARCHAR(100) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Servers') AND name = 'SecretPath')
                        ALTER TABLE [dbo].[Servers] ADD [SecretPath] VARCHAR(250) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Servers') AND name = 'SecretField')
                        ALTER TABLE [dbo].[Servers] ADD [SecretField] VARCHAR(100) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Servers') AND name = 'AuthShape')
                        ALTER TABLE [dbo].[Servers] ADD [AuthShape] VARCHAR(20) NOT NULL DEFAULT 'bearer';
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Servers') AND name = 'CustomHeaderName')
                        ALTER TABLE [dbo].[Servers] ADD [CustomHeaderName] VARCHAR(100) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Servers') AND name = 'Categories')
                        ALTER TABLE [dbo].[Servers] ADD [Categories] NVARCHAR(MAX) NOT NULL DEFAULT '[]';
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Servers') AND name = 'ApiKey')
                        ALTER TABLE [dbo].[Servers] ADD [ApiKey] NVARCHAR(MAX) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Servers') AND name = 'HeadersJson')
                        ALTER TABLE [dbo].[Servers] ADD [HeadersJson] NVARCHAR(MAX) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Servers') AND name = 'AutoDiscovered')
                        ALTER TABLE [dbo].[Servers] ADD [AutoDiscovered] BIT NOT NULL DEFAULT 0;
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'AllowPassThroughAuth' AND Object_ID = Object_ID(N'dbo.Servers'))
                        ALTER TABLE [dbo].[Servers] ADD [AllowPassThroughAuth] BIT NOT NULL DEFAULT 0;
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'DynamicAuthPrompt' AND Object_ID = Object_ID(N'dbo.Servers'))
                        ALTER TABLE [dbo].[Servers] ADD [DynamicAuthPrompt] NVARCHAR(MAX) NULL;
                END;

                IF OBJECT_ID('dbo.Settings', 'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Settings') AND name = 'DashboardTitle')
                        ALTER TABLE [dbo].[Settings] ADD [DashboardTitle] VARCHAR(200) NOT NULL DEFAULT 'MCP Gateway';
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Settings') AND name = 'DashboardIcon')
                        ALTER TABLE [dbo].[Settings] ADD [DashboardIcon] VARCHAR(100) NOT NULL DEFAULT 'fa-solid fa-network-wired';
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Settings') AND name = 'EmbeddingProvider')
                        ALTER TABLE [dbo].[Settings] ADD [EmbeddingProvider] VARCHAR(50) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Settings') AND name = 'EmbeddingApiUrl')
                        ALTER TABLE [dbo].[Settings] ADD [EmbeddingApiUrl] VARCHAR(500) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Settings') AND name = 'EmbeddingApiKey')
                        ALTER TABLE [dbo].[Settings] ADD [EmbeddingApiKey] NVARCHAR(MAX) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Settings') AND name = 'EmbeddingApiModel')
                        ALTER TABLE [dbo].[Settings] ADD [EmbeddingApiModel] VARCHAR(100) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Settings') AND name = 'EmbeddingModelDir')
                        ALTER TABLE [dbo].[Settings] ADD [EmbeddingModelDir] VARCHAR(500) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Settings') AND name = 'GlobalMaxKeys')
                        ALTER TABLE [dbo].[Settings] ADD [GlobalMaxKeys] INT NOT NULL DEFAULT 100;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Settings') AND name = 'UserMaxKeys')
                        ALTER TABLE [dbo].[Settings] ADD [UserMaxKeys] INT NOT NULL DEFAULT 5;
                END;
            ");

            // 2. Migrate Tools.ServerId if INT to VARCHAR(100)
            conn.Execute(@"
                IF OBJECT_ID('dbo.Tools', 'U') IS NOT NULL
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM sys.columns c
                        JOIN sys.types t ON c.user_type_id = t.user_type_id
                        WHERE c.object_id = OBJECT_ID('dbo.Tools')
                          AND c.name = 'ServerId'
                          AND t.name IN ('int', 'bigint', 'smallint', 'tinyint')
                    )
                    BEGIN
                        -- Drop foreign key constraints on Tools
                        DECLARE @sql NVARCHAR(MAX) = N'';
                        SELECT @sql += N'ALTER TABLE [dbo].[Tools] DROP CONSTRAINT [' + fk.name + N'];'
                        FROM sys.foreign_keys fk
                        WHERE fk.parent_object_id = OBJECT_ID('dbo.Tools');
                        IF @sql <> N'' EXEC sp_executesql @sql;

                        SET @sql = N'';
                        SELECT @sql += N'ALTER TABLE [dbo].[Tools] DROP CONSTRAINT [' + kc.name + N'];'
                        FROM sys.key_constraints kc
                        WHERE kc.parent_object_id = OBJECT_ID('dbo.Tools') AND kc.type = 'UQ';
                        IF @sql <> N'' EXEC sp_executesql @sql;

                        IF OBJECT_ID('dbo.McpServers', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.McpServers') AND name = 'ServerId')
                        BEGIN
                            ALTER TABLE [dbo].[Tools] ADD [TempServerId] VARCHAR(100) NULL;
                            EXEC(N'UPDATE t SET t.TempServerId = ms.CodeName FROM [dbo].[Tools] t INNER JOIN [dbo].[McpServers] ms ON t.ServerId = ms.ServerId');
                            ALTER TABLE [dbo].[Tools] DROP COLUMN [ServerId];
                            EXEC sp_rename 'dbo.Tools.TempServerId', 'ServerId', 'COLUMN';
                        END
                        ELSE
                        BEGIN
                            ALTER TABLE [dbo].[Tools] ALTER COLUMN [ServerId] VARCHAR(100) NOT NULL;
                        END;

                        ALTER TABLE [dbo].[Tools] ALTER COLUMN [ServerId] VARCHAR(100) NOT NULL;

                        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Tools_Servers')
                        BEGIN
                            ALTER TABLE [dbo].[Tools] ADD CONSTRAINT [FK_Tools_Servers] FOREIGN KEY ([ServerId]) REFERENCES [dbo].[Servers]([Id]) ON DELETE CASCADE;
                        END;
                        IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_Server_ToolName')
                        BEGIN
                            ALTER TABLE [dbo].[Tools] ADD CONSTRAINT [UQ_Server_ToolName] UNIQUE ([ServerId], [ToolName]);
                        END;
                    END;
                END;
            ");

            // 3. Migrate SecretProviders.ConfigJson -> EncryptedConfigJson
            conn.Execute(@"
                IF OBJECT_ID('dbo.SecretProviders', 'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SecretProviders') AND name = 'EncryptedConfigJson')
                    BEGIN
                        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SecretProviders') AND name = 'ConfigJson')
                        BEGIN
                            ALTER TABLE [dbo].[SecretProviders] ADD [EncryptedConfigJson] NVARCHAR(MAX) NULL;
                            EXEC(N'UPDATE [dbo].[SecretProviders] SET [EncryptedConfigJson] = [ConfigJson] WHERE [EncryptedConfigJson] IS NULL');
                        END
                        ELSE
                        BEGIN
                            ALTER TABLE [dbo].[SecretProviders] ADD [EncryptedConfigJson] NVARCHAR(MAX) NULL;
                        END;
                    END;
                END;
            ");

            // 4. Migrate AuthProviderConfigs.ConfigJson -> EncryptedConfigJson
            conn.Execute(@"
                IF OBJECT_ID('dbo.AuthProviderConfigs', 'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AuthProviderConfigs') AND name = 'EncryptedConfigJson')
                    BEGIN
                        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AuthProviderConfigs') AND name = 'ConfigJson')
                        BEGIN
                            ALTER TABLE [dbo].[AuthProviderConfigs] ADD [EncryptedConfigJson] NVARCHAR(MAX) NULL;
                            EXEC(N'UPDATE [dbo].[AuthProviderConfigs] SET [EncryptedConfigJson] = [ConfigJson] WHERE [EncryptedConfigJson] IS NULL');
                        END
                        ELSE
                        BEGIN
                            ALTER TABLE [dbo].[AuthProviderConfigs] ADD [EncryptedConfigJson] NVARCHAR(MAX) NULL;
                        END;
                    END;
                END;
            ");

            // 5. Migrate AppKeys.OwnerSid and AppKeys.KeyType
            conn.Execute(@"
                IF OBJECT_ID('dbo.AppKeys', 'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AppKeys') AND name = 'OwnerSid')
                    BEGIN
                        ALTER TABLE [dbo].[AppKeys] ADD [OwnerSid] NVARCHAR(200) NOT NULL DEFAULT '';
                    END;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AppKeys') AND name = 'KeyType')
                    BEGIN
                        ALTER TABLE [dbo].[AppKeys] ADD [KeyType] VARCHAR(50) NOT NULL DEFAULT 'personal';
                    END;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AppKeys') AND name = 'LastUsedAt')
                    BEGIN
                        ALTER TABLE [dbo].[AppKeys] ADD [LastUsedAt] DATETIME2 NULL;
                    END;
                END;

                -- 6. Migrate OAuthClients table
                IF OBJECT_ID('dbo.OAuthClients', 'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[OAuthClients] (
                        [ClientId]         VARCHAR(100) PRIMARY KEY,
                        [ClientSecretHash] VARCHAR(256) NOT NULL DEFAULT '',
                        [ClientName]       NVARCHAR(200) NOT NULL,
                        [ClientType]       VARCHAR(50) NOT NULL DEFAULT 'confidential',
                        [RedirectUrisJson] NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                        [GrantTypesJson]   NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                        [ScopesJson]       NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                        [OwnerSid]         NVARCHAR(200) NOT NULL DEFAULT '',
                        [CreatedBy]        NVARCHAR(256) NOT NULL DEFAULT '',
                        [ExpiresAt]        DATETIME2 NULL,
                        [CreatedAt]        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                        [LastUsedAt]       DATETIME2 NULL
                    );
                END
                ELSE
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.OAuthClients') AND name = 'LastUsedAt')
                    BEGIN
                        ALTER TABLE [dbo].[OAuthClients] ADD [LastUsedAt] DATETIME2 NULL;
                    END;
                END;
            ");
        }

        private static void ApplyMySqlMigrations(IDbConnection conn, ILogger logger)
        {
            // 1. Migrate McpServers to Servers if McpServers exists
            var mcpServersExists = conn.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM information_schema.tables 
                WHERE table_schema = DATABASE() AND table_name = 'McpServers';") > 0;

            if (mcpServersExists)
            {
                var serversExists = conn.ExecuteScalar<int>(@"
                    SELECT COUNT(*) FROM information_schema.tables 
                    WHERE table_schema = DATABASE() AND table_name = 'Servers';") > 0;

                if (!serversExists)
                {
                    conn.Execute(@"
                        CREATE TABLE `Servers` (
                            `Id`                VARCHAR(100) PRIMARY KEY,
                            `DisplayName`       VARCHAR(200) NOT NULL,
                            `Url`               VARCHAR(500) NOT NULL,
                            `Enabled`           TINYINT(1) NOT NULL DEFAULT 1,
                            `Hidden`            TINYINT(1) NOT NULL DEFAULT 0,
                            `Type`              VARCHAR(20) NOT NULL DEFAULT 'sse',
                            `SecretProvider`    VARCHAR(50) NOT NULL DEFAULT 'None',
                            `SecretItemKey`     VARCHAR(100) NULL,
                            `SecretMount`       VARCHAR(100) NULL,
                            `SecretPath`        VARCHAR(250) NULL,
                            `SecretField`       VARCHAR(100) NULL,
                            `AuthShape`         VARCHAR(20) NOT NULL DEFAULT 'bearer',
                            `CustomHeaderName`  VARCHAR(100) NULL,
                            `Categories`        LONGTEXT NOT NULL,
                            `ApiKey`            LONGTEXT NULL,
                            `HeadersJson`       LONGTEXT NULL,
                            `AutoDiscovered`    TINYINT(1) NOT NULL DEFAULT 0
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                    ");
                }

                var hasId = conn.ExecuteScalar<int>(@"
                    SELECT COUNT(*) FROM information_schema.columns 
                    WHERE table_schema = DATABASE() AND table_name = 'McpServers' AND column_name = 'Id';") > 0;

                if (hasId)
                {
                    conn.Execute(@"
                        INSERT IGNORE INTO `Servers` (`Id`, `DisplayName`, `Url`, `Enabled`, `Hidden`, `Type`, `SecretProvider`, `SecretItemKey`, `SecretMount`, `SecretPath`, `SecretField`, `AuthShape`, `CustomHeaderName`, `Categories`, `ApiKey`, `HeadersJson`, `AutoDiscovered`)
                        SELECT `Id`, `DisplayName`, `Url`, `Enabled`, `Hidden`, `Type`, `SecretProvider`, `SecretItemKey`, `SecretMount`, `SecretPath`, `SecretField`, `AuthShape`, `CustomHeaderName`, `Categories`, `ApiKey`, `HeadersJson`, `AutoDiscovered`
                        FROM `McpServers`;
                    ");
                }
                else
                {
                    conn.Execute(@"
                        INSERT IGNORE INTO `Servers` (`Id`, `DisplayName`, `Url`, `Enabled`, `Hidden`, `Type`, `SecretProvider`, `Categories`)
                        SELECT `CodeName`, `DisplayName`, `Url`, IFNULL(`IsActive`, 1), 0, 'sse', IFNULL(`SecretProvider`, 'None'), '[]'
                        FROM `McpServers`;
                    ");
                }
            }

            var serversTableExists = conn.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM information_schema.tables 
                WHERE table_schema = DATABASE() AND table_name = 'Servers';") > 0;

            if (serversTableExists)
            {
                var serversCols = conn.Query<string>(@"
                    SELECT COLUMN_NAME FROM information_schema.columns 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Servers';").ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!serversCols.Contains("Enabled"))
                {
                    conn.Execute("ALTER TABLE `Servers` ADD COLUMN `Enabled` TINYINT(1) NOT NULL DEFAULT 1;");
                }

                if (!serversCols.Contains("Hidden"))
                {
                    conn.Execute("ALTER TABLE `Servers` ADD COLUMN `Hidden` TINYINT(1) NOT NULL DEFAULT 0;");
                }

                if (!serversCols.Contains("Type"))
                {
                    conn.Execute("ALTER TABLE `Servers` ADD COLUMN `Type` VARCHAR(20) NOT NULL DEFAULT 'sse';");
                }

                if (!serversCols.Contains("SecretProvider"))
                {
                    conn.Execute("ALTER TABLE `Servers` ADD COLUMN `SecretProvider` VARCHAR(50) NOT NULL DEFAULT 'None';");
                }

                if (!serversCols.Contains("SecretItemKey"))
                {
                    conn.Execute("ALTER TABLE `Servers` ADD COLUMN `SecretItemKey` VARCHAR(100) NULL;");
                }

                if (!serversCols.Contains("SecretMount"))
                {
                    conn.Execute("ALTER TABLE `Servers` ADD COLUMN `SecretMount` VARCHAR(100) NULL;");
                }

                if (!serversCols.Contains("SecretPath"))
                {
                    conn.Execute("ALTER TABLE `Servers` ADD COLUMN `SecretPath` VARCHAR(250) NULL;");
                }

                if (!serversCols.Contains("SecretField"))
                {
                    conn.Execute("ALTER TABLE `Servers` ADD COLUMN `SecretField` VARCHAR(100) NULL;");
                }

                if (!serversCols.Contains("AuthShape"))
                {
                    conn.Execute("ALTER TABLE `Servers` ADD COLUMN `AuthShape` VARCHAR(20) NOT NULL DEFAULT 'bearer';");
                }

                if (!serversCols.Contains("CustomHeaderName"))
                {
                    conn.Execute("ALTER TABLE `Servers` ADD COLUMN `CustomHeaderName` VARCHAR(100) NULL;");
                }

                if (!serversCols.Contains("Categories"))
                {
                    conn.Execute("ALTER TABLE `Servers` ADD COLUMN `Categories` LONGTEXT NOT NULL;");
                }

                if (!serversCols.Contains("ApiKey"))
                {
                    conn.Execute("ALTER TABLE `Servers` ADD COLUMN `ApiKey` LONGTEXT NULL;");
                }

                if (!serversCols.Contains("HeadersJson"))
                {
                    conn.Execute("ALTER TABLE `Servers` ADD COLUMN `HeadersJson` LONGTEXT NULL;");
                }

                if (!serversCols.Contains("AutoDiscovered"))
                {
                    conn.Execute("ALTER TABLE `Servers` ADD COLUMN `AutoDiscovered` TINYINT(1) NOT NULL DEFAULT 0;");
                }

                if (!serversCols.Contains("AllowPassThroughAuth"))
                {
                    conn.Execute("ALTER TABLE Servers ADD COLUMN AllowPassThroughAuth INTEGER DEFAULT 0;");
                }

                if (!serversCols.Contains("DynamicAuthPrompt"))
                {
                    conn.Execute("ALTER TABLE Servers ADD COLUMN DynamicAuthPrompt TEXT NULL;");
                }
            }

            var settingsTableExists = conn.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM information_schema.tables 
                WHERE table_schema = DATABASE() AND table_name = 'Settings';") > 0;

            if (settingsTableExists)
            {
                var settingsCols = conn.Query<string>(@"
                    SELECT COLUMN_NAME FROM information_schema.columns 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Settings';").ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!settingsCols.Contains("DashboardTitle"))
                {
                    conn.Execute("ALTER TABLE `Settings` ADD COLUMN `DashboardTitle` VARCHAR(200) NOT NULL DEFAULT 'MCP Gateway';");
                }

                if (!settingsCols.Contains("DashboardIcon"))
                {
                    conn.Execute("ALTER TABLE `Settings` ADD COLUMN `DashboardIcon` VARCHAR(100) NOT NULL DEFAULT 'fa-solid fa-network-wired';");
                }

                if (!settingsCols.Contains("EmbeddingProvider"))
                {
                    conn.Execute("ALTER TABLE `Settings` ADD COLUMN `EmbeddingProvider` VARCHAR(50) NULL;");
                }

                if (!settingsCols.Contains("EmbeddingApiUrl"))
                {
                    conn.Execute("ALTER TABLE `Settings` ADD COLUMN `EmbeddingApiUrl` VARCHAR(500) NULL;");
                }

                if (!settingsCols.Contains("EmbeddingApiKey"))
                {
                    conn.Execute("ALTER TABLE `Settings` ADD COLUMN `EmbeddingApiKey` LONGTEXT NULL;");
                }

                if (!settingsCols.Contains("EmbeddingApiModel"))
                {
                    conn.Execute("ALTER TABLE `Settings` ADD COLUMN `EmbeddingApiModel` VARCHAR(100) NULL;");
                }

                if (!settingsCols.Contains("EmbeddingModelDir"))
                {
                    conn.Execute("ALTER TABLE `Settings` ADD COLUMN `EmbeddingModelDir` VARCHAR(500) NULL;");
                }

                if (!settingsCols.Contains("GlobalMaxKeys"))
                {
                    conn.Execute("ALTER TABLE `Settings` ADD COLUMN `GlobalMaxKeys` INT NOT NULL DEFAULT 100;");
                }

                if (!settingsCols.Contains("UserMaxKeys"))
                {
                    conn.Execute("ALTER TABLE `Settings` ADD COLUMN `UserMaxKeys` INT NOT NULL DEFAULT 5;");
                }
            }

            // 2. Migrate Tools.ServerId if INT to VARCHAR(100)
            var toolsExists = conn.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM information_schema.tables 
                WHERE table_schema = DATABASE() AND table_name = 'Tools';") > 0;

            if (toolsExists)
            {
                var serverIdType = conn.ExecuteScalar<string>(@"
                    SELECT DATA_TYPE FROM information_schema.columns 
                    WHERE table_schema = DATABASE() AND table_name = 'Tools' AND column_name = 'ServerId';");

                if (serverIdType != null && (serverIdType.Equals("int", StringComparison.OrdinalIgnoreCase) || serverIdType.Equals("bigint", StringComparison.OrdinalIgnoreCase)))
                {
                    var fkNames = conn.Query<string>(@"
                        SELECT CONSTRAINT_NAME FROM information_schema.KEY_COLUMN_USAGE 
                        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Tools' AND REFERENCED_TABLE_NAME IS NOT NULL;");

                    foreach (var fk in fkNames)
                    {
                        try { conn.Execute($"ALTER TABLE `Tools` DROP FOREIGN KEY `{fk}`;"); } catch { }
                    }

                    conn.Execute("ALTER TABLE `Tools` MODIFY COLUMN `ServerId` VARCHAR(100) NOT NULL;");

                    var fkExists = conn.ExecuteScalar<int>(@"
                        SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE 
                        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Tools' AND CONSTRAINT_NAME = 'FK_Tools_Servers';") > 0;

                    if (!fkExists)
                    {
                        try { conn.Execute("ALTER TABLE `Tools` ADD CONSTRAINT `FK_Tools_Servers` FOREIGN KEY (`ServerId`) REFERENCES `Servers` (`Id`) ON DELETE CASCADE;"); } catch { }
                    }
                }
            }

            // 3. Migrate SecretProviders.ConfigJson -> EncryptedConfigJson
            var secretProvidersExists = conn.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM information_schema.tables 
                WHERE table_schema = DATABASE() AND table_name = 'SecretProviders';") > 0;

            if (secretProvidersExists)
            {
                var hasEncrypted = conn.ExecuteScalar<int>(@"
                    SELECT COUNT(*) FROM information_schema.columns 
                    WHERE table_schema = DATABASE() AND table_name = 'SecretProviders' AND column_name = 'EncryptedConfigJson';") > 0;

                if (!hasEncrypted)
                {
                    var hasConfig = conn.ExecuteScalar<int>(@"
                        SELECT COUNT(*) FROM information_schema.columns 
                        WHERE table_schema = DATABASE() AND table_name = 'SecretProviders' AND column_name = 'ConfigJson';") > 0;

                    if (hasConfig)
                    {
                        conn.Execute("ALTER TABLE `SecretProviders` ADD COLUMN `EncryptedConfigJson` LONGTEXT NULL;");
                        conn.Execute("UPDATE `SecretProviders` SET `EncryptedConfigJson` = `ConfigJson` WHERE `EncryptedConfigJson` IS NULL;");
                    }
                    else
                    {
                        conn.Execute("ALTER TABLE `SecretProviders` ADD COLUMN `EncryptedConfigJson` LONGTEXT NULL;");
                    }
                }
            }

            // 4. Migrate AuthProviderConfigs.ConfigJson -> EncryptedConfigJson
            var authProvidersExists = conn.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM information_schema.tables 
                WHERE table_schema = DATABASE() AND table_name = 'AuthProviderConfigs';") > 0;

            if (authProvidersExists)
            {
                var hasEncConfig = conn.ExecuteScalar<int>(@"
                    SELECT COUNT(*) FROM information_schema.columns 
                    WHERE table_schema = DATABASE() AND table_name = 'AuthProviderConfigs' AND column_name = 'EncryptedConfigJson';") > 0;

                if (!hasEncConfig)
                {
                    var hasConfig = conn.ExecuteScalar<int>(@"
                        SELECT COUNT(*) FROM information_schema.columns 
                        WHERE table_schema = DATABASE() AND table_name = 'AuthProviderConfigs' AND column_name = 'ConfigJson';") > 0;

                    if (hasConfig)
                    {
                        conn.Execute("ALTER TABLE `AuthProviderConfigs` ADD COLUMN `EncryptedConfigJson` LONGTEXT NULL;");
                        conn.Execute("UPDATE `AuthProviderConfigs` SET `EncryptedConfigJson` = `ConfigJson` WHERE `EncryptedConfigJson` IS NULL;");
                    }
                    else
                    {
                        conn.Execute("ALTER TABLE `AuthProviderConfigs` ADD COLUMN `EncryptedConfigJson` LONGTEXT NULL;");
                    }
                }
            }

            // 5. Migrate AppKeys.OwnerSid and AppKeys.KeyType
            var appKeysExists = conn.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM information_schema.tables 
                WHERE table_schema = DATABASE() AND table_name = 'AppKeys';") > 0;

            if (appKeysExists)
            {
                var hasOwnerSid = conn.ExecuteScalar<int>(@"
                    SELECT COUNT(*) FROM information_schema.columns 
                    WHERE table_schema = DATABASE() AND table_name = 'AppKeys' AND column_name = 'OwnerSid';") > 0;

                if (!hasOwnerSid)
                {
                    conn.Execute("ALTER TABLE `AppKeys` ADD COLUMN `OwnerSid` VARCHAR(200) NOT NULL DEFAULT '';");
                }

                var hasKeyType = conn.ExecuteScalar<int>(@"
                    SELECT COUNT(*) FROM information_schema.columns 
                    WHERE table_schema = DATABASE() AND table_name = 'AppKeys' AND column_name = 'KeyType';") > 0;

                if (!hasKeyType)
                {
                    conn.Execute("ALTER TABLE `AppKeys` ADD COLUMN `KeyType` VARCHAR(50) NOT NULL DEFAULT 'personal';");
                }

                var hasLastUsedAt = conn.ExecuteScalar<int>(@"
                    SELECT COUNT(*) FROM information_schema.columns 
                    WHERE table_schema = DATABASE() AND table_name = 'AppKeys' AND column_name = 'LastUsedAt';") > 0;

                if (!hasLastUsedAt)
                {
                    conn.Execute("ALTER TABLE `AppKeys` ADD COLUMN `LastUsedAt` DATETIME NULL;");
                }
            }

            // 6. Migrate OAuthClients table
            var oauthClientsExists = conn.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM information_schema.tables 
                WHERE table_schema = DATABASE() AND table_name = 'OAuthClients';") > 0;

            if (!oauthClientsExists)
            {
                conn.Execute(@"
                    CREATE TABLE IF NOT EXISTS `OAuthClients` (
                        `ClientId`         VARCHAR(100) PRIMARY KEY,
                        `ClientSecretHash` VARCHAR(256) NOT NULL DEFAULT '',
                        `ClientName`       VARCHAR(200) NOT NULL,
                        `ClientType`       VARCHAR(50) NOT NULL DEFAULT 'confidential',
                        `RedirectUrisJson` LONGTEXT NOT NULL,
                        `GrantTypesJson`   LONGTEXT NOT NULL,
                        `ScopesJson`       LONGTEXT NOT NULL,
                        `OwnerSid`         VARCHAR(200) NOT NULL DEFAULT '',
                        `CreatedBy`        VARCHAR(256) NOT NULL DEFAULT '',
                        `ExpiresAt`        DATETIME NULL,
                        `CreatedAt`        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        `LastUsedAt`       DATETIME NULL
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                ");
            }
            else
            {
                var hasLastUsedAt = conn.ExecuteScalar<int>(@"
                    SELECT COUNT(*) FROM information_schema.columns 
                    WHERE table_schema = DATABASE() AND table_name = 'OAuthClients' AND column_name = 'LastUsedAt';") > 0;

                if (!hasLastUsedAt)
                {
                    conn.Execute("ALTER TABLE `OAuthClients` ADD COLUMN `LastUsedAt` DATETIME NULL;");
                }
            }
        }

        private static void EnsureBaselineTables(IDbConnection conn, string provider)
        {
            if (provider == "sqlite")
            {
                conn.Execute(@"
                    CREATE TABLE IF NOT EXISTS Servers (
                        Id TEXT PRIMARY KEY,
                        DisplayName TEXT,
                        Url TEXT,
                        Enabled INTEGER DEFAULT 1,
                        Hidden INTEGER DEFAULT 0,
                        Type TEXT DEFAULT 'sse',
                        SecretProvider TEXT DEFAULT 'None',
                        SecretItemKey TEXT,
                        SecretMount TEXT,
                        SecretPath TEXT,
                        SecretField TEXT,
                        AuthShape TEXT DEFAULT 'bearer',
                        CustomHeaderName TEXT,
                        Categories TEXT DEFAULT '[]',
                        ApiKey TEXT,
                        HeadersJson TEXT,
                        AutoDiscovered INTEGER DEFAULT 0,
                        AllowPassThroughAuth INTEGER DEFAULT 0,
                        DynamicAuthPrompt TEXT
                    );

                    CREATE TABLE IF NOT EXISTS Settings (
                        Id TEXT PRIMARY KEY,
                        DashboardTitle TEXT DEFAULT 'MCP Gateway',
                        DashboardIcon TEXT DEFAULT 'fa-solid fa-network-wired',
                        EmbeddingProvider TEXT,
                        EmbeddingApiUrl TEXT,
                        EmbeddingApiKey TEXT,
                        EmbeddingApiModel TEXT,
                        EmbeddingModelDir TEXT,
                        GlobalMaxKeys INTEGER DEFAULT 100,
                        UserMaxKeys INTEGER DEFAULT 5,
                        UserSecretStorage TEXT DEFAULT 'Database'
                    );

                    CREATE TABLE IF NOT EXISTS UserServerCredentials (
                        Id TEXT PRIMARY KEY,
                        Username TEXT,
                        ServerId TEXT,
                        EncryptedSecretJson TEXT
                    );

                    CREATE TABLE IF NOT EXISTS AppKeys (
                        Id TEXT PRIMARY KEY,
                        Name TEXT,
                        Username TEXT,
                        OwnerSid TEXT DEFAULT '',
                        KeyType TEXT DEFAULT 'personal',
                        KeyPrefix TEXT,
                        EncryptedKey TEXT,
                        ScopesJson TEXT DEFAULT '[]',
                        ExpiresAt TEXT,
                        CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                        LastUsedAt DATETIME NULL
                    );

                    CREATE TABLE IF NOT EXISTS UserQuotas (
                        Username TEXT PRIMARY KEY,
                        MaxKeys INTEGER DEFAULT 5,
                        CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS AccessPolicies (
                        Id TEXT PRIMARY KEY,
                        TargetId TEXT,
                        RequiredGroup TEXT,
                        IsAllowed INTEGER DEFAULT 1
                    );

                    CREATE TABLE IF NOT EXISTS GroupMappings (
                        Id TEXT PRIMARY KEY,
                        ExternalId TEXT,
                        InternalGroup TEXT
                    );

                    CREATE TABLE IF NOT EXISTS AuditLogs (
                        RequestId TEXT PRIMARY KEY,
                        UserPrincipalName TEXT,
                        UserSid TEXT,
                        ServerCodeName TEXT,
                        ItemName TEXT,
                        RequestMethod TEXT,
                        ExecutionTimeMs INTEGER,
                        StatusCode INTEGER,
                        RequestPayload TEXT,
                        ResponsePayload TEXT,
                        ErrorMessage TEXT,
                        Timestamp TEXT DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS AdminAuditLogs (
                        Id TEXT PRIMARY KEY,
                        Username TEXT,
                        Action TEXT,
                        Target TEXT,
                        Details TEXT,
                        Success INTEGER,
                        ErrorMessage TEXT,
                        Timestamp TEXT DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS SecretProviders (
                        ProviderName TEXT PRIMARY KEY,
                        DisplayName TEXT,
                        EncryptedConfigJson TEXT,
                        IsEnabled INTEGER DEFAULT 1
                    );

                    CREATE TABLE IF NOT EXISTS AuthProviderConfigs (
                        ProviderName TEXT PRIMARY KEY,
                        DisplayName TEXT,
                        UserHeader TEXT,
                        GroupsHeader TEXT,
                        EncryptedConfigJson TEXT,
                        IsEnabled INTEGER DEFAULT 1
                    );

                    CREATE TABLE IF NOT EXISTS OAuthClients (
                        ClientId TEXT PRIMARY KEY,
                        ClientSecretHash TEXT DEFAULT '',
                        ClientName TEXT NOT NULL,
                        ClientType TEXT DEFAULT 'confidential',
                        RedirectUrisJson TEXT DEFAULT '[]',
                        GrantTypesJson TEXT DEFAULT '[]',
                        ScopesJson TEXT DEFAULT '[]',
                        OwnerSid TEXT DEFAULT '',
                        CreatedBy TEXT DEFAULT '',
                        ExpiresAt TEXT NULL,
                        CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                        LastUsedAt DATETIME NULL
                    );
                ");

                var colDefs = new (string Name, string Ddl)[]
                {
                    ("SecretProvider", "ALTER TABLE Servers ADD COLUMN SecretProvider TEXT DEFAULT 'None'"),
                    ("SecretItemKey", "ALTER TABLE Servers ADD COLUMN SecretItemKey TEXT NULL"),
                    ("SecretMount", "ALTER TABLE Servers ADD COLUMN SecretMount TEXT NULL"),
                    ("SecretPath", "ALTER TABLE Servers ADD COLUMN SecretPath TEXT NULL"),
                    ("SecretField", "ALTER TABLE Servers ADD COLUMN SecretField TEXT NULL"),
                    ("AuthShape", "ALTER TABLE Servers ADD COLUMN AuthShape TEXT DEFAULT 'bearer'"),
                    ("CustomHeaderName", "ALTER TABLE Servers ADD COLUMN CustomHeaderName TEXT NULL")
                };

                foreach (var (_, ddl) in colDefs)
                {
                    try { conn.Execute(ddl); } catch { }
                }
            }
            else if (provider == "mssql")
            {
                conn.Execute(@"
                    IF OBJECT_ID('dbo.Servers', 'U') IS NULL
                    BEGIN
                        CREATE TABLE [dbo].[Servers] (
                            [Id]                VARCHAR(100) PRIMARY KEY,
                            [DisplayName]       NVARCHAR(200) NOT NULL,
                            [Url]               VARCHAR(500) NOT NULL,
                            [Enabled]           BIT NOT NULL DEFAULT 1,
                            [Hidden]            BIT NOT NULL DEFAULT 0,
                            [Type]              VARCHAR(20) NOT NULL DEFAULT 'sse',
                            [SecretProvider]    VARCHAR(50) NOT NULL DEFAULT 'None',
                            [SecretItemKey]     VARCHAR(100) NULL,
                            [SecretMount]       VARCHAR(100) NULL,
                            [SecretPath]        VARCHAR(250) NULL,
                            [SecretField]       VARCHAR(100) NULL,
                            [AuthShape]         VARCHAR(20) NOT NULL DEFAULT 'bearer',
                            [CustomHeaderName]  VARCHAR(100) NULL,
                            [Categories]        NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                            [ApiKey]            NVARCHAR(MAX) NULL,
                            [HeadersJson]       NVARCHAR(MAX) NULL,
                            [AutoDiscovered]    BIT NOT NULL DEFAULT 0,
                            [AllowPassThroughAuth] BIT NOT NULL DEFAULT 0,
                            [DynamicAuthPrompt] NVARCHAR(MAX) NULL
                        );
                    END;

                    IF OBJECT_ID('dbo.Settings', 'U') IS NULL
                    BEGIN
                        CREATE TABLE [dbo].[Settings] (
                            [Id]                      VARCHAR(50) PRIMARY KEY,
                            [DashboardTitle]          VARCHAR(200) NOT NULL DEFAULT 'MCP Gateway',
                            [DashboardIcon]           VARCHAR(100) NOT NULL DEFAULT 'fa-solid fa-network-wired',
                            [EmbeddingProvider]       VARCHAR(50) NULL,
                            [EmbeddingApiUrl]         VARCHAR(500) NULL,
                            [EmbeddingApiKey]         NVARCHAR(MAX) NULL,
                            [EmbeddingApiModel]       VARCHAR(100) NULL,
                            [EmbeddingModelDir]       VARCHAR(500) NULL,
                            [RequireManualApproval]   BIT NOT NULL DEFAULT 0,
                            [GlobalMaxKeys]           INT NOT NULL DEFAULT 100,
                            [UserMaxKeys]             INT NOT NULL DEFAULT 5,
                            [UserSecretStorage]       VARCHAR(50) NOT NULL DEFAULT 'Database'
                        );
                    END;

                    IF OBJECT_ID('dbo.UserServerCredentials', 'U') IS NULL
                    BEGIN
                        CREATE TABLE [dbo].[UserServerCredentials] (
                            [Id]                  VARCHAR(100) PRIMARY KEY,
                            [Username]            NVARCHAR(200) NOT NULL,
                            [ServerId]            VARCHAR(100) NOT NULL,
                            [EncryptedSecretJson] NVARCHAR(MAX) NULL
                        );
                    END;

                    IF OBJECT_ID('dbo.AppKeys', 'U') IS NULL
                    BEGIN
                        CREATE TABLE [dbo].[AppKeys] (
                            [Id]           VARCHAR(100) PRIMARY KEY,
                            [Name]         NVARCHAR(200) NOT NULL,
                            [Username]     NVARCHAR(256) NOT NULL,
                            [OwnerSid]     NVARCHAR(200) NOT NULL DEFAULT '',
                            [KeyType]      VARCHAR(50) NOT NULL DEFAULT 'personal',
                            [KeyPrefix]    VARCHAR(50) NOT NULL,
                            [EncryptedKey] NVARCHAR(MAX) NOT NULL,
                            [ScopesJson]   NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                            [ExpiresAt]    DATETIME2 NULL,
                            [CreatedAt]    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                            [LastUsedAt]   DATETIME2 NULL
                        );
                    END;

                    IF OBJECT_ID('dbo.UserQuotas', 'U') IS NULL
                    BEGIN
                        CREATE TABLE [dbo].[UserQuotas] (
                            [Username]     NVARCHAR(256) PRIMARY KEY,
                            [MaxKeys]      INT NOT NULL DEFAULT 5,
                            [CreatedAt]    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                            [UpdatedAt]    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                        );
                    END;

                    IF OBJECT_ID('dbo.AccessPolicies', 'U') IS NULL
                    BEGIN
                        CREATE TABLE [dbo].[AccessPolicies] (
                            [Id]            VARCHAR(100) PRIMARY KEY,
                            [TargetId]      VARCHAR(250) NOT NULL,
                            [RequiredGroup] NVARCHAR(256) NOT NULL,
                            [IsAllowed]     BIT NOT NULL DEFAULT 1,
                            [CreatedAt]     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                        );
                    END;

                    IF OBJECT_ID('dbo.GroupMappings', 'U') IS NULL
                    BEGIN
                        CREATE TABLE [dbo].[GroupMappings] (
                            [Id]             VARCHAR(100) PRIMARY KEY,
                            [ExternalId]     VARCHAR(256) NOT NULL,
                            [InternalGroup]  NVARCHAR(256) NOT NULL,
                            [CreatedAt]      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                        );
                    END;

                    IF OBJECT_ID('dbo.AuditLogs', 'U') IS NULL
                    BEGIN
                        CREATE TABLE [dbo].[AuditLogs] (
                            [AuditId]           BIGINT IDENTITY(1,1) PRIMARY KEY,
                            [RequestId]         VARCHAR(64) NOT NULL,
                            [UserPrincipalName] NVARCHAR(256) NOT NULL,
                            [UserSid]           VARCHAR(180) NOT NULL,
                            [ServerCodeName]    VARCHAR(100) NOT NULL,
                            [ItemName]          VARCHAR(150) NULL,
                            [RequestMethod]     VARCHAR(50) NOT NULL,
                            [ExecutionTimeMs]   INT NOT NULL,
                            [StatusCode]        INT NOT NULL,
                            [RequestPayload]    NVARCHAR(MAX) NULL,
                            [ResponsePayload]   NVARCHAR(MAX) NULL,
                            [ErrorMessage]      NVARCHAR(MAX) NULL,
                            [Timestamp]         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                        );
                    END;

                    IF OBJECT_ID('dbo.AdminAuditLogs', 'U') IS NULL
                    BEGIN
                        CREATE TABLE [dbo].[AdminAuditLogs] (
                            [Id]           VARCHAR(50) NOT NULL PRIMARY KEY,
                            [Username]     NVARCHAR(256) NOT NULL,
                            [Action]       VARCHAR(100) NOT NULL,
                            [Target]       VARCHAR(256) NOT NULL,
                            [Details]      NVARCHAR(MAX) NULL,
                            [Success]      BIT NOT NULL,
                            [ErrorMessage] NVARCHAR(MAX) NULL,
                            [Timestamp]    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                        );
                    END;

                    IF OBJECT_ID('dbo.SecretProviders', 'U') IS NULL
                    BEGIN
                        CREATE TABLE [dbo].[SecretProviders] (
                            [ProviderId]          INT IDENTITY(1,1) PRIMARY KEY,
                            [ProviderName]        VARCHAR(50) NOT NULL UNIQUE,
                            [DisplayName]         NVARCHAR(100) NOT NULL,
                            [EncryptedConfigJson] NVARCHAR(MAX) NULL,
                            [IsEnabled]           BIT NOT NULL DEFAULT 1,
                            [UpdatedAt]           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                        );
                    END;

                    IF OBJECT_ID('dbo.AuthProviderConfigs', 'U') IS NULL
                    BEGIN
                        CREATE TABLE [dbo].[AuthProviderConfigs] (
                            [AuthId]              INT IDENTITY(1,1) PRIMARY KEY,
                            [ProviderName]        VARCHAR(50) NOT NULL UNIQUE,
                            [DisplayName]         NVARCHAR(100) NOT NULL,
                            [UserHeader]          VARCHAR(100) NULL DEFAULT 'Remote-User',
                            [GroupsHeader]        VARCHAR(100) NULL DEFAULT 'Remote-Groups',
                            [EncryptedConfigJson] NVARCHAR(MAX) NULL,
                            [IsEnabled]           BIT NOT NULL DEFAULT 1,
                            [UpdatedAt]           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                        );
                    END;

                    IF OBJECT_ID('dbo.OAuthClients', 'U') IS NULL
                    BEGIN
                        CREATE TABLE [dbo].[OAuthClients] (
                            [ClientId]         VARCHAR(100) PRIMARY KEY,
                            [ClientSecretHash] VARCHAR(256) NOT NULL DEFAULT '',
                            [ClientName]       NVARCHAR(200) NOT NULL,
                            [ClientType]       VARCHAR(50) NOT NULL DEFAULT 'confidential',
                            [RedirectUrisJson] NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                            [GrantTypesJson]   NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                            [ScopesJson]       NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                            [OwnerSid]         NVARCHAR(200) NOT NULL DEFAULT '',
                            [CreatedBy]        NVARCHAR(256) NOT NULL DEFAULT '',
                            [ExpiresAt]        DATETIME2 NULL,
                            [CreatedAt]        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                            [LastUsedAt]       DATETIME2 NULL
                        );
                    END;
                ");
            }
            else if (provider == "mysql")
            {
                conn.Execute(@"
                    CREATE TABLE IF NOT EXISTS `Servers` (
                        `Id`                VARCHAR(100) PRIMARY KEY,
                        `DisplayName`       VARCHAR(200) NOT NULL,
                        `Url`               VARCHAR(500) NOT NULL,
                        `Enabled`           TINYINT(1) NOT NULL DEFAULT 1,
                        `Hidden`            TINYINT(1) NOT NULL DEFAULT 0,
                        `Type`              VARCHAR(20) NOT NULL DEFAULT 'sse',
                        `SecretProvider`    VARCHAR(50) NOT NULL DEFAULT 'None',
                        `SecretItemKey`     VARCHAR(100) NULL,
                        `SecretMount`       VARCHAR(100) NULL,
                        `SecretPath`        VARCHAR(250) NULL,
                        `SecretField`       VARCHAR(100) NULL,
                        `AuthShape`         VARCHAR(20) NOT NULL DEFAULT 'bearer',
                        `CustomHeaderName`  VARCHAR(100) NULL,
                        `Categories`        LONGTEXT NOT NULL,
                        `ApiKey`            LONGTEXT NULL,
                        `HeadersJson`       LONGTEXT NULL,
                        `AutoDiscovered`    TINYINT(1) NOT NULL DEFAULT 0,
                        `AllowPassThroughAuth` TINYINT(1) NOT NULL DEFAULT 0,
                        `DynamicAuthPrompt` LONGTEXT NULL
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

                    CREATE TABLE IF NOT EXISTS `Settings` (
                        `Id`                      VARCHAR(50) PRIMARY KEY,
                        `DashboardTitle`          VARCHAR(200) NOT NULL DEFAULT 'MCP Gateway',
                        `DashboardIcon`           VARCHAR(100) NOT NULL DEFAULT 'fa-solid fa-network-wired',
                        `EmbeddingProvider`       VARCHAR(50) NULL,
                        `EmbeddingApiUrl`         VARCHAR(500) NULL,
                        `EmbeddingApiKey`         LONGTEXT NULL,
                        `EmbeddingApiModel`       VARCHAR(100) NULL,
                        `EmbeddingModelDir`       VARCHAR(500) NULL,
                        `RequireManualApproval`   TINYINT(1) NOT NULL DEFAULT 0,
                        `GlobalMaxKeys`           INT NOT NULL DEFAULT 100,
                        `UserMaxKeys`             INT NOT NULL DEFAULT 5,
                        `UserSecretStorage`       VARCHAR(50) NOT NULL DEFAULT 'Database'
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

                    CREATE TABLE IF NOT EXISTS `UserServerCredentials` (
                        `Id`                  VARCHAR(100) PRIMARY KEY,
                        `Username`            VARCHAR(200) NOT NULL,
                        `ServerId`            VARCHAR(100) NOT NULL,
                        `EncryptedSecretJson` LONGTEXT NULL
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

                    CREATE TABLE IF NOT EXISTS `AppKeys` (
                        `Id`           VARCHAR(100) PRIMARY KEY,
                        `Name`         VARCHAR(200) NOT NULL,
                        `Username`     VARCHAR(256) NOT NULL,
                        `OwnerSid`     VARCHAR(200) NOT NULL DEFAULT '',
                        `KeyType`      VARCHAR(50) NOT NULL DEFAULT 'personal',
                        `KeyPrefix`    VARCHAR(50) NOT NULL,
                        `EncryptedKey` LONGTEXT NOT NULL,
                        `ScopesJson`   LONGTEXT NOT NULL,
                        `ExpiresAt`    DATETIME NULL,
                        `CreatedAt`    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        `LastUsedAt`   DATETIME NULL
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

                    CREATE TABLE IF NOT EXISTS `UserQuotas` (
                        `Username`     VARCHAR(256) PRIMARY KEY,
                        `MaxKeys`      INT NOT NULL DEFAULT 5,
                        `CreatedAt`    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        `UpdatedAt`    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

                    CREATE TABLE IF NOT EXISTS `AccessPolicies` (
                        `Id`            VARCHAR(100) PRIMARY KEY,
                        `TargetId`      VARCHAR(250) NOT NULL,
                        `RequiredGroup` VARCHAR(256) NOT NULL,
                        `IsAllowed`     TINYINT(1) NOT NULL DEFAULT 1,
                        `CreatedAt`     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

                    CREATE TABLE IF NOT EXISTS `GroupMappings` (
                        `Id`             VARCHAR(100) PRIMARY KEY,
                        `ExternalId`     VARCHAR(256) NOT NULL,
                        `InternalGroup`  VARCHAR(256) NOT NULL,
                        `CreatedAt`      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

                    CREATE TABLE IF NOT EXISTS `AuditLogs` (
                        `AuditId`           BIGINT AUTO_INCREMENT PRIMARY KEY,
                        `RequestId`         VARCHAR(64) NOT NULL,
                        `UserPrincipalName` VARCHAR(256) NOT NULL,
                        `UserSid`           VARCHAR(180) NOT NULL,
                        `ServerCodeName`    VARCHAR(100) NOT NULL,
                        `ItemName`          VARCHAR(150) NULL,
                        `RequestMethod`     VARCHAR(50) NOT NULL,
                        `ExecutionTimeMs`   INT NOT NULL,
                        `StatusCode`        INT NOT NULL,
                        `RequestPayload`    LONGTEXT NULL,
                        `ResponsePayload`   LONGTEXT NULL,
                        `ErrorMessage`      LONGTEXT NULL,
                        `Timestamp`         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

                    CREATE TABLE IF NOT EXISTS `AdminAuditLogs` (
                        `Id`           VARCHAR(50) NOT NULL PRIMARY KEY,
                        `Username`     VARCHAR(256) NOT NULL,
                        `Action`       VARCHAR(100) NOT NULL,
                        `Target`       VARCHAR(256) NOT NULL,
                        `Details`      LONGTEXT NULL,
                        `Success`      TINYINT(1) NOT NULL,
                        `ErrorMessage` LONGTEXT NULL,
                        `Timestamp`    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

                    CREATE TABLE IF NOT EXISTS `SecretProviders` (
                        `ProviderId`          INT AUTO_INCREMENT PRIMARY KEY,
                        `ProviderName`        VARCHAR(50) NOT NULL UNIQUE,
                        `DisplayName`         VARCHAR(100) NOT NULL,
                        `EncryptedConfigJson` LONGTEXT NULL,
                        `IsEnabled`           TINYINT(1) NOT NULL DEFAULT 1,
                        `UpdatedAt`           DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

                    CREATE TABLE IF NOT EXISTS `AuthProviderConfigs` (
                        `AuthId`              INT AUTO_INCREMENT PRIMARY KEY,
                        `ProviderName`        VARCHAR(50) NOT NULL UNIQUE,
                        `DisplayName`         VARCHAR(100) NOT NULL,
                        `UserHeader`          VARCHAR(100) NULL DEFAULT 'Remote-User',
                        `GroupsHeader`        VARCHAR(100) NULL DEFAULT 'Remote-Groups',
                        `EncryptedConfigJson` LONGTEXT NULL,
                        `IsEnabled`           TINYINT(1) NOT NULL DEFAULT 1,
                        `UpdatedAt`           DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

                    CREATE TABLE IF NOT EXISTS `OAuthClients` (
                        `ClientId`         VARCHAR(100) PRIMARY KEY,
                        `ClientSecretHash` VARCHAR(256) NOT NULL DEFAULT '',
                        `ClientName`       VARCHAR(200) NOT NULL,
                        `ClientType`       VARCHAR(50) NOT NULL DEFAULT 'confidential',
                        `RedirectUrisJson` LONGTEXT NOT NULL,
                        `GrantTypesJson`   LONGTEXT NOT NULL,
                        `ScopesJson`       LONGTEXT NOT NULL,
                        `OwnerSid`         VARCHAR(200) NOT NULL DEFAULT '',
                        `CreatedBy`        VARCHAR(256) NOT NULL DEFAULT '',
                        `ExpiresAt`        DATETIME NULL,
                        `CreatedAt`        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        `LastUsedAt`       DATETIME NULL
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                ");
            }
        }

        private static void EnsureDefaultRows(IDbConnection conn, string provider)
        {
            // Check settings and default rows
            var countSettings = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Settings;");
            if (countSettings == 0)
            {
                conn.Execute("INSERT INTO Settings (Id, GlobalMaxKeys, UserMaxKeys) VALUES ('default', 0, 0);");
            }

            // Populate default SecretProviders safely and provider-agnostically (external providers disabled by default)
            var secretProviders = new[]
            {
                new { ProviderName = "Environment", DisplayName = "Container Environment", IsEnabled = 1 },
                new { ProviderName = "Vault", DisplayName = "HashiCorp Vault (KV v2)", IsEnabled = 0 },
                new { ProviderName = "WindowsRegistry", DisplayName = "Windows Registry (DPAPI)", IsEnabled = 0 },
                new { ProviderName = "TokenExchange", DisplayName = "OAuth2 / OIDC Token Exchange (OBO)", IsEnabled = 0 }
            };

            var existingSecretProviders = conn.Query<string>(
                "SELECT ProviderName FROM SecretProviders WHERE ProviderName IN @Names",
                new { Names = secretProviders.Select(p => p.ProviderName).ToArray() }
            ).ToHashSet();

            foreach (var sp in secretProviders)
            {
                if (!existingSecretProviders.Contains(sp.ProviderName))
                {
                    conn.Execute("INSERT INTO SecretProviders (ProviderName, DisplayName, IsEnabled) VALUES (@ProviderName, @DisplayName, @IsEnabled)", sp);
                    existingSecretProviders.Add(sp.ProviderName);
                }
            }

            // Populate default AuthProviderConfigs safely and provider-agnostically (Active Directory disabled by default)
            var authProviders = new[]
            {
                new { ProviderName = "ActiveDirectory", DisplayName = "Active Directory", UserHeader = "Remote-User", GroupsHeader = "Remote-Groups", IsEnabled = 0 },
                new { ProviderName = "HeaderAuth", DisplayName = "Configurable Reverse Proxy Header Auth", UserHeader = "Remote-User", GroupsHeader = "Remote-Groups", IsEnabled = 1 },
                new { ProviderName = "PocketID", DisplayName = "PocketID OIDC", UserHeader = "Remote-User", GroupsHeader = "Remote-Groups", IsEnabled = 1 }
            };

            var existingAuthProviders = conn.Query<string>(
                "SELECT ProviderName FROM AuthProviderConfigs WHERE ProviderName IN @Names",
                new { Names = authProviders.Select(p => p.ProviderName).ToArray() }
            ).ToHashSet();

            foreach (var ap in authProviders)
            {
                if (!existingAuthProviders.Contains(ap.ProviderName))
                {
                    conn.Execute("INSERT INTO AuthProviderConfigs (ProviderName, DisplayName, UserHeader, GroupsHeader, IsEnabled) VALUES (@ProviderName, @DisplayName, @UserHeader, @GroupsHeader, @IsEnabled)", ap);
                    existingAuthProviders.Add(ap.ProviderName);
                }
            }
        }

        public static void ValidateSchemaCompatibility(IDbConnection conn, string provider, ILogger logger)
        {
            logger.LogInformation("Running database schema compatibility validation pass ({Provider})...", provider);

            var tablesToCheck = new Dictionary<string, string>
            {
                { "Servers", "SELECT Id, DisplayName, Url, Enabled, Hidden, Type, SecretProvider, SecretItemKey, SecretMount, SecretPath, SecretField, AuthShape, CustomHeaderName, Categories, ApiKey, HeadersJson, AutoDiscovered FROM Servers WHERE 1=0" },
                { "Settings", "SELECT Id, EmbeddingProvider, EmbeddingApiUrl, EmbeddingApiKey, EmbeddingApiModel, EmbeddingModelDir, GlobalMaxKeys, UserMaxKeys FROM Settings WHERE 1=0" },
                { "AppKeys", "SELECT Id, Name, Username, OwnerSid, KeyType, KeyPrefix, EncryptedKey, ScopesJson, ExpiresAt, CreatedAt, LastUsedAt FROM AppKeys WHERE 1=0" },
                { "OAuthClients", "SELECT ClientId, ClientSecretHash, ClientName, ClientType, RedirectUrisJson, GrantTypesJson, ScopesJson, OwnerSid, CreatedBy, ExpiresAt, CreatedAt, LastUsedAt FROM OAuthClients WHERE 1=0" },
                { "UserQuotas", "SELECT Username, MaxKeys, CreatedAt, UpdatedAt FROM UserQuotas WHERE 1=0" },
                { "AccessPolicies", "SELECT Id, TargetId, RequiredGroup, IsAllowed FROM AccessPolicies WHERE 1=0" },
                { "GroupMappings", "SELECT Id, ExternalId, InternalGroup FROM GroupMappings WHERE 1=0" },
                { "AuditLogs", "SELECT RequestId, UserPrincipalName, UserSid, ServerCodeName, ItemName, RequestMethod, ExecutionTimeMs, StatusCode, RequestPayload, ResponsePayload, ErrorMessage, Timestamp FROM AuditLogs WHERE 1=0" },
                { "AdminAuditLogs", "SELECT Id, Username, Action, Target, Details, Success, ErrorMessage, Timestamp FROM AdminAuditLogs WHERE 1=0" },
                { "SecretProviders", "SELECT ProviderName, DisplayName, EncryptedConfigJson, IsEnabled FROM SecretProviders WHERE 1=0" },
                { "AuthProviderConfigs", "SELECT ProviderName, DisplayName, UserHeader, GroupsHeader, EncryptedConfigJson, IsEnabled FROM AuthProviderConfigs WHERE 1=0" }
            };

            foreach (var table in tablesToCheck)
            {
                try
                {
                    conn.Execute(table.Value);
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Database schema compatibility check failed on table '{table.Key}'. Required column(s) may be missing, mismatched, or the table is defined using a legacy schema (e.g. McpServers instead of Servers). Please apply the migration scripts located in 'scripts/db/{provider}/' or recreate the table. Error detail: {ex.Message}";
                    logger.LogCritical(ex, "{ErrorMessage}", errorMsg);
                    throw new InvalidOperationException(errorMsg, ex);
                }
            }

            // Engine-specific validations for procedures, columns, types, and foreign keys
            if (provider == "sqlite")
            {
                ValidateSqliteSchema(conn, logger);
            }
            else if (provider == "mssql")
            {
                ValidateMssqlSchema(conn, logger);
            }
            else if (provider == "mysql")
            {
                ValidateMySqlSchema(conn, logger);
            }

            logger.LogInformation("Database schema compatibility check passed successfully.");
        }

        private static void ValidateSqliteSchema(IDbConnection conn, ILogger logger)
        {
            var spCols = conn.Query<string>("SELECT name FROM pragma_table_info('SecretProviders');").ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!spCols.Contains("EncryptedConfigJson"))
            {
                throw new InvalidOperationException("SQLite schema compatibility check failed: SecretProviders table is missing 'EncryptedConfigJson' column.");
            }

            var apCols = conn.Query<string>("SELECT name FROM pragma_table_info('AuthProviderConfigs');").ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!apCols.Contains("EncryptedConfigJson"))
            {
                throw new InvalidOperationException("SQLite schema compatibility check failed: AuthProviderConfigs table is missing 'EncryptedConfigJson' column.");
            }

            var appKeyCols = conn.Query<string>("SELECT name FROM pragma_table_info('AppKeys');").ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!appKeyCols.Contains("OwnerSid"))
            {
                throw new InvalidOperationException("SQLite schema compatibility check failed: AppKeys table is missing 'OwnerSid' column.");
            }
            if (!appKeyCols.Contains("KeyType"))
            {
                throw new InvalidOperationException("SQLite schema compatibility check failed: AppKeys table is missing 'KeyType' column.");
            }
            if (!appKeyCols.Contains("LastUsedAt"))
            {
                throw new InvalidOperationException("SQLite schema compatibility check failed: AppKeys table is missing 'LastUsedAt' column.");
            }

            var oauthCols = conn.Query<string>("SELECT name FROM pragma_table_info('OAuthClients');").ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!oauthCols.Contains("ClientId") || !oauthCols.Contains("ClientSecretHash") || !oauthCols.Contains("ClientName"))
            {
                throw new InvalidOperationException("SQLite schema compatibility check failed: OAuthClients table is missing required columns.");
            }
            if (!oauthCols.Contains("LastUsedAt"))
            {
                throw new InvalidOperationException("SQLite schema compatibility check failed: OAuthClients table is missing 'LastUsedAt' column.");
            }
        }

        private static void ValidateMssqlSchema(IDbConnection conn, ILogger logger)
        {
            // 1. Validate Tools.ServerId column type is string (varchar/nvarchar) if Tools table exists
            var toolsExists = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM sys.tables WHERE name = 'Tools';") > 0;
            if (toolsExists)
            {
                var serverIdType = conn.ExecuteScalar<string>(@"
                    SELECT t.name 
                    FROM sys.columns c
                    JOIN sys.types t ON c.user_type_id = t.user_type_id
                    WHERE c.object_id = OBJECT_ID('dbo.Tools') AND c.name = 'ServerId';");

                if (serverIdType != null && (serverIdType.Equals("int", StringComparison.OrdinalIgnoreCase) || serverIdType.Equals("bigint", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("MSSQL schema compatibility check failed: Tools.ServerId column must be VARCHAR(100), but found legacy integer type.");
                }

                // Check foreign key constraint on Tools
                var fkExists = conn.ExecuteScalar<int>(@"
                    SELECT COUNT(*) 
                    FROM sys.foreign_keys fk
                    JOIN sys.tables t ON fk.parent_object_id = t.object_id
                    WHERE t.name = 'Tools' AND fk.referenced_object_id = OBJECT_ID('dbo.Servers');") > 0;

                if (!fkExists)
                {
                    logger.LogWarning("MSSQL schema check: Tools table foreign key to Servers is not defined or using non-standard naming.");
                }
            }

            // 2. Validate all 14 stored procedures exist
            var expectedProcedures = new[]
            {
                "sp_EvaluateUserAccess",
                "sp_GetAllowedItemsForGroups",
                "sp_GetServerSecrets",
                "sp_SaveSecretProvider",
                "sp_SaveAuthProvider",
                "sp_InsertAuditLog",
                "sp_SaveAppKey",
                "sp_DeleteAppKey",
                "sp_GetAppKeys",
                "sp_InsertAdminAuditLog",
                "sp_SaveOAuthClient",
                "sp_GetOAuthClients",
                "sp_GetOAuthClientById",
                "sp_DeleteOAuthClient"
            };

            var existingProcedures = conn.Query<string>("SELECT name FROM sys.procedures;").ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missingProcedures = expectedProcedures.Where(p => !existingProcedures.Contains(p)).ToList();

            if (missingProcedures.Count > 0)
            {
                throw new InvalidOperationException($"MSSQL schema compatibility check failed: missing required stored procedures: {string.Join(", ", missingProcedures)}. Please execute scripts/db/mssql/02_procedures.sql.");
            }

            // 3. Validate sp_SaveAppKey parameters (ensure CreatedAt is NOT a parameter)
            var saveAppKeyParams = conn.Query<string>(@"
                SELECT p.name 
                FROM sys.parameters p
                WHERE p.object_id = OBJECT_ID('dbo.sp_SaveAppKey');").ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (saveAppKeyParams.Contains("@CreatedAt"))
            {
                throw new InvalidOperationException("MSSQL schema compatibility check failed: sp_SaveAppKey should not accept @CreatedAt parameter as CreatedAt is generated via SYSUTCDATETIME().");
            }

            // 4. Validate sp_SaveOAuthClient parameters (ensure CreatedAt is NOT a parameter)
            var saveOAuthClientParams = conn.Query<string>(@"
                SELECT p.name 
                FROM sys.parameters p
                WHERE p.object_id = OBJECT_ID('dbo.sp_SaveOAuthClient');").ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (saveOAuthClientParams.Contains("@CreatedAt"))
            {
                throw new InvalidOperationException("MSSQL schema compatibility check failed: sp_SaveOAuthClient should not accept @CreatedAt parameter as CreatedAt is generated via SYSUTCDATETIME().");
            }
        }

        private static void ValidateMySqlSchema(IDbConnection conn, ILogger logger)
        {
            // 1. Validate Tools.ServerId column type if Tools table exists
            var toolsExists = conn.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM information_schema.tables 
                WHERE table_schema = DATABASE() AND table_name = 'Tools';") > 0;

            if (toolsExists)
            {
                var serverIdType = conn.ExecuteScalar<string>(@"
                    SELECT DATA_TYPE FROM information_schema.columns 
                    WHERE table_schema = DATABASE() AND table_name = 'Tools' AND column_name = 'ServerId';");

                if (serverIdType != null && (serverIdType.Equals("int", StringComparison.OrdinalIgnoreCase) || serverIdType.Equals("bigint", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("MySQL schema compatibility check failed: Tools.ServerId column must be VARCHAR(100), but found legacy integer type.");
                }
            }

            // 2. Validate all 14 stored procedures exist
            var expectedProcedures = new[]
            {
                "sp_EvaluateUserAccess",
                "sp_GetAllowedItemsForGroups",
                "sp_GetServerSecrets",
                "sp_SaveSecretProvider",
                "sp_SaveAuthProvider",
                "sp_InsertAuditLog",
                "sp_SaveAppKey",
                "sp_DeleteAppKey",
                "sp_GetAppKeys",
                "sp_InsertAdminAuditLog",
                "sp_SaveOAuthClient",
                "sp_GetOAuthClients",
                "sp_GetOAuthClientById",
                "sp_DeleteOAuthClient"
            };

            var existingProcedures = conn.Query<string>(@"
                SELECT ROUTINE_NAME 
                FROM information_schema.routines 
                WHERE ROUTINE_SCHEMA = DATABASE() AND ROUTINE_TYPE = 'PROCEDURE';").ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missingProcedures = expectedProcedures.Where(p => !existingProcedures.Contains(p)).ToList();
            if (missingProcedures.Count > 0)
            {
                throw new InvalidOperationException($"MySQL schema compatibility check failed: missing required stored procedures: {string.Join(", ", missingProcedures)}. Please execute scripts/db/mysql/02_procedures.sql.");
            }

            // 3. Validate procedure parameters have p_ prefix
            var sampleParams = conn.Query<string>(@"
                SELECT PARAMETER_NAME 
                FROM information_schema.parameters 
                WHERE SPECIFIC_SCHEMA = DATABASE() AND SPECIFIC_NAME = 'sp_SaveAppKey' AND PARAMETER_NAME IS NOT NULL;").ToList();

            if (sampleParams.Count > 0 && sampleParams.Any(p => !p.StartsWith("p_", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("MySQL schema compatibility check failed: sp_SaveAppKey parameters must use 'p_' prefix (e.g. p_Id, p_Name, p_Username, etc.).");
            }

            // 4. Validate sp_SaveOAuthClient parameters have p_ prefix
            var sampleOAuthParams = conn.Query<string>(@"
                SELECT PARAMETER_NAME 
                FROM information_schema.parameters 
                WHERE SPECIFIC_SCHEMA = DATABASE() AND SPECIFIC_NAME = 'sp_SaveOAuthClient' AND PARAMETER_NAME IS NOT NULL;").ToList();

            if (sampleOAuthParams.Count > 0 && sampleOAuthParams.Any(p => !p.StartsWith("p_", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("MySQL schema compatibility check failed: sp_SaveOAuthClient parameters must use 'p_' prefix (e.g. p_ClientId, p_ClientName, etc.).");
            }
        }
    }
}



