using System.Data;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ModelContextGateway.Tests
{
    public class UserQuotaAndAppKeyRepositoryTests : IDisposable
    {
        private class NonDisposingConnection : IDbConnection
        {
            private readonly IDbConnection _inner;
            public NonDisposingConnection(IDbConnection inner) => _inner = inner;
            [System.Diagnostics.CodeAnalysis.AllowNull]
            public string ConnectionString { get => _inner.ConnectionString ?? string.Empty; set => _inner.ConnectionString = value ?? string.Empty; }
            public int ConnectionTimeout => _inner.ConnectionTimeout;
            public string Database => _inner.Database;
            public ConnectionState State => _inner.State;
            public IDbTransaction BeginTransaction() => _inner.BeginTransaction();
            public IDbTransaction BeginTransaction(IsolationLevel il) => _inner.BeginTransaction(il);
            public void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
            public void Close() { }
            public IDbCommand CreateCommand() => _inner.CreateCommand();
            public void Dispose() { }
            public void Open()
            {
                if (_inner.State != ConnectionState.Open)
                {
                    _inner.Open();
                }
            }
        }

        private readonly SqliteConnection _rawConnection;
        private readonly IDbConnectionFactory _dbFactory;
        private readonly DatabaseRepository _repo;

        public UserQuotaAndAppKeyRepositoryTests()
        {
            _rawConnection = new SqliteConnection($"DataSource=file:mem_repo_{Guid.NewGuid():N}?mode=memory&cache=shared");
            _rawConnection.Open();

            _rawConnection.Execute(@"
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
                CREATE TABLE IF NOT EXISTS Settings (
                    Id TEXT PRIMARY KEY,
                    DashboardTitle TEXT DEFAULT 'MCP Gateway',
                    DashboardIcon TEXT DEFAULT 'fa-solid fa-network-wired',
                    GlobalMaxKeys INTEGER DEFAULT 100,
                    UserMaxKeys INTEGER DEFAULT 5
                );");

            var mockFactory = new Mock<IDbConnectionFactory>();
            mockFactory.Setup(f => f.CreateConnection()).Returns(() => new NonDisposingConnection(_rawConnection));
            mockFactory.Setup(f => f.ProviderName).Returns("sqlite");
            _dbFactory = mockFactory.Object;

            _repo = new DatabaseRepository(_dbFactory);
        }

        public void Dispose()
        {
            _rawConnection.Dispose();
        }

        [Fact]
        [Requirement("DB-QUOTA-REPO-SET-GET", "IUserQuotaRepository persists user quota overrides and retrieves them correctly", Type = RequirementType.Positive, Category = "DB")]
        public async Task UserQuotaRepository_SetAndGet_ReturnsPersistedQuota()
        {
            await _repo.SetUserQuotaAsync("alice", 12);

            var quota = await _repo.GetUserQuotaAsync("alice");

            Assert.NotNull(quota);
            Assert.Equal("alice", quota.Username);
            Assert.Equal(12, quota.MaxKeys);
        }

        [Fact]
        [Requirement("DB-QUOTA-REPO-GET-ALL", "IUserQuotaRepository GetAllUserQuotasAsync retrieves all quotas ordered by username", Type = RequirementType.Positive, Category = "DB")]
        public async Task UserQuotaRepository_GetAll_ReturnsAllUserQuotas()
        {
            await _repo.SetUserQuotaAsync("charlie", 8);
            await _repo.SetUserQuotaAsync("bob", 15);
            await _repo.SetUserQuotaAsync("alice", 3);

            var quotas = (await _repo.GetAllUserQuotasAsync()).ToList();

            Assert.Equal(3, quotas.Count);
            Assert.Equal("alice", quotas[0].Username);
            Assert.Equal(3, quotas[0].MaxKeys);
            Assert.Equal("bob", quotas[1].Username);
            Assert.Equal(15, quotas[1].MaxKeys);
            Assert.Equal("charlie", quotas[2].Username);
            Assert.Equal(8, quotas[2].MaxKeys);
        }

        [Fact]
        [Requirement("DB-QUOTA-REPO-UPDATE-CONFLICT", "IUserQuotaRepository SetUserQuotaAsync updates existing quota on conflict", Type = RequirementType.Positive, Category = "DB")]
        public async Task UserQuotaRepository_Update_UpdatesExistingQuota()
        {
            await _repo.SetUserQuotaAsync("david", 5);
            var initial = await _repo.GetUserQuotaAsync("david");
            Assert.NotNull(initial);
            Assert.Equal(5, initial.MaxKeys);

            await _repo.SetUserQuotaAsync("david", 25);
            var updated = await _repo.GetUserQuotaAsync("david");
            Assert.NotNull(updated);
            Assert.Equal(25, updated.MaxKeys);
        }

        [Fact]
        [Requirement("DB-QUOTA-REPO-DELETE", "IUserQuotaRepository DeleteUserQuotaAsync removes user quota record", Type = RequirementType.Positive, Category = "DB")]
        public async Task UserQuotaRepository_Delete_RemovesQuota()
        {
            await _repo.SetUserQuotaAsync("eve", 10);
            var exists = await _repo.GetUserQuotaAsync("eve");
            Assert.NotNull(exists);

            await _repo.DeleteUserQuotaAsync("eve");

            var afterDelete = await _repo.GetUserQuotaAsync("eve");
            Assert.Null(afterDelete);
        }

        [Fact]
        [Requirement("AUTH-APPKEY-KEYTYPE-PERSISTENCE-FILTER", "IAppKeyRepository persists KeyType and filters keys by personal vs system", Type = RequirementType.Positive, Category = "AUTH")]
        public async Task AppKeyRepository_SaveAndGet_PersistsKeyTypeAndFilters()
        {
            var personalKey = new AppKey
            {
                Id = "key-p1",
                Name = "Alice Personal Key",
                Username = "alice",
                OwnerSid = "S-1-5-21-1",
                KeyType = "personal",
                KeyPrefix = "mcp-p1",
                EncryptedKey = "enc-p1",
                ScopesJson = "[\"read\"]"
            };

            var systemKey = new AppKey
            {
                Id = "key-s1",
                Name = "System Worker Key",
                Username = "admin",
                OwnerSid = "S-1-5-21-0",
                KeyType = "system",
                KeyPrefix = "mcp-s1",
                EncryptedKey = "enc-s1",
                ScopesJson = "[\"all\"]"
            };

            await _repo.SaveAppKeyAsync(personalKey);
            await _repo.SaveAppKeyAsync(systemKey);

            // 1. Fetch alice personal keys as non-admin
            var alicePersonal = (await _repo.GetAppKeysAsync(currentUser: "alice", keyType: "personal")).ToList();
            Assert.Single(alicePersonal);
            Assert.Equal("key-p1", alicePersonal[0].Id);
            Assert.Equal("personal", alicePersonal[0].KeyType);

            // 2. Fetch admin system keys as admin
            var adminSystem = (await _repo.GetAppKeysAsync(isAdmin: true, keyType: "system")).ToList();
            Assert.Single(adminSystem);
            Assert.Equal("key-s1", adminSystem[0].Id);
            Assert.Equal("system", adminSystem[0].KeyType);

            // 3. Fetch all keys as admin without keyType filter
            var allAdminKeys = (await _repo.GetAppKeysAsync(isAdmin: true, keyType: null)).ToList();
            Assert.Equal(2, allAdminKeys.Count);

            // 4. Fetch personal keys as admin
            var allPersonalKeys = (await _repo.GetAppKeysAsync(isAdmin: true, keyType: "personal")).ToList();
            Assert.Single(allPersonalKeys);
            Assert.Equal("key-p1", allPersonalKeys[0].Id);
        }

        [Fact]
        [Requirement("DB-QUOTA-REPO-DI-REGISTRATION", "IUserQuotaRepository is registered in dependency injection and resolvable", Type = RequirementType.Positive, Category = "DB")]
        public void DependencyInjection_RegistersIUserQuotaRepository()
        {
            var builder = WebApplication.CreateBuilder(new string[] { });
            builder.Environment.EnvironmentName = "Development";
            builder.Configuration["DB_PROVIDER"] = "sqlite";
            builder.Configuration["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:;";
            builder.AddModelContextGatewayServices();

            using var app = builder.Build();
            var quotaRepo = app.Services.GetService<IUserQuotaRepository>();

            Assert.NotNull(quotaRepo);
            Assert.IsAssignableFrom<DatabaseRepository>(quotaRepo);
        }
    }
}
