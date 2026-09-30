using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModelContextGateway.Tests
{
    public class AppKeyLastUsedPersistenceTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly IDbConnectionFactory _dbFactory;

        public AppKeyLastUsedPersistenceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"test_mcg_lastused_{Guid.NewGuid():N}.db");
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "MCG_DATABASE_PROVIDER", "sqlite" },
                { "MCG_DB_PATH", _dbPath },
                { "DB_ENCRYPTION_KEY", "TestSecretKey1234567890123456789012" }
            }).Build();

            _dbFactory = new DbConnectionFactory(config);

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddSingleton(_dbFactory);
            services.AddLogging();
            var sp = services.BuildServiceProvider();

            DatabaseSeederService.SeedDatabase(sp, config);
        }

        public void Dispose()
        {
            if (File.Exists(_dbPath))
            {
                try { File.Delete(_dbPath); } catch { }
            }
        }

        [Fact]
        [Requirement("AUTH-118", "AUTH", RequirementType.Positive, "AppKey table contains LastUsedAt column and repository updates and queries it accurately.")]
        public async Task AppKeyRepository_UpdatesAndReads_LastUsedAt()
        {
            var repo = new DatabaseRepository(_dbFactory);
            var now = DateTime.UtcNow;

            using (var conn = _dbFactory.CreateConnection())
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO AppKeys (Id, Name, Username, OwnerSid, KeyType, KeyPrefix, EncryptedKey, ScopesJson, CreatedAt)
                    VALUES ('key1', 'Test Key', 'alice', 'sid1', 'personal', 'mcp-usr-abc', 'hash123', '[""all""]', @CreatedAt)",
                    new { CreatedAt = now });
            }

            var before = await repo.GetAppKeyByIdAsync("key1");
            Assert.NotNull(before);
            Assert.Null(before.LastUsedAt);

            var lastUsed = DateTime.UtcNow.AddMinutes(5);
            await repo.UpdateLastUsedAsync("key1", lastUsed);

            var after = await repo.GetAppKeyByIdAsync("key1");
            Assert.NotNull(after);
            Assert.NotNull(after.LastUsedAt);
            Assert.Equal(lastUsed.ToString("yyyy-MM-dd HH:mm:ss"), after.LastUsedAt.Value.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        [Fact]
        [Requirement("AUTH-119", "AUTH", RequirementType.Positive, "OAuthClients table contains LastUsedAt column and repository updates and queries it accurately.")]
        public async Task OAuthClientRepository_UpdatesAndReads_LastUsedAt()
        {
            var repo = new DatabaseRepository(_dbFactory);
            var client = new OAuthClient
            {
                ClientId = "client1",
                ClientName = "Test Client",
                ClientType = "confidential",
                ClientSecretHash = "secrethash",
                CreatedAt = DateTime.UtcNow
            };

            await repo.SaveOAuthClientAsync(client);

            var before = await repo.GetOAuthClientByIdAsync("client1");
            Assert.NotNull(before);
            Assert.Null(before.LastUsedAt);

            var lastUsed = DateTime.UtcNow.AddMinutes(10);
            await repo.UpdateLastUsedAsync("client1", lastUsed);

            var after = await repo.GetOAuthClientByIdAsync("client1");
            Assert.NotNull(after);
            Assert.NotNull(after.LastUsedAt);
            Assert.Equal(lastUsed.ToString("yyyy-MM-dd HH:mm:ss"), after.LastUsedAt.Value.ToString("yyyy-MM-dd HH:mm:ss"));
        }
    }
}
