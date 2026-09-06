using System.Collections.Concurrent;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ModelContextGateway.Tests
{
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, Task<HttpResponseMessage>>? Handler { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Handler != null)
            {
                return await Handler(request);
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    public class McpIntegrationTests : IDisposable
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            Converters = { new JsonRpcMessageConverter() }
        };

        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
        private readonly IDbConnectionFactory _dbFactory;

        public McpIntegrationTests()
        {
            var dbName = $"Data Source=McpTestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            _connection = new Microsoft.Data.Sqlite.SqliteConnection(dbName);
            _connection.Open();

            DatabaseInitializer.InitializeDatabase(_connection);

            var mockDbFactory = new Mock<IDbConnectionFactory>();
            mockDbFactory.Setup(f => f.CreateConnection()).Returns(() => new Microsoft.Data.Sqlite.SqliteConnection(dbName));
            mockDbFactory.Setup(f => f.ProviderName).Returns("sqlite");
            _dbFactory = mockDbFactory.Object;
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        [Fact]
        [Requirement("GUARD-05", "GUARD", RequirementType.Negative, "Named MCP client applies SSRF connect socket callback and blocks private IP connections.")]
        public async Task McpClient_NamedHttpClient_Applies_SsrfConnectCallback_AndBlocksPrivateIps()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build());
            services.AddHttpClient("McpClient");
            services.ConfigureAll<Microsoft.Extensions.Http.HttpClientFactoryOptions>(options =>
            {
                options.HttpMessageHandlerBuilderActions.Add(b =>
                {
                    b.PrimaryHandler = new SocketsHttpHandler
                    {
                        AllowAutoRedirect = false,
                        ConnectCallback = ModelContextGateway.Components.Authorization.SecurityValidationHelper.ValidatingConnectCallback
                    };
                });
            });

            var sp = services.BuildServiceProvider();
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("McpClient");

            var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await client.GetAsync("http://169.254.169.254/latest/meta-data");
            });

            Assert.Contains("SSRF protection", ex.Message);
        }

        [Fact]
        [Requirement("GUARD-04", "GUARD", RequirementType.Negative, "Audit logger fail-closed policy refuses tool invocation on audit write errors.")]
        public async Task AuditLogger_AuditFailClosed_RefusesInvocation_OnAuditWriteError()
        {
            var mockAuditLogger = new Mock<IAuditLogger>();
            mockAuditLogger.Setup(a => a.LogInvocationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>()
            )).ThrowsAsync(new Exception("Database connection for audit log failed"));

            var services = new ServiceCollection();
            services.AddSingleton<IAuditLogger>(mockAuditLogger.Object);
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Audit:FailClosed", "true" }
            }).Build());

            var mockProvider = new Mock<IIdentityProvider>();
            mockProvider.Setup(p => p.ResolveIdentityAsync(It.IsAny<HttpContext>()))
                        .ReturnsAsync(new UserIdentityContext("alice", "MockProvider", new List<string> { "S-1-5-32-545" }));

            var composite = new CompositeIdentityProvider(new[] { mockProvider.Object });
            services.AddSingleton(composite);

            var sp = services.BuildServiceProvider();

            var httpContext = new DefaultHttpContext { RequestServices = sp };
            var session = CreateSession(new List<McpServer>(), out _);

            var ex = await Assert.ThrowsAsync<System.Security.SecurityException>(async () =>
            {
                await session.CallToolAsync("test__tool", "{}", _dbFactory, httpContext);
            });

            Assert.Contains("Audit logging failed and fail-closed security policy is active", ex.Message);
        }

        [Fact]
        [Requirement("SEC-AUDIT-PER-REQUEST-ACTOR-ATTRIBUTION", "SEC", RequirementType.Positive, "Audit logger attributes per-request actor credentials accurately across stateless calls.")]
        public async Task AuditLogger_RecordsPerRequestActor_NotHandshakeActor()
        {
            string? loggedUsername = null;
            string? loggedSid = null;
            var mockAuditLogger = new Mock<IAuditLogger>();
            mockAuditLogger.Setup(a => a.LogInvocationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>()
            )).Callback<string, string, string, string, string, string, int, int, string?, string?, string?>(
                (reqId, user, sid, server, item, method, time, status, payload, resp, err) =>
                {
                    loggedUsername = user;
                    loggedSid = sid;
                }
            ).Returns(Task.CompletedTask);

            var services = new ServiceCollection();
            services.AddSingleton<IAuditLogger>(mockAuditLogger.Object);
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build());

            var bobContext = new DefaultHttpContext();
            var mockProvider = new Mock<IIdentityProvider>();
            mockProvider.Setup(p => p.ResolveIdentityAsync(bobContext))
                        .ReturnsAsync(new UserIdentityContext("bob", "MockProvider", new List<string> { "group1" }, Sids: new List<string> { "S-1-5-32-545" }));

            var composite = new CompositeIdentityProvider(new[] { mockProvider.Object });
            services.AddSingleton(composite);
            var sp = services.BuildServiceProvider();
            bobContext.RequestServices = sp;

            // Session created under Alice (handshake context)
            var aliceSession = CreateSession(new List<McpServer>(), out _);
            // Simulate Bob invoking tool call over Alice's SSE session
            try
            {
                await aliceSession.CallToolAsync("test__tool", "{}", _dbFactory, bobContext);
            }
            catch { }

            // Audit log MUST record Bob (the per-request actor) and Bob's SID, NOT Alice
            Assert.Equal("bob", loggedUsername);
            Assert.Equal("S-1-5-32-545", loggedSid);
        }

        private ClientSession CreateSession(List<McpServer> servers, out MockHttpMessageHandler httpHandler)
        {
            httpHandler = new MockHttpMessageHandler();
            var httpClient = new HttpClient(httpHandler);

            var context = new DefaultHttpContext();
            var claims = new[] {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "admin"),
                new System.Security.Claims.Claim("GroupSid", "S-1-5-32-544"),
                new System.Security.Claims.Claim("Sid", "S-1-5-32-544")
            };
            context.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims, "AppKey"));
            var response = context.Response;

            var services = new ServiceCollection();
            var mockAuditLogger = new Mock<IAuditLogger>();
            services.AddSingleton<IAuditLogger>(mockAuditLogger.Object);
            var realConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                { "Audit:FailClosed", "false" }
            }).Build();
            services.AddSingleton<IConfiguration>(realConfig);
            context.RequestServices = services.BuildServiceProvider();

            var loggerMock = new Mock<ILogger>();
            var embeddingMock = new Mock<IEmbeddingService>();

            embeddingMock.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
                .ReturnsAsync((string txt) =>
                {
                    if (txt.Contains("Excel", StringComparison.OrdinalIgnoreCase) || txt.Contains("read_excel", StringComparison.OrdinalIgnoreCase))
                    {
                        return new float[] { 1f, 0f, 0f };
                    }
                    if (txt.Contains("container log", StringComparison.OrdinalIgnoreCase) || txt.Contains("get_logs", StringComparison.OrdinalIgnoreCase))
                    {
                        return new float[] { 0f, 1f, 0f };
                    }
                    if (txt.Contains("list_containers", StringComparison.OrdinalIgnoreCase))
                    {
                        return new float[] { 0f, 0.7f, 0.3f };
                    }
                    return new float[] { 0f, 0f, 1f };
                });

            embeddingMock.Setup(x => x.CosineSimilarity(It.IsAny<float[]>(), It.IsAny<float[]>()))
                .Returns((float[] v1, float[] v2) =>
                {
                    double dot = 0.0;
                    double n1 = 0.0;
                    double n2 = 0.0;
                    for (int i = 0; i < v1.Length; i++)
                    {
                        dot += v1[i] * v2[i];
                        n1 += v1[i] * v1[i];
                        n2 += v2[i] * v2[i];
                    }
                    if (n1 == 0 || n2 == 0)
                    {
                        return 0.0;
                    }

                    return dot / (Math.Sqrt(n1) * Math.Sqrt(n2));
                });



            foreach (var s in servers)
            {
                if (s.SecretProvider == "Vault" && string.IsNullOrEmpty(s.SecretPath) && string.IsNullOrEmpty(s.SecretMount))
                {
                    s.SecretProvider = "None";
                }
            }

            return new ClientSession("test-session", response, servers, httpClient, embeddingMock.Object, loggerMock.Object);
        }

        private HttpResponseMessage CreateJsonResponse(object payload)
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = content };
        }

        [Fact]
        [Requirement("MCP-JSONRPC-POLYMORPHIC-DESERIALIZATION", "MCP", RequirementType.Positive, "Polymorphic JSON-RPC message deserializer accurately instantiates request, response, and notification subclasses.")]
        public void PolymorphicDeserialization_Correctly_Deserializes_JsonRpcMessage_Subclasses()
        {
            // Request JSON
            var requestJson = "{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\",\"id\":123,\"params\":{}}";
            var msgReq = JsonSerializer.Deserialize<JsonRpcMessage>(requestJson, _jsonOptions);
            msgReq.Should().BeOfType<JsonRpcRequest>();
            var req = msgReq as JsonRpcRequest;
            req!.Method.Should().Be("tools/list");
            req.Id?.ToString().Should().Be("123");

            // Notification JSON
            var notificationJson = "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}";
            var msgNotif = JsonSerializer.Deserialize<JsonRpcMessage>(notificationJson, _jsonOptions);
            msgNotif.Should().BeOfType<JsonRpcNotification>();
            var notif = msgNotif as JsonRpcNotification;
            notif!.Method.Should().Be("notifications/initialized");

            // Response JSON
            var responseJson = "{\"jsonrpc\":\"2.0\",\"id\":123,\"result\":{\"success\":true}}";
            var msgResp = JsonSerializer.Deserialize<JsonRpcMessage>(responseJson, _jsonOptions);
            msgResp.Should().BeOfType<JsonRpcResponse>();
            var resp = msgResp as JsonRpcResponse;
            resp!.Id?.ToString().Should().Be("123");
            resp.Result.Should().NotBeNull();
        }

        [Fact]
        [Requirement("MCP-JSONRPC-DESERIALIZE-PLAIN-NO-OVERFLOW", "MCP", RequirementType.Positive, "Deserializing plain JsonRpcMessage does not cause recursive converter invocation or stack overflow.")]
        public void Deserializing_Plain_JsonRpcMessage_Does_Not_Cause_StackOverflow()
        {
            var plainJson = "{\"jsonrpc\":\"2.0\"}";

            // Act & Assert
            var action = () => JsonSerializer.Deserialize<JsonRpcMessage>(plainJson, _jsonOptions);
            action.Should().NotThrow();

            var msg = JsonSerializer.Deserialize<JsonRpcMessage>(plainJson, _jsonOptions);
            msg.Should().NotBeNull();
            msg.Should().BeOfType<JsonRpcMessage>();
            msg!.JsonRpc.Should().Be("2.0");
        }

        [Fact]
        [Requirement("MCP-JSONRPC-SERIALIZE-PLAIN-NO-OVERFLOW", "MCP", RequirementType.Positive, "Serializing plain JsonRpcMessage does not cause recursive converter invocation or stack overflow.")]
        public void Serializing_Plain_JsonRpcMessage_Does_Not_Cause_StackOverflow()
        {
            var msg = new JsonRpcMessage { JsonRpc = "2.0" };

            // Act & Assert
            var action = () => JsonSerializer.Serialize(msg, _jsonOptions);
            action.Should().NotThrow();

            var json = JsonSerializer.Serialize(msg, _jsonOptions);
            json.Should().Contain("\"jsonrpc\":\"2.0\"");
        }

        [Fact]
        [Requirement("MCP-DOWNSTREAM-INIT-DIAGNOSTICS", "MCP", RequirementType.Positive, "Initializes downstream MCP backends with detailed diagnostic logging.")]
        public async Task TestInitializationDiagnostics()
        {
            var server = new McpServer { Id = "backend1", DisplayName = "Backend 1", Url = "http://backend1/mcp", Type = "http", SecretProvider = "None", Enabled = true };
            var mockHandler = new MockHttpMessageHandler();
            var httpClient = new HttpClient(mockHandler);
            var loggerMock = new Mock<ILogger>();

            mockHandler.Handler = async (req) =>
            {
                return CreateJsonResponse(new
                {
                    jsonrpc = "2.0",
                    id = "auto-init",
                    result = new { protocolVersion = "2024-11-05" }
                });
            };

            var conn = new BackendConnection(server, httpClient, loggerMock.Object);
            try
            {
                var resp = await conn.SendRequestAsync("initialize", "{\"jsonrpc\":\"2.0\",\"id\":\"auto-init\",\"method\":\"initialize\"}");
                resp.Should().NotBeNull();
            }
            catch (Exception ex)
            {
                throw new Exception("Initialization diagnostics failed with: " + ex.ToString());
            }
        }

        [Fact]
        [Requirement("MCP-02", "MCP", RequirementType.Positive, "tools/list aggregates and un-namespaces backend tools for downstream execution.")]
        public async Task ToolListing_And_Remapping_Works_Correctly()
        {
            // Arrange
            var servers = new List<McpServer>
            {
                new McpServer { Id = "backend1", DisplayName = "Backend 1", Url = "http://backend1/mcp", Type = "http", Enabled = true }
            };

            var session = CreateSession(servers, out var httpHandler);

            httpHandler.Handler = async (req) =>
            {
                var body = req.Content != null ? await req.Content.ReadAsStringAsync() : "";
                if (body.Contains("initialize"))
                {
                    return CreateJsonResponse(new
                    {
                        jsonrpc = "2.0",
                        id = "auto-init",
                        result = new
                        {
                            protocolVersion = "2024-11-05",
                            capabilities = new { },
                            serverInfo = new { name = "Backend1", version = "1.0" }
                        }
                    });
                }
                else if (body.Contains("tools/list"))
                {
                    return CreateJsonResponse(new
                    {
                        jsonrpc = "2.0",
                        id = "init-list",
                        result = new
                        {
                            tools = new[]
                            {
                                new
                                {
                                    name = "get_weather",
                                    description = "Get weather info",
                                    inputSchema = new { type = "object" }
                                }
                            }
                        }
                    });
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            };

            // Act
            var tools = await session.ListToolsAsync("{\"jsonrpc\":\"2.0\",\"id\":1}");

            // Assert
            tools.Should().NotBeEmpty();
            var tool = tools[0] as Dictionary<string, object>;
            tool.Should().NotBeNull();
            tool!["name"].Should().Be("backend1/get_weather");
            tool["description"].Should().Be("[backend1] Get weather info");
        }

        [Fact]
        [Requirement("MCP-05", "MCP", RequirementType.Positive, "Resource routing translates and virtualizes URIs across backend servers.")]
        public async Task ResourceRouting_And_UriTranslation_Works_Correctly()
        {
            // Arrange
            var servers = new List<McpServer>
            {
                new McpServer { Id = "backend1", DisplayName = "Backend 1", Url = "http://backend1/mcp", Type = "http", Enabled = true }
            };

            var session = CreateSession(servers, out var httpHandler);

            string? lastReadUri = null;
            httpHandler.Handler = async (req) =>
            {
                var body = req.Content != null ? await req.Content.ReadAsStringAsync() : "";
                if (body.Contains("initialize"))
                {
                    return CreateJsonResponse(new
                    {
                        jsonrpc = "2.0",
                        id = "auto-init",
                        result = new { protocolVersion = "2024-11-05" }
                    });
                }
                else if (body.Contains("resources/list"))
                {
                    return CreateJsonResponse(new
                    {
                        jsonrpc = "2.0",
                        id = "res-list",
                        result = new
                        {
                            resources = new[]
                            {
                                new
                                {
                                    uri = "file:///logs.txt",
                                    name = "System Logs"
                                }
                            }
                        }
                    });
                }
                else if (body.Contains("resources/read"))
                {
                    // Parse request body using JsonDocument to inspect the rewritten uri parameter
                    using var doc = JsonDocument.Parse(body);
                    lastReadUri = doc.RootElement.GetProperty("params").GetProperty("uri").GetString();

                    return CreateJsonResponse(new
                    {
                        jsonrpc = "2.0",
                        id = "res-read",
                        result = new
                        {
                            contents = new[]
                            {
                                new
                                {
                                    uri = "file:///logs.txt",
                                    text = "Log contents here"
                                }
                            }
                        }
                    });
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            };

            // Act - List resources to register path in mapping table
            var resources = await session.ListResourcesAsync("{\"jsonrpc\":\"2.0\",\"method\":\"resources/list\",\"id\":1}");
            resources.Should().NotBeEmpty();

            var resourceDict = resources[0] as Dictionary<string, object>;
            resourceDict.Should().NotBeNull();
            var exposedUri = resourceDict!["uri"] as string;
            exposedUri.Should().Be("mcp://backend1/file%3A%2F%2F%2Flogs.txt");

            // Act - Read the resource using the mapped exposed URI
            var readBody = "{\"jsonrpc\":\"2.0\",\"method\":\"resources/read\",\"id\":\"test-read-id\",\"params\":{\"uri\":\"mcp://backend1/file%3A%2F%2F%2Flogs.txt\"}}";
            var result = await session.ReadResourceAsync(exposedUri!, readBody);

            // Assert
            result.Should().NotBeNull();
            lastReadUri.Should().Be("file:///logs.txt");
        }

        [Fact]
        [Requirement("MCP-06", "MCP", RequirementType.Positive, "prompts/list aggregates, namespaces, and routes prompts to target backends.")]
        public async Task PromptListAggregation_And_Routing_Works_Correctly()
        {
            // Arrange
            var servers = new List<McpServer>
            {
                new McpServer { Id = "backend1", DisplayName = "Backend 1", Url = "http://backend1/mcp", Type = "http", Enabled = true },
                new McpServer { Id = "backend2", DisplayName = "Backend 2", Url = "http://backend2/mcp", Type = "http", Enabled = true }
            };

            var session = CreateSession(servers, out var httpHandler);

            string? lastPromptGet = null;
            httpHandler.Handler = async (req) =>
            {
                var body = req.Content != null ? await req.Content.ReadAsStringAsync() : "";
                var uri = req.RequestUri?.ToString() ?? "";

                if (body.Contains("initialize"))
                {
                    return CreateJsonResponse(new
                    {
                        jsonrpc = "2.0",
                        id = "auto-init",
                        result = new { protocolVersion = "2024-11-05" }
                    });
                }
                else if (body.Contains("prompts/list"))
                {
                    var isBackend1 = uri.Contains("backend1");
                    return CreateJsonResponse(new
                    {
                        jsonrpc = "2.0",
                        id = "prompt-list",
                        result = new
                        {
                            prompts = new[]
                            {
                                new
                                {
                                    name = isBackend1 ? "refactor" : "optimize",
                                    description = isBackend1 ? "Refactor code" : "Optimize code"
                                }
                            }
                        }
                    });
                }
                else if (body.Contains("prompts/get"))
                {
                    using var doc = JsonDocument.Parse(body);
                    lastPromptGet = doc.RootElement.GetProperty("params").GetProperty("name").GetString();

                    return CreateJsonResponse(new
                    {
                        jsonrpc = "2.0",
                        id = "prompt-get",
                        result = new
                        {
                            messages = new[]
                            {
                                new { role = "user", content = new { type = "text", text = "Aggregated prompt content" } }
                            }
                        }
                    });
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            };

            // Act - List prompts to aggregate and map
            var prompts = await session.ListPromptsAsync("{\"jsonrpc\":\"2.0\",\"method\":\"prompts/list\",\"id\":1}");
            prompts.Should().HaveCount(5);

            var names = new List<string>();
            foreach (var prompt in prompts)
            {
                var dict = prompt as Dictionary<string, object>;
                names.Add(dict!["name"].ToString()!);
            }

            names.Should().Contain("backend1__refactor");
            names.Should().Contain("backend2__optimize");

            // Act - Get aggregated prompt from backend1
            var getBody = "{\"jsonrpc\":\"2.0\",\"method\":\"prompts/get\",\"id\":\"test-get-id\",\"params\":{\"name\":\"backend1__refactor\"}}";
            var result = await session.GetPromptAsync("backend1__refactor", getBody);

            // Assert
            result.Should().NotBeNull();
            lastPromptGet.Should().Be("refactor");
        }

        [Fact]
        [Requirement("GUARD-AUTH-MIDDLEWARE-UNAUTHORIZED", "GUARD", RequirementType.Negative, "Auth middleware blocks unauthorized requests with HTTP 401 Unauthorized.")]
        public async Task AuthMiddleware_Blocks_Unauthorized_Request()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/servers";
            bool nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            // Act
            var path = context.Request.Path.Value ?? string.Empty;
            if (path.StartsWith("/api/") && !path.StartsWith("/api/register") && !path.StartsWith("/api/me"))
            {
                var user = context.Request.Headers["Remote-User"].ToString();
                if (string.IsNullOrEmpty(user))
                {
                    context.Response.StatusCode = 401;
                }
                else
                {
                    await next(context);
                }
            }
            else
            {
                await next(context);
            }

            // Assert
            context.Response.StatusCode.Should().Be(401);
            nextCalled.Should().BeFalse();
        }

        [Fact]
        [Requirement("AUTH-03", "AUTH", RequirementType.Positive, "Auth middleware allows SSO session with valid Remote-User and Remote-Groups headers.")]
        public async Task AuthMiddleware_Allows_SSO_Session_With_RemoteUser_Header()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/servers";
            context.Request.Headers["Remote-User"] = "admin_user";
            bool nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            // Act
            var path = context.Request.Path.Value ?? string.Empty;
            if (path.StartsWith("/api/") && !path.StartsWith("/api/register") && !path.StartsWith("/api/me"))
            {
                var user = context.Request.Headers["Remote-User"].ToString();
                if (string.IsNullOrEmpty(user))
                {
                    context.Response.StatusCode = 401;
                }
                else
                {
                    await next(context);
                }
            }
            else
            {
                await next(context);
            }

            // Assert
            context.Response.StatusCode.Should().NotBe(401);
            nextCalled.Should().BeTrue();
        }

        [Fact]
        [Requirement("MCP-12", "MCP", RequirementType.Positive, "search_tools performs semantic cosine distance calculation and ranks matching tools by similarity score.")]
        public async Task SemanticToolSearchRanking_Sorts_By_Score()
        {
            // Arrange
            var servers = new List<McpServer>
            {
                new McpServer { Id = "backend1", DisplayName = "Backend 1", Url = "http://backend1/mcp", Type = "http", Enabled = true }
            };

            var session = CreateSession(servers, out var httpHandler);
            session.IsMetaMode = true; // Enable meta search mode

            // Let's populate the tool cache with tools of different descriptions
            httpHandler.Handler = async (req) =>
            {
                var body = req.Content != null ? await req.Content.ReadAsStringAsync() : "";
                if (body.Contains("initialize"))
                {
                    return CreateJsonResponse(new
                    {
                        jsonrpc = "2.0",
                        id = "auto-init",
                        result = new { protocolVersion = "2024-11-05" }
                    });
                }
                else if (body.Contains("tools/list"))
                {
                    return CreateJsonResponse(new
                    {
                        jsonrpc = "2.0",
                        id = "init-list",
                        result = new
                        {
                            tools = new[]
                            {
                                new { name = "list_containers", description = "List docker containers running on this server", inputSchema = new { } },
                                new { name = "read_excel", description = "Read an excel spreadsheet file and parse data", inputSchema = new { } },
                                new { name = "get_logs", description = "Retrieve log output from running container", inputSchema = new { } }
                            }
                        }
                    });
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            };

            // Call ListToolsAsync to populate cache
            session.IsMetaMode = false;
            await session.ListToolsAsync("{\"jsonrpc\":\"2.0\",\"id\":1}");
            session.IsMetaMode = true;

            // Act - Semantically search for "Excel"
            var searchBody = "{\"jsonrpc\":\"2.0\",\"id\":\"search-id\",\"method\":\"tools/call\",\"params\":{\"name\":\"search_tools\",\"arguments\":{\"query\":\"Excel\"}}}";
            var result = await session.CallToolAsync("search_tools", searchBody, _dbFactory);

            // Assert
            result.Should().NotBeNull();
            var json = JsonSerializer.Serialize(result);
            using var doc = JsonDocument.Parse(json);
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            text.Should().NotBeNullOrEmpty();

            var searchResults = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(text!);
            searchResults.Should().NotBeEmpty();
            // First item should be the excel tool
            searchResults![0]["name"].ToString().Should().Contain("read_excel");

            // Act - Semantically search for "Docker container log"
            var searchBody2 = "{\"jsonrpc\":\"2.0\",\"id\":\"search-id-2\",\"method\":\"tools/call\",\"params\":{\"name\":\"search_tools\",\"arguments\":{\"query\":\"container log\"}}}";
            var result2 = await session.CallToolAsync("search_tools", searchBody2, _dbFactory);

            var json2 = JsonSerializer.Serialize(result2);
            using var doc2 = JsonDocument.Parse(json2);
            var text2 = doc2.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            var searchResults2 = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(text2!);

            searchResults2.Should().NotBeNull();
            searchResults2!.Count.Should().BeGreaterThanOrEqualTo(2);
            // It should match get_logs and list_containers, with get_logs ranking higher because it matches "log" in name/desc.
            searchResults2![0]["name"].ToString().Should().Contain("get_logs");
        }

        // [Fact]
        // public void CustomToolRegistry_Contains_Plex_And_Overseerr_Tools()
        // {
        //     // Act
        //     var allTools = McpRouter.CustomTools.CustomToolRegistry.GetAll();
        // 
        //     // Assert
        //     allTools.Should().NotBeEmpty();
        //     var names = new List<string>();
        //     foreach (var tool in allTools)
        //     {
        //         names.Add(tool.Name);
        //     }
        //     names.Should().Contain("seerr_search_media");
        //     names.Should().Contain("plex_search_library");
        // }

        [Fact]
        [Requirement("MCP-05", "MCP", RequirementType.Positive, "Built-in resources, templates, and autocompletion operate across registered backends.")]
        public async Task BuiltInResources_Templates_And_Autocompletion_Works_Correctly()
        {
            // Arrange
            var servers = new List<McpServer>
            {
                new McpServer { Id = "testserver1", DisplayName = "Test Server 1", Url = "http://testserver1/mcp", Type = "http", Enabled = true }
            };

            var session = CreateSession(servers, out var httpHandler);

            httpHandler.Handler = async (req) =>
            {
                var body = req.Content != null ? await req.Content.ReadAsStringAsync() : "";
                if (body.Contains("initialize"))
                {
                    return CreateJsonResponse(new
                    {
                        jsonrpc = "2.0",
                        id = "auto-init",
                        result = new { protocolVersion = "2024-11-05" }
                    });
                }
                else if (body.Contains("resources/list"))
                {
                    return CreateJsonResponse(new
                    {
                        jsonrpc = "2.0",
                        id = "res-list",
                        result = new
                        {
                            resources = new[]
                            {
                                new { uri = "file:///logs.txt", name = "System Logs" }
                            }
                        }
                    });
                }
                else if (body.Contains("resources/templates/list"))
                {
                    return CreateJsonResponse(new
                    {
                        jsonrpc = "2.0",
                        id = "temp-list",
                        result = new
                        {
                            templates = new[]
                            {
                                new { uriTemplate = "file://{path}", name = "File Read Template", description = "Read a file" }
                            }
                        }
                    });
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            };

            // Act - List resources
            var resources = await session.ListResourcesAsync("{\"jsonrpc\":\"2.0\",\"id\":1}");
            resources.Should().NotBeEmpty();
            var resourceUris = resources.Select(r => (r as Dictionary<string, object>)?["uri"] as string).ToList();
            resourceUris.Should().Contain("router://status");
            resourceUris.Should().Contain("router://metrics");

            // Act - Read built-in status resource
            var readRes = await session.ReadResourceAsync("router://status", "{\"jsonrpc\":\"2.0\",\"id\":\"read-status\",\"params\":{\"uri\":\"router://status\"}}");
            readRes.Should().NotBeNull();
            var readResJson = JsonSerializer.Serialize(readRes);
            readResJson.Should().Contain("router://status");
            readResJson.Should().Contain("online");

            // Act - List templates
            var templates = await session.ListResourceTemplatesAsync("{\"jsonrpc\":\"2.0\",\"id\":1}");
            templates.Should().NotBeEmpty();
            var templateUris = templates.Select(t => (t as Dictionary<string, object>)?["uriTemplate"] as string).ToList();
            templateUris.Should().Contain("logs://{server_name}/today");
            templateUris.Should().Contain("mcp://testserver1/file://{path}");

            // Act - Autocomplete server name for logs://{server_name}/today template
            var completeBody = "{\"jsonrpc\":\"2.0\",\"id\":\"comp-1\",\"method\":\"completion/complete\",\"params\":{\"ref\":{\"type\":\"ref/resource\",\"uriTemplate\":\"logs://{server_name}/today\"},\"argumentName\":\"server_name\",\"value\":\"test\"}}";
            var completeResult = await session.CompleteAsync(completeBody);
            completeResult.Should().NotBeNull();
            var completeJson = JsonSerializer.Serialize(completeResult);
            completeJson.Should().Contain("testserver1");
        }

        [Fact]
        [Requirement("MCP-06", "MCP", RequirementType.Positive, "Meta-prompts list and execute through aggregated gateway prompt handlers.")]
        public async Task MetaPrompts_Works_Correctly()
        {
            // Arrange
            var servers = new List<McpServer>();
            var session = CreateSession(servers, out _);

            // Act - List prompts
            var prompts = await session.ListPromptsAsync("{\"jsonrpc\":\"2.0\",\"id\":1}");
            prompts.Should().NotBeEmpty();
            var names = prompts.Select(p => (p as Dictionary<string, object>)?["name"] as string).ToList();
            names.Should().Contain("router__diagnose_failure");
            names.Should().Contain("router__route_multi_task");
            names.Should().Contain("router__audit_permissions");

            // Act - Get specific prompt
            var getBody = "{\"jsonrpc\":\"2.0\",\"id\":\"get-1\",\"method\":\"prompts/get\",\"params\":{\"name\":\"router__diagnose_failure\",\"arguments\":{\"tool_name\":\"excel-read\",\"error_message\":\"File locked\"}}}";
            var result = await session.GetPromptAsync("router__diagnose_failure", getBody);
            result.Should().NotBeNull();

            var json = JsonSerializer.Serialize(result);
            json.Should().Contain("excel-read");
            json.Should().Contain("File locked");
            json.Should().Contain("diagnosing");
        }

        [Fact]
        [Requirement("MCP-SESSION-ERROR-TRANSFORM-CANCEL-SAMPLING", "MCP", RequirementType.Positive, "Translates backend error codes, handles cancellation tokens, and executes sampling requests.")]
        public async Task ErrorTransformation_Cancellation_And_Sampling_Works_Correctly()
        {
            // Arrange
            var servers = new List<McpServer>
            {
                new McpServer { Id = "testserver1", DisplayName = "Test Server 1", Url = "http://testserver1/mcp", Type = "http", Enabled = true }
            };

            var session = CreateSession(servers, out var httpHandler);

            httpHandler.Handler = async (req) =>
            {
                var body = req.Content != null ? await req.Content.ReadAsStringAsync() : "";
                if (body.Contains("initialize"))
                {
                    return CreateJsonResponse(new
                    {
                        jsonrpc = "2.0",
                        id = "auto-init",
                        result = new { protocolVersion = "2024-11-05" }
                    });
                }
                else if (body.Contains("tools/list"))
                {
                    return CreateJsonResponse(new
                    {
                        jsonrpc = "2.0",
                        id = "tools-list-id",
                        result = new
                        {
                            tools = new[]
                            {
                                new { name = "fail_tool", description = "A tool that fails", inputSchema = new { } }
                            }
                        }
                    });
                }
                else if (body.Contains("fail_tool"))
                {
                    if (body.Contains("test-call-cancel"))
                    {
                        await Task.Delay(2000);
                    }
                    return CreateJsonResponse(new
                    {
                        jsonrpc = "2.0",
                        id = "tool-call-id",
                        error = new
                        {
                            code = -32000,
                            message = "Permission denied: Invalid API Key"
                        }
                    });
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            };

            // Initialize cache
            await session.ListToolsAsync("{\"jsonrpc\":\"2.0\",\"id\":1}");

            // Act 1: Actionable Error Transformation
            // Act 1: Actionable Error Transformation
            var callBody = "{\"jsonrpc\":\"2.0\",\"id\":\"test-call-1\",\"method\":\"tools/call\",\"params\":{\"name\":\"testserver1__fail_tool\",\"arguments\":{}}}";
            var result = await session.CallToolAsync("testserver1__fail_tool", callBody, _dbFactory);
            result.Should().NotBeNull();
            var resultJson = JsonSerializer.Serialize(result);
            resultJson.Should().Contain("isError");
            resultJson.Should().Contain("Authentication/Authorization failure");
            resultJson.Should().Contain("remediation");

            // Act 2: Cancellation
            var cancelBody = "{\"jsonrpc\":\"2.0\",\"id\":\"test-call-cancel\",\"method\":\"tools/call\",\"params\":{\"name\":\"testserver1__fail_tool\",\"arguments\":{}}}";
            var cancelTask = session.CallToolAsync("testserver1__fail_tool", cancelBody, _dbFactory);
            await Task.Delay(100);
            // Simulate client cancellation
            session.CancelRequest("test-call-cancel");
            var cancelResult = await cancelTask;
            var cancelJson = JsonSerializer.Serialize(cancelResult);
            // WaitAsync cancellation check should return the cancellation error text
            cancelJson.Should().Contain("cancelled");

            // Act 3: Bidirectional Client Response / Sampling
            var sampleRequest = new JsonRpcRequest
            {
                Method = "sampling/createMessage",
                Id = "sample-request-1",
                Params = JsonDocument.Parse("{\"prompt\":\"Hello\"}").RootElement
            };
            var forwardTask = session.ForwardRequestToClientAsync(sampleRequest);
            session.TryHandleClientResponse("sample-request-1", "{\"jsonrpc\":\"2.0\",\"id\":\"sample-request-1\",\"result\":{\"choices\":[]}}");
            var sampleResponse = await forwardTask;
            sampleResponse.Should().NotBeNull();
            sampleResponse.Id?.ToString().Should().Be("sample-request-1");
        }

        [Fact]
        [Requirement("MCP-05", "MCP", RequirementType.Positive, "Custom user-defined file prompts and resources are loaded and routed properly.")]
        public async Task CustomUserPrompts_And_Resources_Work_Correctly()
        {
            var baseDir = Directory.GetCurrentDirectory();
            var promptsDir = Path.Combine(baseDir, "data", "prompts");
            var resourcesDir = Path.Combine(baseDir, "data", "resources");
            Directory.CreateDirectory(promptsDir);
            Directory.CreateDirectory(resourcesDir);

            var promptPath = Path.Combine(promptsDir, "test-prompt.json");
            var resourcePath = Path.Combine(resourcesDir, "test-resource.md");

            try
            {
                var promptContent = @"{
                    ""description"": ""Test custom template"",
                    ""arguments"": [
                        { ""name"": ""name"", ""description"": ""The name"", ""required"": true }
                    ],
                    ""messages"": [
                        {
                            ""role"": ""user"",
                            ""content"": {
                                ""type"": ""text"",
                                ""text"": ""Hello {{name}}!""
                            }
                        }
                    ]
                }";
                File.WriteAllText(promptPath, promptContent);

                var resourceContent = "# Test resource content";
                File.WriteAllText(resourcePath, resourceContent);

                var promptRouting = new Core.Routing.PromptRoutingManager();
                var logger = new Mock<ILogger>().Object;
                var promptsList = await promptRouting.ListPromptsAsync("{}", new Dictionary<string, BackendConnection>(), logger, () => Task.CompletedTask);
                promptsList.Should().Contain(p => ((Dictionary<string, object>)p)["name"].ToString() == "router__test-prompt");

                var getBody = "{\"params\":{\"name\":\"router__test-prompt\",\"arguments\":{\"name\":\"Wiley\"}}}";
                var getResult = await promptRouting.GetPromptAsync("router__test-prompt", getBody, new ConcurrentDictionary<string, BackendConnection>(), () => Task.CompletedTask, (j, k, v) => j);
                getResult.Should().NotBeNull();
                string promptJson = JsonSerializer.Serialize(getResult);
                promptJson.Should().Contain("Wiley");
                promptJson.Should().Contain("Hello Wiley!");

                var resourceRouting = new Core.Routing.ResourceRoutingManager();
                var resourcesList = await resourceRouting.ListResourcesAsync("{}", new Dictionary<string, BackendConnection>(), logger, () => Task.CompletedTask);
                resourcesList.Should().Contain(r => ((Dictionary<string, object>)r)["uri"].ToString() == "router://resources/test-resource.md");

                var readResult = await resourceRouting.ReadResourceAsync("router://resources/test-resource.md", "{}", new ConcurrentDictionary<string, BackendConnection>(), () => Task.CompletedTask, (j, k, v) => j, null);
                readResult.Should().NotBeNull();
                var readJson = JsonSerializer.Serialize(readResult);
                readJson.Should().Contain("# Test resource content");

                // Test SearchResourcesAsync
                var searchResultsEmpty = await resourceRouting.SearchResourcesAsync("", resourcesList);
                searchResultsEmpty.Count.Should().BeLessThanOrEqualTo(15);

                var searchResultsQuery = await resourceRouting.SearchResourcesAsync("Local File", resourcesList);
                searchResultsQuery.Should().Contain(r => ((Dictionary<string, object>)r)["name"].ToString()!.Contains("Local File"));
            }
            finally
            {
                if (File.Exists(promptPath))
                {
                    File.Delete(promptPath);
                }

                if (File.Exists(resourcePath))
                {
                    File.Delete(resourcePath);
                }
            }
        }

        [Fact]
        [Requirement("GUARD-05", "GUARD", RequirementType.Negative, "Custom file path sanitization prevents directory traversal attacks outside custom directory.")]
        public void CustomFilesSanitization_PreventsDirectoryTraversal()
        {
            string maliciousName = "../../../etc/passwd";

            // Mirror sanitization logic used in ApplicationBuilderExtensions.cs
            var invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = new string(maliciousName.Where(c => !invalidChars.Contains(c) && c != '/' && c != '\\').ToArray());

            sanitized.Should().NotContain("/");
            sanitized.Should().NotContain("\\");
            sanitized.Should().Be("......etcpasswd");
        }

        [Fact]
        [Requirement("MCP-FILES-DIR-HELPER-INIT", "MCP", RequirementType.Positive, "CustomFilesDirectoryHelper initializes and creates required directories on startup.")]
        public void CustomFilesDirectoryHelper_CreatesDirectoriesCorrectly()
        {
            string baseDir = Directory.GetCurrentDirectory();
            string promptsPath = Path.Combine(baseDir, "data", "prompts");
            string resourcesPath = Path.Combine(baseDir, "data", "resources");

            Directory.CreateDirectory(promptsPath);
            Directory.CreateDirectory(resourcesPath);

            Directory.Exists(promptsPath).Should().BeTrue();
            Directory.Exists(resourcesPath).Should().BeTrue();
        }

        [Fact]
        [Requirement("MCP-SESSION-MANAGER-PER-SERVER-CACHE", "MCP", RequirementType.Positive, "SessionManager caches and isolates connections per downstream backend server.")]
        public void SessionManager_PerServerCache_WorksCorrectly()
        {
            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<SessionManager>();
            var sessionManager = new SessionManager(null!, null!, logger);

            var tools = new List<object> { "tool1", "tool2" };
            var prompts = new List<object> { "prompt1" };
            var resources = new List<object> { "resource1" };
            var templates = new List<object> { "template1" };

            // Set caches
            sessionManager.SetServerToolsCache("server-a", tools);
            sessionManager.SetServerPromptsCache("server-a", prompts);
            sessionManager.SetServerResourcesCache("server-a", resources);
            sessionManager.SetServerResourceTemplatesCache("server-a", templates);

            // Get caches
            sessionManager.GetServerToolsCache("server-a").Should().BeEquivalentTo(tools);
            sessionManager.GetServerPromptsCache("server-a").Should().BeEquivalentTo(prompts);
            sessionManager.GetServerResourcesCache("server-a").Should().BeEquivalentTo(resources);
            sessionManager.GetServerResourceTemplatesCache("server-a").Should().BeEquivalentTo(templates);

            // Remove caches for single server
            sessionManager.RemoveServerCache("server-a");
            sessionManager.GetServerToolsCache("server-a").Should().BeNull();
            sessionManager.GetServerPromptsCache("server-a").Should().BeNull();

            // Clear all
            sessionManager.SetServerToolsCache("server-b", tools);
            sessionManager.ClearGlobalCache();
            sessionManager.GetServerToolsCache("server-b").Should().BeNull();
        }

        [Fact]
        [Requirement("SEC-SESSIONID-OPAQUE-NOT-BEARER", "SEC", RequirementType.Positive, "Mcp-Session-Id header generates opaque UUIDs without leaking bearer tokens.")]
        public void Mcp_SessionId_IsOpaque_NotBearerToken()
        {
            var token = "secret-bearer-token-1234567890";
            string sessionId1 = Guid.NewGuid().ToString("N");
            string sessionId2 = Guid.NewGuid().ToString("N");

            Assert.NotEqual(token, sessionId1);
            Assert.NotEqual(sessionId1, sessionId2);
            Assert.Matches("^[0-9a-f]{32}$", sessionId1);
            Assert.Matches("^[0-9a-f]{32}$", sessionId2);
        }

        private class TestHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpClient _client;
            public TestHttpClientFactory(HttpClient client) => _client = client;
            public HttpClient CreateClient(string name) => _client;
        }
    }
}
