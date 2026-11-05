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

        /// <summary>
        /// Verifies that HttpTransport transparently recovers from an expired Mcp-Session-Id on 404, re-runs initialize, and retries the request.
        /// </summary>
        [Fact]
        [Requirement("TRANS-07", "HttpTransport transparently re-initializes and retries on 404 session expiration", Type = RequirementType.Positive, Category = "TRANS")]
        public async Task SendRequestAsync_RecoversFromExpiredSessionOn404()
        {
            var server = new McpServer
            {
                Id = "playwright-invisible",
                Url = "http://localhost:8080/mcp",
                Type = "http"
            };

            int initCount = 0;
            int notifCount = 0;
            int toolsCount = 0;

            var handler = new MockHttpMessageHandler(req =>
            {
                var content = req.Content != null ? req.Content.ReadAsStringAsync().GetAwaiter().GetResult() : string.Empty;

                if (content.Contains("\"method\":\"initialize\""))
                {
                    initCount++;
                    var initSession = initCount == 1 ? "sess-initial-1" : "sess-recovered-2";
                    var resp = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":\"2026-07-28\"}}", System.Text.Encoding.UTF8, "application/json")
                    };
                    resp.Headers.TryAddWithoutValidation("Mcp-Session-Id", initSession);
                    return Task.FromResult(resp);
                }

                if (content.Contains("\"method\":\"notifications/initialized\""))
                {
                    notifCount++;
                    var resp = new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
                    return Task.FromResult(resp);
                }

                if (content.Contains("\"method\":\"tools/list\""))
                {
                    toolsCount++;
                    req.Headers.TryGetValues("Mcp-Session-Id", out var sidVals);
                    var sid = sidVals?.FirstOrDefault();

                    // If request carries the old session id, simulate backend session expiration (404)
                    if (sid == "sess-initial-1")
                    {
                        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
                        {
                            Content = new StringContent("Session expired or not found", System.Text.Encoding.UTF8, "text/plain")
                        });
                    }

                    // Once recovered with the new session id, succeed
                    if (sid == "sess-recovered-2")
                    {
                        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{\"tools\":[{\"name\":\"browser_navigate\"}]}}", System.Text.Encoding.UTF8, "application/json")
                        });
                    }
                }

                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest));
            });

            var httpClient = new HttpClient(handler);
            using var transport = new HttpTransport(server, httpClient, NullLogger.Instance);

            // 1. Initial initialize call establishes sess-initial-1
            var initResp = await transport.SendRequestAsync("initialize", "{\"jsonrpc\":\"2.0\",\"method\":\"initialize\",\"id\":1}");
            Assert.NotNull(initResp.Result);
            Assert.Equal(1, initCount);

            // 2. tools/list initially hits 404, auto-recovers with new initialize, and succeeds
            var toolsResp = await transport.SendRequestAsync("tools/list", "{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\",\"id\":2}");
            Assert.NotNull(toolsResp.Result);
            Assert.True(toolsResp.Result.Value.TryGetProperty("tools", out var toolsArr));
            Assert.Equal("browser_navigate", toolsArr[0].GetProperty("name").GetString());

            Assert.Equal(2, initCount);
            Assert.Equal(1, notifCount);
            Assert.Equal(2, toolsCount);
        }

        /// <summary>
        /// Verifies that a genuine 404 on a request without Mcp-Session-Id does not trigger an initialize recursion loop.
        /// </summary>
        [Fact]
        [Requirement("TRANS-08", "HttpTransport 404 on sessionless request throws HttpRequestException without re-initializing", Type = RequirementType.Negative, Category = "TRANS")]
        public async Task SendRequestAsync_404WithoutSessionId_DoesNotTriggerReInit()
        {
            var server = new McpServer
            {
                Id = "dead-endpoint",
                Url = "http://localhost:8080/nonexistent",
                Type = "http"
            };

            int initCalls = 0;
            var handler = new MockHttpMessageHandler(req =>
            {
                var content = req.Content != null ? req.Content.ReadAsStringAsync().GetAwaiter().GetResult() : string.Empty;
                if (content.Contains("\"method\":\"initialize\""))
                {
                    initCalls++;
                }

                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
            });

            var httpClient = new HttpClient(handler);
            using var transport = new HttpTransport(server, httpClient, NullLogger.Instance);

            // Directly calling tools/list without prior session
            await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await transport.SendRequestAsync("tools/list", "{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\",\"id\":1}");
            });

            Assert.Equal(0, initCalls);
        }

        /// <summary>
        /// Verifies that non-2xx responses (e.g. 400 Bad Request) carrying Mcp-Session-Id do not poison the transport's session ID.
        /// </summary>
        [Fact]
        [Requirement("TRANS-09", "HttpTransport ignores Mcp-Session-Id headers on non-2xx responses to prevent session poisoning", Type = RequirementType.Negative, Category = "TRANS")]
        public async Task SendRequestAsync_Non2xxResponse_DoesNotPoisonSessionId()
        {
            var server = new McpServer
            {
                Id = "python-sdk-error",
                Url = "http://localhost:8080/mcp",
                Type = "http"
            };

            var handler = new MockHttpMessageHandler(req =>
            {
                // Returns 400 with a throwaway Mcp-Session-Id
                var resp = new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("Invalid client headers", System.Text.Encoding.UTF8, "text/plain")
                };
                resp.Headers.TryAddWithoutValidation("Mcp-Session-Id", "poisoned-session-uuid");
                return Task.FromResult(resp);
            });

            var httpClient = new HttpClient(handler);
            using var transport = new HttpTransport(server, httpClient, NullLogger.Instance);

            await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await transport.SendRequestAsync("tools/list", "{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\",\"id\":1}");
            });

            // Inspect internal session id via reflection to confirm it remained empty
            var field = typeof(HttpTransport).GetField("_sessionId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var currentSessionId = field?.GetValue(transport) as string;
            Assert.True(string.IsNullOrEmpty(currentSessionId));
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
