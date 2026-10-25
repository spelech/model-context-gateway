using System.Collections.Concurrent;
using System.Text.Json;
using FluentAssertions;
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
            tools.Should().HaveCount(2);
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
    }
}
