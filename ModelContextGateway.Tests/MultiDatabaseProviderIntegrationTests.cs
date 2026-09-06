using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace ModelContextGateway.Tests
{
    public class MultiDatabaseProviderIntegrationTests
    {
        [Theory]
        [InlineData("sqlite")]
        [InlineData("mysql")]
        [InlineData("mssql")]
        [Requirement("DB-PROVIDER-INSTANTIATION-DIALECTS", "DB", RequirementType.Positive, "DbConnectionFactory instantiates valid IDbConnection instances across sqlite, mysql, and mssql dialects.")]
        public void DbConnectionFactory_Instantiates_SupportedProviders(string provider)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DB_PROVIDER", provider },
                { "ConnectionStrings:DefaultConnection", provider == "sqlite" ? "Data Source=:memory:;" : "Server=127.0.0.1;Database=test;" }
            }).Build();

            var factory = new DbConnectionFactory(config);
            Assert.Equal(provider, factory.ProviderName);

            var conn = factory.CreateConnection();
            Assert.NotNull(conn);
        }

        [Fact]
        [Requirement("GUARD-UNSUPPORTED-DB-PROVIDER", "GUARD", RequirementType.Negative, "DbConnectionFactory fails closed and throws InvalidOperationException when configured with unsupported database provider.")]
        public void DbConnectionFactory_Throws_OnUnsupportedProvider()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DB_PROVIDER", "oracle" }
            }).Build();

            var factory = new DbConnectionFactory(config);
            Assert.Throws<InvalidOperationException>(() => factory.CreateConnection());
        }

        [Fact]
        [Requirement("DB-TYPEHANDLER-JSON-LIST-SERIALIZATION", "DB", RequirementType.Positive, "JsonListTypeHandler serializes and deserializes string collections to JSON text across database providers.")]
        public async Task JsonListTypeHandler_SerializesAndDeserializes_StringLists()
        {
            var conn = new SqliteConnection("Data Source=:memory:;Mode=Memory;Cache=Shared");
            conn.Open();

            conn.Execute("CREATE TABLE TestJsonTable (Id TEXT PRIMARY KEY, Tags TEXT);");

            var originalTags = new List<string> { "infrastructure", "smart-home", "media" };
            conn.Execute("INSERT INTO TestJsonTable (Id, Tags) VALUES (@Id, @Tags);", new
            {
                Id = "item-1",
                Tags = originalTags
            });

            var result = await conn.QuerySingleAsync<TestEntity>("SELECT Id, Tags FROM TestJsonTable WHERE Id = @Id;", new { Id = "item-1" });

            Assert.NotNull(result);
            Assert.Equal("item-1", result.Id);
            Assert.Equal(3, result.Tags.Count);
            Assert.Contains("infrastructure", result.Tags);
            Assert.Contains("smart-home", result.Tags);
            Assert.Contains("media", result.Tags);
        }

        private class TestEntity
        {
            public string Id { get; set; } = string.Empty;
            public List<string> Tags { get; set; } = new();
        }
    }
}
