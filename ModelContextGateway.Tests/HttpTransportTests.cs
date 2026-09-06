using System.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace ModelContextGateway.Tests
{
    public class HttpTransportTests
    {
        /// <summary>
        /// Verifies that HTTP transport resolves plaintext API key when secret provider is None.
        /// </summary>
        [Fact]
        [Requirement("TRANS-HTTP-RESOLVE-STATIC-APIKEY", "HTTP stateless transport resolves static API keys when secret provider is None", Type = RequirementType.Positive, Category = "TRANS")]
        public async Task ResolveTokenAsync_ReturnsApiKey_WhenProviderNone()
        {
            var server = new McpServer
            {
                Id = "test-s1",
                Url = "http://localhost:8080/mcp",
                SecretProvider = "None",
                ApiKey = "plaintext-api-key"
            };

            var transport = new HttpTransport(server, new HttpClient(), NullLogger.Instance);
            var token = await transport.ResolveTokenAsync();
            Assert.Equal("plaintext-api-key", token);
        }

        /// <summary>
        /// Ensures HTTP transport fails closed with SecurityException when secret retriever fails.
        /// </summary>
        [Fact]
        [Requirement("GUARD-HTTP-SECRET-RETRIEVAL-FAIL-CLOSED", "HTTP stateless transport fails closed with SecurityException when secret resolution fails", Type = RequirementType.Negative, Category = "GUARD")]
        public async Task ResolveTokenAsync_ThrowsSecurityException_WhenSecretProviderFails()
        {
            var server = new McpServer
            {
                Id = "test-s1",
                Url = "http://localhost:8080/mcp",
                SecretProvider = "Vault",
                SecretMount = "secret",
                SecretPath = "my-app",
                SecretField = "key"
            };

            var mockRetriever = new EnvironmentSecretRetriever(); // returns null
            var transport = new HttpTransport(server, new HttpClient(), NullLogger.Instance, mockRetriever);

            await Assert.ThrowsAsync<SecurityException>(() => transport.ResolveTokenAsync());
        }

        /// <summary>
        /// Ensures HTTP transport fails closed when no secret retriever is registered.
        /// </summary>
        [Fact]
        [Requirement("GUARD-HTTP-NO-SECRET-RETRIEVER", "HTTP stateless transport fails closed with InvalidOperationException when no secret retriever is configured", Type = RequirementType.Negative, Category = "GUARD")]
        public async Task ResolveTokenAsync_ThrowsInvalidOperationException_WhenNoRetrieverRegistered()
        {
            var server = new McpServer
            {
                Id = "test-s1",
                Url = "http://localhost:8080/mcp",
                SecretProvider = "Vault"
            };

            var transport = new HttpTransport(server, new HttpClient(), NullLogger.Instance, secretRetriever: null);
            await Assert.ThrowsAsync<InvalidOperationException>(() => transport.ResolveTokenAsync());
        }

        /// <summary>
        /// Verifies that HTTP transport correctly parses FastMCP SSE streams containing intermediate notification events before the response.
        /// </summary>
        [Fact]
        [Requirement("TRANS-04", "HTTP stateless transport correctly accumulates multi-line SSE streams and skips intermediate notification events", Type = RequirementType.Positive, Category = "TRANS")]
        public async Task SendRequestAsync_ParsesSseStreamWithIntermediateNotifications()
        {
            var server = new McpServer
            {
                Id = "ha",
                Url = "http://localhost:8080/mcp",
                Type = "http"
            };

            var ssePayload = "event: message\ndata: {\"jsonrpc\":\"2.0\",\"method\":\"notifications/message\",\"params\":{\"level\":\"info\",\"data\":{\"msg\":\"deep_search starting...\"}}}\n\nevent: message\ndata: {\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"tools\":[{\"name\":\"ha_search\",\"description\":\"Search Home Assistant entities\"}]}}\n\n";

            var handler = new MockHttpMessageHandler(req =>
            {
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(ssePayload, System.Text.Encoding.UTF8, "text/event-stream")
                };
                return Task.FromResult(response);
            });

            var httpClient = new HttpClient(handler);
            var transport = new HttpTransport(server, httpClient, NullLogger.Instance);

            var reqBody = "{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\",\"id\":1}";
            var resp = await transport.SendRequestAsync("tools/list", reqBody);

            Assert.NotNull(resp.Result);
            Assert.True(resp.Result.Value.TryGetProperty("tools", out var toolsList));
            Assert.Equal(1, toolsList.GetArrayLength());
            Assert.Equal("ha_search", toolsList[0].GetProperty("name").GetString());
        }

        /// <summary>
        /// Verifies that HTTP transport parses indented / multi-line formatted JSON bodies without truncation.
        /// </summary>
        [Fact]
        [Requirement("TRANS-05", "HTTP stateless transport reads entire multi-line and formatted JSON response bodies without premature truncation", Type = RequirementType.Positive, Category = "TRANS")]
        public async Task SendRequestAsync_ParsesFormattedMultiLineJsonPayload()
        {
            var server = new McpServer
            {
                Id = "ha",
                Url = "http://localhost:8080/mcp",
                Type = "http"
            };

            var formattedJson = "{\n  \"jsonrpc\": \"2.0\",\n  \"id\": 1,\n  \"result\": {\n    \"tools\": [\n      {\n        \"name\": \"ha_call_service\",\n        \"description\": \"Execute Home Assistant service\"\n      }\n    ]\n  }\n}";

            var handler = new MockHttpMessageHandler(req =>
            {
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(formattedJson, System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            });

            var httpClient = new HttpClient(handler);
            var transport = new HttpTransport(server, httpClient, NullLogger.Instance);

            var reqBody = "{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\",\"id\":1}";
            var resp = await transport.SendRequestAsync("tools/list", reqBody);

            Assert.NotNull(resp.Result);
            Assert.True(resp.Result.Value.TryGetProperty("tools", out var toolsList));
            Assert.Equal(1, toolsList.GetArrayLength());
            Assert.Equal("ha_call_service", toolsList[0].GetProperty("name").GetString());
        }

        /// <summary>
        /// Verifies that HTTP transport joins multi-line SSE data fields into a complete JSON payload.
        /// </summary>
        [Fact]
        [Requirement("TRANS-06", "HTTP stateless transport joins multi-line SSE data fields into complete JSON payloads", Type = RequirementType.Positive, Category = "TRANS")]
        public async Task SendRequestAsync_ParsesMultiLineDataLinesInSse()
        {
            var server = new McpServer
            {
                Id = "ha",
                Url = "http://localhost:8080/mcp",
                Type = "http"
            };

            var multiLineSse = "event: message\ndata: {\"jsonrpc\":\"2.0\",\"id\":1,\ndata: \"result\":{\"tools\":[{\"name\":\"ha_get_state\"}]}}\n\n";

            var handler = new MockHttpMessageHandler(req =>
            {
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(multiLineSse, System.Text.Encoding.UTF8, "text/event-stream")
                };
                return Task.FromResult(response);
            });

            var httpClient = new HttpClient(handler);
            var transport = new HttpTransport(server, httpClient, NullLogger.Instance);

            var reqBody = "{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\",\"id\":1}";
            var resp = await transport.SendRequestAsync("tools/list", reqBody);

            Assert.NotNull(resp.Result);
            Assert.True(resp.Result.Value.TryGetProperty("tools", out var toolsList));
            Assert.Equal("ha_get_state", toolsList[0].GetProperty("name").GetString());
        }

        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

            public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return _handler(request);
            }
        }
    }
}
