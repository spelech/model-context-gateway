using System.Collections;
using System.Security.Claims;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ModelContextGateway.Tests
{
    public class DownstreamSessionIntegrationTests : IDisposable
    {
        private readonly SqliteConnection _dbConnection;
        private readonly IDbConnectionFactory _dbFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly MockHttpMessageHandler _mockHttpHandler;
        private readonly HttpClient _mockHttpClient;
        private readonly IConfiguration _configuration;

        public DownstreamSessionIntegrationTests()
        {
            var dbName = $"Data Source=DownstreamIntegrationTest_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            _dbConnection = new SqliteConnection(dbName);
            _dbConnection.Open();

            _dbConnection.Execute(@"
                CREATE TABLE IF NOT EXISTS Servers (
                    Id TEXT PRIMARY KEY,
                    DisplayName TEXT,
                    Url TEXT,
                    Enabled INTEGER DEFAULT 1,
                    Hidden INTEGER DEFAULT 0,
                    Type TEXT DEFAULT 'http',
                    SecretProvider TEXT DEFAULT 'None',
                    SecretItemKey TEXT,
                    AuthShape TEXT DEFAULT 'bearer',
                    CustomHeaderName TEXT,
                    Categories TEXT DEFAULT '[]',
                    ApiKey TEXT,
                    HeadersJson TEXT,
                    AutoDiscovered INTEGER DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS AccessPolicies (
                    Id TEXT PRIMARY KEY,
                    TargetId TEXT,
                    RequiredGroup TEXT,
                    IsAllowed INTEGER DEFAULT 1
                );
                CREATE TABLE IF NOT EXISTS Settings (
                    Id TEXT PRIMARY KEY,
                    EmbeddingProvider TEXT,
                    EmbeddingApiUrl TEXT,
                    EmbeddingApiKey TEXT,
                    EmbeddingApiModel TEXT,
                    EmbeddingModelDir TEXT,
                    DashboardTitle TEXT DEFAULT 'MCP Gateway',
                    DashboardIcon TEXT DEFAULT 'fa-solid fa-network-wired',
                    GlobalMaxKeys INTEGER DEFAULT 100,
                    UserMaxKeys INTEGER DEFAULT 5
                );
            ");

            var mockDbFactory = new Mock<IDbConnectionFactory>();
            mockDbFactory.Setup(f => f.CreateConnection()).Returns(() => new SqliteConnection(dbName));
            mockDbFactory.Setup(f => f.ProviderName).Returns("sqlite");
            _dbFactory = mockDbFactory.Object;

            _configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Admin:GroupSid", "full_admin" },
                { "Audit:FailClosed", "false" }
            }).Build();

            _mockHttpHandler = new MockHttpMessageHandler();
            _mockHttpClient = new HttpClient(_mockHttpHandler);

            var services = new ServiceCollection();
            services.AddSingleton(_dbFactory);
            services.AddSingleton(_configuration);
            services.AddSingleton<IAuditLogger>(new NullAuditLogger());

            var mockEmbedding = new Mock<IEmbeddingService>();
            mockEmbedding.Setup(e => e.GetEmbeddingAsync(It.IsAny<string>())).ReturnsAsync(new float[384]);
            mockEmbedding.Setup(e => e.CosineSimilarity(It.IsAny<float[]>(), It.IsAny<float[]>())).Returns(0.95);
            services.AddSingleton(mockEmbedding.Object);

            var secretRetrieverMock = new Mock<ISecretRetriever>();
            secretRetrieverMock.Setup(s => s.ProviderName).Returns("Environment");
            var compositeRetriever = new CompositeSecretRetriever(new[] { secretRetrieverMock.Object });
            services.AddSingleton(compositeRetriever);

            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_mockHttpClient);
            services.AddSingleton(mockHttpClientFactory.Object);

            services.AddLogging();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton<SessionManager>();

            _serviceProvider = services.BuildServiceProvider();
        }

        public void Dispose()
        {
            _dbConnection.Dispose();
            _mockHttpClient.Dispose();
        }

        private HttpContext CreateAdminHttpContext()
        {
            var ctx = new DefaultHttpContext();
            ctx.RequestServices = _serviceProvider;
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "admin_user"),
                new Claim("Sid", "full_admin"),
                new Claim("GroupSid", "full_admin"),
                new Claim(ClaimTypes.Role, "full_admin")
            };
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
            return ctx;
        }

        [Fact]
        [Requirement("MCP-COLDSTART-01", "MCP", RequirementType.Positive, "Full cold-start cycle: SessionManager cache seeded -> search_tools -> execute_tool dispatches to downstream mock server and returns output.")]
        public async Task ColdStartCycle_SeedsRoutingTable_AndDispatchesExecuteToolDownstream()
        {
            // Arrange: Register downstream server 'seerr'
            await _dbConnection.ExecuteAsync(
                "INSERT INTO Servers (Id, DisplayName, Url, Enabled, Type, SecretProvider) VALUES (@Id, @DisplayName, @Url, 1, 'http', 'None')",
                new { Id = "seerr", DisplayName = "Overseerr Service", Url = "http://seerr-mcp:8000" }
            );

            // Mock downstream HTTP interactions for initialization and tool calling
            _mockHttpHandler.Handler = async (req) =>
            {
                var body = await req.Content!.ReadAsStringAsync();

                if (body.Contains("\"method\":\"initialize\""))
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{\"tools\":{}},\"serverInfo\":{\"name\":\"seerr-mock\",\"version\":\"1.0\"}}}",
                            System.Text.Encoding.UTF8, "application/json")
                    };
                }

                if (body.Contains("\"method\":\"notifications/initialized\""))
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
                    };
                }

                if (body.Contains("\"method\":\"tools/call\""))
                {
                    // Verify the name was rewritten from 'seerr__seerr_get_requests' to 'seerr_get_requests'
                    body.Should().Contain("\"name\":\"seerr_get_requests\"");
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{\"content\":[{\"type\":\"text\",\"text\":\"{\\\"total\\\":1,\\\"results\\\":[{\\\"id\\\":42,\\\"media\\\":{\\\"title\\\":\\\"Inception\\\"}}]}\"}]}}",
                            System.Text.Encoding.UTF8, "application/json")
                    };
                }

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{}}", System.Text.Encoding.UTF8, "application/json")
                };
            };

            var sessionManager = _serviceProvider.GetRequiredService<SessionManager>();

            // Seed SessionManager's global tool cache (as happens across persistent background scans)
            var seededTool = new Dictionary<string, object>
            {
                ["name"] = "seerr__seerr_get_requests",
                ["description"] = "[seerr] Fetch user media requests from Overseerr",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["take"] = new Dictionary<string, object> { ["type"] = "integer" }
                    }
                }
            };
            sessionManager.SetServerToolsCache("seerr", new List<object> { seededTool });

            // Create a fresh ClientSession in meta mode (cold-start: its internal _cachedTools and _toolRoutingTable are empty)
            var httpContext = CreateAdminHttpContext();
            var session = await sessionManager.CreateSessionAsync("test-cold-start-session", httpContext.Response, targetServerId: null, metaMode: true);

            // Act 1: Call search_tools to trigger cold-start fallback seeding
            var searchPayload = "{\"jsonrpc\":\"2.0\",\"id\":\"req-search-1\",\"method\":\"tools/call\",\"params\":{\"name\":\"search_tools\",\"arguments\":{\"query\":\"seerr\"}}}";
            var searchResult = await session.CallToolAsync("search_tools", searchPayload, _dbFactory, httpContext);

            // Assert 1: search_tools returned the namespaced tool
            searchResult.Should().NotBeNull();
            var searchJson = JsonSerializer.Serialize(searchResult);
            searchJson.Should().Contain("seerr__seerr_get_requests");

            // Act 2: Execute the tool via execute_tool (this previously threw KeyNotFoundException)
            var executePayload = "{\"jsonrpc\":\"2.0\",\"id\":\"req-exec-1\",\"method\":\"tools/call\",\"params\":{\"name\":\"execute_tool\",\"arguments\":{\"name\":\"seerr__seerr_get_requests\",\"arguments\":{\"take\":1}}}}";
            var execResult = await session.CallToolAsync("execute_tool", executePayload, _dbFactory, httpContext);

            // Assert 2: Downstream execution succeeded and returned Inception
            execResult.Should().NotBeNull();
            var execJson = JsonSerializer.Serialize(execResult);
            execJson.Should().NotContain("Tool seerr__seerr_get_requests not found in routing table");
            execJson.Should().NotContain("KeyNotFoundException");
            execJson.Should().Contain("Inception");
            execJson.Should().Contain("42");
        }

        [Fact]
        [Requirement("GUARD-DISPOSED-01", "GUARD", RequirementType.Positive, "Stateless HTTP POST lifecycle: HTTP response completes, HttpContext is marked disposed, background backend initialization completes successfully without ObjectDisposedException.")]
        public async Task StatelessHttpLifecycle_DisposedHttpContext_CompletesInitializationWithoutThrowing()
        {
            // Arrange: Register downstream server 'plex'
            await _dbConnection.ExecuteAsync(
                "INSERT INTO Servers (Id, DisplayName, Url, Enabled, Type, SecretProvider) VALUES (@Id, @DisplayName, @Url, 1, 'http', 'None')",
                new { Id = "plex", DisplayName = "Plex Media Server", Url = "http://plex:32400" }
            );

            _mockHttpHandler.Handler = async (req) =>
            {
                var body = await req.Content!.ReadAsStringAsync();

                if (body.Contains("\"method\":\"initialize\""))
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{\"tools\":{}},\"serverInfo\":{\"name\":\"plex-mock\",\"version\":\"1.0\"}}}",
                            System.Text.Encoding.UTF8, "application/json")
                    };
                }

                if (body.Contains("\"method\":\"notifications/initialized\""))
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
                    };
                }

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{}}", System.Text.Encoding.UTF8, "application/json")
                };
            };

            var sessionManager = _serviceProvider.GetRequiredService<SessionManager>();

            // Create a custom HttpContext with a disposed feature collection
            var disposedFeatureCollection = new DisposedFeatureCollection();
            var disposedContext = new DefaultHttpContext(disposedFeatureCollection);

            // Establish the global-stateless-session holding the disposed context
            var session = await sessionManager.CreateSessionAsync("global-stateless-session", disposedContext.Response, targetServerId: null, metaMode: false);

            // Simulate the subsequent HTTP request lifecycle: update client response, then decouple upon completion
            session.UpdateClientResponse(disposedContext.Response);

            // Act 1: Verify ResolveUserIdentityAsync handles disposed HttpContext without throwing ObjectDisposedException
            var resolvedIdentity = await session.ResolveUserIdentityAsync();
            resolvedIdentity.Should().NotBeNull();
            resolvedIdentity.Username.Should().BeOneOf("system", "anonymous");

            // Act 2: Simulate request completion: decouple client response
            session.DecoupleClientResponse();
            session.GetClientResponse().Should().BeNull();

            // Act 3: Start backend initialization in the background while HttpContext is disposed/decoupled
            session.StartInitializationForBackend("plex");
            await session.EnsureBackendsInitializedAsync();

            // Assert 3: The backend connection initialized successfully without ObjectDisposedException
            sessionManager.BackendStatuses.Should().ContainKey("plex");
            sessionManager.BackendStatuses["plex"].Status.Should().Be("Connected");
            sessionManager.BackendStatuses["plex"].Error.Should().BeEmpty();
        }

        [Fact]
        [Requirement("MCP-RESILIENT-01", "MCP", RequirementType.Positive, "Prefix-based resilient routing: execute_tool called with unregistered but prefixed tool name dynamically resolves server and executes.")]
        public async Task PrefixBasedResilientRouting_DynamicallyRegistersAndDispatchesTool()
        {
            // Arrange: Register downstream server 'overseerr'
            await _dbConnection.ExecuteAsync(
                "INSERT INTO Servers (Id, DisplayName, Url, Enabled, Type, SecretProvider) VALUES (@Id, @DisplayName, @Url, 1, 'http', 'None')",
                new { Id = "overseerr", DisplayName = "Overseerr Core", Url = "http://overseerr:5055" }
            );

            _mockHttpHandler.Handler = async (req) =>
            {
                var body = await req.Content!.ReadAsStringAsync();

                if (body.Contains("\"method\":\"initialize\""))
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{\"tools\":{}},\"serverInfo\":{\"name\":\"overseerr-mock\",\"version\":\"1.0\"}}}",
                            System.Text.Encoding.UTF8, "application/json")
                    };
                }

                if (body.Contains("\"method\":\"notifications/initialized\""))
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
                    };
                }

                if (body.Contains("\"method\":\"tools/call\""))
                {
                    // Downstream call should have stripped prefix
                    body.Should().Contain("\"name\":\"get_media_status\"");
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"jsonrpc\":\"2.0\",\"id\":3,\"result\":{\"content\":[{\"type\":\"text\",\"text\":\"Media status: Approved\"}]}}",
                            System.Text.Encoding.UTF8, "application/json")
                    };
                }

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{}}", System.Text.Encoding.UTF8, "application/json")
                };
            };

            var sessionManager = _serviceProvider.GetRequiredService<SessionManager>();
            var httpContext = CreateAdminHttpContext();

            // Create session in meta mode without seeding any tool cache
            var session = await sessionManager.CreateSessionAsync("test-resilient-routing-session", httpContext.Response, targetServerId: null, metaMode: true);

            // Act 1: Call execute_tool with a tool name that has NOT been discovered or registered in routing table
            var executePayload = "{\"jsonrpc\":\"2.0\",\"id\":\"pfx-exec-1\",\"method\":\"tools/call\",\"params\":{\"name\":\"execute_tool\",\"arguments\":{\"name\":\"overseerr__get_media_status\",\"arguments\":{\"mediaId\":100}}}}";
            var result = await session.CallToolAsync("execute_tool", executePayload, _dbFactory, httpContext);

            // Assert 1: Dynamic prefix registration succeeded and output was returned from downstream
            result.Should().NotBeNull();
            var resultJson = JsonSerializer.Serialize(result);
            resultJson.Should().NotContain("not found in routing table");
            resultJson.Should().NotContain("KeyNotFoundException");
            resultJson.Should().Contain("Media status: Approved");

            // Act 2: Direct call with namespaced tool also routes seamlessly now that it is registered
            var directPayload = "{\"jsonrpc\":\"2.0\",\"id\":\"pfx-direct-1\",\"method\":\"tools/call\",\"params\":{\"name\":\"overseerr__get_media_status\",\"arguments\":{\"mediaId\":100}}}";
            var directResult = await session.CallToolAsync("overseerr__get_media_status", directPayload, _dbFactory, httpContext);

            // Assert 2: Direct call succeeds
            directResult.Should().NotBeNull();
            var directJson = JsonSerializer.Serialize(directResult);
            directJson.Should().Contain("Media status: Approved");
        }

        [Fact]
        [Requirement("MCP-22", "MCP", RequirementType.Positive, "Downstream backend protocol version mismatch dynamically negotiates down to backend supported versions.")]
        public async Task DownstreamBackend_ProtocolVersionMismatch_NegotiatesOlderVersionSuccessfully()
        {
            // Arrange: register a backend that only supports older protocol versions (2025-11-25)
            await _dbConnection.ExecuteAsync(@"
                INSERT INTO Servers (Id, DisplayName, Url, Enabled, Hidden, Type, AutoDiscovered)
                VALUES ('srv-legacy-llm', 'Legacy LLM Server', 'http://mock-llm:5000/mcp', 1, 0, 'http', 0);
            ");

            int initializeAttempts = 0;
            string lastReceivedProtocolVersion = string.Empty;

            _mockHttpHandler.Handler = async (req) =>
            {
                var contentStr = req.Content != null ? await req.Content.ReadAsStringAsync() : "";

                if (contentStr.Contains("\"method\":\"initialize\""))
                {
                    initializeAttempts++;
                    using var doc = JsonDocument.Parse(contentStr);
                    var idRaw = doc.RootElement.TryGetProperty("id", out var idElem) ? idElem.GetRawText() : "1";
                    
                    if (doc.RootElement.TryGetProperty("params", out var pElem) && pElem.TryGetProperty("protocolVersion", out var verElem))
                    {
                        lastReceivedProtocolVersion = verElem.GetString() ?? "";
                    }

                    if (lastReceivedProtocolVersion == "2026-07-28")
                    {
                        // Simulate server rejecting 2026-07-28 with standard -32022 error
                        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                                $"{{\"jsonrpc\":\"2.0\",\"id\":{idRaw},\"error\":{{\"code\":-32022,\"message\":\"Protocol version '2026-07-28' is not available through the initialize handshake.\",\"data\":{{\"supported\":[\"2024-11-05\",\"2025-03-26\",\"2025-06-18\",\"2025-11-25\"]}}}}}}",
                                System.Text.Encoding.UTF8, "application/json")
                        };
                    }
                    else if (lastReceivedProtocolVersion == "2025-11-25")
                    {
                        // Simulate server accepting 2025-11-25
                        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                                $"{{\"jsonrpc\":\"2.0\",\"id\":{idRaw},\"result\":{{\"protocolVersion\":\"2025-11-25\",\"capabilities\":{{\"tools\":{{}}}},\"serverInfo\":{{\"name\":\"MockLlm\",\"version\":\"1.0\"}}}}}}",
                                System.Text.Encoding.UTF8, "application/json")
                        };
                    }
                }

                if (contentStr.Contains("\"method\":\"notifications/initialized\""))
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
                    };
                }

                if (contentStr.Contains("\"method\":\"tools/list\""))
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"tools\":[{\"name\":\"get_gpu_vram\",\"description\":\"VRAM stats\",\"inputSchema\":{\"type\":\"object\"}}]}}",
                            System.Text.Encoding.UTF8, "application/json")
                    };
                }

                if (contentStr.Contains("\"method\":\"tools/call\""))
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"content\":[{\"type\":\"text\",\"text\":\"GPU VRAM 16GB\"}]}}",
                            System.Text.Encoding.UTF8, "application/json")
                    };
                }

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{}}", System.Text.Encoding.UTF8, "application/json")
                };
            };

            var sessionManager = _serviceProvider.GetRequiredService<SessionManager>();
            var httpContext = CreateAdminHttpContext();

            // Act: create session and initialize backends
            var session = await sessionManager.CreateSessionAsync("test-negotiation-session", httpContext.Response, targetServerId: null, metaMode: true);
            await session.EnsureBackendsInitializedAsync();

            // Assert: backend negotiated down to 2025-11-25 and connected
            initializeAttempts.Should().BeGreaterThanOrEqualTo(2);
            lastReceivedProtocolVersion.Should().Be("2025-11-25");

            // Execute tool to verify end-to-end dispatch through negotiated session
            var execPayload = "{\"jsonrpc\":\"2.0\",\"id\":\"exec-vram\",\"method\":\"tools/call\",\"params\":{\"name\":\"execute_tool\",\"arguments\":{\"name\":\"srv-legacy-llm__get_gpu_vram\",\"arguments\":{}}}}";
            var result = await session.CallToolAsync("execute_tool", execPayload, _dbFactory, httpContext);

            result.Should().NotBeNull();
            var resultJson = JsonSerializer.Serialize(result);
            resultJson.Should().Contain("GPU VRAM 16GB");
        }

        private class DisposedFeatureCollection : IFeatureCollection
        {
            public object? this[Type key]
            {
                get => throw new ObjectDisposedException("Collection", "IFeatureCollection has been disposed. Object name: 'Collection'.");
                set => throw new ObjectDisposedException("Collection");
            }

            public bool IsReadOnly => true;
            public int Revision => 0;

            public object? Get(Type key) => throw new ObjectDisposedException("Collection", "IFeatureCollection has been disposed. Object name: 'Collection'.");
            public void Set(Type key, object? value) => throw new ObjectDisposedException("Collection");

            public TFeature? Get<TFeature>() => throw new ObjectDisposedException("Collection", "IFeatureCollection has been disposed. Object name: 'Collection'.");
            public void Set<TFeature>(TFeature? instance) => throw new ObjectDisposedException("Collection");

            public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => throw new ObjectDisposedException("Collection");
            IEnumerator IEnumerable.GetEnumerator() => throw new ObjectDisposedException("Collection");
        }

        private class NullAuditLogger : IAuditLogger
        {
            public Task LogInvocationAsync(string requestId, string userPrincipalName, string userSid, string serverCodeName, string itemName, string requestMethod, int executionTimeMs, int statusCode, string? requestPayload = null, string? responsePayload = null, string? errorMessage = null) => Task.CompletedTask;
            public Task LogAdminActionAsync(string username, string action, string target, string details, bool success, string? errorMessage = null) => Task.CompletedTask;
        }
    }
}
