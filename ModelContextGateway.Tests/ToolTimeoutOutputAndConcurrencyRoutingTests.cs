using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextGateway.Core.CodeMode;
using ModelContextGateway.Tests.TestHelpers;
using Moq;

namespace ModelContextGateway.Tests
{
    public class ToolTimeoutOutputAndConcurrencyRoutingTests
    {
        private sealed class DelayAwareMcpHandler : HttpMessageHandler
        {
            public Func<HttpRequestMessage, string, CancellationToken, Task<HttpResponseMessage>> Handler { get; }

            public DelayAwareMcpHandler(Func<HttpRequestMessage, string, CancellationToken, Task<HttpResponseMessage>> handler)
            {
                Handler = handler;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = request.Content != null
                    ? await request.Content.ReadAsStringAsync(cancellationToken)
                    : string.Empty;
                return await Handler(request, body, cancellationToken);
            }
        }

        private static (SqliteConnection Conn, IDbConnectionFactory Factory) CreateInMemoryDb()
        {
            var connString = $"Data Source=mem_timeout_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            var conn = new SqliteConnection(connString);
            conn.Open();
            DatabaseInitializer.InitializeDatabase(conn);

            var mockFactory = new Mock<IDbConnectionFactory>();
            mockFactory.Setup(f => f.CreateConnection()).Returns(() => new SqliteConnection(connString));
            mockFactory.Setup(f => f.ProviderName).Returns("sqlite");
            return (conn, mockFactory.Object);
        }

        private static DynamicEmbeddingService CreateSettingsService(RouterSettings settings)
        {
            var sp = new ServiceCollection().BuildServiceProvider();
            var mock = new Mock<DynamicEmbeddingService>(new HttpClient(), NullLoggerFactory.Instance, sp);
            mock.Setup(s => s.GetSettings()).Returns(settings);
            return mock.Object;
        }

        private sealed class NullAuditLogger : IAuditLogger
        {
            public Task LogInvocationAsync(string requestId, string userPrincipalName, string userSid, string serverCodeName, string itemName, string requestMethod, int executionTimeMs, int statusCode, string? requestPayload = null, string? responsePayload = null, string? errorMessage = null) => Task.CompletedTask;
            public Task LogAdminActionAsync(string username, string action, string target, string details, bool success, string? errorMessage = null) => Task.CompletedTask;
        }

        [Fact]
        [Requirement("MCP-TIMEOUT-INFINITE-HTTPCLIENT", "MCP", RequirementType.Positive, "Named McpClient HttpClient uses Timeout.InfiniteTimeSpan so per-request CancellationToken governs tool timeouts.")]
        public void AddModelContextGatewayServices_ConfiguresMcpClientWithInfiniteTimeout()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing"
            });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DB_ENCRYPTION_KEY"] = "TestSecretKey1234567890123456789012"
            });
            builder.AddModelContextGatewayServices();

            using var app = builder.Build();
            var factory = app.Services.GetRequiredService<IHttpClientFactory>();
            using var client = factory.CreateClient("McpClient");

            client.Timeout.Should().Be(Timeout.InfiniteTimeSpan);
        }

        [Fact]
        [Requirement("MCP-TIMEOUT-HIERARCHY", "MCP", RequirementType.Positive, "Timeout resolution honors per-call > HTTP header/query > server > global > 120s and strips MCG meta-parameters before forwarding.")]
        public async Task ExecuteTool_TimeoutHierarchy_AndMetaParamStripping_WorksEndToEnd()
        {
            var (conn, dbFactory) = CreateInMemoryDb();
            using var dbConn = conn;

            string? forwardedToolBody = null;
            var handler = new DelayAwareMcpHandler(async (req, body, ct) =>
            {
                using var doc = JsonDocument.Parse(body);
                var method = doc.RootElement.GetProperty("method").GetString();
                var id = doc.RootElement.GetProperty("id").GetRawText();

                if (method == "initialize")
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{{}}}}}}", Encoding.UTF8, "application/json")
                    };
                }

                if (method == "tools/call")
                {
                    forwardedToolBody = body;
                    var delayMs = doc.RootElement.GetProperty("params").GetProperty("arguments").GetProperty("delayMs").GetInt32();
                    await Task.Delay(delayMs, ct);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{{\"content\":[{{\"type\":\"text\",\"text\":\"typed_ok\"}}]}}}}", Encoding.UTF8, "application/json")
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{{}}}}", Encoding.UTF8, "application/json")
                };
            });

            var server = new McpServer
            {
                Id = "pw",
                DisplayName = "Playwright",
                Type = "http",
                Url = "http://pw:3000/mcp",
                Enabled = true,
                RequestTimeoutSeconds = 1 // Low server timeout (1s) that per-call override (5s) must beat
            };

            var httpClient = new HttpClient(handler);
            var backendConn = new BackendConnection(server, httpClient, NullLogger.Instance);
            await backendConn.ConnectAsync();

            var connections = new ConcurrentDictionary<string, BackendConnection>();
            connections[server.Id] = backendConn;

            var settings = new RouterSettings { ToolExecutionTimeoutSeconds = 1 };
            var embService = CreateSettingsService(settings);
            var router = new ToolRoutingManager(outputBuffer: new ToolOutputBuffer(), concurrencyGate: new BackendConcurrencyGate());

            // 1. Per-call timeout_seconds (5s) overrides 1s server & global timeout -> 1.3s operation succeeds and strips meta-params
            var successCall = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "req-ok",
                method = "tools/call",
                @params = new
                {
                    name = "execute_tool",
                    arguments = new
                    {
                        name = "pw__browser_type",
                        timeout_seconds = 5,
                        max_chars = 500,
                        offset = 0,
                        concurrency_key = "main",
                        arguments = new { delayMs = 1250, text = "long-sku-payload" }
                    }
                }
            });

            var okResp = await router.CallToolAsync(
                "execute_tool",
                successCall,
                dbFactory,
                connections,
                new[] { server },
                NullLogger.Instance,
                httpClient,
                embService,
                () => Task.CompletedTask,
                (b, k, v) => b);

            var okJson = JsonSerializer.Serialize(okResp);
            using (var okDoc = JsonDocument.Parse(okJson))
            {
                okDoc.RootElement.TryGetProperty("isError", out var errProp).Should().BeFalse();
                okDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString().Should().Be("typed_ok");
            }

            // Verify meta-parameters were NOT forwarded in downstream tool arguments
            forwardedToolBody.Should().NotBeNull();
            using (var fwdDoc = JsonDocument.Parse(forwardedToolBody!))
            {
                var fwdArgs = fwdDoc.RootElement.GetProperty("params").GetProperty("arguments");
                fwdArgs.TryGetProperty("timeout_seconds", out _).Should().BeFalse();
                fwdArgs.TryGetProperty("max_chars", out _).Should().BeFalse();
                fwdArgs.TryGetProperty("offset", out _).Should().BeFalse();
                fwdArgs.TryGetProperty("concurrency_key", out _).Should().BeFalse();
                fwdArgs.GetProperty("text").GetString().Should().Be("long-sku-payload");
            }

            // 2. Without per-call override, server.RequestTimeoutSeconds = 1s triggers explicit TimeoutException error message
            var timeoutCall = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "req-timeout",
                method = "tools/call",
                @params = new
                {
                    name = "execute_tool",
                    arguments = new
                    {
                        name = "pw__browser_type",
                        arguments = new { delayMs = 4000, text = "too-slow" }
                    }
                }
            });

            var timeoutResp = await router.CallToolAsync(
                "execute_tool",
                timeoutCall,
                dbFactory,
                connections,
                new[] { server },
                NullLogger.Instance,
                httpClient,
                embService,
                () => Task.CompletedTask,
                (b, k, v) => b);

            var timeoutJson = JsonSerializer.Serialize(timeoutResp);
            using (var toDoc = JsonDocument.Parse(timeoutJson))
            {
                toDoc.RootElement.GetProperty("isError").GetBoolean().Should().BeTrue();
                toDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString().Should().Contain("timed out");
            }

            // 3. HTTP header override X-MCG-Timeout-Seconds via ClientSession beats server's 1s timeout
            var services = new ServiceCollection();
            services.AddSingleton(dbFactory);
            services.AddSingleton<IAuditLogger, NullAuditLogger>();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DB_ENCRYPTION_KEY"] = "TestSecretKey1234567890123456789012",
                ["Admin:GroupSid"] = "full_admin"
            }).Build());
            services.AddSingleton(new ToolOutputBuffer());
            services.AddSingleton(new BackendConcurrencyGate());
            services.AddLogging();
            var sp = services.BuildServiceProvider();

            var httpCtx = new DefaultHttpContext { RequestServices = sp };
            httpCtx.Request.Headers["X-MCG-Timeout-Seconds"] = "5";
            httpCtx.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "steve"),
                new System.Security.Claims.Claim("Sid", "sid-steve"),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "full_admin")
            }, "TestAuth"));

            var clientSession = new ClientSession(
                "sess-timeout-hdr",
                null!,
                new List<McpServer> { server },
                httpClient,
                embService,
                null,
                NullLogger<ClientSession>.Instance,
                sp);

            // Pre-register backend connection into session via reflection or CallToolAsync after initialize
            var backendField = typeof(ClientSession).GetField("_backendConnections", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var sessionConns = (ConcurrentDictionary<string, BackendConnection>)backendField!.GetValue(clientSession)!;
            sessionConns[server.Id] = backendConn;
            clientSession.StartInitialization("{\"jsonrpc\":\"2.0\",\"method\":\"initialize\",\"id\":1}");

            var hdrCall = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "req-hdr",
                method = "tools/call",
                @params = new
                {
                    name = "execute_tool",
                    arguments = new
                    {
                        name = "pw__browser_type",
                        arguments = new { delayMs = 1250, text = "header-override-ok" }
                    }
                }
            });

            var hdrResp = await clientSession.CallToolAsync("execute_tool", hdrCall, dbFactory, httpCtx);
            var hdrJson = JsonSerializer.Serialize(hdrResp);
            hdrJson.Should().Contain("typed_ok");
        }

        [Fact]
        [Requirement("MCP-DISCOVERY-LEAN-FORMATS", "MCP", RequirementType.Positive, "search_tools supports format='typescript'|'compact'|'names' without bloated inputSchema blobs, server filtering, and output paging.")]
        public async Task SearchTools_LeanFormats_ServerFilter_AndPaging_WorkAsExpected()
        {
            var (conn, dbFactory) = CreateInMemoryDb();
            using var dbConn = conn;

            var pwMock = new MockDownstreamMcpServer();
            pwMock.Tools.Clear();
            pwMock.AddTool("browser_type", "Types characters into a browser field", new
            {
                type = "object",
                properties = new
                {
                    selector = new { type = "string", description = "CSS selector" },
                    text = new { type = "string", description = "Text to type" },
                    role = new { type = "string", description = "Browser slot role" }
                },
                required = new[] { "selector", "text" }
            });
            pwMock.AddTool("browser_snapshot", "Captures DOM snapshot of the active browser tab");

            var dockerMock = new MockDownstreamMcpServer();
            dockerMock.Tools.Clear();
            dockerMock.AddTool("browser_container_logs", "Reads browser container logs from docker");

            var srvPw = new McpServer { Id = "pw", DisplayName = "Playwright", Type = "http", Url = "http://pw/mcp", Enabled = true };
            var srvDocker = new McpServer { Id = "docker", DisplayName = "Docker", Type = "http", Url = "http://docker/mcp", Enabled = true };

            var connections = new Dictionary<string, BackendConnection>
            {
                ["pw"] = new BackendConnection(srvPw, pwMock.CreateHttpClient(), NullLogger.Instance),
                ["docker"] = new BackendConnection(srvDocker, dockerMock.CreateHttpClient(), NullLogger.Instance)
            };

            var outputBuffer = new ToolOutputBuffer();
            var router = new ToolRoutingManager(outputBuffer: outputBuffer, concurrencyGate: new BackendConcurrencyGate());
            await router.ListToolsAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}", false, connections, NullLogger.Instance, () => Task.CompletedTask, new[] { srvPw, srvDocker });

            var concurrentConns = new ConcurrentDictionary<string, BackendConnection>(connections);
            var embService = CreateSettingsService(new RouterSettings { ToolSearchDefaultLimit = 10 });

            // 1. format='typescript' + server='pw' -> no inputSchema in structuredContent, only pw tools
            var tsReq = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "s-ts",
                method = "tools/call",
                @params = new
                {
                    name = "search_tools",
                    arguments = new { query = "browser", server = "pw", format = "typescript" }
                }
            });

            var tsResp = await router.CallToolAsync("search_tools", tsReq, dbFactory, concurrentConns, new[] { srvPw, srvDocker }, NullLogger.Instance, new HttpClient(), embService, () => Task.CompletedTask, (b, k, v) => b, clientSessionId: "s1");
            var tsJson = JsonSerializer.Serialize(tsResp);
            using (var tsDoc = JsonDocument.Parse(tsJson))
            {
                var sc = tsDoc.RootElement.GetProperty("structuredContent");
                sc.GetProperty("format").GetString().Should().Be("typescript");
                var toolsArr = sc.GetProperty("tools");
                toolsArr.GetArrayLength().Should().Be(2);
                toolsArr.GetRawText().Should().NotContain("inputSchema");
                toolsArr.GetRawText().Should().NotContain("docker");
            }

            // 2. format='compact' -> param names array, no inputSchema
            var compactReq = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "s-compact",
                method = "tools/call",
                @params = new
                {
                    name = "search_tools",
                    arguments = new { query = "browser", server = "pw", format = "compact" }
                }
            });

            var compactResp = await router.CallToolAsync("search_tools", compactReq, dbFactory, concurrentConns, new[] { srvPw, srvDocker }, NullLogger.Instance, new HttpClient(), embService, () => Task.CompletedTask, (b, k, v) => b, clientSessionId: "s1");
            using (var cDoc = JsonDocument.Parse(JsonSerializer.Serialize(compactResp)))
            {
                var sc = cDoc.RootElement.GetProperty("structuredContent");
                sc.GetProperty("format").GetString().Should().Be("compact");
                sc.GetRawText().Should().NotContain("inputSchema");
                sc.GetRawText().Should().Contain("selector");
            }

            // 3. format='names' -> newline-separated tool names
            var namesReq = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "s-names",
                method = "tools/call",
                @params = new
                {
                    name = "search_tools",
                    arguments = new { query = "browser", format = "names" }
                }
            });
            var namesResp = await router.CallToolAsync("search_tools", namesReq, dbFactory, concurrentConns, new[] { srvPw, srvDocker }, NullLogger.Instance, new HttpClient(), embService, () => Task.CompletedTask, (b, k, v) => b, clientSessionId: "s1");
            using (var nDoc = JsonDocument.Parse(JsonSerializer.Serialize(namesResp)))
            {
                var text = nDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
                text.Should().Contain("pw/browser_type");
                text.Should().Contain("docker/browser_container_logs");
            }

            // 4. max_chars caps large search output and exposes output_id for read_output
            var pagedReq = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "s-paged",
                method = "tools/call",
                @params = new
                {
                    name = "search_tools",
                    arguments = new { query = "browser", format = "json", max_chars = 250 }
                }
            });
            var pagedResp = await router.CallToolAsync("search_tools", pagedReq, dbFactory, concurrentConns, new[] { srvPw, srvDocker }, NullLogger.Instance, new HttpClient(), embService, () => Task.CompletedTask, (b, k, v) => b, ownerKey: "sid:search-owner");
            using var pDoc = JsonDocument.Parse(JsonSerializer.Serialize(pagedResp));
            var pagedText = pDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            pagedText.Length.Should().BeLessThanOrEqualTo(250);
            var outId = pDoc.RootElement.GetProperty("structuredContent").GetProperty("output_id").GetString()!;
            var nextOffset = pDoc.RootElement.GetProperty("structuredContent").GetProperty("next_offset").GetInt32();

            var readReq = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "r-1",
                method = "tools/call",
                @params = new
                {
                    name = "read_output",
                    arguments = new { output_id = outId, offset = nextOffset, max_chars = 2000 }
                }
            });
            var readResp = await router.CallToolAsync("read_output", readReq, dbFactory, concurrentConns, new[] { srvPw, srvDocker }, NullLogger.Instance, new HttpClient(), embService, () => Task.CompletedTask, (b, k, v) => b, ownerKey: "sid:search-owner");
            using var rDoc = JsonDocument.Parse(JsonSerializer.Serialize(readResp));
            rDoc.RootElement.TryGetProperty("isError", out _).Should().BeFalse();
            rDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString().Should().NotBeEmpty();
        }

        [Fact]
        [Requirement("MCP-OUTPUT-READ-TOOL-ISOLATION", "MCP", RequirementType.Positive, "execute_tool buffers large output into read_output and isolates buffered reads per session/owner.")]
        public async Task ExecuteTool_LargeOutput_BuffersAndEnforcesSessionIsolation()
        {
            var (conn, dbFactory) = CreateInMemoryDb();
            using var dbConn = conn;

            var bigLines = string.Join("\n", Enumerable.Range(1, 150).Select(i => $"PART-{i:D3}: McMaster 91251A10{i % 10} Hex Socket Screw"));
            var mockDownstream = new MockDownstreamMcpServer();
            mockDownstream.Tools.Clear();
            mockDownstream.AddTool("browser_read_text", "Reads full page text");
            mockDownstream.ToolCallHandler = (_, _) => new
            {
                content = new[] { new { type = "text", text = bigLines } }
            };

            var server = new McpServer { Id = "pw", DisplayName = "Playwright", Type = "http", Url = "http://pw/mcp", Enabled = true };
            var client = mockDownstream.CreateHttpClient();
            var backendConn = new BackendConnection(server, client, NullLogger.Instance);
            var connections = new ConcurrentDictionary<string, BackendConnection> { ["pw"] = backendConn };

            var outputBuffer = new ToolOutputBuffer();
            var router = new ToolRoutingManager(outputBuffer: outputBuffer, concurrencyGate: new BackendConcurrencyGate());
            var embService = CreateSettingsService(new RouterSettings { MaxToolOutputChars = 600 });

            var execCall = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "exec-1",
                method = "tools/call",
                @params = new
                {
                    name = "execute_tool",
                    arguments = new
                    {
                        name = "pw__browser_read_text",
                        arguments = new { }
                    }
                }
            });

            var resp = await router.CallToolAsync("execute_tool", execCall, dbFactory, connections, new[] { server }, NullLogger.Instance, client, embService, () => Task.CompletedTask, (b, k, v) => b, ownerKey: "sid:owner-1");
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(resp));

            var firstText = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            firstText.Length.Should().BeLessThanOrEqualTo(600);
            var outId = doc.RootElement.GetProperty("structuredContent").GetProperty("output_id").GetString()!;
            var nextOffset = doc.RootElement.GetProperty("structuredContent").GetProperty("next_offset").GetInt32();

            // Owner-1 can read next chunk via read_output
            var readCall = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "read-1",
                method = "tools/call",
                @params = new
                {
                    name = "read_output",
                    arguments = new { output_id = outId, offset = nextOffset, max_chars = 1000 }
                }
            });

            var ownerRead = await router.CallToolAsync("read_output", readCall, dbFactory, connections, new[] { server }, NullLogger.Instance, client, embService, () => Task.CompletedTask, (b, k, v) => b, ownerKey: "sid:owner-1");
            using var ownerDoc = JsonDocument.Parse(JsonSerializer.Serialize(ownerRead));
            ownerDoc.RootElement.TryGetProperty("isError", out _).Should().BeFalse();
            ownerDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString().Should().Contain("PART-");

            // Owner-2 cannot read Owner-1's output
            var otherRead = await router.CallToolAsync("read_output", readCall, dbFactory, connections, new[] { server }, NullLogger.Instance, client, embService, () => Task.CompletedTask, (b, k, v) => b, ownerKey: "sid:owner-2");
            using var otherDoc = JsonDocument.Parse(JsonSerializer.Serialize(otherRead));
            otherDoc.RootElement.GetProperty("isError").GetBoolean().Should().BeTrue();
        }

        [Fact]
        [Requirement("TRANS-STDIO-CONCURRENT-SAME-ID", "TRANS", RequirementType.Positive, "StdioTransport isolates concurrent requests sharing identical JSON-RPC IDs and completes all callers accurately.")]
        public async Task StdioTransport_ConcurrentCallsWithIdenticalJsonRpcIds_CompleteAccurately()
        {
            var scriptPath = new[]
            {
                "mock_stdio.js",
                "ModelContextGateway.Tests/mock_stdio.js",
                "../ModelContextGateway.Tests/mock_stdio.js",
                "../../ModelContextGateway.Tests/mock_stdio.js",
                "../../../ModelContextGateway.Tests/mock_stdio.js",
                "../../../../ModelContextGateway.Tests/mock_stdio.js"
            }.Select(Path.GetFullPath).First(File.Exists);

            var server = new McpServer
            {
                Id = "stdio-concurrent",
                Url = $"node \"{scriptPath}\"",
                Type = "stdio",
                Enabled = true
            };

            using var transport = new StdioTransport(server, NullLogger.Instance, new JsonRpcStateManager());
            await transport.ConnectAsync();
            transport.StartReader(_ => Task.CompletedTask);

            var initResp = await transport.SendRequestAsync("initialize", "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}");
            initResp.Error.Should().BeNull();

            // Send 8 concurrent requests all sharing JSON-RPC id=1
            var tasks = Enumerable.Range(1, 8).Select(async idx =>
            {
                var payload = JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    method = "tools/call",
                    @params = new { name = "echo", arguments = new { message = $"msg-{idx}" } }
                });
                var resp = await transport.SendRequestAsync("tools/call", payload);
                return (idx, resp);
            }).ToArray();

            var results = await Task.WhenAll(tasks);
            foreach (var (idx, resp) in results)
            {
                resp.Error.Should().BeNull();
                resp.Result.Should().NotBeNull();
                resp.Result!.Value.ToString().Should().Contain($"msg-{idx}");
            }
        }

        [Fact]
        [Requirement("MCP-CODE-CONCURRENT-OPTIONS-READOUTPUT", "MCP", RequirementType.Positive, "CodeModeExecutionEngine supports Promise.all concurrent tool calls with McgCallOptions and mcg.readOutput.")]
        public async Task CodeModeExecutionEngine_PromiseAll_AndMcgCallOptions_AndReadOutput_WorkSafely()
        {
            var outputBuffer = new ToolOutputBuffer();
            var routerSettings = new RouterSettings
            {
                EnableCodeMode = true,
                CodeModeExecutionTimeoutSeconds = 10,
                CodeModeMaxToolCalls = 20
            };

            var observedOptions = new ConcurrentBag<(string Name, CodeModeCallOptions? Options)>();
            string storedBigText = string.Join("\n", Enumerable.Range(1, 80).Select(i => $"Row {i:D2}: Part-Alpha-{i:D3}"));

            var callbacks = new CodeModeSandboxCallbacks
            {
                CallToolWithOptionsAsync = async (toolName, toolArgs, options, ct) =>
                {
                    observedOptions.Add((toolName, options));
                    await Task.Delay(15, ct);
                    if (toolName == "pw__read_big")
                    {
                        var shaped = outputBuffer.ShapeText("sid:cm", toolName, storedBigText, options?.MaxChars ?? 300, options?.Offset, options?.Filter);
                        return new
                        {
                            resultType = "complete",
                            structuredContent = new
                            {
                                truncated = shaped.WasTruncated,
                                output_id = shaped.OutputId,
                                total_chars = shaped.TotalChars,
                                next_offset = shaped.NextOffset,
                                has_more = shaped.HasMore
                            },
                            content = new[] { new { type = "text", text = shaped.Text } }
                        };
                    }

                    return new
                    {
                        content = new[] { new { type = "text", text = JsonSerializer.Serialize(new { echo = toolName, key = options?.ConcurrencyKey }) } }
                    };
                },
                ReadOutputAsync = async (outputId, offset, maxChars) =>
                {
                    await Task.Yield();
                    outputBuffer.TryReadChunk("sid:cm", outputId, offset ?? 0, maxChars ?? 4000, out var chunk).Should().BeTrue();
                    return new
                    {
                        output_id = chunk!.OutputId,
                        text = chunk.Text,
                        has_more = chunk.HasMore,
                        next_offset = chunk.NextOffset
                    };
                }
            };

            var script = @"
                const [a, b, c] = await Promise.all([
                    mcg.callTool('pw__click', { selector: '#a' }, { timeoutSeconds: 45, concurrencyKey: 'main' }),
                    mcg.callTool('pw__click', { selector: '#b' }, { timeoutSeconds: 45, concurrencyKey: 'support' }),
                    mcg.callTool('pw__read_big', {}, { maxChars: 260 })
                ]);
                console.log('first big snippet length:', c.length);
                const match = c.match(/output_id=""([^""]+)""/);
                const nextMatch = c.match(/next_offset=(\d+)/);
                const chunk2 = await mcg.readOutput(match[1], { offset: Number(nextMatch[1]), maxChars: 500 });
                return { a, b, hasOutputId: !!match, chunk2TextLen: chunk2.text.length };
            ";

            var engine = new CodeModeExecutionEngine();
            var execResult = await engine.ExecuteAsync(
                script,
                null,
                routerSettings,
                (name, args, token) => Task.FromResult<object>(new { }),
                CancellationToken.None,
                callbacks);

            execResult.IsError.Should().BeFalse(execResult.ErrorMessage);
            execResult.ToolCallsCount.Should().Be(4);
            execResult.Logs.Should().Contain(l => l.Contains("first big snippet length:"));

            using var resDoc = JsonDocument.Parse(execResult.ResultJson!);
            resDoc.RootElement.GetProperty("a").GetProperty("key").GetString().Should().Be("main");
            resDoc.RootElement.GetProperty("b").GetProperty("key").GetString().Should().Be("support");
            resDoc.RootElement.GetProperty("hasOutputId").GetBoolean().Should().BeTrue();
            resDoc.RootElement.GetProperty("chunk2TextLen").GetInt32().Should().BeGreaterThan(0);
            observedOptions.Should().Contain(x => x.Name == "pw__read_big" && x.Options != null && x.Options.MaxChars == 260);
        }

        [Fact]
        [Requirement("MCP-CONCURRENCY-ROUTING-E2E", "MCP", RequirementType.Positive, "execute_tool enforces per-slot FIFO serialization on 'main' while allowing 'support' slot to execute concurrently.")]
        public async Task ExecuteTool_ConcurrencyGate_SerializesSameSlotAndParallelizesDistinctSlots()
        {
            var (conn, dbFactory) = CreateInMemoryDb();
            using var dbConn = conn;

            int activeMain = 0;
            int maxActiveMain = 0;
            var main1Entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var supportEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseMain1 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var handler = new DelayAwareMcpHandler(async (req, body, ct) =>
            {
                using var doc = JsonDocument.Parse(body);
                var method = doc.RootElement.GetProperty("method").GetString();
                var id = doc.RootElement.GetProperty("id").GetRawText();

                if (method == "initialize")
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{{}}}}}}", Encoding.UTF8, "application/json")
                    };
                }

                var args = doc.RootElement.GetProperty("params").GetProperty("arguments");
                var role = args.GetProperty("role").GetString()!;
                var tag = args.GetProperty("tag").GetString()!;

                if (role == "main")
                {
                    int cur = Interlocked.Increment(ref activeMain);
                    maxActiveMain = Math.Max(maxActiveMain, cur);
                    if (tag == "m1")
                    {
                        main1Entered.TrySetResult();
                        await releaseMain1.Task.WaitAsync(ct);
                    }
                    else
                    {
                        await Task.Delay(20, ct);
                    }
                    Interlocked.Decrement(ref activeMain);
                }
                else if (role == "support")
                {
                    supportEntered.TrySetResult();
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{{\"content\":[{{\"type\":\"text\",\"text\":\"{tag}-ok\"}}]}}}}", Encoding.UTF8, "application/json")
                };
            });

            var server = new McpServer
            {
                Id = "pw",
                DisplayName = "Playwright",
                Type = "http",
                Url = "http://pw/mcp",
                Enabled = true,
                MaxConcurrentRequests = 1,
                ConcurrencyKeyField = "role,browser"
            };

            var client = new HttpClient(handler);
            var backendConn = new BackendConnection(server, client, NullLogger.Instance);
            await backendConn.ConnectAsync();

            var connections = new ConcurrentDictionary<string, BackendConnection> { ["pw"] = backendConn };
            var router = new ToolRoutingManager(outputBuffer: new ToolOutputBuffer(), concurrencyGate: new BackendConcurrencyGate());
            var embService = CreateSettingsService(new RouterSettings());

            string BuildCall(string id, string role, string tag) => JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id,
                method = "tools/call",
                @params = new
                {
                    name = "execute_tool",
                    arguments = new
                    {
                        name = "pw__browser_snapshot",
                        arguments = new { role, tag }
                    }
                }
            });

            var m1Task = router.CallToolAsync("execute_tool", BuildCall("c1", "main", "m1"), dbFactory, connections, new[] { server }, NullLogger.Instance, client, embService, () => Task.CompletedTask, (b, k, v) => b);
            await main1Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

            // m2 on 'main' must queue behind m1; s1 on 'support' must run concurrently while m1 is still blocked
            var m2Task = router.CallToolAsync("execute_tool", BuildCall("c2", "main", "m2"), dbFactory, connections, new[] { server }, NullLogger.Instance, client, embService, () => Task.CompletedTask, (b, k, v) => b);
            var s1Task = router.CallToolAsync("execute_tool", BuildCall("c3", "support", "s1"), dbFactory, connections, new[] { server }, NullLogger.Instance, client, embService, () => Task.CompletedTask, (b, k, v) => b);

            await supportEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await s1Task;
            m2Task.IsCompleted.Should().BeFalse();

            releaseMain1.SetResult();
            await Task.WhenAll(m1Task, m2Task);
            maxActiveMain.Should().Be(1);
        }

        [Fact]
        [Requirement("MCP-PERSISTENCE-TIMEOUT-OUTPUT-CONCURRENCY", "MCP", RequirementType.Positive, "DatabaseRepository persists and retrieves server timeout/concurrency fields and global tool timeout/output settings.")]
        public async Task DatabaseRepository_PersistsTimeoutOutputAndConcurrencySettings()
        {
            var (conn, dbFactory) = CreateInMemoryDb();
            using var dbConn = conn;

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DB_ENCRYPTION_KEY"] = "TestSecretKey1234567890123456789012"
            }).Build();
            var repo = new DatabaseRepository(dbFactory, config);

            var server = new McpServer
            {
                Id = "pw-stealth",
                DisplayName = "Playwright Stealth",
                Type = "http",
                Url = "http://pw-stealth:3000/mcp",
                Enabled = true,
                RequestTimeoutSeconds = 240,
                MaxConcurrentRequests = 1,
                ConcurrencyKeyField = "role,browser"
            };
            await repo.SaveServerAsync(server);

            var loadedServer = await repo.GetServerByIdAsync("pw-stealth");
            loadedServer.Should().NotBeNull();
            loadedServer!.RequestTimeoutSeconds.Should().Be(240);
            loadedServer.MaxConcurrentRequests.Should().Be(1);
            loadedServer.ConcurrencyKeyField.Should().Be("role,browser");

            var settings = new RouterSettings
            {
                ToolExecutionTimeoutSeconds = 180,
                MaxToolOutputChars = 4096,
                CodeModeExecutionTimeoutSeconds = 300
            };
            await repo.SaveSettingsAsync(settings);

            var loadedSettings = await repo.GetSettingsAsync();
            loadedSettings.Should().NotBeNull();
            loadedSettings!.ToolExecutionTimeoutSeconds.Should().Be(180);
            loadedSettings.MaxToolOutputChars.Should().Be(4096);
            loadedSettings.CodeModeExecutionTimeoutSeconds.Should().Be(300);
        }
    }
}
