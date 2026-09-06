using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextGateway.Tests.TestHelpers;

namespace ModelContextGateway.Tests
{
    public class TransportResilienceTests
    {
        [Fact]
        [Requirement("TRANS-SSE-CALLMETHOD-DISCONNECT-GUARD", "TRANS", RequirementType.Negative, "SseTransport CallMethodAsync returns -32001 Not Connected when backend is disconnected.")]
        public async Task SseTransport_CallMethodAsync_ReturnsNotConnected_WhenDisconnected()
        {
            var server = new McpServer
            {
                Id = "sse-test-disconnected",
                Url = "http://localhost:9000/sse",
                SecretProvider = "None"
            };

            var stateManager = new JsonRpcStateManager();
            var transport = new SseTransport(server, new HttpClient(), NullLogger.Instance, stateManager);

            // Dispose sets stateManager to Disconnected
            transport.Dispose();

            var response = await transport.CallMethodAsync("tools/list", new { });
            response.Should().NotBeNull();
            response.Error.Should().NotBeNull();
            response.Error!.Code.Should().Be(-32001);
            response.Error.Message.Should().Be("Not connected");
        }

        [Fact]
        [Requirement("TRANS-SSE-SENDREQUEST-DISCONNECT-GUARD", "TRANS", RequirementType.Negative, "SseTransport SendRequestAsync returns -32001 Not Connected when backend is disconnected.")]
        public async Task SseTransport_SendRequestAsync_ReturnsNotConnected_WhenDisconnected()
        {
            var server = new McpServer
            {
                Id = "sse-test-disconnected",
                Url = "http://localhost:9000/sse",
                SecretProvider = "None"
            };

            var stateManager = new JsonRpcStateManager();
            var transport = new SseTransport(server, new HttpClient(), NullLogger.Instance, stateManager);

            transport.Dispose();

            var response = await transport.SendRequestAsync("tools/list", "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}");
            response.Should().NotBeNull();
            response.Error.Should().NotBeNull();
            response.Error!.Code.Should().Be(-32001);
            response.Error.Message.Should().Be("Not connected");
        }

        [Fact]
        [Requirement("TRANS-HTTP-DISPOSED-GUARD", "TRANS", RequirementType.Negative, "HttpTransport SendRequestAsync returns -32001 Not Connected when transport has been disposed.")]
        public async Task HttpTransport_SendRequestAsync_ReturnsDisposedError_WhenDisposed()
        {
            var server = new McpServer
            {
                Id = "http-test-disposed",
                Url = "http://localhost:9000/mcp",
                SecretProvider = "None"
            };

            var transport = new HttpTransport(server, new HttpClient(), NullLogger.Instance);
            transport.Dispose();

            var response = await transport.SendRequestAsync("tools/list", "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}");
            response.Should().NotBeNull();
            response.Error.Should().NotBeNull();
            response.Error!.Code.Should().Be(-32001);
            response.Error.Message.Should().Be("Not connected");
        }

        [Fact]
        [Requirement("TRANS-HTTP-DISPOSED-GUARD", "TRANS", RequirementType.Negative, "HttpTransport CallMethodAsync returns -32001 Not Connected when transport has been disposed.")]
        public async Task HttpTransport_CallMethodAsync_ReturnsDisposedError_WhenDisposed()
        {
            var server = new McpServer
            {
                Id = "http-test-disposed",
                Url = "http://localhost:9000/mcp",
                SecretProvider = "None"
            };

            var transport = new HttpTransport(server, new HttpClient(), NullLogger.Instance);
            transport.Dispose();

            var response = await transport.CallMethodAsync("tools/list", new { });
            response.Should().NotBeNull();
            response.Error.Should().NotBeNull();
            response.Error!.Code.Should().Be(-32001);
            response.Error.Message.Should().Be("Not connected");
        }

        [Fact]
        [Requirement("TRANS-STDIO-DISPOSED-GUARD", "TRANS", RequirementType.Negative, "StdioTransport SendRequestAsync returns -32001 Process Not Running when transport has been disposed.")]
        public async Task StdioTransport_SendRequestAsync_ReturnsProcessNotRunning_WhenDisposed()
        {
            var server = new McpServer
            {
                Id = "stdio-test-disposed",
                Url = "echo",
                SecretProvider = "None"
            };

            var stateManager = new JsonRpcStateManager();
            var transport = new StdioTransport(server, NullLogger.Instance, stateManager);
            transport.Dispose();

            var response = await transport.SendRequestAsync("tools/list", "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}");
            response.Should().NotBeNull();
            response.Error.Should().NotBeNull();
            response.Error!.Code.Should().Be(-32001);
            response.Error.Message.Should().Be("Process not running");
        }

        [Fact]
        [Requirement("MCP-MOCK-RESOURCES-PROMPTS-SUPPORT", "MCP", RequirementType.Positive, "MockDownstreamMcpServer handles resources/list and prompts/list MCP protocol methods.")]
        public async Task MockDownstreamMcpServer_HandlesResourcesAndPrompts()
        {
            var server = new MockDownstreamMcpServer();
            server.AddResource("test://config", "Configuration", "System configuration details", "application/json");
            server.AddPrompt("summarize", "Summarize the given text");
            var client = server.CreateHttpClient();

            // 1. resources/list
            var resReq = new HttpRequestMessage(HttpMethod.Post, "http://localhost/mcp")
            {
                Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":10,\"method\":\"resources/list\"}", Encoding.UTF8, "application/json")
            };
            var resResp = await client.SendAsync(resReq);
            resResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var resBody = await resResp.Content.ReadAsStringAsync();
            using var resDoc = JsonDocument.Parse(resBody);
            var resources = resDoc.RootElement.GetProperty("result").GetProperty("resources");
            resources.GetArrayLength().Should().Be(1);
            resources[0].GetProperty("name").GetString().Should().Be("Configuration");
            resources[0].GetProperty("uri").GetString().Should().Be("test://config");

            // 2. prompts/list
            var pReq = new HttpRequestMessage(HttpMethod.Post, "http://localhost/mcp")
            {
                Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":11,\"method\":\"prompts/list\"}", Encoding.UTF8, "application/json")
            };
            var pResp = await client.SendAsync(pReq);
            pResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var pBody = await pResp.Content.ReadAsStringAsync();
            using var pDoc = JsonDocument.Parse(pBody);
            var prompts = pDoc.RootElement.GetProperty("result").GetProperty("prompts");
            prompts.GetArrayLength().Should().Be(1);
            prompts[0].GetProperty("name").GetString().Should().Be("summarize");
        }
    }
}
