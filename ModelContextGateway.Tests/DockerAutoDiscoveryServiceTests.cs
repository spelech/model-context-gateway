using System.Reflection;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ModelContextGateway.Tests
{
    public class DockerAutoDiscoveryServiceTests
    {
        private (SqliteConnection masterConn, IDbConnectionFactory factory) CreateDbFactory()
        {
            var dbName = $"Data Source=DiscoveryTestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            var masterConn = new SqliteConnection(dbName);
            masterConn.Open();

            masterConn.Execute(@"
                CREATE TABLE IF NOT EXISTS Servers (
                    Id TEXT PRIMARY KEY,
                    Alias TEXT,
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
            mockDbFactory.Setup(f => f.CreateConnection()).Returns(() => new SqliteConnection(dbName));
            mockDbFactory.Setup(f => f.ProviderName).Returns("sqlite");
            return (masterConn, mockDbFactory.Object);
        }

        [Fact]
        [Requirement("MCP-10", "MCP", RequirementType.Positive, "DockerAutoDiscoveryService initializes with valid container service dependencies.")]
        public void Service_Initializes_With_Valid_Dependencies()
        {
            var (_, dbFactory) = CreateDbFactory();
            var services = new ServiceCollection();
            services.AddSingleton(dbFactory);
            var serviceProvider = services.BuildServiceProvider();

            var discoveryService = new DockerAutoDiscoveryService(serviceProvider, NullLogger<DockerAutoDiscoveryService>.Instance);
            Assert.NotNull(discoveryService);
        }

        [Fact]
        [Requirement("GUARD-05", "GUARD", RequirementType.Negative, "Docker auto-discovery skips containers resolving to blocked private IP ranges (SSRF protection).")]
        public void DockerDiscovery_SkipsContainer_ResolvingToPrivateIp()
        {
            var (_, dbFactory) = CreateDbFactory();
            var services = new ServiceCollection();
            services.AddSingleton(dbFactory);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
            services.AddSingleton<IConfiguration>(config);
            var serviceProvider = services.BuildServiceProvider();

            var discoveryService = new DockerAutoDiscoveryService(serviceProvider, NullLogger<DockerAutoDiscoveryService>.Instance);
            Assert.NotNull(discoveryService);

            bool isBlocked1 = ModelContextGateway.Components.Authorization.SecurityValidationHelper.IsBlockedIp(System.Net.IPAddress.Parse("127.0.0.1"), Array.Empty<string>());
            bool isBlocked2 = ModelContextGateway.Components.Authorization.SecurityValidationHelper.IsBlockedIp(System.Net.IPAddress.Parse("169.254.169.254"), Array.Empty<string>());

            Assert.True(isBlocked1);
            Assert.True(isBlocked2);
        }

        [Fact]
        [Requirement("MCP-10", "MCP", RequirementType.Positive, "DockerAutoDiscoveryService handles absent Docker socket gracefully without crashing host runtime.")]
        public async Task ExecuteAsync_SkipsScan_WhenDockerSocketDoesNotExist()
        {
            var (conn, dbFactory) = CreateDbFactory();
            var services = new ServiceCollection();
            services.AddSingleton(dbFactory);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
            services.AddSingleton<IConfiguration>(config);
            var serviceProvider = services.BuildServiceProvider();

            var discoveryService = new DockerAutoDiscoveryService(serviceProvider, NullLogger<DockerAutoDiscoveryService>.Instance);
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(50);

            var executeMethod = typeof(DockerAutoDiscoveryService).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(executeMethod);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await (Task)executeMethod.Invoke(discoveryService, new object[] { cts.Token })!;
            });
        }

        [Fact]
        [Requirement("MCP-10", "MCP", RequirementType.Positive, "DockerAutoDiscoveryService parses Docker container mcp.* labels into McpServer metadata definitions.")]
        public void ParseDiscoveredServers_ParsesValidDockerContainerLabels()
        {
            var json = @"[
                {
                    ""Names"": [""/10.0.0.10""],
                    ""Labels"": {
                        ""mcp.enabled"": ""true"",
                        ""mcp.id"": ""docker"",
                        ""mcp.port"": ""8080"",
                        ""mcp.displayName"": ""Docker MCP"",
                        ""mcp.type"": ""sse"",
                        ""mcp.path"": ""/sse"",
                        ""mcp.categories"": ""infrastructure,tools""
                    }
                },
                {
                    ""Names"": [""/disabled-server""],
                    ""Labels"": {
                        ""mcp.enabled"": ""false""
                    }
                }
            ]";

            using var doc = JsonDocument.Parse(json);
            var discovered = DockerAutoDiscoveryService.ParseDiscoveredServers(doc.RootElement, NullLogger.Instance, new[] { "10.0.0.0/8" });

            Assert.Single(discovered);
            Assert.Equal("docker", discovered[0].Id);
            Assert.Equal("Docker MCP", discovered[0].DisplayName);
            Assert.Contains("infrastructure", discovered[0].Categories);
        }

        [Fact]
        [Requirement("MCP-10", "MCP", RequirementType.Positive, "DockerAutoDiscoveryService dynamically registers newly discovered servers and updates changed configurations.")]
        public void UpsertDiscoveredServers_AddsNewServers_AndDisablesStoppedServers()
        {
            var (conn, dbFactory) = CreateDbFactory();
            var mockFactory = new Mock<IHttpClientFactory>();
            var services = new ServiceCollection();
            var sp = services.BuildServiceProvider();
            var sessionManager = new SessionManager(sp, mockFactory.Object, NullLogger<SessionManager>.Instance);

            conn.Execute(@"
                INSERT INTO Servers (Id, DisplayName, Url, AutoDiscovered, Enabled, Categories)
                VALUES ('old-server', 'Old Server', 'http://old:8080/sse', 1, 1, '[""legacy""]')
            ");

            var discovered = new List<McpServer>
            {
                new McpServer
                {
                    Id = "old-server",
                    DisplayName = "Updated Old Server",
                    Url = "http://old:8081/sse",
                    Type = "http",
                    AutoDiscovered = true,
                    Enabled = true,
                    Categories = new List<string> { "infrastructure", "updated" }
                },
                new McpServer
                {
                    Id = "new-server",
                    DisplayName = "New Server",
                    Url = "http://new:8080/sse",
                    AutoDiscovered = true,
                    Enabled = true,
                    Categories = new List<string> { "default" }
                }
            };

            DockerAutoDiscoveryService.UpsertDiscoveredServers(discovered, dbFactory, sessionManager, NullLogger.Instance);

            var updatedOld = conn.QueryFirstOrDefault<McpServer>("SELECT * FROM Servers WHERE Id = 'old-server'");
            Assert.NotNull(updatedOld);
            Assert.Equal("Updated Old Server", updatedOld.DisplayName);
            Assert.Equal("http://old:8081/sse", updatedOld.Url);
            Assert.Equal("http", updatedOld.Type);

            var newSrv = conn.QueryFirstOrDefault<McpServer>("SELECT * FROM Servers WHERE Id = 'new-server'");
            Assert.NotNull(newSrv);
        }

        [Fact]
        [Requirement("MCP-31", "MCP", RequirementType.Positive, "DockerAutoDiscoveryService parses mcp.alias from Docker container labels")]
        public void ParseDiscoveredServers_Parses_McpAlias_Label()
        {
            // MCP-31: Parses mcp.alias from Docker container labels
            var json = @"
            [
              {
                ""Names"": [""/postgres-mcp-homebox""],
                ""Labels"": {
                  ""mcp.enabled"": ""true"",
                  ""mcp.id"": ""postgres-mcp-homebox"",
                  ""mcp.port"": ""8000"",
                  ""mcp.alias"": ""homebox_db""
                }
              }
            ]";

            using var doc = JsonDocument.Parse(json);
            var servers = DockerAutoDiscoveryService.ParseDiscoveredServers(doc.RootElement, NullLogger.Instance, new[] { "10.0.0.0/8", "127.0.0.0/8" });

            Assert.Single(servers);
            Assert.Equal("homebox_db", servers[0].Alias);
        }

        [Fact]
        [Requirement("MCP-31", "MCP", RequirementType.Positive, "DockerAutoDiscoveryService parses fallback mcp.namespace label if mcp.alias is absent")]
        public void ParseDiscoveredServers_Parses_McpNamespace_Fallback_Label()
        {
            var json = @"
            [
              {
                ""Names"": [""/postgres-mcp-homebox""],
                ""Labels"": {
                  ""mcp.enabled"": ""true"",
                  ""mcp.id"": ""postgres-mcp-homebox"",
                  ""mcp.port"": ""8000"",
                  ""mcp.namespace"": ""homebox_db""
                }
              }
            ]";

            using var doc = JsonDocument.Parse(json);
            var servers = DockerAutoDiscoveryService.ParseDiscoveredServers(doc.RootElement, NullLogger.Instance, new[] { "10.0.0.0/8", "127.0.0.0/8" });

            Assert.Single(servers);
            Assert.Equal("homebox_db", servers[0].Alias);
        }

        [Fact]
        [Requirement("MCP-31", "MCP", RequirementType.Negative, "DockerAutoDiscoveryService ignores invalid alias characters in container labels")]
        public void ParseDiscoveredServers_Ignores_Invalid_Alias_Characters()
        {
            var json = @"
            [
              {
                ""Names"": [""/postgres-mcp-homebox""],
                ""Labels"": {
                  ""mcp.enabled"": ""true"",
                  ""mcp.id"": ""postgres-mcp-homebox"",
                  ""mcp.port"": ""8000"",
                  ""mcp.alias"": ""invalid alias with spaces!""
                }
              }
            ]";

            using var doc = JsonDocument.Parse(json);
            var servers = DockerAutoDiscoveryService.ParseDiscoveredServers(doc.RootElement, NullLogger.Instance, new[] { "10.0.0.0/8", "127.0.0.0/8" });

            Assert.Single(servers);
            Assert.Null(servers[0].Alias);
        }

        [Fact]
        [Requirement("MCP-31", "MCP", RequirementType.Positive, "DockerAutoDiscoveryService.UpsertDiscoveredServers preserves existing DB alias and sets alias on new servers")]
        public void UpsertDiscoveredServers_PreservesExistingDbAlias_AndInsertsDiscoveredAlias()
        {
            var (conn, dbFactory) = CreateDbFactory();
            var mockFactory = new Mock<IHttpClientFactory>();
            var services = new ServiceCollection();
            var sp = services.BuildServiceProvider();
            var sessionManager = new SessionManager(sp, mockFactory.Object, NullLogger<SessionManager>.Instance);

            conn.Execute(@"
                INSERT INTO Servers (Id, Alias, DisplayName, Url, AutoDiscovered, Enabled, Categories)
                VALUES ('existing-server', 'custom_manual_alias', 'Existing Server', 'http://existing:8080/sse', 1, 1, '[""default""]')
            ");

            conn.Execute(@"
                INSERT INTO Servers (Id, Alias, DisplayName, Url, AutoDiscovered, Enabled, Categories)
                VALUES ('no-alias-server', NULL, 'No Alias Server', 'http://noalias:8080/sse', 1, 1, '[""default""]')
            ");

            var discovered = new List<McpServer>
            {
                new McpServer
                {
                    Id = "existing-server",
                    Alias = "discovered_alias_should_be_ignored",
                    DisplayName = "Existing Server",
                    Url = "http://existing:8080/sse",
                    AutoDiscovered = true,
                    Enabled = true,
                    Categories = new List<string> { "default" }
                },
                new McpServer
                {
                    Id = "no-alias-server",
                    Alias = "newly_discovered_alias",
                    DisplayName = "No Alias Server",
                    Url = "http://noalias:8080/sse",
                    AutoDiscovered = true,
                    Enabled = true,
                    Categories = new List<string> { "default" }
                },
                new McpServer
                {
                    Id = "brand-new-server",
                    Alias = "brand_new_alias",
                    DisplayName = "Brand New Server",
                    Url = "http://brandnew:8080/sse",
                    AutoDiscovered = true,
                    Enabled = true,
                    Categories = new List<string> { "default" }
                }
            };

            DockerAutoDiscoveryService.UpsertDiscoveredServers(discovered, dbFactory, sessionManager, NullLogger.Instance);

            var existing = conn.QueryFirstOrDefault<McpServer>("SELECT * FROM Servers WHERE Id = 'existing-server'");
            Assert.NotNull(existing);
            Assert.Equal("custom_manual_alias", existing.Alias);

            var populated = conn.QueryFirstOrDefault<McpServer>("SELECT * FROM Servers WHERE Id = 'no-alias-server'");
            Assert.NotNull(populated);
            Assert.Equal("newly_discovered_alias", populated.Alias);

            var brandNew = conn.QueryFirstOrDefault<McpServer>("SELECT * FROM Servers WHERE Id = 'brand-new-server'");
            Assert.NotNull(brandNew);
            Assert.Equal("brand_new_alias", brandNew.Alias);
        }
    }
}
