using System.Net;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ModelContextGateway.Tests
{
    public class BackendHealthCheckServiceTests
    {
        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_handler(request));
            }
        }

        private (SqliteConnection conn, IDbConnectionFactory factory) CreateDbFactory()
        {
            var cs = $"Data Source=HealthCheckTests_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            var connection = new SqliteConnection(cs);
            connection.Open();

            DatabaseInitializer.InitializeDatabase(connection);

            var mockDbFactory = new Mock<IDbConnectionFactory>();
            mockDbFactory.Setup(f => f.CreateConnection()).Returns(() => new SqliteConnection(cs));
            mockDbFactory.Setup(f => f.ProviderName).Returns("sqlite");
            return (connection, mockDbFactory.Object);
        }

        [Fact]
        [Requirement("HEALTH-PROBE-HTTP-PING-CONNECTED-200", "MCP", RequirementType.Positive, "BackendHealthCheckService marks server as Connected when downstream JSON-RPC ping returns 200 OK.")]
        public async Task ProbeServerAsync_Sets_Connected_When_JsonRpc_Ping_Responds_200()
        {
            var (conn, dbFactory) = CreateDbFactory();
            var server = new McpServer
            {
                Id = "test-ping-1",
                DisplayName = "Test Server Ping",
                Url = "http://localhost:9999/mcp",
                Enabled = true,
                ApiKey = "secret123",
                HealthCheckMode = "ping"
            };
            conn.Execute("INSERT INTO Servers (Id, DisplayName, Url, Enabled, ApiKey) VALUES (@Id, @DisplayName, @Url, 1, @ApiKey)", server);

            var handler = new MockHttpMessageHandler(req =>
            {
                Assert.Equal("Bearer secret123", req.Headers.Authorization?.ToString());
                Assert.Equal(HttpMethod.Post, req.Method);
                Assert.Equal("http://localhost:9999/mcp", req.RequestUri?.ToString());
                Assert.Contains("application/json", req.Headers.Accept.ToString());
                Assert.Contains("text/event-stream", req.Headers.Accept.ToString());
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
            var client = new HttpClient(handler);

            var services = new ServiceCollection();
            services.AddSingleton(dbFactory);
            var serviceProvider = services.BuildServiceProvider();

            var httpClientFactory = new MockHttpClientFactory(client);
            var sessionManager = new SessionManager(serviceProvider, httpClientFactory, NullLogger<SessionManager>.Instance);
            var logger = NullLogger<BackendHealthCheckService>.Instance;

            var healthService = new BackendHealthCheckService(serviceProvider, httpClientFactory, sessionManager, logger);

            await healthService.ProbeServerAsync(server);

            var status = sessionManager.BackendStatuses["test-ping-1"];
            Assert.True(status.Status == "Connected", $"Probe failed with: {status.Error}");
            Assert.Equal("Connected", status.Status);
            Assert.Equal(1, status.Attempts);
            Assert.Empty(status.Error);
        }

        [Fact]
        [Requirement("HEALTH-PROBE-HTTP-PING-FALLBACK-ROOT", "MCP", RequirementType.Positive, "BackendHealthCheckService falls back to base authority root probe when downstream JSON-RPC ping returns 405 Method Not Allowed.")]
        public async Task ProbeServerAsync_Falls_Back_To_Root_Probe_When_Ping_Returns_405()
        {
            var (conn, dbFactory) = CreateDbFactory();
            var server = new McpServer
            {
                Id = "test-ping-fallback",
                DisplayName = "Test Fallback Server",
                Url = "http://localhost:9999/sse",
                Enabled = true,
                ApiKey = "secret123",
                HealthCheckMode = "ping"
            };
            conn.Execute("INSERT INTO Servers (Id, DisplayName, Url, Enabled, ApiKey) VALUES (@Id, @DisplayName, @Url, 1, @ApiKey)", server);

            int callCount = 0;
            var handler = new MockHttpMessageHandler(req =>
            {
                callCount++;
                if (callCount == 1)
                {
                    Assert.Equal(HttpMethod.Post, req.Method);
                    Assert.Equal("http://localhost:9999/sse", req.RequestUri?.ToString());
                    return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                }
                Assert.Equal(HttpMethod.Get, req.Method);
                Assert.Equal("http://localhost:9999/", req.RequestUri?.ToString());
                return new HttpResponseMessage(HttpStatusCode.NotFound); // Any response means server is alive
            });
            var client = new HttpClient(handler);

            var services = new ServiceCollection();
            services.AddSingleton(dbFactory);
            var serviceProvider = services.BuildServiceProvider();

            var httpClientFactory = new MockHttpClientFactory(client);
            var sessionManager = new SessionManager(serviceProvider, httpClientFactory, NullLogger<SessionManager>.Instance);
            var logger = NullLogger<BackendHealthCheckService>.Instance;

            var healthService = new BackendHealthCheckService(serviceProvider, httpClientFactory, sessionManager, logger);

            await healthService.ProbeServerAsync(server);

            var status = sessionManager.BackendStatuses["test-ping-fallback"];
            Assert.Equal("Connected", status.Status);
            Assert.Equal(2, callCount);
        }

        [Fact]
        [Requirement("HEALTH-PROBE-ROOT-MODE-BYPASSES-PING", "MCP", RequirementType.Positive, "BackendHealthCheckService directly probes base authority root when HealthCheckMode is root or server is postgres.")]
        public async Task ProbeServerAsync_Probes_Root_Directly_When_Mode_Is_Root_Or_Postgres()
        {
            var (conn, dbFactory) = CreateDbFactory();
            var server = new McpServer
            {
                Id = "postgres-mcp-ha",
                DisplayName = "Postgres HA MCP",
                Url = "http://postgres-ha:8000/sse",
                Enabled = true,
                HealthCheckMode = "root"
            };
            conn.Execute("INSERT INTO Servers (Id, DisplayName, Url, Enabled) VALUES (@Id, @DisplayName, @Url, 1)", server);

            int callCount = 0;
            var handler = new MockHttpMessageHandler(req =>
            {
                callCount++;
                Assert.Equal(HttpMethod.Get, req.Method);
                Assert.Equal("http://postgres-ha:8000/", req.RequestUri?.ToString());
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });
            var client = new HttpClient(handler);

            var services = new ServiceCollection();
            services.AddSingleton(dbFactory);
            var serviceProvider = services.BuildServiceProvider();

            var httpClientFactory = new MockHttpClientFactory(client);
            var sessionManager = new SessionManager(serviceProvider, httpClientFactory, NullLogger<SessionManager>.Instance);
            var logger = NullLogger<BackendHealthCheckService>.Instance;

            var healthService = new BackendHealthCheckService(serviceProvider, httpClientFactory, sessionManager, logger);

            await healthService.ProbeServerAsync(server);

            var status = sessionManager.BackendStatuses["postgres-mcp-ha"];
            Assert.Equal("Connected", status.Status);
            Assert.Equal(1, callCount);
        }

        [Fact]
        [Requirement("HEALTH-PROBE-NONE-MODE-NOOP", "MCP", RequirementType.Positive, "BackendHealthCheckService marks server as Connected without HTTP requests when HealthCheckMode is none.")]
        public async Task ProbeServerAsync_Marks_Connected_Without_Http_When_Mode_Is_None()
        {
            var (_, dbFactory) = CreateDbFactory();
            var server = new McpServer
            {
                Id = "server-none",
                DisplayName = "No Probe Server",
                Url = "http://localhost:9999/mcp",
                Enabled = true,
                HealthCheckMode = "none"
            };

            bool httpCalled = false;
            var handler = new MockHttpMessageHandler(req =>
            {
                httpCalled = true;
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            });
            var client = new HttpClient(handler);

            var services = new ServiceCollection();
            services.AddSingleton(dbFactory);
            var serviceProvider = services.BuildServiceProvider();

            var httpClientFactory = new MockHttpClientFactory(client);
            var sessionManager = new SessionManager(serviceProvider, httpClientFactory, NullLogger<SessionManager>.Instance);
            var logger = NullLogger<BackendHealthCheckService>.Instance;

            var healthService = new BackendHealthCheckService(serviceProvider, httpClientFactory, sessionManager, logger);

            await healthService.ProbeServerAsync(server);

            Assert.False(httpCalled);
            var status = sessionManager.BackendStatuses["server-none"];
            Assert.Equal("Connected", status.Status);
        }

        [Fact]
        [Requirement("HEALTH-PROBE-HTTP-FAILED-EXCEPTION", "MCP", RequirementType.Positive, "BackendHealthCheckService marks server as Failed when downstream HTTP connection fails.")]
        public async Task ProbeServerAsync_Sets_Failed_When_Endpoint_Throws_Exception()
        {
            var (conn, dbFactory) = CreateDbFactory();
            var server = new McpServer
            {
                Id = "test-2",
                DisplayName = "Test Server 2",
                Url = "http://invalid-host:9999/sse",
                Enabled = true
            };
            conn.Execute("INSERT INTO Servers (Id, DisplayName, Url, Enabled) VALUES (@Id, @DisplayName, @Url, 1)", server);

            var handler = new MockHttpMessageHandler(req => throw new HttpRequestMessageException("Connection refused"));
            var client = new HttpClient(handler);

            var services = new ServiceCollection();
            services.AddSingleton(dbFactory);
            var serviceProvider = services.BuildServiceProvider();

            var httpClientFactory = new MockHttpClientFactory(client);
            var sessionManager = new SessionManager(serviceProvider, httpClientFactory, NullLogger<SessionManager>.Instance);
            var logger = NullLogger<BackendHealthCheckService>.Instance;

            var healthService = new BackendHealthCheckService(serviceProvider, httpClientFactory, sessionManager, logger);

            await healthService.ProbeServerAsync(server);

            var status = sessionManager.BackendStatuses["test-2"];
            Assert.Equal("Failed", status.Status);
            Assert.Equal(1, status.Attempts);
            Assert.Contains("Connection refused", status.Error);
        }

        [Fact]
        [Requirement("HEALTH-PROBE-DISABLED-SERVER", "MCP", RequirementType.Positive, "BackendHealthCheckService marks disabled servers as Disabled.")]
        public async Task ProbeServerAsync_Sets_Disabled_When_Server_Not_Enabled()
        {
            var (_, dbFactory) = CreateDbFactory();
            var server = new McpServer
            {
                Id = "test-3",
                DisplayName = "Test Server 3",
                Url = "http://localhost:9999/sse",
                Enabled = false
            };

            var services = new ServiceCollection();
            services.AddSingleton(dbFactory);
            var serviceProvider = services.BuildServiceProvider();

            var httpClientFactory = new MockHttpClientFactory(new HttpClient());
            var sessionManager = new SessionManager(serviceProvider, httpClientFactory, NullLogger<SessionManager>.Instance);
            var logger = NullLogger<BackendHealthCheckService>.Instance;

            var healthService = new BackendHealthCheckService(serviceProvider, httpClientFactory, sessionManager, logger);

            await healthService.ProbeServerAsync(server);

            var status = sessionManager.BackendStatuses["test-3"];
            Assert.Equal("Disabled", status.Status);
        }

        [Fact]
        [Requirement("HEALTH-PROBE-ALL-ENABLED-FLEET", "MCP", RequirementType.Positive, "BackendHealthCheckService probes all enabled backend servers in the fleet.")]
        public async Task ProbeAllServersAsync_Probes_All_Enabled_Servers()
        {
            var (conn, dbFactory) = CreateDbFactory();
            conn.Execute("INSERT INTO Servers (Id, DisplayName, Url, Enabled) VALUES ('s1', 'S1', 'http://localhost:1111/sse', 1)");
            conn.Execute("INSERT INTO Servers (Id, DisplayName, Url, Enabled) VALUES ('s2', 'S2', 'http://localhost:2222/sse', 1)");
            conn.Execute("INSERT INTO Servers (Id, DisplayName, Url, Enabled) VALUES ('s3', 'S3', 'http://localhost:3333/sse', 0)");

            var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK));
            var client = new HttpClient(handler);

            var services = new ServiceCollection();
            services.AddSingleton(dbFactory);
            var serviceProvider = services.BuildServiceProvider();

            var httpClientFactory = new MockHttpClientFactory(client);
            var sessionManager = new SessionManager(serviceProvider, httpClientFactory, NullLogger<SessionManager>.Instance);
            var logger = NullLogger<BackendHealthCheckService>.Instance;

            var healthService = new BackendHealthCheckService(serviceProvider, httpClientFactory, sessionManager, logger);

            await healthService.ProbeAllServersAsync();

            Assert.Equal("Connected", sessionManager.BackendStatuses["s1"].Status);
            Assert.Equal("Connected", sessionManager.BackendStatuses["s2"].Status);
            Assert.False(sessionManager.BackendStatuses.ContainsKey("s3"));
        }

        [Fact]
        [Requirement("HEALTH-PROBE-STDIO-VALID-CONNECTED", "MCP", RequirementType.Positive, "BackendHealthCheckService sets valid STDIO servers to Connected without making network HTTP probes.")]
        public async Task ProbeServerAsync_Sets_Connected_For_Valid_Stdio_Server_Without_Http_Probe()
        {
            var (conn, dbFactory) = CreateDbFactory();
            var server = new McpServer
            {
                Id = "stdio-valid",
                DisplayName = "Valid STDIO Server",
                Url = "node /app/server.js --arg=1",
                Type = "stdio",
                Enabled = true
            };

            // HTTP handler that should never be called for stdio servers
            bool httpCalled = false;
            var handler = new MockHttpMessageHandler(req =>
            {
                httpCalled = true;
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            });
            var client = new HttpClient(handler);

            var services = new ServiceCollection();
            services.AddSingleton(dbFactory);
            var serviceProvider = services.BuildServiceProvider();

            var httpClientFactory = new MockHttpClientFactory(client);
            var sessionManager = new SessionManager(serviceProvider, httpClientFactory, NullLogger<SessionManager>.Instance);
            var logger = NullLogger<BackendHealthCheckService>.Instance;

            var healthService = new BackendHealthCheckService(serviceProvider, httpClientFactory, sessionManager, logger);

            await healthService.ProbeServerAsync(server);

            Assert.False(httpCalled);
            var status = sessionManager.BackendStatuses["stdio-valid"];
            Assert.Equal("Connected", status.Status);
            Assert.Equal(1, status.Attempts);
            Assert.Empty(status.Error);
        }

        [Fact]
        [Requirement("GUARD-05", "GUARD", RequirementType.Negative, "BackendHealthCheckService marks invalid or dangerous STDIO commands as Failed under security policy.")]
        public async Task ProbeServerAsync_Sets_Failed_For_Invalid_Stdio_Server_Command()
        {
            var (_, dbFactory) = CreateDbFactory();
            var server = new McpServer
            {
                Id = "stdio-invalid",
                DisplayName = "Invalid STDIO Server",
                Url = "bash evil_script.sh",
                Type = "stdio",
                Enabled = true
            };

            var services = new ServiceCollection();
            services.AddSingleton(dbFactory);
            var serviceProvider = services.BuildServiceProvider();

            var httpClientFactory = new MockHttpClientFactory(new HttpClient());
            var sessionManager = new SessionManager(serviceProvider, httpClientFactory, NullLogger<SessionManager>.Instance);
            var logger = NullLogger<BackendHealthCheckService>.Instance;

            var healthService = new BackendHealthCheckService(serviceProvider, httpClientFactory, sessionManager, logger);

            await healthService.ProbeServerAsync(server);

            var status = sessionManager.BackendStatuses["stdio-invalid"];
            Assert.Equal("Failed", status.Status);
            Assert.Equal(1, status.Attempts);
            Assert.Contains("blocked under the security policy", status.Error);
        }

        [Fact]
        [Requirement("HEALTH-PROBE-CUSTOM-SERVER-CONNECTED", "MCP", RequirementType.Positive, "BackendHealthCheckService marks registered custom servers as Connected.")]
        public async Task ProbeServerAsync_Sets_Connected_For_Custom_Server()
        {
            var (_, dbFactory) = CreateDbFactory();
            var server = new McpServer
            {
                Id = "custom-server",
                DisplayName = "Custom Native Tool Server",
                Url = "",
                Type = "custom",
                Enabled = true
            };

            var services = new ServiceCollection();
            services.AddSingleton(dbFactory);
            var serviceProvider = services.BuildServiceProvider();

            var httpClientFactory = new MockHttpClientFactory(new HttpClient());
            var sessionManager = new SessionManager(serviceProvider, httpClientFactory, NullLogger<SessionManager>.Instance);
            var logger = NullLogger<BackendHealthCheckService>.Instance;

            var healthService = new BackendHealthCheckService(serviceProvider, httpClientFactory, sessionManager, logger);

            await healthService.ProbeServerAsync(server);

            var status = sessionManager.BackendStatuses["custom-server"];
            Assert.Equal("Connected", status.Status);
            Assert.Equal(1, status.Attempts);
            Assert.Empty(status.Error);
        }

        [Fact]
        [Requirement("HEALTH-PROBE-REPO-PERSISTS-HEALTHCHECKMODE", "MCP", RequirementType.Positive, "Server repository persists and retrieves HealthCheckMode setting accurately.")]
        public async Task ServerRepository_Persists_And_Retrieves_HealthCheckMode()
        {
            var (_, dbFactory) = CreateDbFactory();
            var repo = new DatabaseRepository(dbFactory);

            var server = new McpServer
            {
                Id = "test-repo-health-mode",
                DisplayName = "Test Health Mode Repo",
                Url = "http://localhost:8080/mcp",
                Enabled = true,
                HealthCheckMode = "root"
            };

            await repo.SaveServerAsync(server);

            var retrieved = await repo.GetServerByIdAsync("test-repo-health-mode");
            Assert.NotNull(retrieved);
            Assert.Equal("root", retrieved.HealthCheckMode);

            var allServers = (await repo.GetServersAsync()).ToList();
            var fromAll = allServers.FirstOrDefault(s => s.Id == "test-repo-health-mode");
            Assert.NotNull(fromAll);
            Assert.Equal("root", fromAll.HealthCheckMode);
        }

        private class MockHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpClient _client;
            public MockHttpClientFactory(HttpClient client) => _client = client;
            public HttpClient CreateClient(string name) => _client;
        }

        private class HttpRequestMessageException : HttpRequestException
        {
            public HttpRequestMessageException(string message) : base(message) { }
        }
    }
}
