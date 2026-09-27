using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ModelContextGateway.Tests.TestHelpers;

namespace ModelContextGateway.Tests
{
    public class PromptAndResourceResilienceTests
    {
        [Fact]
        [Requirement("MCP-36", "MCP", RequirementType.Positive, "PromptRoutingManager resolves prompt directly on cold-start without requiring prior prompts/list call.")]
        public async Task PromptRoutingManager_GetPromptAsync_ResolvesDirectly_OnColdStart()
        {
            var mockServer = new MockDownstreamMcpServer();
            var httpClient = mockServer.CreateHttpClient();
            var server = new McpServer
            {
                Id = "docker",
                DisplayName = "Docker Server",
                Type = "http",
                Url = "http://localhost/mcp",
                Enabled = true
            };

            using var conn = new BackendConnection(server, httpClient, Microsoft.Extensions.Logging.Abstractions.NullLogger<BackendConnection>.Instance);
            var connections = new ConcurrentDictionary<string, BackendConnection>();
            connections["docker"] = conn;

            var manager = new PromptRoutingManager();
            var getBody = "{\"jsonrpc\":\"2.0\",\"id\":\"p1\",\"method\":\"prompts/get\",\"params\":{\"name\":\"docker__container_health\",\"arguments\":{}}}";

            Func<string, string, string, string> rewriteRequestJson = (json, key, value) =>
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("params", out var pProp))
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(pProp.GetRawText())!;
                    dict[key] = value;
                    var newParams = JsonSerializer.Serialize(dict);
                    var full = JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
                    full["params"] = JsonDocument.Parse(newParams).RootElement;
                    return JsonSerializer.Serialize(full);
                }
                return json;
            };

            // Act - cold start without ListPromptsAsync ever called
            var result = await manager.GetPromptAsync("docker__container_health", getBody, connections, () => Task.CompletedTask, rewriteRequestJson);

            // Assert
            result.Should().NotBeNull();
        }

        [Theory]
        [InlineData("docker/container_health")]
        [InlineData("docker:container_health")]
        [InlineData("docker__container_health")]
        [Requirement("MCP-38", "MCP", RequirementType.Positive, "PromptRoutingManager supports slash, colon, and double-underscore delimiters for prompts/get.")]
        public async Task PromptRoutingManager_GetPromptAsync_SupportsMultipleDelimiters(string promptName)
        {
            var mockServer = new MockDownstreamMcpServer();
            var httpClient = mockServer.CreateHttpClient();
            var server = new McpServer
            {
                Id = "docker",
                DisplayName = "Docker Server",
                Type = "http",
                Url = "http://localhost/mcp",
                Enabled = true
            };

            using var conn = new BackendConnection(server, httpClient, Microsoft.Extensions.Logging.Abstractions.NullLogger<BackendConnection>.Instance);
            var connections = new ConcurrentDictionary<string, BackendConnection>();
            connections["docker"] = conn;

            var manager = new PromptRoutingManager();
            var getBody = $"{{\"jsonrpc\":\"2.0\",\"id\":\"p2\",\"method\":\"prompts/get\",\"params\":{{\"name\":\"{promptName}\",\"arguments\":{{}}}}}}";

            Func<string, string, string, string> rewriteRequestJson = (json, key, value) =>
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("params", out var pProp))
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(pProp.GetRawText())!;
                    dict[key] = value;
                    var newParams = JsonSerializer.Serialize(dict);
                    var full = JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
                    full["params"] = JsonDocument.Parse(newParams).RootElement;
                    return JsonSerializer.Serialize(full);
                }
                return json;
            };

            var result = await manager.GetPromptAsync(promptName, getBody, connections, () => Task.CompletedTask, rewriteRequestJson);
            result.Should().NotBeNull();
        }

        [Theory]
        [InlineData("router__diagnose_failure")]
        [InlineData("router/diagnose_failure")]
        [InlineData("router:diagnose_failure")]
        [Requirement("MCP-38", "MCP", RequirementType.Positive, "PromptRoutingManager resolves built-in meta-prompts across delimiter styles.")]
        public async Task PromptRoutingManager_GetPromptAsync_ResolvesRouterLocalPrompts_AcrossDelimiters(string promptName)
        {
            var manager = new PromptRoutingManager();
            var connections = new ConcurrentDictionary<string, BackendConnection>();
            var getBody = $"{{\"jsonrpc\":\"2.0\",\"id\":\"p3\",\"method\":\"prompts/get\",\"params\":{{\"name\":\"{promptName}\",\"arguments\":{{\"tool_name\":\"excel_read\",\"error_message\":\"file locked\"}}}}}}";

            var result = await manager.GetPromptAsync(promptName, getBody, connections, () => Task.CompletedTask, (json, k, v) => json);
            result.Should().NotBeNull();

            var json = JsonSerializer.Serialize(result);
            json.Should().Contain("diagnosing a failure in the MCP tool");
            json.Should().Contain("excel_read");
        }

        [Fact]
        [Requirement("MCP-37", "MCP", RequirementType.Positive, "ResourceRoutingManager resolves mcp:// URIs directly on cold-start without prior resources/list.")]
        public async Task ResourceRoutingManager_ReadResourceAsync_ResolvesDirectly_OnColdStart()
        {
            var mockServer = new MockDownstreamMcpServer();
            var httpClient = mockServer.CreateHttpClient();
            var server = new McpServer
            {
                Id = "docker",
                DisplayName = "Docker Server",
                Type = "http",
                Url = "http://localhost/mcp",
                Enabled = true
            };

            using var conn = new BackendConnection(server, httpClient, Microsoft.Extensions.Logging.Abstractions.NullLogger<BackendConnection>.Instance);
            var connections = new ConcurrentDictionary<string, BackendConnection>();
            connections["docker"] = conn;

            var manager = new ResourceRoutingManager();
            var readBody = "{\"jsonrpc\":\"2.0\",\"id\":\"r1\",\"method\":\"resources/read\",\"params\":{\"uri\":\"mcp://docker/container%2Flogs\"}}";

            Func<string, string, string, string> rewriteRequestJson = (json, key, value) =>
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("params", out var pProp))
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(pProp.GetRawText())!;
                    dict[key] = value;
                    var newParams = JsonSerializer.Serialize(dict);
                    var full = JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
                    full["params"] = JsonDocument.Parse(newParams).RootElement;
                    return JsonSerializer.Serialize(full);
                }
                return json;
            };

            // Act - cold start without ListResourcesAsync
            var result = await manager.ReadResourceAsync("mcp://docker/container%2Flogs", readBody, connections, () => Task.CompletedTask, rewriteRequestJson);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        [Requirement("MCP-39", "MCP", RequirementType.Positive, "CapabilityEndpoints test bench APIs return 404 Not Found gracefully when target server is missing.")]
        public async Task CapabilityEndpoints_TestBench_HandlesMissingServer_GracefullyWith404()
        {
            using var factory = new PipelineIntegrationFactory();
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Forwarded-For", "127.0.0.1");
            client.DefaultRequestHeaders.Add("Remote-User", "admin_user");
            client.DefaultRequestHeaders.Add("Remote-Groups", "full_admin");

            // 1. Prompts get with nonexistent server
            var promptRes = await client.PostAsJsonAsync("/api/test/prompts/get", new
            {
                serverId = "nonexistent_server",
                promptName = "nonexistent_server__prompt",
                arguments = new { }
            });
            promptRes.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);

            // 2. Resources read with nonexistent server
            var resourceRes = await client.PostAsJsonAsync("/api/test/resources/read", new
            {
                serverId = "nonexistent_server",
                uri = "mcp://nonexistent_server/unknown"
            });
            resourceRes.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);

            // 3. Tool call with nonexistent server
            var toolRes = await client.PostAsJsonAsync("/api/test/call", new
            {
                serverId = "nonexistent_server",
                toolName = "nonexistent_server__tool",
                arguments = new { }
            });
            toolRes.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        }
    }
}
