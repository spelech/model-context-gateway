using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ModelContextGateway.Tests
{
    public class ClientSessionTests
    {
        private (SqliteConnection conn, IDbConnectionFactory factory) CreateDbFactory()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            var mockDbFactory = new Mock<IDbConnectionFactory>();
            mockDbFactory.Setup(f => f.CreateConnection()).Returns(connection);
            mockDbFactory.Setup(f => f.ProviderName).Returns("sqlite");
            return (connection, mockDbFactory.Object);
        }

        private class NullAuditLogger : IAuditLogger
        {
            public Task LogInvocationAsync(string requestId, string userPrincipalName, string userSid, string serverCodeName, string itemName, string requestMethod, int executionTimeMs, int statusCode, string? requestPayload = null, string? responsePayload = null, string? errorMessage = null) => Task.CompletedTask;
            public Task LogAdminActionAsync(string username, string action, string target, string details, bool success, string? errorMessage = null) => Task.CompletedTask;
        }

        private HttpContext CreateMockHttpContext(IDbConnectionFactory dbFactory)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IAuditLogger, NullAuditLogger>();
            services.AddSingleton(dbFactory);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DB_ENCRYPTION_KEY", "TestSecretKey1234567890123456789012" },
                { "Admin:GroupSid", "full_admin" }
            }).Build();
            services.AddSingleton<IConfiguration>(config);
            services.AddLogging();

            var sp = services.BuildServiceProvider();
            var ctx = new DefaultHttpContext();
            ctx.RequestServices = sp;

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "admin_user"),
                new Claim("Sid", "full_admin"),
                new Claim(ClaimTypes.Role, "full_admin")
            };
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

            return ctx;
        }

        private ClientSession CreateTestSession(IDbConnectionFactory dbFactory, List<McpServer>? servers = null)
        {
            var logger = NullLogger<ClientSession>.Instance;
            servers ??= new List<McpServer>();

            var session = new ClientSession(
                sessionId: "test-client-sess-1",
                clientResponse: null!,
                servers: servers,
                httpClient: new HttpClient(),
                embeddingService: new DummyEmbeddingService(),
                sessionManager: null,
                logger: logger
            );

            session.StartInitialization("{\"jsonrpc\":\"2.0\",\"method\":\"initialize\",\"id\":1}");
            return session;
        }

        [Fact]
        [Requirement("MCP-01", "MCP", RequirementType.Positive, "ClientSession exposes meta-mode tools search_tools and execute_tool in meta mode.")]
        public async Task ListToolsAsync_WhenInMetaMode_ExposesSearchAndExecuteTools()
        {
            var (_, dbFactory) = CreateDbFactory();
            var httpContext = CreateMockHttpContext(dbFactory);
            var session = CreateTestSession(dbFactory);
            session.IsMetaMode = true;

            var tools = await session.ListToolsAsync("{}", httpContext);

            tools.Should().NotBeNull();
            tools.Should().HaveCount(2);

            var json = JsonSerializer.Serialize(tools);
            json.Should().Contain("\"name\":\"search_tools\"");
            json.Should().Contain("\"name\":\"execute_tool\"");
        }

        [Fact]
        [Requirement("MCP-01", "MCP", RequirementType.Positive, "ClientSession lists built-in resources including router://status.")]
        public async Task ListResourcesAsync_ReturnsBuiltinRouterStatusResource()
        {
            var (_, dbFactory) = CreateDbFactory();
            var httpContext = CreateMockHttpContext(dbFactory);
            var session = CreateTestSession(dbFactory);

            var resources = await session.ListResourcesAsync("{}", httpContext);

            resources.Should().NotBeNull();
            var json = JsonSerializer.Serialize(resources);
            json.Should().Contain("router://status");
            json.Should().Contain("application/json");
        }

        [Fact]
        [Requirement("MCP-01", "MCP", RequirementType.Positive, "ClientSession lists built-in diagnostic and routing prompts.")]
        public async Task ListPromptsAsync_ReturnsBuiltinDiagnosticPrompts()
        {
            var (_, dbFactory) = CreateDbFactory();
            var httpContext = CreateMockHttpContext(dbFactory);
            var session = CreateTestSession(dbFactory);

            var prompts = await session.ListPromptsAsync("{}", httpContext);

            prompts.Should().NotBeNull();
            var json = JsonSerializer.Serialize(prompts);
            json.Should().Contain("\"name\":\"router__diagnose_failure\"");
            json.Should().Contain("\"name\":\"router__route_multi_task\"");
            json.Should().Contain("\"name\":\"router__audit_permissions\"");
        }

        [Fact]
        [Requirement("MCP-01", "MCP", RequirementType.Positive, "ClientSession calls built-in search_tools and returns structured search results.")]
        public async Task CallToolAsync_SearchTools_ExecutesSuccessfullyWithStructuredContent()
        {
            var (_, dbFactory) = CreateDbFactory();
            var httpContext = CreateMockHttpContext(dbFactory);
            var session = CreateTestSession(dbFactory);

            var callResult = await session.CallToolAsync(
                "search_tools",
                "{\"jsonrpc\":\"2.0\",\"method\":\"tools/call\",\"id\":2,\"params\":{\"name\":\"search_tools\",\"arguments\":{\"query\":\"docker\"}}}",
                dbFactory,
                httpContext
            );

            callResult.Should().NotBeNull();
            var json = JsonSerializer.Serialize(callResult);
            json.Should().Contain("\"resultType\":\"complete\"");
            json.Should().Contain("\"content\"");
        }

        [Fact]
        [Requirement("MCP-01", "MCP", RequirementType.Positive, "ClientSession reads router://status resource and returns online gateway metadata.")]
        public async Task ReadResourceAsync_RouterStatus_ReturnsOnlineStatusPayload()
        {
            var (_, dbFactory) = CreateDbFactory();
            var httpContext = CreateMockHttpContext(dbFactory);
            var session = CreateTestSession(dbFactory);

            var resResult = await session.ReadResourceAsync("router://status", "{}", httpContext);

            resResult.Should().NotBeNull();
            var json = JsonSerializer.Serialize(resResult);
            using var doc = JsonDocument.Parse(json);
            var contents = doc.RootElement.GetProperty("contents");
            contents.GetArrayLength().Should().Be(1);
            contents[0].GetProperty("uri").GetString().Should().Be("router://status");
            contents[0].GetProperty("mimeType").GetString().Should().Be("application/json");

            var innerJson = contents[0].GetProperty("text").GetString()!;
            using var innerDoc = JsonDocument.Parse(innerJson);
            innerDoc.RootElement.GetProperty("status").GetString().Should().Be("online");
        }

        [Fact]
        [Requirement("MCP-01", "MCP", RequirementType.Positive, "ClientSession gets router__diagnose_failure prompt and returns diagnostic prompt instructions.")]
        public async Task GetPromptAsync_DiagnoseFailure_ReturnsDiagnosticInstructions()
        {
            var (_, dbFactory) = CreateDbFactory();
            var httpContext = CreateMockHttpContext(dbFactory);
            var session = CreateTestSession(dbFactory);

            var promptResult = await session.GetPromptAsync(
                "router__diagnose_failure",
                "{\"params\":{\"arguments\":{\"tool_name\":\"db_query\",\"error_message\":\"Connection timeout\"}}}",
                httpContext
            );

            promptResult.Should().NotBeNull();
            var json = JsonSerializer.Serialize(promptResult);
            json.Should().Contain("\"messages\"");
            json.Should().Contain("expert systems administrator");
            json.Should().Contain("db_query");
            json.Should().Contain("Connection timeout");
        }

        [Fact]
        [Requirement("MCP-01", "MCP", RequirementType.Positive, "ClientSession registers and triggers request cancellation for active cancellation tokens.")]
        public void RegisterRequestCancellation_And_CancelRequest_CancelsActiveToken()
        {
            var (_, dbFactory) = CreateDbFactory();
            var session = CreateTestSession(dbFactory);
            using var cts = new CancellationTokenSource();

            var registered = session.RegisterRequestCancellation("req-test-99", cts);
            registered.Should().BeTrue();
            cts.IsCancellationRequested.Should().BeFalse();

            session.CancelRequest("req-test-99");
            cts.IsCancellationRequested.Should().BeTrue();
        }

        [Fact]
        [Requirement("MCP-01", "MCP", RequirementType.Positive, "ClientSession returns false when handling client response for unregistered request ID.")]
        public void TryHandleClientResponse_ReturnsFalse_WhenNoPendingRequest()
        {
            var (_, dbFactory) = CreateDbFactory();
            var session = CreateTestSession(dbFactory);

            var handled = session.TryHandleClientResponse("unregistered-req-id", "{}");
            handled.Should().BeFalse();
        }
    }
}
