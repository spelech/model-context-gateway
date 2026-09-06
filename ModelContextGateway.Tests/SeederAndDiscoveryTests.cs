using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ModelContextGateway.Tests
{
    public class DummyEmbeddingService : IEmbeddingService
    {
        public bool IsConfigured => false;
        public Task<float[]> GetEmbeddingAsync(string text) => Task.FromResult(new float[384]);
        public Task<List<float[]>> GetEmbeddingsAsync(List<string> texts) => Task.FromResult(new List<float[]> { new float[384] });
        public double CosineSimilarity(float[] vectorA, float[] vectorB) => 1.0;
        public void ReloadSettings(RouterSettings settings) { }
    }

    public class SeederAndDiscoveryTests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public SeederAndDiscoveryTests()
        {
            _connection = new SqliteConnection("Data Source=InMemorySeederDb;Mode=Memory;Cache=Shared");
            _connection.Open();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private (IServiceProvider sp, IDbConnectionFactory factory) CreateServiceProvider()
        {
            var mockFactory = new Mock<IDbConnectionFactory>();
            mockFactory.Setup(f => f.CreateConnection()).Returns(_connection);
            mockFactory.Setup(f => f.ProviderName).Returns("sqlite");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IDbConnectionFactory>(mockFactory.Object);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DB_ENCRYPTION_KEY", "TestSecretKey1234567890123456789012" }
            }).Build();
            services.AddSingleton<IConfiguration>(config);

            return (services.BuildServiceProvider(), mockFactory.Object);
        }

        [Fact]
        [Requirement("DB-01", "DB", RequirementType.Positive, "DatabaseSeeder initializes default router tables, settings, and seed servers.")]
        public void DatabaseSeeder_SeedsDefaultData_Successfully()
        {
            using var freshConn = new SqliteConnection("Data Source=FreshSeederDb;Mode=Memory;Cache=Shared");
            freshConn.Open();

            var mockFactory = new Mock<IDbConnectionFactory>();
            mockFactory.Setup(f => f.CreateConnection()).Returns(() => new SqliteConnection("Data Source=FreshSeederDb;Mode=Memory;Cache=Shared"));
            mockFactory.Setup(f => f.ProviderName).Returns("sqlite");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IDbConnectionFactory>(mockFactory.Object);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DB_ENCRYPTION_KEY", "TestSecretKey1234567890123456789012" }
            }).Build();
            services.AddSingleton<IConfiguration>(config);
            var sp = services.BuildServiceProvider();

            DatabaseSeederService.SeedDatabase(sp, config);

            var settingsCount = freshConn.QuerySingle<int>("SELECT COUNT(*) FROM Settings");
            var serversCount = freshConn.QuerySingle<int>("SELECT COUNT(*) FROM Servers");
            Assert.True(settingsCount > 0);
            Assert.True(serversCount > 0);
        }

        [Fact]
        [Requirement("MCP-10", "MCP", RequirementType.Positive, "DockerAutoDiscoveryService handles missing Docker socket gracefully without throwing unhandled exceptions.")]
        public async Task DockerAutoDiscovery_ScanContainers_HandlesMissingSocketGracefully()
        {
            var (sp, _) = CreateServiceProvider();
            var logger = NullLogger<DockerAutoDiscoveryService>.Instance;

            var discovery = new DockerAutoDiscoveryService(sp, logger);
            var act = async () =>
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
                try
                {
                    await discovery.StartAsync(cts.Token);
                    await discovery.StopAsync(cts.Token);
                }
                catch (OperationCanceledException) { }
            };

            var exception = await Record.ExceptionAsync(act);
            Assert.Null(exception);
        }

        [Fact]
        [Requirement("MCP-12", "MCP", RequirementType.Positive, "SemanticSearchService gracefully handles fallback execution when embedding provider is unconfigured.")]
        public async Task SemanticSearchService_Fallback_With_DummyEmbeddings()
        {
            var toolsList = new List<object>
            {
                new { name = "docker__list_containers", description = "List docker containers" },
                new { name = "plex__get_sessions", description = "Get Plex active streams" }
            };

            var dummyEmbedding = new DummyEmbeddingService();
            var results = await SemanticSearchService.SearchToolsSemanticAsync("docker", toolsList, dummyEmbedding);
            Assert.NotNull(results);
            Assert.NotEmpty(results);
        }
    }
}
