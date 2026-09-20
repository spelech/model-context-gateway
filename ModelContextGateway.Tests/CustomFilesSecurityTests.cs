using FluentAssertions;
using ModelContextGateway.Components.Capabilities;

namespace ModelContextGateway.Tests
{
    public class CustomFilesSecurityTests
    {
        [Fact]
        [Requirement("GUARD-07", "GUARD", RequirementType.FailClosedGuardrail, "Custom files endpoint validates file path ensuring sibling directory traversal and parent traversal fail closed.")]
        public void IsSafePath_BlocksSiblingDirectoryTraversal_AndAllowsLegitimateFiles()
        {
            var tempBase = Path.Combine(Path.GetTempPath(), "mcg_test_" + Guid.NewGuid().ToString("N"));
            var promptsDir = Path.Combine(tempBase, "prompts");
            var promptsSecretDir = Path.Combine(tempBase, "prompts-secret");
            Directory.CreateDirectory(promptsDir);
            Directory.CreateDirectory(promptsSecretDir);

            try
            {
                var legitimateFile = Path.Combine(promptsDir, "test.json");
                var siblingFile = Path.Combine(promptsSecretDir, "secret.json");
                var parentTraversalFile = Path.Combine(promptsDir, "..", "secret.json");

                CapabilityEndpoints.IsSafePath(legitimateFile, promptsDir).Should().BeTrue();
                CapabilityEndpoints.IsSafePath(siblingFile, promptsDir).Should().BeFalse();
                CapabilityEndpoints.IsSafePath(parentTraversalFile, promptsDir).Should().BeFalse();
            }
            finally
            {
                if (Directory.Exists(tempBase))
                {
                    Directory.Delete(tempBase, true);
                }
            }
        }
    }
}
