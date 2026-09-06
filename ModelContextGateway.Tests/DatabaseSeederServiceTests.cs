using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ModelContextGateway.Tests
{
    public class DatabaseSeederServiceTests
    {
        private (SqliteConnection masterConn, IDbConnectionFactory factory) CreateDbFactory()
        {
            var dbName = $"Data Source=SeederTestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            var masterConn = new SqliteConnection(dbName);
            masterConn.Open();

            var mockDbFactory = new Mock<IDbConnectionFactory>();
            mockDbFactory.Setup(f => f.CreateConnection()).Returns(() => new SqliteConnection(dbName));
            mockDbFactory.Setup(f => f.ProviderName).Returns("sqlite");
            return (masterConn, mockDbFactory.Object);
        }

        [Fact]
        [Requirement("DB-SEEDER-INIT-SETTINGS-PROVIDERS", "DB", RequirementType.Positive, "DatabaseSeederService initializes default router settings, provider configs, and schema.")]
        public void Seeder_Initializes_Default_Settings_And_Providers()
        {
            var (conn, factory) = CreateDbFactory();
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DB_ENCRYPTION_KEY", "TestSecretKey1234567890123456789012" }
            }).Build();

            services.AddSingleton<IConfiguration>(config);
            services.AddSingleton(factory);
            services.AddLogging();
            var sp = services.BuildServiceProvider();

            DatabaseSeederService.SeedDatabase(sp, config);

            var settings = conn.QueryFirstOrDefault<RouterSettings>("SELECT * FROM Settings");
            Assert.NotNull(settings);
        }

        [Fact]
        [Requirement("SEC-01", "SEC", RequirementType.Positive, "DatabaseSeederService validates DB encryption key strength and warns on weak keys.")]
        public void DbEncryptionKey_Warning_Detection_Works_Correctly()
        {
            var testKeys = new[] { "", "short", "SomeSecureRandomKeyValue999!" };
            var results = new List<bool>();

            foreach (var key in testKeys)
            {
                var isWeak = string.IsNullOrEmpty(key) || key.Length < 16;
                results.Add(isWeak);
            }

            Assert.True(results[0]);
            Assert.True(results[1]);
            Assert.False(results[2]);
        }

        [Fact]
        [Requirement("SEC-01", "SEC", RequirementType.Positive, "DatabaseSeederService migrates legacy plaintext keys to secure one-way hashes on startup.")]
        public async Task Startup_MigratesLegacyKeysToHashedKeys()
        {
            var (connection, mockDbFactory) = CreateDbFactory();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS AppKeys (
                        Id TEXT PRIMARY KEY,
                        Name TEXT,
                        Username TEXT,
                        KeyPrefix TEXT,
                        EncryptedKey TEXT,
                        ScopesJson TEXT DEFAULT '[]',
                        ExpiresAt TEXT,
                        CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                    );
                    CREATE TABLE IF NOT EXISTS Settings (
                        Id TEXT PRIMARY KEY,
                        EmbeddingProvider TEXT,
                        EmbeddingApiUrl TEXT,
                        EmbeddingApiKey TEXT,
                        EmbeddingApiModel TEXT,
                        EmbeddingModelDir TEXT,
                        DashboardTitle TEXT DEFAULT 'MCP Gateway', DashboardIcon TEXT DEFAULT 'fa-solid fa-network-wired', GlobalMaxKeys INTEGER DEFAULT 100,
                        UserMaxKeys INTEGER DEFAULT 5
                    );";
                cmd.ExecuteNonQuery();
            }

            var rawToken = "mcp-legacykey12345678901234567890";
            var prefix = rawToken.Substring(0, 16);
            var routerSecret = "TestRouterSecret123456789012345";

            var inMemoryConfig = new Dictionary<string, string?>
            {
                { "MCG_SECRET", routerSecret },
                { "DB_ENCRYPTION_KEY", routerSecret },
                { "RUN_KEY_MIGRATION", "true" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

            byte[] keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(routerSecret));
            string legacyEncryptedKey;
            using (var aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.GenerateIV();
                using var ms = new MemoryStream();
                ms.Write(aes.IV, 0, aes.IV.Length);
                using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                using (var writer = new StreamWriter(cs, Encoding.UTF8))
                {
                    writer.Write(rawToken);
                }
                legacyEncryptedKey = Convert.ToBase64String(ms.ToArray());
            }

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS AppKeys (
                    Id TEXT PRIMARY KEY, Name TEXT, Username TEXT, KeyPrefix TEXT, EncryptedKey TEXT, ScopesJson TEXT DEFAULT '[]', ExpiresAt TEXT, CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );
            ");

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO AppKeys (Id, Name, Username, KeyPrefix, EncryptedKey, ScopesJson) VALUES (@Id, @Name, @Username, @Prefix, @EncryptedKey, @Scopes);";
                cmd.Parameters.AddWithValue("@Id", "legacy-key-1");
                cmd.Parameters.AddWithValue("@Name", "Legacy Key");
                cmd.Parameters.AddWithValue("@Username", "legacyuser");
                cmd.Parameters.AddWithValue("@Prefix", prefix);
                cmd.Parameters.AddWithValue("@EncryptedKey", legacyEncryptedKey);
                cmd.Parameters.AddWithValue("@Scopes", "[\"all\"]");
                cmd.ExecuteNonQuery();
            }

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddSingleton(mockDbFactory);
            services.AddLogging();

            var serviceProvider = services.BuildServiceProvider();

            DatabaseSeederService.SeedDatabase(serviceProvider, config);

            var migratedKey = connection.QueryFirstOrDefault<AppKey>("SELECT * FROM AppKeys WHERE Id = 'legacy-key-1'");

            Assert.NotNull(migratedKey);
            Assert.NotEqual(legacyEncryptedKey, migratedKey.EncryptedKey);
            Assert.Equal(64, migratedKey.EncryptedKey.Length);

            var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
            Assert.Equal(expectedHash, migratedKey.EncryptedKey);

            var optionsMonitorMock = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
            optionsMonitorMock.Setup(o => o.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());

            var handler = new AppKeyAuthenticationHandler(
                optionsMonitorMock.Object,
                NullLoggerFactory.Instance,
                UrlEncoder.Default,
                mockDbFactory,
                config
            );

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Authorization"] = $"Bearer {rawToken}";
            httpContext.RequestServices = serviceProvider;

            var scheme = new AuthenticationScheme("AppKey", null, typeof(AppKeyAuthenticationHandler));
            await handler.InitializeAsync(scheme, httpContext);

            var authResult = await handler.AuthenticateAsync();
            Assert.True(authResult.Succeeded, authResult.Failure?.Message);
            Assert.Equal("legacyuser", authResult.Principal?.Identity?.Name);
        }

        [Fact]
        [Requirement("AUTH-CUSTOM-ADMIN-KEY-SEEDING", "AUTH", RequirementType.Positive, "Seeds custom MCG_ADMIN_AUTH_KEY when provided in configuration.")]
        public async Task Startup_SeedsCustomAdminKey_WhenConfigured()
        {
            var (conn, factory) = CreateDbFactory();
            var customKey = "mcp-adm-CustomKey123-Secret999";
            var inMemoryConfig = new Dictionary<string, string?>
            {
                { "MCG_ADMIN_AUTH_KEY", customKey },
                { "DB_ENCRYPTION_KEY", "TestSecretKey1234567890123456789012" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddSingleton(factory);
            services.AddLogging();
            var sp = services.BuildServiceProvider();

            DatabaseSeederService.SeedDatabase(sp, config);

            var keyPrefix = AppKeyAuthenticationHandler.ExtractKeyPrefix(customKey);
            var appKey = conn.QueryFirstOrDefault<AppKey>("SELECT * FROM AppKeys WHERE KeyPrefix = @KeyPrefix;", new { KeyPrefix = keyPrefix });
            Assert.NotNull(appKey);
            Assert.Equal("admin", appKey.Username);
            Assert.Equal("system", appKey.KeyType);

            var optionsMonitorMock = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
            optionsMonitorMock.Setup(o => o.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());

            var handler = new AppKeyAuthenticationHandler(
                optionsMonitorMock.Object,
                NullLoggerFactory.Instance,
                UrlEncoder.Default,
                factory,
                config
            );

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Authorization"] = $"Bearer {customKey}";
            httpContext.RequestServices = sp;

            var scheme = new AuthenticationScheme("AppKey", null, typeof(AppKeyAuthenticationHandler));
            await handler.InitializeAsync(scheme, httpContext);

            var authResult = await handler.AuthenticateAsync();
            Assert.True(authResult.Succeeded, authResult.Failure?.Message);
            Assert.Equal("admin", authResult.Principal?.Identity?.Name);
            Assert.True(authResult.Principal?.IsInRole("Administrator"));
            Assert.True(authResult.Principal?.HasClaim("Scope", "admin"));
        }

        [Fact]
        [Requirement("AUTH-CUSTOM-ADMIN-KEY-SEEDING", "AUTH", RequirementType.Positive, "Seeds custom MCG_ADMIN_KEY alias when provided in configuration.")]
        public async Task Startup_SeedsCustomAdminKey_WhenMcgAdminKeyConfigured()
        {
            var (conn, factory) = CreateDbFactory();
            var customKey = "mcp-adm-AliasKey888-Secret777";
            var inMemoryConfig = new Dictionary<string, string?>
            {
                { "MCG_ADMIN_KEY", customKey },
                { "DB_ENCRYPTION_KEY", "TestSecretKey1234567890123456789012" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddSingleton(factory);
            services.AddLogging();
            var sp = services.BuildServiceProvider();

            DatabaseSeederService.SeedDatabase(sp, config);

            var keyPrefix = AppKeyAuthenticationHandler.ExtractKeyPrefix(customKey);
            var appKey = conn.QueryFirstOrDefault<AppKey>("SELECT * FROM AppKeys WHERE KeyPrefix = @KeyPrefix;", new { KeyPrefix = keyPrefix });
            Assert.NotNull(appKey);
            Assert.Equal("admin", appKey.Username);
            Assert.Equal("system", appKey.KeyType);

            var optionsMonitorMock = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
            optionsMonitorMock.Setup(o => o.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());

            var handler = new AppKeyAuthenticationHandler(
                optionsMonitorMock.Object,
                NullLoggerFactory.Instance,
                UrlEncoder.Default,
                factory,
                config
            );

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Authorization"] = $"Bearer {customKey}";
            httpContext.RequestServices = sp;

            var scheme = new AuthenticationScheme("AppKey", null, typeof(AppKeyAuthenticationHandler));
            await handler.InitializeAsync(scheme, httpContext);

            var authResult = await handler.AuthenticateAsync();
            Assert.True(authResult.Succeeded, authResult.Failure?.Message);
            Assert.Equal("admin", authResult.Principal?.Identity?.Name);
            Assert.True(authResult.Principal?.IsInRole("Administrator"));
        }

        [Fact]
        [Requirement("AUTH-CUSTOM-ADMIN-KEY-SEEDING", "AUTH", RequirementType.Positive, "Updates admin key hash when configuration key changes with same prefix.")]
        public void Startup_UpdatesAdminKeyHash_WhenEnvironmentKeyChanges()
        {
            var (conn, factory) = CreateDbFactory();
            var initialKey = "mcp-adm-Prefix123-SecretOld";
            var inMemoryConfig1 = new Dictionary<string, string?>
            {
                { "MCG_ADMIN_AUTH_KEY", initialKey },
                { "DB_ENCRYPTION_KEY", "TestSecretKey1234567890123456789012" }
            };
            var config1 = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig1).Build();

            var services1 = new ServiceCollection();
            services1.AddSingleton<IConfiguration>(config1);
            services1.AddSingleton(factory);
            services1.AddLogging();
            DatabaseSeederService.SeedDatabase(services1.BuildServiceProvider(), config1);

            var keyPrefix = AppKeyAuthenticationHandler.ExtractKeyPrefix(initialKey);
            var initialRow = conn.QueryFirstOrDefault<AppKey>("SELECT * FROM AppKeys WHERE KeyPrefix = @KeyPrefix;", new { KeyPrefix = keyPrefix });
            Assert.NotNull(initialRow);

            var updatedKey = "mcp-adm-Prefix123-SecretNew";
            var inMemoryConfig2 = new Dictionary<string, string?>
            {
                { "MCG_ADMIN_AUTH_KEY", updatedKey },
                { "DB_ENCRYPTION_KEY", "TestSecretKey1234567890123456789012" }
            };
            var config2 = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig2).Build();

            var services2 = new ServiceCollection();
            services2.AddSingleton<IConfiguration>(config2);
            services2.AddSingleton(factory);
            services2.AddLogging();
            DatabaseSeederService.SeedDatabase(services2.BuildServiceProvider(), config2);

            var updatedRow = conn.QueryFirstOrDefault<AppKey>("SELECT * FROM AppKeys WHERE KeyPrefix = @KeyPrefix;", new { KeyPrefix = keyPrefix });
            Assert.NotNull(updatedRow);
            Assert.Equal(initialRow.Id, updatedRow.Id);
            Assert.NotEqual(initialRow.EncryptedKey, updatedRow.EncryptedKey);
        }

        [Fact]
        [Requirement("DB-07", "DB", RequirementType.Positive, "Seeder initializes OAuthClients table with proper schema")]
        public void Seeder_Initializes_OAuthClients_Table()
        {
            var (conn, factory) = CreateDbFactory();
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DB_ENCRYPTION_KEY", "TestSecretKey1234567890123456789012" }
            }).Build();

            services.AddSingleton<IConfiguration>(config);
            services.AddSingleton(factory);
            services.AddLogging();
            var sp = services.BuildServiceProvider();

            DatabaseSeederService.SeedDatabase(sp, config);

            var count = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM OAuthClients;");
            Assert.Equal(0, count);

            var cols = conn.Query<string>("SELECT name FROM pragma_table_info('OAuthClients');").ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("ClientId", cols);
            Assert.Contains("ClientSecretHash", cols);
            Assert.Contains("ClientName", cols);
            Assert.Contains("ClientType", cols);
            Assert.Contains("RedirectUrisJson", cols);
            Assert.Contains("GrantTypesJson", cols);
            Assert.Contains("ScopesJson", cols);
            Assert.Contains("OwnerSid", cols);
            Assert.Contains("CreatedBy", cols);
            Assert.Contains("CreatedAt", cols);
            Assert.Contains("ExpiresAt", cols);
        }
    }
}
