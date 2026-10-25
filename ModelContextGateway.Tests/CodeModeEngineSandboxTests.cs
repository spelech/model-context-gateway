using FluentAssertions;
using ModelContextGateway.Core.CodeMode;

namespace ModelContextGateway.Tests
{
    public class CodeModeEngineSandboxTests
    {
        private readonly CodeModeExecutionEngine _engine = new();

        [Fact]
        [Requirement("GUARD-CODE-01", "GUARD", RequirementType.FailClosedGuardrail, "Statement cap terminates infinite loops within allotted execution budget.")]
        public async Task ExecuteAsync_InfiniteLoop_TerminatesOnStatementLimit()
        {
            var settings = new RouterSettings { CodeModeMaxStatements = 1000, CodeModeExecutionTimeoutSeconds = 5 };
            var script = "while (true) {}";

            var result = await _engine.ExecuteAsync(script, null, settings, (name, args, token) => Task.FromResult<object>("ok"), CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.ErrorMessage.Should().Contain("statement");
        }

        [Fact]
        [Requirement("GUARD-CODE-02", "GUARD", RequirementType.FailClosedGuardrail, "Execution timeout terminates long-running script.")]
        public async Task ExecuteAsync_Timeout_TerminatesExecution()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            var settings = new RouterSettings { CodeModeExecutionTimeoutSeconds = 1 };
            var script = "let x = 0; while (x < 10000000) { x++; }";

            var result = await _engine.ExecuteAsync(script, null, settings, (name, args, token) => Task.FromResult<object>("ok"), cts.Token);

            result.IsError.Should().BeTrue();
        }

        [Fact]
        [Requirement("SEC-CODE-02", "SEC", RequirementType.FailClosedGuardrail, "Sandbox prevents CLR reflection and process spawning.")]
        public async Task ExecuteAsync_ReflectionAttempt_IsBlocked()
        {
            var settings = new RouterSettings();
            var script = "return typeof System !== 'undefined' ? System.Environment.MachineName : 'BLOCKED';";

            var result = await _engine.ExecuteAsync(script, null, settings, (name, args, token) => Task.FromResult<object>("ok"), CancellationToken.None);

            result.IsError.Should().BeFalse();
            result.ResultJson.Should().Be("\"BLOCKED\"");
        }

        [Fact]
        [Requirement("GUARD-CODE-01", "GUARD", RequirementType.FailClosedGuardrail, "Memory limit terminates memory-exhausting script.")]
        public async Task ExecuteAsync_MemoryLimit_TerminatesOnMemoryExceeded()
        {
            // Low memory ceiling: 1MB
            var settings = new RouterSettings { CodeModeMaxMemoryMb = 1, CodeModeMaxStatements = 500000 };
            var script = "let arr = []; while(true) { arr.push(new Array(10000).fill('data')); }";

            var result = await _engine.ExecuteAsync(script, null, settings, (name, args, token) => Task.FromResult<object>("ok"), CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.ErrorMessage?.ToLowerInvariant().Should().MatchRegex("memory (limit|ceiling)");
        }

        [Fact]
        [Requirement("GUARD-CODE-01", "GUARD", RequirementType.Positive, "Console logs are captured in execution result.")]
        public async Task ExecuteAsync_ConsoleLogging_CapturesLogs()
        {
            var settings = new RouterSettings();
            var script = @"
                console.log('info log');
                console.warn('warn log');
                console.error('error log');
                return 42;
            ";

            var result = await _engine.ExecuteAsync(script, null, settings, (name, args, token) => Task.FromResult<object>("ok"), CancellationToken.None);

            result.IsError.Should().BeFalse();
            result.ResultJson.Should().Be("42");
            result.Logs.Should().Contain(l => l.Contains("info log"));
            result.Logs.Should().Contain(l => l.Contains("warn log"));
            result.Logs.Should().Contain(l => l.Contains("error log"));
        }
    }
}
