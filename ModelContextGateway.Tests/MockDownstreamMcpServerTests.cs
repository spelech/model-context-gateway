using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using ModelContextGateway.Tests.TestHelpers;

namespace ModelContextGateway.Tests
{
    public class MockDownstreamMcpServerTests
    {
        [Fact]
        [Requirement("MCP-01", "MCP", RequirementType.Positive, "MockDownstreamMcpServer handles initialize, initialized, tools/list, and tools/call JSON-RPC 2.0 protocol cycles.")]
        public async Task MockDownstreamMcpServer_HandlesStandardJsonRpcFlow()
        {
            var server = new MockDownstreamMcpServer();
            server.AddTool("custom_tool", "Custom tool description");
            var client = server.CreateHttpClient();

            // 1. initialize
            var initReq = new HttpRequestMessage(HttpMethod.Post, "http://localhost/mcp")
            {
                Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\"}}", Encoding.UTF8, "application/json")
            };
            var initResp = await client.SendAsync(initReq);
            initResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var initBody = await initResp.Content.ReadAsStringAsync();
            using var initDoc = JsonDocument.Parse(initBody);
            initDoc.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString().Should().Be("2024-11-05");

            // 2. notifications/initialized
            var notifReq = new HttpRequestMessage(HttpMethod.Post, "http://localhost/mcp")
            {
                Content = new StringContent("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}", Encoding.UTF8, "application/json")
            };
            var notifResp = await client.SendAsync(notifReq);
            notifResp.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. tools/list
            var listReq = new HttpRequestMessage(HttpMethod.Post, "http://localhost/mcp")
            {
                Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}", Encoding.UTF8, "application/json")
            };
            var listResp = await client.SendAsync(listReq);
            listResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var listBody = await listResp.Content.ReadAsStringAsync();
            using var listDoc = JsonDocument.Parse(listBody);
            var tools = listDoc.RootElement.GetProperty("result").GetProperty("tools");
            tools.GetArrayLength().Should().Be(2);

            // 4. tools/call
            var callReq = new HttpRequestMessage(HttpMethod.Post, "http://localhost/mcp")
            {
                Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"custom_tool\",\"arguments\":{}}}", Encoding.UTF8, "application/json")
            };
            var callResp = await client.SendAsync(callReq);
            callResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var callBody = await callResp.Content.ReadAsStringAsync();
            using var callDoc = JsonDocument.Parse(callBody);
            callDoc.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString().Should().Contain("custom_tool");
        }

        [Fact]
        [Requirement("AUTH-14", "AUTH", RequirementType.Positive, "MockDownstreamMcpServer simulates 401 Unauthorized status code for authentication testing.")]
        public async Task MockDownstreamMcpServer_Simulates401Unauthorized()
        {
            var server = new MockDownstreamMcpServer
            {
                ReturnUnauthorized = true
            };
            var client = server.CreateHttpClient();

            var req = new HttpRequestMessage(HttpMethod.Post, "http://localhost/mcp")
            {
                Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}", Encoding.UTF8, "application/json")
            };
            var resp = await client.SendAsync(req);
            resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
