using FluentAssertions;

namespace ModelContextGateway.Tests
{
    public class CodeModeSettingsTests
    {
        [Fact]
        [Requirement("MCP-CODE-01", "MCP", RequirementType.Positive, "RouterSettings provides default Code Mode configuration values.")]
        public void RouterSettings_HasSensibleCodeModeDefaults()
        {
            var settings = new RouterSettings();

            settings.EnableCodeMode.Should().BeTrue();
            settings.CodeModeExecutionTimeoutSeconds.Should().Be(30);
            settings.CodeModeMaxToolCalls.Should().Be(50);
            settings.CodeModeMaxMemoryMb.Should().Be(64);
            settings.CodeModeMaxStatements.Should().Be(100000);
            settings.CodeModeAllowedGroups.Should().BeEmpty();
        }
    }
}
