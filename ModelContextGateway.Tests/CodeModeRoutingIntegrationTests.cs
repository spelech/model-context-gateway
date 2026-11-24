using System.Collections.Concurrent;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextGateway.Core.CodeMode;
using Moq;

namespace ModelContextGateway.Tests
{
    public class CodeModeRoutingIntegrationTests
    {
        [Fact]
        [Requirement("MCP-CODE-01", "MCP", RequirementType.Positive, "Meta-Mode exposes execute_script and returns structured metadata envelope.")]
        public void GetMetaTools_IncludesExecuteScript_WhenEnabled()
        {
            var router = new ToolRoutingManager();
            var tools = router.GetMetaTools(enableCodeMode: true);

            var scriptTool = tools.FirstOrDefault(t => t.name == "execute_script");
            scriptTool.Should().NotBeNull();
            scriptTool!.description.Should().Contain("JavaScript");
        }

        [Fact]
        [Requirement("MCP-CODE-01", "MCP", RequirementType.Positive, "GetMetaTools omits execute_script when enableCodeMode is false.")]
        public void GetMetaTools_OmitsExecuteScript_WhenDisabled()
        {
            var router = new ToolRoutingManager();
            var tools = router.GetMetaTools(enableCodeMode: false);

            var scriptTool = tools.FirstOrDefault(t => t.name == "execute_script");
            scriptTool.Should().BeNull();
            tools.Should().HaveCount(3);
            tools.Select(t => t.name).Should().Contain(new[] { "search_tools", "execute_tool", "read_output" });
        }

        [Fact]
        [Requirement("SEC-CODE-01", "SEC", RequirementType.FailClosedGuardrail, "ExecuteScript rejects unauthorized SSO groups.")]
        public void ExecuteScript_UnauthorizedGroup_ReturnsSecurityError()
        {
            var settings = new RouterSettings { CodeModeAllowedGroups = "developers,full_admin" };
            var userGroups = new[] { "standard_user" };

            var isAllowed = ToolRoutingManager.IsCodeModeAuthorized(settings, userGroups, isAdmin: false);
            isAllowed.Should().BeFalse();
        }

        [Fact]
        [Requirement("SEC-CODE-01", "SEC", RequirementType.Positive, "IsCodeModeAuthorized permits admin, wildcard, and matching groups.")]
        public void IsCodeModeAuthorized_PermitsAdminWildcardAndMatchingGroups()
        {
            var settings = new RouterSettings { CodeModeAllowedGroups = "developers,full_admin" };

            // Admin bypass
            ToolRoutingManager.IsCodeModeAuthorized(settings, new[] { "guest" }, isAdmin: true).Should().BeTrue();

            // Matching group (case-insensitive)
            ToolRoutingManager.IsCodeModeAuthorized(settings, new[] { "Developers" }, isAdmin: false).Should().BeTrue();

            // Wildcard setting
            var wildcardSettings = new RouterSettings { CodeModeAllowedGroups = "*" };
            ToolRoutingManager.IsCodeModeAuthorized(wildcardSettings, new[] { "guest" }, isAdmin: false).Should().BeTrue();

            // Empty setting allows all
            var emptySettings = new RouterSettings { CodeModeAllowedGroups = "" };
            ToolRoutingManager.IsCodeModeAuthorized(emptySettings, new[] { "guest" }, isAdmin: false).Should().BeTrue();
        }

        [Fact]
        [Requirement("GUARD-CODE-01", "GUARD", RequirementType.FailClosedGuardrail, "ExecuteScript returns error when CodeMode is disabled in gateway settings.")]
        public async Task ExecuteScript_DisabledCodeMode_ReturnsError()
        {
            var engineMock = new Mock<ICodeModeExecutionEngine>();
            var router = new ToolRoutingManager(codeModeEngine: engineMock.Object);

            var mockDbFactory = new Mock<IDbConnectionFactory>();
            var settings = new RouterSettings { EnableCodeMode = false };
            var embeddingServiceMock = new Mock<DynamicEmbeddingService>(new HttpClient(), NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider());
            embeddingServiceMock.Setup(s => s.GetSettings()).Returns(settings);

            var payload = new
            {
                jsonrpc = "2.0",
                id = "req-1",
                method = "tools/call",
                @params = new
                {
                    name = "execute_script",
                    arguments = new
                    {
                        script = "return 42;"
                    }
                }
            };
            var body = JsonSerializer.Serialize(payload);

            var result = await router.CallToolAsync(
                "execute_script",
                body,
                mockDbFactory.Object,
                new ConcurrentDictionary<string, BackendConnection>(),
                new List<McpServer>(),
                NullLogger.Instance,
                new HttpClient(),
                embeddingServiceMock.Object,
                () => Task.CompletedTask,
                (b, k, v) => b
            );

            var json = JsonSerializer.Serialize(result);
            using var doc = JsonDocument.Parse(json);
            doc.RootElement.GetProperty("isError").GetBoolean().Should().BeTrue();
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            text.Should().Contain("Code mode is currently disabled in gateway settings.");
        }

        [Fact]
        [Requirement("GUARD-CODE-02", "GUARD", RequirementType.FailClosedGuardrail, "ExecuteScript returns error when script parameter is missing or empty.")]
        public async Task ExecuteScript_MissingScript_ReturnsError()
        {
            var engineMock = new Mock<ICodeModeExecutionEngine>();
            var router = new ToolRoutingManager(codeModeEngine: engineMock.Object);

            var mockDbFactory = new Mock<IDbConnectionFactory>();
            var settings = new RouterSettings { EnableCodeMode = true };
            var embeddingServiceMock = new Mock<DynamicEmbeddingService>(new HttpClient(), NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider());
            embeddingServiceMock.Setup(s => s.GetSettings()).Returns(settings);

            var payload = new
            {
                jsonrpc = "2.0",
                id = "req-2",
                method = "tools/call",
                @params = new
                {
                    name = "execute_script",
                    arguments = new { }
                }
            };
            var body = JsonSerializer.Serialize(payload);

            var result = await router.CallToolAsync(
                "execute_script",
                body,
                mockDbFactory.Object,
                new ConcurrentDictionary<string, BackendConnection>(),
                new List<McpServer>(),
                NullLogger.Instance,
                new HttpClient(),
                embeddingServiceMock.Object,
                () => Task.CompletedTask,
                (b, k, v) => b
            );

            var json = JsonSerializer.Serialize(result);
            using var doc = JsonDocument.Parse(json);
            doc.RootElement.GetProperty("isError").GetBoolean().Should().BeTrue();
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            text.Should().Contain("script parameter is required");
        }

        [Fact]
        [Requirement("MCP-CODE-02", "MCP", RequirementType.Positive, "ExecuteScript returns structured execution envelope with result and telemetry.")]
        public async Task ExecuteScript_ValidExecution_ReturnsStructuredEnvelope()
        {
            var router = new ToolRoutingManager();

            var mockDbFactory = new Mock<IDbConnectionFactory>();
            var settings = new RouterSettings { EnableCodeMode = true };
            var embeddingServiceMock = new Mock<DynamicEmbeddingService>(new HttpClient(), NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider());
            embeddingServiceMock.Setup(s => s.GetSettings()).Returns(settings);

            var payload = new
            {
                jsonrpc = "2.0",
                id = "req-3",
                method = "tools/call",
                @params = new
                {
                    name = "execute_script",
                    arguments = new
                    {
                        script = "console.log('test log'); return { answer: 42 };"
                    }
                }
            };
            var body = JsonSerializer.Serialize(payload);

            var result = await router.CallToolAsync(
                "execute_script",
                body,
                mockDbFactory.Object,
                new ConcurrentDictionary<string, BackendConnection>(),
                new List<McpServer>(),
                NullLogger.Instance,
                new HttpClient(),
                embeddingServiceMock.Object,
                () => Task.CompletedTask,
                (b, k, v) => b
            );

            var json = JsonSerializer.Serialize(result);
            using var doc = JsonDocument.Parse(json);
            doc.RootElement.GetProperty("isError").GetBoolean().Should().BeFalse();
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            text.Should().NotBeNullOrEmpty();

            using var envelopeDoc = JsonDocument.Parse(text!);
            envelopeDoc.RootElement.GetProperty("result").GetProperty("answer").GetInt32().Should().Be(42);
            envelopeDoc.RootElement.GetProperty("logs")[0].GetString().Should().Contain("test log");
            envelopeDoc.RootElement.GetProperty("executionTimeMs").GetInt64().Should().BeGreaterThanOrEqualTo(0);
            envelopeDoc.RootElement.GetProperty("toolCallsCount").GetInt32().Should().Be(0);
        }

        [Fact]
        [Requirement("MCP-CODE-02", "MCP", RequirementType.Positive, "ExecuteScript accepts both code and script argument parameter names.")]
        public async Task ExecuteScript_WithCodeParameter_ExecutesSuccessfully()
        {
            var router = new ToolRoutingManager();

            var mockDbFactory = new Mock<IDbConnectionFactory>();
            var settings = new RouterSettings { EnableCodeMode = true };
            var embeddingServiceMock = new Mock<DynamicEmbeddingService>(new HttpClient(), NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider());
            embeddingServiceMock.Setup(s => s.GetSettings()).Returns(settings);

            var payload = new
            {
                jsonrpc = "2.0",
                id = "req-code-alias",
                method = "tools/call",
                @params = new
                {
                    name = "execute_script",
                    arguments = new
                    {
                        code = "return 42;"
                    }
                }
            };
            var body = JsonSerializer.Serialize(payload);

            var result = await router.CallToolAsync(
                "execute_script",
                body,
                mockDbFactory.Object,
                new ConcurrentDictionary<string, BackendConnection>(),
                new List<McpServer>(),
                NullLogger.Instance,
                new HttpClient(),
                embeddingServiceMock.Object,
                () => Task.CompletedTask,
                (b, k, v) => b
            );

            var json = JsonSerializer.Serialize(result);
            using var doc = JsonDocument.Parse(json);
            doc.RootElement.GetProperty("isError").GetBoolean().Should().BeFalse();
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            text.Should().NotBeNullOrEmpty();

            using var envelopeDoc = JsonDocument.Parse(text!);
            envelopeDoc.RootElement.GetProperty("result").GetInt32().Should().Be(42);
        }

        [Fact]
        [Requirement("SEC-CODE-01", "SEC", RequirementType.FailClosedGuardrail, "ExecuteScript CallToolAsync fails closed when caller lacks authorized group.")]
        public async Task ExecuteScript_UnauthorizedCallerGroup_ReturnsSecurityErrorEnvelope()
        {
            var router = new ToolRoutingManager();

            var mockDbFactory = new Mock<IDbConnectionFactory>();
            var settings = new RouterSettings
            {
                EnableCodeMode = true,
                CodeModeAllowedGroups = "developers,full_admin"
            };
            var embeddingServiceMock = new Mock<DynamicEmbeddingService>(new HttpClient(), NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider());
            embeddingServiceMock.Setup(s => s.GetSettings()).Returns(settings);

            var payload = new
            {
                jsonrpc = "2.0",
                id = "req-4",
                method = "tools/call",
                @params = new
                {
                    name = "execute_script",
                    arguments = new
                    {
                        script = "return 1;"
                    }
                }
            };
            var body = JsonSerializer.Serialize(payload);

            var result = await router.CallToolAsync(
                "execute_script",
                body,
                mockDbFactory.Object,
                new ConcurrentDictionary<string, BackendConnection>(),
                new List<McpServer>(),
                NullLogger.Instance,
                new HttpClient(),
                embeddingServiceMock.Object,
                () => Task.CompletedTask,
                (b, k, v) => b,
                callerGroups: new[] { "unauthorized_group" },
                isCallerAdmin: false
            );

            var json = JsonSerializer.Serialize(result);
            using var doc = JsonDocument.Parse(json);
            doc.RootElement.GetProperty("isError").GetBoolean().Should().BeTrue();
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            text.Should().Contain("Security Error: User is not authorized to execute code mode scripts.");
        }

        [Fact]
        [Requirement("SEC-CODE-01", "SEC", RequirementType.Positive, "ExecuteScript CallToolAsync allows admin even if not in CodeModeAllowedGroups.")]
        public async Task ExecuteScript_AdminBypass_ExecutesSuccessfully()
        {
            var router = new ToolRoutingManager();

            var mockDbFactory = new Mock<IDbConnectionFactory>();
            var settings = new RouterSettings
            {
                EnableCodeMode = true,
                CodeModeAllowedGroups = "developers_only"
            };
            var embeddingServiceMock = new Mock<DynamicEmbeddingService>(new HttpClient(), NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider());
            embeddingServiceMock.Setup(s => s.GetSettings()).Returns(settings);

            var payload = new
            {
                jsonrpc = "2.0",
                id = "req-5",
                method = "tools/call",
                @params = new
                {
                    name = "execute_script",
                    arguments = new
                    {
                        script = "return 99;"
                    }
                }
            };
            var body = JsonSerializer.Serialize(payload);

            var result = await router.CallToolAsync(
                "execute_script",
                body,
                mockDbFactory.Object,
                new ConcurrentDictionary<string, BackendConnection>(),
                new List<McpServer>(),
                NullLogger.Instance,
                new HttpClient(),
                embeddingServiceMock.Object,
                () => Task.CompletedTask,
                (b, k, v) => b,
                callerGroups: new[] { "guest" },
                isCallerAdmin: true
            );

            var json = JsonSerializer.Serialize(result);
            using var doc = JsonDocument.Parse(json);
            doc.RootElement.GetProperty("isError").GetBoolean().Should().BeFalse();
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            using var envDoc = JsonDocument.Parse(text!);
            envDoc.RootElement.GetProperty("result").GetInt32().Should().Be(99);
        }

        [Fact]
        [Requirement("GUARD-CODE-03", "GUARD", RequirementType.FailClosedGuardrail, "ExecuteScript returns structured envelope with isError on script runtime exception.")]
        public async Task ExecuteScript_RuntimeError_ReturnsStructuredErrorEnvelope()
        {
            var router = new ToolRoutingManager();

            var mockDbFactory = new Mock<IDbConnectionFactory>();
            var settings = new RouterSettings { EnableCodeMode = true };
            var embeddingServiceMock = new Mock<DynamicEmbeddingService>(new HttpClient(), NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider());
            embeddingServiceMock.Setup(s => s.GetSettings()).Returns(settings);

            var payload = new
            {
                jsonrpc = "2.0",
                id = "req-6",
                method = "tools/call",
                @params = new
                {
                    name = "execute_script",
                    arguments = new
                    {
                        script = "throw new Error('Custom execution error');"
                    }
                }
            };
            var body = JsonSerializer.Serialize(payload);

            var result = await router.CallToolAsync(
                "execute_script",
                body,
                mockDbFactory.Object,
                new ConcurrentDictionary<string, BackendConnection>(),
                new List<McpServer>(),
                NullLogger.Instance,
                new HttpClient(),
                embeddingServiceMock.Object,
                () => Task.CompletedTask,
                (b, k, v) => b
            );

            var json = JsonSerializer.Serialize(result);
            using var doc = JsonDocument.Parse(json);
            doc.RootElement.GetProperty("isError").GetBoolean().Should().BeTrue();
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            using var envDoc = JsonDocument.Parse(text!);
            envDoc.RootElement.GetProperty("result").GetString().Should().Contain("Custom execution error");
        }

        [Fact]
        [Requirement("MCP-CODE-01", "MCP", RequirementType.Positive, "execute_script allows scripts to chain search_tools internally and unbox the results.")]
        public async Task ExecuteScript_ChainingSearchTools_ExecutesSuccessfully()
        {
            var router = new ToolRoutingManager();
            var settings = new RouterSettings { EnableCodeMode = true };
            var mockDbFactory = new Mock<IDbConnectionFactory>();
            var embeddingServiceMock = new Mock<DynamicEmbeddingService>(new HttpClient(), NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider());
            embeddingServiceMock.Setup(s => s.GetSettings()).Returns(settings);

            var payload = new
            {
                jsonrpc = "2.0",
                id = "req-chain-search",
                method = "tools/call",
                @params = new
                {
                    name = "execute_script",
                    arguments = new
                    {
                        script = "const res = await callTool('search_tools', { query: 'test' }); return { count: res ? (res.length || 0) : 0 };"
                    }
                }
            };
            var body = JsonSerializer.Serialize(payload);

            var result = await router.CallToolAsync(
                "execute_script",
                body,
                mockDbFactory.Object,
                new ConcurrentDictionary<string, BackendConnection>(),
                new List<McpServer>(),
                NullLogger.Instance,
                new HttpClient(),
                embeddingServiceMock.Object,
                () => Task.CompletedTask,
                (b, k, v) => b
            );

            var json = JsonSerializer.Serialize(result);
            using var doc = JsonDocument.Parse(json);
            doc.RootElement.GetProperty("isError").GetBoolean().Should().BeFalse();
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            using var envDoc = JsonDocument.Parse(text!);
            envDoc.RootElement.GetProperty("toolCallsCount").GetInt32().Should().Be(1);
        }

        [Fact]
        [Requirement("GUARD-CODE-05", "GUARD", RequirementType.FailClosedGuardrail, "Gateway meta-tools (search_tools, execute_tool, read_output, execute_script) are reserved during bare-name resolution so identically named downstream tools cannot shadow them.")]
        public async Task ReservedMetaTools_AreNotShadowedByDownstreamToolsWithIdenticalBareNames()
        {
            var router = new ToolRoutingManager();
            var servers = new List<McpServer>
            {
                new() { Id = "contextcortex", DisplayName = "ContextCortex", Enabled = true, Type = "http", Url = "http://contextcortex/mcp" }
            };

            foreach (var meta in new[] { "search_tools", "execute_tool", "read_output", "execute_script" })
            {
                router.ToolRoutingTable[$"contextcortex/{meta}"] = "contextcortex";
                router.ToolRoutingTable[$"contextcortex__{meta}"] = "contextcortex";
                router.ToolRoutingTable[$"contextcortex:{meta}"] = "contextcortex";

                var (norm, err) = router.NormalizeTargetToolName(meta, servers);
                err.Should().BeNull();
                norm.Should().Be(meta);
            }

            var (namespacedNorm, namespacedErr) = router.NormalizeTargetToolName("contextcortex/execute_script", servers);
            namespacedErr.Should().BeNull();
            namespacedNorm.Should().Be("contextcortex__execute_script");

            // End-to-end ClientSession verification with a downstream server advertising execute_script
            var ccMock = new TestHelpers.MockDownstreamMcpServer();
            ccMock.Tools.Clear();
            ccMock.AddTool("execute_script", "ContextCortex local sandbox");
            ccMock.AddTool("search", "ContextCortex code search");

            using var sqlite = new Microsoft.Data.Sqlite.SqliteConnection("Filename=:memory:");
            sqlite.Open();
            Dapper.SqlMapper.Execute(sqlite, "CREATE TABLE Settings (Id TEXT PRIMARY KEY, EnableCodeMode INTEGER DEFAULT 1); INSERT INTO Settings (Id, EnableCodeMode) VALUES ('default', 1);");
            var mockDbFactory = new Mock<IDbConnectionFactory>();
            mockDbFactory.Setup(f => f.CreateConnection()).Returns(sqlite);
            mockDbFactory.Setup(f => f.ProviderName).Returns("sqlite");

            var services = new ServiceCollection();
            services.AddSingleton(Mock.Of<IAuditLogger>());
            services.AddSingleton(mockDbFactory.Object);
            services.AddLogging();
            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { { "Admin:GroupSid", "full_admin" } })
                .Build();
            services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(config);
            var sp = services.BuildServiceProvider();

            var httpCtx = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = sp };
            httpCtx.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "admin"),
                    new System.Security.Claims.Claim("Sid", "full_admin"),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "full_admin")
                }, "TestAuth"));

            var embeddingMock = new Mock<DynamicEmbeddingService>(new HttpClient(), NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider());
            embeddingMock.Setup(s => s.GetSettings()).Returns(new RouterSettings { EnableCodeMode = true });

            var session = new ClientSession(
                "sess-shadow-test",
                null,
                servers,
                ccMock.CreateHttpClient(),
                embeddingMock.Object,
                NullLogger<ClientSession>.Instance,
                sp)
            {
                IsMetaMode = true
            };

            // Warm cache via non-meta listing so contextcortex/execute_script is registered in _toolRoutingTable
            session.IsMetaMode = false;
            await session.ListToolsAsync("{\"jsonrpc\":\"2.0\",\"id\":1}", httpCtx);
            session.IsMetaMode = true;

            // Calling bare execute_script must run MCG's Jint Code Mode engine (mcg.listServers() works), NOT contextcortex/execute_script
            var jintBody = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "req-jint",
                method = "tools/call",
                @params = new
                {
                    name = "execute_script",
                    arguments = new
                    {
                        script = "const srvs = await mcg.listServers(); return { serverCount: srvs.length, firstId: srvs[0].id };"
                    }
                }
            });
            var jintRes = await session.CallToolAsync("execute_script", jintBody, mockDbFactory.Object, httpCtx);
            var jintDoc = JsonDocument.Parse(JsonSerializer.Serialize(jintRes));
            jintDoc.RootElement.GetProperty("isError").GetBoolean().Should().BeFalse();
            var envJson = jintDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            using var parsedEnv = JsonDocument.Parse(envJson!);
            parsedEnv.RootElement.GetProperty("result").GetProperty("firstId").GetString().Should().Be("contextcortex");

            // Calling explicit contextcortex/execute_script via execute_tool must still route to downstream contextcortex
            var downstreamBody = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = "req-cc",
                method = "tools/call",
                @params = new
                {
                    name = "execute_tool",
                    arguments = new
                    {
                        name = "contextcortex/execute_script",
                        arguments = new { code = "return 1;" }
                    }
                }
            });
            var ccRes = await session.CallToolAsync("execute_tool", downstreamBody, mockDbFactory.Object, httpCtx);
            var ccJson = JsonSerializer.Serialize(ccRes);
            ccJson.Should().Contain("Mock executed tool execute_script");
        }
    }
}
