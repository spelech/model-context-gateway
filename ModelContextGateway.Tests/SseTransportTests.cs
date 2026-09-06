using System.Security;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ModelContextGateway.Tests
{
    public class SseTransportTests
    {
        /// <summary>
        /// Verifies that SSE transport resolves plaintext API key when secret provider is None.
        /// </summary>
        [Fact]
        [Requirement("TRANS-01", "SSE transport resolves static plaintext API keys when provider is None", Type = RequirementType.Positive, Category = "TRANS")]
        public async Task ResolveTokenAsync_ReturnsApiKey_WhenProviderNone()
        {
            var server = new McpServer
            {
                Id = "test-s1",
                Url = "http://localhost:8080/sse",
                SecretProvider = "None",
                ApiKey = "sse-plaintext-key"
            };

            var stateManager = new JsonRpcStateManager();
            var transport = new SseTransport(server, new HttpClient(), NullLogger.Instance, stateManager);
            var token = await transport.ResolveTokenAsync();
            Assert.Equal("sse-plaintext-key", token);
        }

        /// <summary>
        /// Ensures SSE transport fails closed with SecurityException when secret retriever fails.
        /// </summary>
        [Fact]
        [Requirement("GUARD-02", "SSE transport fails closed with SecurityException when secret provider resolution fails", Type = RequirementType.Negative, Category = "GUARD")]
        public async Task ResolveTokenAsync_ThrowsSecurityException_WhenSecretProviderFails()
        {
            var server = new McpServer
            {
                Id = "test-s1",
                Url = "http://localhost:8080/sse",
                SecretProvider = "Vault"
            };

            var mockRetriever = new EnvironmentSecretRetriever(); // returns null
            var stateManager = new JsonRpcStateManager();
            var transport = new SseTransport(server, new HttpClient(), NullLogger.Instance, stateManager, mockRetriever);

            await Assert.ThrowsAsync<SecurityException>(() => transport.ResolveTokenAsync());
        }

        /// <summary>
        /// Ensures SSE transport fails closed when no secret retriever is registered.
        /// </summary>
        [Fact]
        [Requirement("GUARD-02", "SSE transport fails closed with InvalidOperationException when no secret retriever is configured", Type = RequirementType.Negative, Category = "GUARD")]
        public async Task ResolveTokenAsync_ThrowsInvalidOperationException_WhenNoRetrieverRegistered()
        {
            var server = new McpServer
            {
                Id = "test-s1",
                Url = "http://localhost:8080/sse",
                SecretProvider = "Vault"
            };

            var stateManager = new JsonRpcStateManager();
            var transport = new SseTransport(server, new HttpClient(), NullLogger.Instance, stateManager, secretRetriever: null);
            await Assert.ThrowsAsync<InvalidOperationException>(() => transport.ResolveTokenAsync());
        }



        /// <summary>
        /// Ensures SSE transport handles exceptions when waiting for SSE endpoint URL gracefully by logging them.
        /// </summary>
        [Fact]
        [Requirement("TRANS-01", "TRANS", RequirementType.Positive, "SSE transport logs exceptions gracefully when waiting for endpoint URL without throwing unhandled exceptions.")]
        public async Task SendRequestAsync_HandlesEndpointWaitTimeoutGracefully()
        {
            var server = new McpServer
            {
                Id = "test-s1",
                Url = "http://localhost:8080/sse",
                SecretProvider = "None"
            };

            var stateManager = new JsonRpcStateManager();
            var transport = new SseTransport(server, new HttpClient(), NullLogger.Instance, stateManager);

            // Cancelling the internal CTS via Dispose causes the endpoint wait to complete immediately without sleeping 5 seconds
            transport.Dispose();
            var response = await transport.SendRequestAsync("testMethod", "{}");

            response.Should().NotBeNull();
            response.Error.Should().NotBeNull();
            response.Error!.Code.Should().Be(-32001);
            response.Error.Message.Should().Be("Not connected");
        }

        [Fact]
        [Requirement("TRANS-SSE-STREAM-LIFECYCLE", "TRANS", RequirementType.Positive, "SSE transport correctly resolves relative endpoint URLs and ignores keep-alive SSE comments.")]
        public async Task SseTransport_ResolvesRelativeEndpointUrl_AndProcessesKeepAliveComments()
        {
            var server = new McpServer
            {
                Id = "sse_srv",
                Url = "http://10.0.0.10:8080/sse",
                SecretProvider = "None"
            };

            var sseStream = new DynamicSseStream();
            sseStream.PushMessage(": keep-alive\n\n");
            sseStream.PushMessage(": ping\n\n");
            sseStream.PushMessage("event: endpoint\ndata: /api/mcp/messages?sessionId=test-sess-1\n\n");

            var mockHandler = new MockHttpMessageHandler();
            mockHandler.Handler = (req) =>
            {
                if (req.Method == HttpMethod.Get)
                {
                    var content = new StreamContent(sseStream);
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = content });
                }
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Accepted));
            };

            var httpClient = new HttpClient(mockHandler);
            var stateManager = new JsonRpcStateManager();
            var transport = new SseTransport(server, httpClient, NullLogger.Instance, stateManager);

            transport.StartReader(async (msg) => await Task.CompletedTask);

            var notificationTask = transport.SendNotificationAsync("notifications/initialized", "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
            var completed = await Task.WhenAny(notificationTask, Task.Delay(2000));
            completed.Should().Be(notificationTask, "Relative endpoint URL should be resolved from SSE stream without timing out");

            stateManager.IsDisconnected.Should().BeFalse();
            transport.Dispose();
            sseStream.Complete();
        }

        [Fact]
        [Requirement("TRANS-SSE-STREAM-LIFECYCLE", "TRANS", RequirementType.Positive, "SSE transport correlates incoming response event to active pending request.")]
        public async Task SseTransport_MultiplexesResponse_CorrelatingUpstreamRequestId()
        {
            var server = new McpServer
            {
                Id = "sse_srv",
                Url = "http://10.0.0.10:8080/sse",
                SecretProvider = "None"
            };

            var sseStream = new DynamicSseStream();
            sseStream.PushMessage("event: endpoint\ndata: http://10.0.0.10:8080/messages\n\n");

            string? capturedUpstreamId = null;
            var postReceived = new TaskCompletionSource<bool>();

            var mockHandler = new MockHttpMessageHandler();
            mockHandler.Handler = async (req) =>
            {
                if (req.Method == HttpMethod.Get)
                {
                    var content = new StreamContent(sseStream);
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = content };
                }
                if (req.Method == HttpMethod.Post)
                {
                    var body = await req.Content!.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    capturedUpstreamId = doc.RootElement.GetProperty("id").GetString();
                    postReceived.TrySetResult(true);
                    return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            };

            var httpClient = new HttpClient(mockHandler);
            var stateManager = new JsonRpcStateManager();
            var transport = new SseTransport(server, httpClient, NullLogger.Instance, stateManager);

            transport.StartReader(async (msg) =>
            {
                if (msg is JsonRpcResponse r && r.Id != null)
                {
                    stateManager.TryCompleteRequest(r.Id.ToString()!, r);
                }
                await Task.CompletedTask;
            });

            var sendTask = transport.SendRequestAsync("tools/call", "{\"jsonrpc\":\"2.0\",\"id\":\"client-req-42\",\"method\":\"tools/call\",\"params\":{\"name\":\"test\"}}");

            await postReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));
            capturedUpstreamId.Should().NotBeNull();

            // Push JSON-RPC response event with matching upstream ID
            sseStream.PushMessage($"event: message\ndata: {{\"jsonrpc\":\"2.0\",\"id\":\"{capturedUpstreamId}\",\"result\":{{\"success\":true}}}}\n\n");

            var resp = await sendTask.WaitAsync(TimeSpan.FromSeconds(2));
            resp.Should().NotBeNull();
            resp.Id!.ToString().Should().Be("client-req-42");
            resp.Result.Should().NotBeNull();
            resp.Result!.Value.GetProperty("success").GetBoolean().Should().BeTrue();

            transport.Dispose();
            sseStream.Complete();
        }
    }
}
