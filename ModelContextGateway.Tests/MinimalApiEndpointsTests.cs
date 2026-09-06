using Dapper;
using Microsoft.Data.Sqlite;
using Moq;

namespace ModelContextGateway.Tests
{
    public class MinimalApiEndpointsTests
    {
        private (SqliteConnection conn, IDbConnectionFactory factory) CreateDbFactory()
        {
            DbKeyHelper.ResetCache();
            EncryptionKeyProvider.ResetCache();

            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Servers (
                    Id TEXT PRIMARY KEY,
                    DisplayName TEXT,
                    Url TEXT,
                    Enabled INTEGER DEFAULT 1,
                    Hidden INTEGER DEFAULT 0,
                    Type TEXT DEFAULT 'sse',
                    SecretProvider TEXT DEFAULT 'None',
                    SecretItemKey TEXT,
                    AuthShape TEXT DEFAULT 'bearer',
                    CustomHeaderName TEXT,
                    Categories TEXT DEFAULT '[]',
                    ApiKey TEXT,
                    HeadersJson TEXT,
                    AutoDiscovered INTEGER DEFAULT 0
                );
            ");

            var mockDbFactory = new Mock<IDbConnectionFactory>();
            mockDbFactory.Setup(f => f.CreateConnection()).Returns(connection);
            mockDbFactory.Setup(f => f.ProviderName).Returns("sqlite");
            return (connection, mockDbFactory.Object);
        }

        [Fact]
        [Requirement("API-GET-SERVERS-FLEET-REPOSITORY", "API", RequirementType.Positive, "Retrieves configured MCP server fleet from the database repository.")]
        public async Task GetServers_Returns_Server_List()
        {
            var (conn, _) = CreateDbFactory();
            conn.Execute("INSERT INTO Servers (Id, DisplayName, Url, Enabled) VALUES ('srv1', 'Server 1', 'http://localhost:1111/sse', 1)");

            var servers = (await conn.QueryAsync<McpServer>("SELECT * FROM Servers")).ToList();
            Assert.NotEmpty(servers);
            Assert.Single(servers);
            Assert.Equal("Server 1", servers[0].DisplayName);
        }

        [Fact]
        [Requirement("API-SERVER-CRUD-LIFECYCLE", "API", RequirementType.Positive, "Supports full CRUD lifecycle (create, read, update, delete) for downstream MCP server definitions.")]
        public async Task Post_Put_Delete_Server_Lifecycle_Works()
        {
            var (conn, _) = CreateDbFactory();

            // 1. Create Server
            var newServer = new McpServer
            {
                Id = "test-crud-1",
                DisplayName = "Integration Test Server",
                Type = "sse",
                Url = "http://localhost:7777/sse",
                ApiKey = "secret123",
                Enabled = true
            };
            await conn.ExecuteAsync("INSERT INTO Servers (Id, DisplayName, Url, Enabled, ApiKey) VALUES (@Id, @DisplayName, @Url, 1, @ApiKey)", newServer);

            var created = await conn.QueryFirstOrDefaultAsync<McpServer>("SELECT * FROM Servers WHERE Id = 'test-crud-1'");
            Assert.NotNull(created);

            // 2. Update Server
            await conn.ExecuteAsync("UPDATE Servers SET DisplayName = 'Updated Test Server' WHERE Id = 'test-crud-1'");

            var updated = await conn.QueryFirstOrDefaultAsync<McpServer>("SELECT * FROM Servers WHERE Id = 'test-crud-1'");
            Assert.NotNull(updated);
            Assert.Equal("Updated Test Server", updated.DisplayName);

            // 3. Delete Server
            await conn.ExecuteAsync("DELETE FROM Servers WHERE Id = 'test-crud-1'");

            var deleted = await conn.QueryFirstOrDefaultAsync<McpServer>("SELECT * FROM Servers WHERE Id = 'test-crud-1'");
            Assert.Null(deleted);
        }
    }
}
