using System.Data;
using Dapper;

namespace ModelContextGateway.Infrastructure.Persistence
{
    public static class DatabaseInitializer
    {
        public static void InitializeDatabase(IDbConnection conn)
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
                    DynamicAuthPrompt TEXT,
                    Alias TEXT NULL
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
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
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
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );
            ");

            EnsureAliasColumn(conn);
        }

        public static void EnsureAliasColumn(IDbConnection conn)
        {
            try
            {
                conn.Execute("ALTER TABLE Servers ADD COLUMN Alias TEXT NULL;");
            }
            catch
            {
                // Column already exists
            }
        }
    }
}
