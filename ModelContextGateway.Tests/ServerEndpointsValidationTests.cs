using Microsoft.Extensions.Configuration;

namespace ModelContextGateway.Tests
{
    public class ServerEndpointsValidationTests
    {
        [Theory]
        [InlineData("node /path/to/server.js", true)]
        [InlineData("python3 -m mcp_server --arg=val", true)]
        [InlineData("   ", false)]
        [InlineData("bash script.sh", false)]
        [InlineData("sh script.sh", false)]
        [InlineData("powershell script.ps1", false)]
        [InlineData("node; rm -rf /", false)]
        [InlineData("python3 | cat", false)]
        [InlineData("cat `whoami`", false)]
        [Requirement("GUARD-VALIDATION-STDIO-SHELL-OPERATORS", "GUARD", RequirementType.Negative, "ServerValidationHelper validates stdio commands against unsafe shell operators, piping, and command injection.")]
        public void IsValidStdioCommand_ValidatesExecutableAndDisallowsUnsafeCommands(string command, bool expectedValid)
        {
            var valid = ServerValidationHelper.IsValidStdioCommand(command, out var err);
            Assert.Equal(expectedValid, valid);
            if (!expectedValid)
            {
                Assert.NotNull(err);
            }
        }

        [Fact]
        [Requirement("GUARD-05", "GUARD", RequirementType.Negative, "ServerValidationHelper rejects invalid HTTP URI formats for SSE MCP servers.")]
        public void IsValidServerUrl_Rejects_Invalid_Http_Urls()
        {
            var config = new ConfigurationBuilder().Build();
            var valid = ServerValidationHelper.IsValidServerUrl("not-a-valid-url", config, out var err);
            Assert.False(valid);
            Assert.Contains("must be a valid HTTP or HTTPS URI", err);
        }

        [Fact]
        [Requirement("TRANS-VALIDATION-HTTP-ALLOWED-IPS", "TRANS", RequirementType.Positive, "ServerValidationHelper accepts valid HTTP/HTTPS endpoints allowed by IP security rules.")]
        public void IsValidServerUrl_Accepts_Valid_Http_Urls()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:AllowedIpRanges:0"] = "127.0.0.1/32"
                })
                .Build();
            var valid = ServerValidationHelper.IsValidServerUrl("http://127.0.0.1:8080/sse", config, out var err);
            Assert.True(valid);
            Assert.Null(err);
        }

        [Fact]
        [Requirement("GUARD-05", "GUARD", RequirementType.Negative, "ServerValidationHelper rejects server transport updates that leave incompatible URLs or command configurations.")]
        public void Validation_Rejects_TypeOnly_Update_Leaving_Incompatible_Url()
        {
            var config = new ConfigurationBuilder().Build();

            // Scenario 1: Existing SSE server with HTTP URL updated to 'stdio' without changing URL
            var httpUrl = "http://api.example.com/sse";
            var stdioValid = ServerValidationHelper.IsValidStdioCommand(httpUrl, out var stdioErr);
            Assert.False(stdioValid);
            Assert.NotNull(stdioErr);

            // Scenario 2: Existing STDIO server with command updated to 'sse' without changing URL
            var stdioCommand = "node /app/server.js";
            var sseValid = ServerValidationHelper.IsValidServerUrl(stdioCommand, config, out var sseErr);
            Assert.False(sseValid);
            Assert.NotNull(sseErr);
        }

        [Fact]
        [Requirement("MCP-29", "MCP", RequirementType.Negative, "ServerValidationHelper rejects invalid characters in Alias.")]
        public void ValidateServer_Rejects_Invalid_Alias_Characters()
        {
            // MCP-29: Rejects invalid characters in Alias
            var server = new McpServer
            {
                Id = "test-server",
                Alias = "invalid alias!@#",
                Url = "http://localhost:8000/sse"
            };

            var error = ServerValidationHelper.ValidateAlias(server.Alias, server.Id, new List<McpServer>());
            Assert.NotNull(error);
            Assert.Contains("letters, numbers, underscores, and hyphens", error);
        }

        [Fact]
        [Requirement("MCP-29", "MCP", RequirementType.Negative, "ServerValidationHelper rejects Alias colliding with another server's Id.")]
        public void ValidateServer_Rejects_Alias_Colliding_With_Existing_ServerId()
        {
            // MCP-29: Rejects Alias colliding with another server's Id
            var existing = new List<McpServer>
            {
                new McpServer { Id = "docker", DisplayName = "Docker" }
            };

            var error = ServerValidationHelper.ValidateAlias("docker", "other-server", existing);
            Assert.NotNull(error);
            Assert.Contains("collides with an existing server ID", error);
        }

        [Fact]
        [Requirement("MCP-29", "MCP", RequirementType.Negative, "ServerValidationHelper rejects Alias colliding with another server's Alias.")]
        public void ValidateServer_Rejects_Alias_Colliding_With_Existing_Server_Alias()
        {
            // MCP-29: Rejects Alias colliding with another server's Alias
            var existing = new List<McpServer>
            {
                new McpServer { Id = "db1", Alias = "shared_db" }
            };

            var error = ServerValidationHelper.ValidateAlias("shared_db", "db2", existing);
            Assert.NotNull(error);
            Assert.Contains("already in use by another server", error);
        }

        [Fact]
        [Requirement("MCP-29", "MCP", RequirementType.Positive, "ServerValidationHelper accepts valid alias and permits server to keep its own alias.")]
        public void ValidateServer_Accepts_Valid_Alias_And_Self_Retention()
        {
            var existing = new List<McpServer>
            {
                new McpServer { Id = "db1", Alias = "shared_db" }
            };

            // Updating db1 with its own existing alias should not collide with itself
            var errorSelf = ServerValidationHelper.ValidateAlias("shared_db", "db1", existing);
            Assert.Null(errorSelf);

            // Valid alias that doesn't collide
            var errorNew = ServerValidationHelper.ValidateAlias("new_db-2", "db2", existing);
            Assert.Null(errorNew);

            // Null or whitespace alias is valid (clearing alias)
            Assert.Null(ServerValidationHelper.ValidateAlias(null, "db2", existing));
            Assert.Null(ServerValidationHelper.ValidateAlias("   ", "db2", existing));
        }

        [Fact]
        [Requirement("MCP-29", "MCP", RequirementType.Negative, "ServerValidationHelper rejects case-insensitive collisions with IDs and Aliases.")]
        public void ValidateServer_Rejects_CaseInsensitive_Collisions()
        {
            var existing = new List<McpServer>
            {
                new McpServer { Id = "Docker-Server", Alias = "My_Alias" }
            };

            var errId = ServerValidationHelper.ValidateAlias("docker-server", "other", existing);
            Assert.NotNull(errId);
            Assert.Contains("collides with an existing server ID", errId);

            var errAlias = ServerValidationHelper.ValidateAlias("MY_ALIAS", "other", existing);
            Assert.NotNull(errAlias);
            Assert.Contains("already in use by another server", errAlias);
        }
    }
}
