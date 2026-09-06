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
        [Requirement("HEALTH-PROBE-HTTP-CONNECTED-200", "MCP", RequirementType.Positive, "BackendHealthCheckService marks server as Connected when downstream HTTP/SSE endpoint returns 200 OK.")]
        public async Task ProbeServerAsync_Sets_Connected_When_Endpoint_Responds_200()
        {
            var (conn, dbFactory) = CreateDbFactory();
            var server = new McpServer
            {
                Id = "test-1",
                DisplayName = "Test Server 1",
                Url = "http://localhost:9999/sse",
                Enabled = true,
                ApiKey = "secret123"
            };
            conn.Execute("INSERT INTO Servers (Id, DisplayName, Url, Enabled, ApiKey) VALUES (@Id, @DisplayName, @Url, 1, @ApiKey)", server);

            var handler = new MockHttpMessageHandler(req =>
            {
                Assert.Equal("Bearer secret123", req.Headers.Authorization?.ToString());
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

            var status = sessionManager.BackendStatuses["test-1"];
            Assert.Equal("Connected", status.Status);
            Assert.Equal(1, status.Attempts);
            Assert.Empty(status.Error);
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
