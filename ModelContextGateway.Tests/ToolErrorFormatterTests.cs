using System.Text.Json;

namespace ModelContextGateway.Tests
{
    public class ToolErrorFormatterTests
    {
        [Fact]
        [Requirement("MCP-ERROR-FORMAT-JSONRPC-REMEDIATION", "MCP", RequirementType.Positive, "ToolErrorFormatter attaches actionable suggestions and remediation resource URIs to JSON-RPC error payloads.")]
        public void TransformError_FormatsJsonRpcErrorWithRemediation()
        {
            var err = new JsonRpcError
            {
                Code = -32602,
                Message = "Invalid parameter supplied for query"
            };

            var formatted = ToolErrorFormatter.TransformError(err, "docker__restart_container", "docker");
            using var doc = JsonDocument.Parse(formatted);
            var root = doc.RootElement;

            Assert.Equal("Invalid parameter supplied for query", root.GetProperty("error").GetString());
            Assert.Equal(-32602, root.GetProperty("code").GetInt32());
            Assert.Contains("Invalid arguments passed to tool", root.GetProperty("suggestion").GetString());
            Assert.Contains("logs://docker/today", root.GetProperty("remediation").GetString());
        }

        [Fact]
        [Requirement("MCP-ERROR-FORMAT-UNHANDLED-EXCEPTION", "MCP", RequirementType.Positive, "ToolErrorFormatter converts unhandled exceptions to standardized JSON-RPC error format with suggestions.")]
        public void TransformException_FormatsExceptionWithRemediation()
        {
            var ex = new Exception("Connection refused by target socket");

            var formatted = ToolErrorFormatter.TransformException(ex, "plex__search", "plex");
            using var doc = JsonDocument.Parse(formatted);
            var root = doc.RootElement;

            Assert.Equal("Connection refused by target socket", root.GetProperty("error").GetString());
            Assert.Equal(-32603, root.GetProperty("code").GetInt32());
            Assert.Contains("Network connection refused", root.GetProperty("suggestion").GetString());
        }

        [Theory]
        [InlineData("Unauthorized access token", "auth")]
        [InlineData("Request timed out", "timeout")]
        [InlineData("Connection refused by server", "connection")]
        [InlineData("Invalid argument passed", "argument")]
        [InlineData("Unknown exception occurred", "unexpected")]
        [Requirement("MCP-ERROR-ACTIONABLE-SUGGESTION-CATEGORIES", "MCP", RequirementType.Positive, "ToolErrorFormatter categorizes error messages and produces domain-specific remediation guidance.")]
        public void GetActionableSuggestion_ReturnsExpectedCategory(string errorMsg, string category)
        {
            var suggestion = ToolErrorFormatter.GetActionableSuggestion(errorMsg, "test_tool", "server1");
            Assert.NotNull(suggestion);

            if (category == "auth")
            {
                Assert.Contains("Authentication/Authorization failure", suggestion);
            }
            else if (category == "timeout")
            {
                Assert.Contains("timed out", suggestion);
            }
            else if (category == "connection")
            {
                Assert.Contains("Network connection refused", suggestion);
            }
            else if (category == "argument")
            {
                Assert.Contains("Invalid arguments", suggestion);
            }
            else
            {
                Assert.Contains("unexpected error occurred", suggestion);
            }
        }
    }
}
