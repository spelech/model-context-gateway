using System.Data;
using Dapper;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace ModelContextGateway.Tests
{
    public class DatabaseInitializerTests
    {
        [Fact]
        [Requirement("DB-INITIALIZER-SCHEMA-BASELINE", "DB", RequirementType.Positive, "DatabaseInitializer creates all 12 canonical tables on a fresh SQLite database.")]
        public void InitializeDatabase_CreatesAllExpectedTables_OnFreshSqliteConnection()
        {
            using var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            DatabaseInitializer.InitializeDatabase(conn);

            var tables = conn.Query<string>("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';").ToList();

            var expectedTables = new[]
            {
                "Servers",
                "Settings",
                "UserServerCredentials",
                "AppKeys",
                "UserQuotas",
                "AccessPolicies",
                "GroupMappings",
                "AuditLogs",
                "AdminAuditLogs",
                "SecretProviders",
                "AuthProviderConfigs",
                "OAuthClients"
            };

            tables.Should().Contain(expectedTables);
        }

        [Fact]
        [Requirement("DB-INITIALIZER-IDEMPOTENCY", "DB", RequirementType.Positive, "DatabaseInitializer is idempotent and succeeds without error when executed multiple times.")]
        public void InitializeDatabase_IsIdempotent_WhenCalledMultipleTimes()
        {
            using var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            // Run twice
            DatabaseInitializer.InitializeDatabase(conn);
            var act = () => DatabaseInitializer.InitializeDatabase(conn);

            act.Should().NotThrow();
        }

        [Fact]
        [Requirement("DB-INITIALIZER-ENSURE-ALIAS", "DB", RequirementType.Positive, "EnsureAliasColumn safely adds Alias column if missing and is idempotent.")]
        public void EnsureAliasColumn_AddsColumnIfMissing_AndIsIdempotent()
        {
            using var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            DatabaseInitializer.InitializeDatabase(conn);

            var act = () => DatabaseInitializer.EnsureAliasColumn(conn);
            act.Should().NotThrow();

            // Verify column exists
            var pragmaRows = conn.Query("PRAGMA table_info(Servers);").Select(r => (string)r.name).ToList();
            pragmaRows.Should().Contain("Alias");
        }

        [Fact]
        [Requirement("DB-INITIALIZER-CRUD-CONTRACT", "DB", RequirementType.Positive, "DatabaseInitializer baseline schema supports CRUD operations across core domain tables.")]
        public void InitializeDatabase_SupportsCrud_AcrossCoreTables()
        {
            using var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            DatabaseInitializer.InitializeDatabase(conn);

            // 1. Insert and query Server
            conn.Execute(@"
                INSERT INTO Servers (Id, DisplayName, Url, Enabled, Alias)
                VALUES (@Id, @DisplayName, @Url, @Enabled, @Alias);",
                new { Id = "srv-1", DisplayName = "Server One", Url = "http://localhost:8000/sse", Enabled = 1, Alias = "srv1" });

            var srv = conn.QuerySingleOrDefault<dynamic>("SELECT Id, DisplayName, Alias FROM Servers WHERE Id = 'srv-1'");
            Assert.NotNull(srv);
            ((string)srv!.Id).Should().Be("srv-1");
            ((string)srv.DisplayName).Should().Be("Server One");
            ((string)srv.Alias).Should().Be("srv1");

            // 2. Insert and query AppKey
            conn.Execute(@"
                INSERT INTO AppKeys (Id, Name, Username, OwnerSid, KeyType, KeyPrefix, EncryptedKey, ScopesJson)
                VALUES (@Id, @Name, @Username, @OwnerSid, @KeyType, @KeyPrefix, @EncryptedKey, @ScopesJson);",
                new { Id = "key-1", Name = "Test Key", Username = "alice", OwnerSid = "S-1-5-21", KeyType = "personal", KeyPrefix = "mcg_ak_test", EncryptedKey = "enc123", ScopesJson = "[\"read\"]" });

            var key = conn.QuerySingleOrDefault<dynamic>("SELECT Id, Name, Username FROM AppKeys WHERE Id = 'key-1'");
            Assert.NotNull(key);
            ((string)key!.Id).Should().Be("key-1");
            ((string)key.Name).Should().Be("Test Key");
            ((string)key.Username).Should().Be("alice");

            // 3. Insert and query OAuthClient
            conn.Execute(@"
                INSERT INTO OAuthClients (ClientId, ClientSecretHash, ClientName, ClientType, RedirectUrisJson)
                VALUES (@ClientId, @ClientSecretHash, @ClientName, @ClientType, @RedirectUrisJson);",
                new { ClientId = "client-1", ClientSecretHash = "hash123", ClientName = "Test Client", ClientType = "confidential", RedirectUrisJson = "[\"http://localhost:3000/cb\"]" });

            var client = conn.QuerySingleOrDefault<dynamic>("SELECT ClientId, ClientName FROM OAuthClients WHERE ClientId = 'client-1'");
            Assert.NotNull(client);
            ((string)client!.ClientId).Should().Be("client-1");
            ((string)client.ClientName).Should().Be("Test Client");
        }
    }
}
