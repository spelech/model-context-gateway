using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace ModelContextGateway.Tests
{
    public class DbConnectionFactoryTests
    {
        [Fact]
        [Requirement("DB-FACTORY-SQLITE-DEFAULT", "DB", RequirementType.Positive, "DbConnectionFactory initializes SQLite database connection with SQLCipher encryption.")]
        public void Factory_Creates_Sqlite_Connection_By_Default()
        {
            var inMemoryConfig = new Dictionary<string, string?>
            {
                { "DB_PROVIDER", "sqlite" },
                { "DB_ENCRYPTION_KEY", "TestSecretKey1234567890123456789012" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

            var factory = new DbConnectionFactory(config);
            Assert.Equal("sqlite", factory.ProviderName);

            using var conn = factory.CreateConnection();
            Assert.IsType<SqliteConnection>(conn);
        }

        [Fact]
        [Requirement("DB-FACTORY-MYSQL-CONFIGURED", "DB", RequirementType.Positive, "DbConnectionFactory initializes MySQL database connection using MySqlConnector.")]
        public void Factory_Creates_MySql_Connection_When_Configured()
        {
            var inMemoryConfig = new Dictionary<string, string?>
            {
                { "DB_PROVIDER", "mysql" },
                { "ConnectionStrings:DefaultConnection", "Server=localhost;Database=McpDb;Uid=root;Pwd=secret;" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

            var factory = new DbConnectionFactory(config);
            Assert.Equal("mysql", factory.ProviderName);

            using var conn = factory.CreateConnection();
            Assert.IsType<MySqlConnection>(conn);
        }

        [Fact]
        [Requirement("DB-FACTORY-MSSQL-CONFIGURED", "DB", RequirementType.Positive, "DbConnectionFactory initializes MS SQL Server database connection using Microsoft.Data.SqlClient.")]
        public void Factory_Creates_MsSql_Connection_When_Configured()
        {
            var inMemoryConfig = new Dictionary<string, string?>
            {
                { "DB_PROVIDER", "mssql" },
                { "ConnectionStrings:DefaultConnection", "Server=localhost;Database=McpDb;User Id=sa;Password=Secret123!;" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

            var factory = new DbConnectionFactory(config);
            Assert.Equal("mssql", factory.ProviderName);

            using var conn = factory.CreateConnection();
            Assert.IsType<SqlConnection>(conn);
        }
    }
}
