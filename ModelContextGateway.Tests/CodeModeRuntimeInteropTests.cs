using System.Text.Json;
using FluentAssertions;
using ModelContextGateway.Core.CodeMode;

namespace ModelContextGateway.Tests
{
    public class CodeModeRuntimeInteropTests
    {
        private readonly CodeModeExecutionEngine _engine = new();

        [Fact]
        [Requirement("MCP-CODE-01", "MCP", RequirementType.Positive, "Script can invoke tools via dual dispatcher and unbox JSON responses.")]
        public async Task ExecuteAsync_DualDispatcherAndUnboxing_WorksSuccessfully()
        {
            var settings = new RouterSettings();
            var script = @"
                console.log('Fetching containers...');
                const data = await tools.docker.list_containers();
                const running = data.filter(c => c.status === 'running');
                return { count: running.length, names: running.map(c => c.name) };
            ";

            var dummyResponse = new
            {
                content = new[] {
                    new { type = "text", text = "[{\"name\":\"app1\",\"status\":\"running\"},{\"name\":\"app2\",\"status\":\"stopped\"}]" }
                }
            };

            var result = await _engine.ExecuteAsync(
                script,
                null,
                settings,
                (toolName, args, token) => Task.FromResult<object>(dummyResponse),
                CancellationToken.None);

            result.IsError.Should().BeFalse();
            result.ToolCallsCount.Should().Be(1);
            result.Logs.Should().Contain(l => l.Contains("Fetching containers..."));

            using var doc = JsonDocument.Parse(result.ResultJson!);
            doc.RootElement.GetProperty("count").GetInt32().Should().Be(1);
            doc.RootElement.GetProperty("names")[0].GetString().Should().Be("app1");
        }

        [Fact]
        [Requirement("GUARD-CODE-04", "GUARD", RequirementType.FailClosedGuardrail, "Exceeding MaxToolCalls aborts execution.")]
        public async Task ExecuteAsync_ToolCallsQuotaExceeded_FailsClosed()
        {
            var settings = new RouterSettings { CodeModeMaxToolCalls = 2 };
            var script = @"
                await callTool('test__tool1');
                await callTool('test__tool2');
                await callTool('test__tool3');
            ";

            var result = await _engine.ExecuteAsync(
                script,
                null,
                settings,
                (toolName, args, token) => Task.FromResult<object>("ok"),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.ErrorMessage.Should().Contain("maximum tool invocation limit");
        }

        [Fact]
        [Requirement("MCP-CODE-01", "MCP", RequirementType.Positive, "Caller args are pre-injected and accessible via global args and mcg.args.")]
        public async Task ExecuteAsync_PreInjectedArgs_AccessibleInScript()
        {
            var settings = new RouterSettings();
            var script = @"
                return {
                    globalLimit: args.limit,
                    mcgLimit: mcg.args.limit,
                    region: args.region
                };
            ";

            using var doc = JsonDocument.Parse("{\"limit\": 25, \"region\": \"us-west-2\"}");
            var result = await _engine.ExecuteAsync(
                script,
                doc.RootElement,
                settings,
                (toolName, args, token) => Task.FromResult<object>("ok"),
                CancellationToken.None);

            result.IsError.Should().BeFalse();
            using var resDoc = JsonDocument.Parse(result.ResultJson!);
            resDoc.RootElement.GetProperty("globalLimit").GetInt32().Should().Be(25);
            resDoc.RootElement.GetProperty("mcgLimit").GetInt32().Should().Be(25);
            resDoc.RootElement.GetProperty("region").GetString().Should().Be("us-west-2");
        }

        [Fact]
        [Requirement("MCP-CODE-01", "MCP", RequirementType.Positive, "Top-level callTool and mcg.callTool forward arguments and options.")]
        public async Task ExecuteAsync_CallToolAndMcgCallTool_ForwardArgumentsAndOptions()
        {
            var settings = new RouterSettings();
            var script = @"
                const r1 = await callTool('server1__toolA', { param1: 'val1' }, { authToken: 'secretA' });
                const r2 = await mcg.callTool('server2__toolB', { param2: 'val2' }, { auth_token: 'secretB' });
                return { r1, r2 };
            ";

            var invoked = new List<(string name, JsonElement args, string? token)>();

            var result = await _engine.ExecuteAsync(
                script,
                null,
                settings,
                (toolName, args, token) =>
                {
                    invoked.Add((toolName, args.Clone(), token));
                    return Task.FromResult<object>($"echo_{toolName}");
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse();
            result.ToolCallsCount.Should().Be(2);

            invoked.Should().HaveCount(2);
            invoked[0].name.Should().Be("server1__toolA");
            invoked[0].args.GetProperty("param1").GetString().Should().Be("val1");
            invoked[0].token.Should().Be("secretA");

            invoked[1].name.Should().Be("server2__toolB");
            invoked[1].args.GetProperty("param2").GetString().Should().Be("val2");
            invoked[1].token.Should().Be("secretB");

            using var resDoc = JsonDocument.Parse(result.ResultJson!);
            resDoc.RootElement.GetProperty("r1").GetString().Should().Be("echo_server1__toolA");
            resDoc.RootElement.GetProperty("r2").GetString().Should().Be("echo_server2__toolB");
        }

        [Fact]
        [Requirement("MCP-CODE-01", "MCP", RequirementType.Positive, "Non-JSON text content blocks unbox cleanly to raw strings.")]
        public async Task ExecuteAsync_NonJsonContent_ReturnsRawText()
        {
            var settings = new RouterSettings();
            var script = @"
                const text = await tools.github.get_readme();
                return { text };
            ";

            var dummyResponse = new
            {
                content = new[] {
                    new { type = "text", text = "# Project Title\nThis is plain markdown not JSON." }
                }
            };

            var result = await _engine.ExecuteAsync(
                script,
                null,
                settings,
                (toolName, args, token) => Task.FromResult<object>(dummyResponse),
                CancellationToken.None);

            result.IsError.Should().BeFalse();
            using var doc = JsonDocument.Parse(result.ResultJson!);
            doc.RootElement.GetProperty("text").GetString().Should().Be("# Project Title\nThis is plain markdown not JSON.");
        }
    }
}
