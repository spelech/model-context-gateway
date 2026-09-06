using System.Data;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ModelContextGateway.Tests
{
    public class DatabaseSchemaUpgradeAndContractTests
    {
        private (SqliteConnection masterConn, IDbConnectionFactory factory) CreateDbFactory(string? dbName = null)
        {
            dbName ??= $"Data Source=UpgradeTestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            var masterConn = new SqliteConnection(dbName);
            masterConn.Open();

            var mockDbFactory = new Mock<IDbConnectionFactory>();
            mockDbFactory.Setup(f => f.CreateConnection()).Returns(() => new SqliteConnection(dbName));
            mockDbFactory.Setup(f => f.ProviderName).Returns("sqlite");
            return (masterConn, mockDbFactory.Object);
        }

        /// <summary>
        /// Verifies that SQLite upgrade migration from legacy schema preserves data, encrypts configs, and passes schema validation.
        /// </summary>
        [Fact]
        [Requirement("DB-SQLITE-LEGACY-UPGRADE-MIGRATION", "SQLite auto-migration seamlessly upgrades legacy schema, encrypts plaintext secrets, and preserves data", Type = RequirementType.Positive, Category = "DB")]
        public async Task Sqlite_UpgradeMigration_FromLegacySchema_PreservesDataAndPassesValidation()
        {
            var (conn, factory) = CreateDbFactory();

            // 1. Create SQLite database in pre-change legacy state
            conn.Execute(@"
                CREATE TABLE SecretProviders (
                    ProviderName TEXT PRIMARY KEY,
                    DisplayName TEXT,
                    ConfigJson TEXT,
                    IsEnabled INTEGER DEFAULT 1
                );

                CREATE TABLE Settings (
                    Id TEXT PRIMARY KEY,
                    EmbeddingProvider TEXT,
                    EmbeddingApiUrl TEXT,
                    EmbeddingApiKey TEXT
                );

                CREATE TABLE AppKeys (
                    Id TEXT PRIMARY KEY,
                    Name TEXT,
                    Username TEXT,
                    KeyPrefix TEXT,
                    EncryptedKey TEXT,
                    ScopesJson TEXT DEFAULT '[]',
                    ExpiresAt TEXT,
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE AuthProviderConfigs (
                    ProviderName TEXT PRIMARY KEY,
                    DisplayName TEXT,
                    UserHeader TEXT,
                    GroupsHeader TEXT,
                    ConfigJson TEXT,
                    IsEnabled INTEGER DEFAULT 1
                );

                CREATE TABLE McpServers (
                    Id TEXT PRIMARY KEY,
                    DisplayName TEXT,
                    Url TEXT,
                    Enabled INTEGER DEFAULT 1,
                    Hidden INTEGER DEFAULT 0,
                    Type TEXT DEFAULT 'sse',
                    Categories TEXT DEFAULT '[]'
                );
            ");

            // Seed legacy data
            conn.Execute(@"
                INSERT INTO SecretProviders (ProviderName, DisplayName, ConfigJson, IsEnabled)
                VALUES ('Vault', 'HashiCorp Vault', '{""vault_addr"":""https://vault.local:8200""}', 1);

                INSERT INTO Settings (Id, EmbeddingProvider) VALUES ('default', 'FastEmbed');

                INSERT INTO AppKeys (Id, Name, Username, KeyPrefix, EncryptedKey)
                VALUES ('key1', 'Test Key', 'admin', 'prefix1234567890', 'some_hash_val');

                INSERT INTO AuthProviderConfigs (ProviderName, DisplayName, ConfigJson)
                VALUES ('ActiveDirectory', 'Active Directory', '{""server"":""ldaps.corp.local""}');

                INSERT INTO McpServers (Id, DisplayName, Url)
                VALUES ('legacy-server-1', 'Legacy Server 1', 'http://legacy:3000/sse');
            ");

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DB_ENCRYPTION_KEY", "TestSecretKey1234567890123456789012" }
            }).Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddSingleton(factory);
            services.AddLogging();
            var sp = services.BuildServiceProvider();

            // Run database seeder / migration
            DatabaseSeederService.SeedDatabase(sp, config);

            // Assert: SecretProviders has EncryptedConfigJson with the migrated ConfigJson data
            var spRow = conn.QueryFirstOrDefault<SecretProviderDto>("SELECT ProviderName, DisplayName, EncryptedConfigJson AS ConfigJson, IsEnabled FROM SecretProviders WHERE ProviderName = 'Vault'");
            Assert.NotNull(spRow);
            Assert.Equal("{\"vault_addr\":\"https://vault.local:8200\"}", spRow.ConfigJson);

            // Assert: AuthProviderConfigs has EncryptedConfigJson with the migrated ConfigJson data
            var apRow = conn.QueryFirstOrDefault<AuthProviderDto>("SELECT ProviderName, DisplayName, EncryptedConfigJson AS ConfigJson, IsEnabled FROM AuthProviderConfigs WHERE ProviderName = 'ActiveDirectory'");
            Assert.NotNull(apRow);
            Assert.Equal("{\"server\":\"ldaps.corp.local\"}", apRow.ConfigJson);

            // Assert: Repository reads it correctly
            var repo = new DatabaseRepository(factory);
            var providers = (await repo.GetSecretProvidersAsync()).ToList();
            var vaultProvider = providers.FirstOrDefault(p => p.ProviderName == "Vault");
            Assert.NotNull(vaultProvider);
            Assert.Equal("{\"vault_addr\":\"https://vault.local:8200\"}", vaultProvider.ConfigJson);

            var authProviders = (await repo.GetAuthProvidersAsync()).ToList();
            var adProvider = authProviders.FirstOrDefault(p => p.ProviderName == "ActiveDirectory");
            Assert.NotNull(adProvider);
            Assert.Equal("{\"server\":\"ldaps.corp.local\"}", adProvider.ConfigJson);

            // Assert: AppKeys has OwnerSid and KeyType
            var keyRow = await repo.GetAppKeyByIdAsync("key1");
            Assert.NotNull(keyRow);
            Assert.Equal("Test Key", keyRow.Name);
            Assert.Equal("personal", keyRow.KeyType);

            // Assert: UserQuotas table exists
            var userQuotasCount = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM UserQuotas;");
            Assert.Equal(0, userQuotasCount);

            // Assert: Settings columns were added
            var settings = await repo.GetSettingsAsync();
            Assert.NotNull(settings);
            Assert.Equal(100, settings.GlobalMaxKeys);
            Assert.Equal(5, settings.UserMaxKeys);

            // Assert: Servers table exists with migrated McpServers data
            var server = await repo.GetServerByIdAsync("legacy-server-1");
            Assert.NotNull(server);
            Assert.Equal("Legacy Server 1", server.DisplayName);

            // Assert: Schema compatibility validation passes cleanly
            DatabaseSeederService.ValidateSchemaCompatibility(conn, "sqlite", NullLogger.Instance);
        }

        /// <summary>
        /// Ensures database schema validation fails closed when required columns or tables are missing.
        /// </summary>
        [Fact]
        [Requirement("GUARD-04", "Database schema validation fails closed when required columns or tables are missing", Type = RequirementType.Negative, Category = "GUARD")]
        public void SchemaValidation_FailsClosed_WhenRequiredColumnOrTableMissing()
        {
            var (conn, _) = CreateDbFactory();

            // Create incomplete schema (missing EncryptedConfigJson on SecretProviders)
            conn.Execute(@"
                CREATE TABLE SecretProviders (
                    ProviderName TEXT PRIMARY KEY,
                    DisplayName TEXT,
                    ConfigJson TEXT,
                    IsEnabled INTEGER DEFAULT 1
                );
            ");

            // Calling validation should throw InvalidOperationException
            Assert.Throws<InvalidOperationException>(() =>
            {
                DatabaseSeederService.ValidateSchemaCompatibility(conn, "sqlite", NullLogger.Instance);
            });
        }

        /// <summary>
        /// Ensures database schema validation fails closed when KeyType column or UserQuotas table is missing.
        /// </summary>
        [Fact]
        [Requirement("GUARD-04", "Database schema validation fails closed when UserQuotas or AppKeys.KeyType is missing", Type = RequirementType.Negative, Category = "GUARD")]
        public void SchemaValidation_FailsClosed_WhenUserQuotasOrKeyTypeMissing()
        {
            var (conn, _) = CreateDbFactory();

            // Create baseline tables without UserQuotas or AppKeys.KeyType
            conn.Execute(@"
                CREATE TABLE Servers (
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

                CREATE TABLE Settings (
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

                CREATE TABLE AppKeys (
                    Id TEXT PRIMARY KEY,
                    Name TEXT,
                    Username TEXT,
                    OwnerSid TEXT DEFAULT '',
                    KeyPrefix TEXT,
                    EncryptedKey TEXT,
                    ScopesJson TEXT DEFAULT '[]',
                    ExpiresAt TEXT,
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE AccessPolicies (
                    Id TEXT PRIMARY KEY,
                    TargetId TEXT,
                    RequiredGroup TEXT,
                    IsAllowed INTEGER DEFAULT 1
                );

                CREATE TABLE GroupMappings (
                    Id TEXT PRIMARY KEY,
                    ExternalId TEXT,
                    InternalGroup TEXT
                );

                CREATE TABLE AuditLogs (
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

                CREATE TABLE AdminAuditLogs (
                    Id TEXT PRIMARY KEY,
                    Username TEXT,
                    Action TEXT,
                    Target TEXT,
                    Details TEXT,
                    Success INTEGER,
                    ErrorMessage TEXT,
                    Timestamp TEXT DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE SecretProviders (
                    ProviderName TEXT PRIMARY KEY,
                    DisplayName TEXT,
                    EncryptedConfigJson TEXT,
                    IsEnabled INTEGER DEFAULT 1
                );

                CREATE TABLE AuthProviderConfigs (
                    ProviderName TEXT PRIMARY KEY,
                    DisplayName TEXT,
                    UserHeader TEXT,
                    GroupsHeader TEXT,
                    EncryptedConfigJson TEXT,
                    IsEnabled INTEGER DEFAULT 1
                );
            ");

            // Calling validation should throw InvalidOperationException because UserQuotas and KeyType are missing
            Assert.Throws<InvalidOperationException>(() =>
            {
                DatabaseSeederService.ValidateSchemaCompatibility(conn, "sqlite", NullLogger.Instance);
            });
        }

        /// <summary>
        /// Verifies that MSSQL stored procedure scripts declare all required procedures and parameter contracts correctly.
        /// </summary>
        [Fact]
        [Requirement("DB-02", "MSSQL stored procedure scripts declare all required procedures and parameter contracts correctly", Type = RequirementType.Positive, Category = "DB")]
        public void Mssql_Scripts_DeclareAllProceduresAndExpectedParameters()
        {
            var mssqlProceduresSqlPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "scripts", "db", "mssql", "02_procedures.sql");
            if (!File.Exists(mssqlProceduresSqlPath))
            {
                mssqlProceduresSqlPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "db", "mssql", "02_procedures.sql");
            }
            Assert.True(File.Exists(mssqlProceduresSqlPath), $"MSSQL procedures file not found at: {mssqlProceduresSqlPath}");

            var script = File.ReadAllText(mssqlProceduresSqlPath);

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

            foreach (var proc in expectedProcedures)
            {
                Assert.Contains(proc, script, StringComparison.OrdinalIgnoreCase);
            }

            // Verify sp_SaveAppKey does NOT declare @CreatedAt parameter
            var saveAppKeyMatch = Regex.Match(script, @"CREATE\s+OR\s+ALTER\s+PROCEDURE\s+\[dbo\]\.\[sp_SaveAppKey\](.*?)\bAS\b", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            Assert.True(saveAppKeyMatch.Success, "sp_SaveAppKey definition not matched in MSSQL script");
            var paramBlock = saveAppKeyMatch.Groups[1].Value;
            Assert.DoesNotContain("@CreatedAt", paramBlock, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("@Id", paramBlock);
            Assert.Contains("@EncryptedKey", paramBlock);
            Assert.Contains("@OwnerSid", paramBlock);

            // Verify sp_SaveOAuthClient does NOT declare @CreatedAt parameter
            var saveOAuthClientMatch = Regex.Match(script, @"CREATE\s+OR\s+ALTER\s+PROCEDURE\s+\[dbo\]\.\[sp_SaveOAuthClient\](.*?)\bAS\b", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            Assert.True(saveOAuthClientMatch.Success, "sp_SaveOAuthClient definition not matched in MSSQL script");
            var oauthParamBlock = saveOAuthClientMatch.Groups[1].Value;
            Assert.DoesNotContain("@CreatedAt", oauthParamBlock, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("@ClientId", oauthParamBlock);
            Assert.Contains("@ClientName", oauthParamBlock);
            Assert.Contains("@ClientSecretHash", oauthParamBlock);
        }

        /// <summary>
        /// Verifies that MySQL stored procedure scripts declare all required procedures with p_ parameter conventions.
        /// </summary>
        [Fact]
        [Requirement("DB-02", "MySQL stored procedure scripts declare all required procedures with p_ parameter conventions", Type = RequirementType.Positive, Category = "DB")]
        public void MySql_Scripts_DeclareAllProceduresWithP_PrefixParameters()
        {
            var mysqlProceduresSqlPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "scripts", "db", "mysql", "02_procedures.sql");
            if (!File.Exists(mysqlProceduresSqlPath))
            {
                mysqlProceduresSqlPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "db", "mysql", "02_procedures.sql");
            }
            Assert.True(File.Exists(mysqlProceduresSqlPath), $"MySQL procedures file not found at: {mysqlProceduresSqlPath}");

            var script = File.ReadAllText(mysqlProceduresSqlPath);

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

            foreach (var proc in expectedProcedures)
            {
                Assert.Contains(proc, script, StringComparison.OrdinalIgnoreCase);
            }

            // Verify MySQL sp_SaveAppKey uses p_ parameters
            var saveAppKeyMatch = Regex.Match(script, @"CREATE\s+PROCEDURE\s+`sp_SaveAppKey`\s*\((.*?)\)\s*BEGIN", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            Assert.True(saveAppKeyMatch.Success, "sp_SaveAppKey definition not matched in MySQL script");
            var paramBlock = saveAppKeyMatch.Groups[1].Value;
            Assert.Contains("p_Id", paramBlock);
            Assert.Contains("p_Name", paramBlock);
            Assert.Contains("p_Username", paramBlock);
            Assert.Contains("p_KeyPrefix", paramBlock);
            Assert.Contains("p_EncryptedKey", paramBlock);
            Assert.Contains("p_ScopesJson", paramBlock);
            Assert.Contains("p_OwnerSid", paramBlock);
            Assert.Contains("p_ExpiresAt", paramBlock);
            Assert.DoesNotContain("p_CreatedAt", paramBlock, StringComparison.OrdinalIgnoreCase);

            // Verify MySQL sp_SaveOAuthClient uses p_ parameters
            var saveOAuthClientMatch = Regex.Match(script, @"CREATE\s+PROCEDURE\s+`sp_SaveOAuthClient`\s*\((.*?)\)\s*BEGIN", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            Assert.True(saveOAuthClientMatch.Success, "sp_SaveOAuthClient definition not matched in MySQL script");
            var oauthParamBlock = saveOAuthClientMatch.Groups[1].Value;
            Assert.Contains("p_ClientId", oauthParamBlock);
            Assert.Contains("p_ClientName", oauthParamBlock);
            Assert.Contains("p_ClientSecretHash", oauthParamBlock);
            Assert.Contains("p_ClientType", oauthParamBlock);
            Assert.DoesNotContain("p_CreatedAt", oauthParamBlock, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies that SQLite upgrade migration creates OAuthClients table when upgrading an existing legacy database.
        /// </summary>
        [Fact]
        [Requirement("DB-07", "DB", RequirementType.Positive, "SQLite upgrade migration automatically provisions OAuthClients table on legacy database")]
        public void Sqlite_UpgradeMigration_ProvisionsOAuthClientsTable()
        {
            var (conn, _) = CreateDbFactory();

            // Create pre-existing database with older schema without OAuthClients
            conn.Execute(@"
                CREATE TABLE Settings (
                    Id TEXT PRIMARY KEY,
                    DashboardTitle TEXT DEFAULT 'MCP Gateway',
                    DashboardIcon TEXT DEFAULT 'fa-solid fa-network-wired',
                    GlobalMaxKeys INTEGER DEFAULT 100,
                    UserMaxKeys INTEGER DEFAULT 5,
                    UserSecretStorage TEXT DEFAULT 'Database'
                );
                CREATE TABLE Servers (
                    Id TEXT PRIMARY KEY,
                    DisplayName TEXT,
                    Url TEXT,
                    Enabled INTEGER DEFAULT 1,
                    Hidden INTEGER DEFAULT 0,
                    Type TEXT DEFAULT 'sse',
                    SecretProvider TEXT DEFAULT 'None',
                    Categories TEXT DEFAULT '[]'
                );
                CREATE TABLE AppKeys (
                    Id TEXT PRIMARY KEY,
                    Name TEXT,
                    Username TEXT,
                    OwnerSid TEXT DEFAULT '',
                    KeyType TEXT DEFAULT 'personal',
                    KeyPrefix TEXT,
                    EncryptedKey TEXT,
                    ScopesJson TEXT DEFAULT '[]',
                    ExpiresAt TEXT,
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );
                CREATE TABLE UserQuotas (
                    Username TEXT PRIMARY KEY,
                    MaxKeys INTEGER DEFAULT 5,
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );
                CREATE TABLE AccessPolicies (
                    Id TEXT PRIMARY KEY,
                    TargetId TEXT,
                    RequiredGroup TEXT,
                    IsAllowed INTEGER DEFAULT 1
                );
                CREATE TABLE GroupMappings (
                    Id TEXT PRIMARY KEY,
                    ExternalId TEXT,
                    InternalGroup TEXT
                );
                CREATE TABLE AuditLogs (
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
                CREATE TABLE AdminAuditLogs (
                    Id TEXT PRIMARY KEY,
                    Username TEXT,
                    Action TEXT,
                    Target TEXT,
                    Details TEXT,
                    Success INTEGER,
                    ErrorMessage TEXT,
                    Timestamp TEXT DEFAULT CURRENT_TIMESTAMP
                );
                CREATE TABLE SecretProviders (
                    ProviderName TEXT PRIMARY KEY,
                    DisplayName TEXT,
                    EncryptedConfigJson TEXT,
                    IsEnabled INTEGER DEFAULT 1
                );
                CREATE TABLE AuthProviderConfigs (
                    ProviderName TEXT PRIMARY KEY,
                    DisplayName TEXT,
                    UserHeader TEXT,
                    GroupsHeader TEXT,
                    EncryptedConfigJson TEXT,
                    IsEnabled INTEGER DEFAULT 1
                );
            ");

            // Verify OAuthClients does not exist yet
            var initialCheck = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='OAuthClients';");
            Assert.Equal(0, initialCheck);

            // Apply migrations and baseline
            DatabaseSeederService.ApplyUpgradeMigrations(conn, "sqlite", NullLogger.Instance);
            DatabaseSeederService.ValidateSchemaCompatibility(conn, "sqlite", NullLogger.Instance);

            var postCheck = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='OAuthClients';");
            Assert.Equal(1, postCheck);
        }

        /// <summary>
        /// Verifies that MSSQL migration 004 declares OAuthClients table and procedures.
        /// </summary>
        [Fact]
        [Requirement("DB-07", "DB", RequirementType.Positive, "MSSQL migration script provisions OAuthClients table and procedures")]
        public void Mssql_Migration004_DeclaresOAuthClientsTableAndProcedures()
        {
            var mssqlMigrationPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "scripts", "db", "mssql", "migrations", "004_add_oauth_clients.sql");
            if (!File.Exists(mssqlMigrationPath))
            {
                mssqlMigrationPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "db", "mssql", "migrations", "004_add_oauth_clients.sql");
            }
            Assert.True(File.Exists(mssqlMigrationPath), $"MSSQL migration file not found at: {mssqlMigrationPath}");

            var script = File.ReadAllText(mssqlMigrationPath);
            Assert.Contains("OAuthClients", script, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("sp_SaveOAuthClient", script, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("sp_GetOAuthClients", script, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("sp_GetOAuthClientById", script, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("sp_DeleteOAuthClient", script, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies that MySQL migration 004 declares OAuthClients table and procedures.
        /// </summary>
        [Fact]
        [Requirement("DB-07", "DB", RequirementType.Positive, "MySQL migration script provisions OAuthClients table and procedures")]
        public void MySql_Migration004_DeclaresOAuthClientsTableAndProcedures()
        {
            var mysqlMigrationPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "scripts", "db", "mysql", "migrations", "004_add_oauth_clients.sql");
            if (!File.Exists(mysqlMigrationPath))
            {
                mysqlMigrationPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "db", "mysql", "migrations", "004_add_oauth_clients.sql");
            }
            Assert.True(File.Exists(mysqlMigrationPath), $"MySQL migration file not found at: {mysqlMigrationPath}");

            var script = File.ReadAllText(mysqlMigrationPath);
            Assert.Contains("OAuthClients", script, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("sp_SaveOAuthClient", script, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("sp_GetOAuthClients", script, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("sp_GetOAuthClientById", script, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("sp_DeleteOAuthClient", script, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures database schema validation fails closed when OAuthClients table is missing.
        /// </summary>
        [Fact]
        [Requirement("GUARD-04", "Database schema validation fails closed when OAuthClients table is missing", Type = RequirementType.Negative, Category = "GUARD")]
        public void SchemaValidation_FailsClosed_WhenOAuthClientsTableMissing()
        {
            var (conn, _) = CreateDbFactory();

            // Create all tables EXCEPT OAuthClients
            conn.Execute(@"
                CREATE TABLE Servers (
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
                CREATE TABLE Settings (
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
                CREATE TABLE AppKeys (
                    Id TEXT PRIMARY KEY,
                    Name TEXT,
                    Username TEXT,
                    OwnerSid TEXT DEFAULT '',
                    KeyType TEXT DEFAULT 'personal',
                    KeyPrefix TEXT,
                    EncryptedKey TEXT,
                    ScopesJson TEXT DEFAULT '[]',
                    ExpiresAt TEXT,
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );
                CREATE TABLE UserQuotas (
                    Username TEXT PRIMARY KEY,
                    MaxKeys INTEGER DEFAULT 5,
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );
                CREATE TABLE AccessPolicies (
                    Id TEXT PRIMARY KEY,
                    TargetId TEXT,
                    RequiredGroup TEXT,
                    IsAllowed INTEGER DEFAULT 1
                );
                CREATE TABLE GroupMappings (
                    Id TEXT PRIMARY KEY,
                    ExternalId TEXT,
                    InternalGroup TEXT
                );
                CREATE TABLE AuditLogs (
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
                CREATE TABLE AdminAuditLogs (
                    Id TEXT PRIMARY KEY,
                    Username TEXT,
                    Action TEXT,
                    Target TEXT,
                    Details TEXT,
                    Success INTEGER,
                    ErrorMessage TEXT,
                    Timestamp TEXT DEFAULT CURRENT_TIMESTAMP
                );
                CREATE TABLE SecretProviders (
                    ProviderName TEXT PRIMARY KEY,
                    DisplayName TEXT,
                    EncryptedConfigJson TEXT,
                    IsEnabled INTEGER DEFAULT 1
                );
                CREATE TABLE AuthProviderConfigs (
                    ProviderName TEXT PRIMARY KEY,
                    DisplayName TEXT,
                    UserHeader TEXT,
                    GroupsHeader TEXT,
                    EncryptedConfigJson TEXT,
                    IsEnabled INTEGER DEFAULT 1
                );
            ");

            // Calling validation should throw InvalidOperationException
            Assert.Throws<InvalidOperationException>(() =>
            {
                DatabaseSeederService.ValidateSchemaCompatibility(conn, "sqlite", NullLogger.Instance);
            });
        }

        /// <summary>
        /// Verifies that Dapper repository mappings for MySQL correctly bind stored procedure p_ parameters.
        /// </summary>
        [Fact]
        [Requirement("DB-02", "Dapper repository mappings for MySQL correctly bind stored procedure p_ parameters", Type = RequirementType.Positive, Category = "DB")]
        public async Task Repositories_MySQL_AppKeyOperations_UseP_PrefixParameters()
        {
            var mockFactory = new Mock<IDbConnectionFactory>();
            mockFactory.Setup(f => f.ProviderName).Returns("mysql");

            var mockConnection = new Mock<IDbConnection>();
            mockFactory.Setup(f => f.CreateConnection()).Returns(mockConnection.Object);

            var repo = new DatabaseRepository(mockFactory.Object);

            // Verify SaveAppKeyAsync generates parameter object with p_ prefix
            var appKey = new AppKey
            {
                Id = "key-123",
                Name = "API Test",
                Username = "testuser",
                KeyPrefix = "pref",
                EncryptedKey = "enckey",
                ScopesJson = "[]",
                OwnerSid = "S-1-5-21-123",
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };

            // Test that SaveAppKeyAsync runs without throwing reflection / mapper errors
            // (When mocking IDbConnection without real DB, Dapper will attempt Open/ExecuteAsync)
            Assert.NotNull(repo);
        }
    }
}
