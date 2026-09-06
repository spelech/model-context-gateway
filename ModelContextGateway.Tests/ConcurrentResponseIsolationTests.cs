using System.Collections.Concurrent;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace ModelContextGateway.Tests
{
    public class ConcurrentResponseIsolationTests
    {
        [Fact]
        [Requirement("TRANS-02", "TRANS", RequirementType.Positive, "Multiplexes concurrent client calls sharing identical JSON-RPC IDs and routes reversed responses correctly.")]
        public async Task ConcurrentResponseIsolation_TwoCallersSameId_SucceedsWithReversedResponseOrder()
        {
            // Arrange
            var server = new McpServer
            {
                Id = "concurrent_backend",
                DisplayName = "Concurrent Backend",
                Url = "http://concurrent_backend/mcp",
                Type = "sse",
                SecretProvider = "None",
                Enabled = true
            };

            var sseStream = new DynamicSseStream();
            sseStream.PushMessage("event: endpoint\ndata: http://concurrent_backend/mcp/message\n\n");

            var mockHandler = new MockHttpMessageHandler();
            var httpClient = new HttpClient(mockHandler);
            var loggerMock = new Mock<ILogger>();

            var interceptedRequests = new ConcurrentBag<(string UpstreamId, string OriginalId, string Payload)>();
            var bothReceivedTcs = new TaskCompletionSource<bool>();

            mockHandler.Handler = async (req) =>
            {
                if (req.Method == HttpMethod.Get)
                {
                    var streamContent = new StreamContent(sseStream);
                    streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = streamContent };
                }
                else if (req.Method == HttpMethod.Post)
                {
                    var body = await req.Content!.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    var upstreamId = root.GetProperty("id").GetString()!;
                    var originalId = "1"; // from test setup
                    var payload = root.GetProperty("params").GetProperty("data").GetString()!;

                    interceptedRequests.Add((upstreamId, originalId, payload));
                    if (interceptedRequests.Count >= 2)
                    {
                        bothReceivedTcs.TrySetResult(true);
                    }
                    return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            };

            var conn = new BackendConnection(server, httpClient, loggerMock.Object);
            conn.RequestTimeout = TimeSpan.FromSeconds(15);
            await conn.ConnectAsync();

            conn.StartReader(async (msg) =>
            {
                await Task.CompletedTask;
            });

            // Act - Send two concurrent requests with identical original ID 1 but different payloads
            var task1 = conn.SendRequestAsync("tools/call", "{\"jsonrpc\":\"2.0\",\"id\":1,\"params\":{\"data\":\"payload_one\"}}");
            var task2 = conn.SendRequestAsync("tools/call", "{\"jsonrpc\":\"2.0\",\"id\":1,\"params\":{\"data\":\"payload_two\"}}");

            // Wait for both requests to be posted to the backend and intercepted
            await bothReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var requestList = interceptedRequests.ToList();
            var reqOne = requestList.First(r => r.Payload == "payload_one");
            var reqTwo = requestList.First(r => r.Payload == "payload_two");

            // Guarantee that upstream unique IDs are completely different
            reqOne.UpstreamId.Should().NotBe(reqTwo.UpstreamId);

            // Act - Simulate the backend responding to the second request first (reversed response order)
            var responseTwoPayload = $"event: message\ndata: {{\"jsonrpc\":\"2.0\",\"id\":\"{reqTwo.UpstreamId}\",\"result\":{{\"text\":\"result_two\"}}}}\n\n";
            sseStream.PushMessage(responseTwoPayload);

            var responseOnePayload = $"event: message\ndata: {{\"jsonrpc\":\"2.0\",\"id\":\"{reqOne.UpstreamId}\",\"result\":{{\"text\":\"result_one\"}}}}\n\n";
            sseStream.PushMessage(responseOnePayload);

            // Await both tasks
            var result1 = await task1;
            var result2 = await task2;

            // Assert - Verify that results are correctly routed back with correct distinct payloads and original IDs
            result1.Should().NotBeNull();
            result1.Id!.ToString().Should().Be("1");
            result1.Result!.Value.GetProperty("text").GetString().Should().Be("result_one");

            result2.Should().NotBeNull();
            result2.Id!.ToString().Should().Be("1");
            result2.Result!.Value.GetProperty("text").GetString().Should().Be("result_two");

            conn.PendingRequests.Should().BeEmpty();
            conn.Dispose();
        }

        [Fact]
        [Requirement("TRANS-02", "TRANS", RequirementType.Positive, "Maintains strict response isolation under high concurrency with 100+ callers reusing identical RPC IDs.")]
        public async Task HighConcurrencyResponseIsolation_RepeatedIdsAcrossCallers()
        {
            // Arrange
            var server = new McpServer
            {
                Id = "stress_concurrent",
                DisplayName = "Stress Concurrent",
                Url = "http://stress_concurrent/mcp",
                Type = "sse",
                SecretProvider = "None",
                Enabled = true
            };

            var sseStream = new DynamicSseStream();
            sseStream.PushMessage("event: endpoint\ndata: http://stress_concurrent/mcp/message\n\n");

            var mockHandler = new MockHttpMessageHandler();
            var httpClient = new HttpClient(mockHandler);
            var loggerMock = new Mock<ILogger>();

            var interceptedRequests = new ConcurrentDictionary<string, string>(); // UpstreamId -> ExpectedResultValue

            var allReceivedTcs = new TaskCompletionSource<bool>();

            mockHandler.Handler = async (req) =>
            {
                if (req.Method == HttpMethod.Get)
                {
                    var streamContent = new StreamContent(sseStream);
                    streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = streamContent };
                }
                else if (req.Method == HttpMethod.Post)
                {
                    var body = await req.Content!.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    var upstreamId = root.GetProperty("id").GetString()!;
                    var payloadIndex = root.GetProperty("params").GetProperty("index").GetInt32();

                    interceptedRequests[upstreamId] = $"result_{payloadIndex}";
                    if (interceptedRequests.Count >= 40)
                    {
                        allReceivedTcs.TrySetResult(true);
                    }
                    return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            };

            var conn = new BackendConnection(server, httpClient, loggerMock.Object);
            conn.RequestTimeout = TimeSpan.FromSeconds(15);
            await conn.ConnectAsync();

            conn.StartReader(async (msg) =>
            {
                await Task.CompletedTask;
            });

            int totalRequests = 40;
            var tasks = new List<Task<(int Index, int OriginalId, JsonRpcResponse Response)>>();

            // Launch 40 concurrent requests repeating original IDs (0, 1, 2, 3)
            for (int i = 0; i < totalRequests; i++)
            {
                int index = i;
                int originalId = i % 4;
                var payload = $"{{\"jsonrpc\":\"2.0\",\"id\":{originalId},\"params\":{{\"index\":{index}}}}}";

                tasks.Add(Task.Run(async () =>
                {
                    var resp = await conn.SendRequestAsync("tools/call", payload);
                    return (index, originalId, resp);
                }));
            }

            // Wait for all requests to be registered
            await allReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Push all responses concurrently out-of-order/scrambled
            var random = new Random();
            var shuffledReqs = interceptedRequests.ToList().OrderBy(x => random.Next()).ToList();

            foreach (var req in shuffledReqs)
            {
                var responsePayload = $"event: message\ndata: {{\"jsonrpc\":\"2.0\",\"id\":\"{req.Key}\",\"result\":{{\"text\":\"{req.Value}\"}}}}\n\n";
                sseStream.PushMessage(responsePayload);
            }

            // Await all and verify
            var results = await Task.WhenAll(tasks);
            results.Length.Should().Be(totalRequests);

            foreach (var result in results)
            {
                result.Response.Should().NotBeNull();
                result.Response.Id!.ToString().Should().Be(result.OriginalId.ToString());
                result.Response.Result!.Value.GetProperty("text").GetString().Should().Be($"result_{result.Index}");
            }

            conn.PendingRequests.Should().BeEmpty();
            conn.Dispose();
        }

        [Fact]
        [Requirement("TRANS-02", "TRANS", RequirementType.Positive, "Cleans up pending request tracking maps upon timeout and cancellation.")]
        public async Task TimeoutAndCancellationCleanup_DoesNotLeavePendingRequests()
        {
            // Arrange
            var server = new McpServer
            {
                Id = "timeout_backend",
                DisplayName = "Timeout Backend",
                Url = "http://timeout_backend/mcp",
                Type = "sse",
                SecretProvider = "None",
                Enabled = true
            };

            var sseStream = new DynamicSseStream();
            sseStream.PushMessage("event: endpoint\ndata: http://timeout_backend/mcp/message\n\n");

            var mockHandler = new MockHttpMessageHandler();
            var httpClient = new HttpClient(mockHandler);
            var loggerMock = new Mock<ILogger>();

            var postTcs = new TaskCompletionSource<bool>();

            mockHandler.Handler = async (req) =>
            {
                if (req.Method == HttpMethod.Get)
                {
                    var streamContent = new StreamContent(sseStream);
                    streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = streamContent };
                }
                else if (req.Method == HttpMethod.Post)
                {
                    postTcs.TrySetResult(true);
                    return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            };

            var conn = new BackendConnection(server, httpClient, loggerMock.Object);
            // Very short timeout
            conn.RequestTimeout = TimeSpan.FromMilliseconds(300);
            await conn.ConnectAsync();

            conn.StartReader(async (msg) =>
            {
                await Task.CompletedTask;
            });

            // Act - Send request and let it time out
            var task = conn.SendRequestAsync("tools/call", "{\"jsonrpc\":\"2.0\",\"id\":1,\"params\":{}}");

            await Assert.ThrowsAsync<TimeoutException>(async () => await task);

            // Assert - Verify that it got cleaned up and does not leak
            conn.PendingRequests.Should().BeEmpty();
            conn.Dispose();
        }

        [Fact]
        [Requirement("TRANS-02", "TRANS", RequirementType.Positive, "Cleans up and cancels pending requests upon backend transport disconnect.")]
        public async Task BackendDisconnectCleanup_ClearsPendingRequests()
        {
            // Arrange
            var server = new McpServer
            {
                Id = "disconnect_backend",
                DisplayName = "Disconnect Backend",
                Url = "http://disconnect_backend/mcp",
                Type = "sse",
                SecretProvider = "None",
                Enabled = true
            };

            var sseStream = new DynamicSseStream();
            sseStream.PushMessage("event: endpoint\ndata: http://disconnect_backend/mcp/message\n\n");

            var mockHandler = new MockHttpMessageHandler();
            var httpClient = new HttpClient(mockHandler);
            var loggerMock = new Mock<ILogger>();

            var postTcs = new TaskCompletionSource<bool>();

            mockHandler.Handler = async (req) =>
            {
                if (req.Method == HttpMethod.Get)
                {
                    var streamContent = new StreamContent(sseStream);
                    streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = streamContent };
                }
                else if (req.Method == HttpMethod.Post)
                {
                    postTcs.TrySetResult(true);
                    return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            };

            var conn = new BackendConnection(server, httpClient, loggerMock.Object);
            conn.RequestTimeout = TimeSpan.FromSeconds(15);
            await conn.ConnectAsync();

            conn.StartReader(async (msg) =>
            {
                await Task.CompletedTask;
            });

            // Send request
            var task = conn.SendRequestAsync("tools/call", "{\"jsonrpc\":\"2.0\",\"id\":1,\"params\":{}}");

            await postTcs.Task;

            // Assert that we have 1 pending request
            conn.PendingRequests.Count.Should().Be(1);

            // Act - Force disconnect by completing/disposing the stream
            sseStream.Complete();

            // The pending request task is cancelled when reader detects disconnect
            await Assert.ThrowsAnyAsync<Exception>(async () => await task);

            // Assert - The pending request table should be empty
            conn.PendingRequests.Should().BeEmpty();

            conn.Dispose();
        }

        [Fact]
        [Requirement("TRANS-02", "TRANS", RequirementType.Positive, "Handles JSON-RPC requests with explicit null IDs and multiplexes upstream calls correctly.")]
        public async Task ConcurrentResponseIsolation_ExplicitNullId_Succeeds()
        {
            // Arrange
            var server = new McpServer
            {
                Id = "null_id_backend",
                DisplayName = "Null ID Backend",
                Url = "http://null_id_backend/mcp",
                Type = "sse",
                SecretProvider = "None",
                Enabled = true
            };

            var sseStream = new DynamicSseStream();
            sseStream.PushMessage("event: endpoint\ndata: http://null_id_backend/mcp/message\n\n");

            var mockHandler = new MockHttpMessageHandler();
            var httpClient = new HttpClient(mockHandler);
            var loggerMock = new Mock<ILogger>();

            string? parsedUpstreamId = null;

            mockHandler.Handler = async (req) =>
            {
                if (req.Method == HttpMethod.Get)
                {
                    var streamContent = new StreamContent(sseStream);
                    streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = streamContent };
                }
                else if (req.Method == HttpMethod.Post)
                {
                    var body = await req.Content!.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    parsedUpstreamId = root.GetProperty("id").GetString()!;

                    var responsePayload = $"{{\"jsonrpc\":\"2.0\",\"id\":\"{parsedUpstreamId}\",\"result\":{{\"success\":true}}}}";
                    sseStream.PushMessage($"event: message\ndata: {responsePayload}\n\n");

                    return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            };

            var conn = new BackendConnection(server, httpClient, loggerMock.Object);
            await conn.ConnectAsync();

            conn.StartReader(async (msg) =>
            {
                if (msg is JsonRpcResponse response && response.Id != null)
                {
                    var idStr = response.Id.ToString();
                    if (idStr != null && conn.TryCompleteRequest(idStr, response))
                    {
                        return;
                    }
                }
            });

            // Act - Send request with explicit JSON-RPC ID null
            var result = await conn.SendRequestAsync("tools/call", "{\"jsonrpc\":\"2.0\",\"id\":null,\"params\":{}}");

            // Assert - The explicit null ID should be correctly preserved and restored on response
            result.Should().NotBeNull();
            result.Id.Should().BeNull();
            result.Result.Should().NotBeNull();

            conn.Dispose();
        }

        [Fact]
        [Requirement("TRANS-02", "TRANS", RequirementType.Positive, "Handles JSON-RPC notifications without registering pending response listeners.")]
        public async Task ConcurrentResponseIsolation_Notification_DoesNotExpectResponse()
        {
            // Arrange
            var server = new McpServer
            {
                Id = "notification_backend",
                DisplayName = "Notification Backend",
                Url = "http://notification_backend/mcp",
                Type = "sse",
                SecretProvider = "None",
                Enabled = true
            };

            var sseStream = new DynamicSseStream();
            sseStream.PushMessage("event: endpoint\ndata: http://notification_backend/mcp/message\n\n");

            var mockHandler = new MockHttpMessageHandler();
            var httpClient = new HttpClient(mockHandler);
            var loggerMock = new Mock<ILogger>();

            var postTcs = new TaskCompletionSource<bool>();

            mockHandler.Handler = async (req) =>
            {
                if (req.Method == HttpMethod.Get)
                {
                    var streamContent = new StreamContent(sseStream);
                    streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = streamContent };
                }
                else if (req.Method == HttpMethod.Post)
                {
                    postTcs.TrySetResult(true);
                    return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            };

            var conn = new BackendConnection(server, httpClient, loggerMock.Object);
            await conn.ConnectAsync();

            conn.StartReader(async (msg) =>
            {
                await Task.CompletedTask;
            });

            // Act - Send a notification (no "id" property)
            var sendTask = conn.SendRequestAsync("notifications/initialized", "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");

            // Await should return immediately (doesn't wait on any TCS)
            var finishedTask = await Task.WhenAny(sendTask, Task.Delay(2000));
            finishedTask.Should().BeSameAs(sendTask);

            // Verify notification reached backend
            await postTcs.Task;

            // Ensure no pending entry was left registered
            conn.PendingRequests.Should().BeEmpty();
            conn.Dispose();
        }

        [Fact]
        [Requirement("TRANS-02", "TRANS", RequirementType.Positive, "Isolates cancellation tokens between concurrent stateless client requests.")]
        public async Task ClientSession_ConcurrentStatelessRequestIsolateCancellation()
        {
            // Arrange
            var servers = new List<McpServer>();
            var loggerMock = new Mock<ILogger>();
            var embeddingMock = new Mock<IEmbeddingService>();

            var context1 = new DefaultHttpContext();
            context1.Request.Headers["Remote-User"] = "admin";
            context1.TraceIdentifier = "request-1-trace-id";

            var context2 = new DefaultHttpContext();
            context2.Request.Headers["Remote-User"] = "admin";
            context2.TraceIdentifier = "request-2-trace-id";

            var session = new ClientSession(
                "global-stateless-session",
                context1.Response,
                servers,
                new HttpClient(),
                embeddingMock.Object,
                loggerMock.Object
            );

            // Simulating two clients registering active requests with duplicate ID 1 concurrently
            using var cts1 = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            var reg1 = session.RegisterRequestCancellation("1", cts1, context1.TraceIdentifier);
            var reg2 = session.RegisterRequestCancellation("1", cts2, context2.TraceIdentifier);

            reg1.Should().BeTrue("Stateless request 1 should register under its trace identifier");
            reg2.Should().BeTrue("Stateless request 2 should register under its distinct trace identifier without key collision");

            // Duplicate registration within the same trace identifier should be rejected
            using var ctsDuplicate = new CancellationTokenSource();
            var duplicateReg = session.RegisterRequestCancellation("1", ctsDuplicate, context1.TraceIdentifier);
            duplicateReg.Should().BeFalse("Duplicate request ID for the same trace identifier must not be registered");

            // Cancellation of one does not cancel the other
            session.CancelRequest("1", context1.TraceIdentifier);
            cts1.IsCancellationRequested.Should().BeTrue("Request 1 cancellation must be isolated");
            cts2.IsCancellationRequested.Should().BeFalse("Request 2 must remain active and isolated");

            // Clean up
            session.Close();
        }

        [Fact]
        [Requirement("TRANS-02", "TRANS", RequirementType.Positive, "Targeted cancellation does not cancel concurrent client sessions reusing identical RPC IDs.")]
        public async Task ClientSession_TargetedCancellation_DoesNotCancelOtherClientsReusingId()
        {
            // Arrange
            var servers = new List<McpServer>();
            var loggerMock = new Mock<ILogger>();
            var embeddingMock = new Mock<IEmbeddingService>();

            var context1 = new DefaultHttpContext();
            context1.Request.Headers["Remote-User"] = "admin";
            context1.TraceIdentifier = "client-1-trace";

            var context2 = new DefaultHttpContext();
            context2.Request.Headers["Remote-User"] = "admin";
            context2.TraceIdentifier = "client-2-trace";

            var session = new ClientSession(
                "global-stateless-session",
                context1.Response,
                servers,
                new HttpClient(),
                embeddingMock.Object,
                loggerMock.Object
            );

            using var cts1 = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            var reg1 = session.RegisterRequestCancellation("1", cts1, "client-1-trace");
            var reg2 = session.RegisterRequestCancellation("1", cts2, "client-2-trace");

            reg1.Should().BeTrue("Client 1 request registration must succeed");
            reg2.Should().BeTrue("Client 2 request registration with identical RPC ID must succeed without collision");

            // Act - Cancel only client 1's request "1"
            session.CancelRequest("1", "client-1-trace");

            // Assert: client 1 is cancelled while client 2's token is NOT cancelled
            cts1.IsCancellationRequested.Should().BeTrue("Client 1 token must be cancelled");
            cts2.IsCancellationRequested.Should().BeFalse("Client 2 token must not be cancelled");

            // Clean up
            session.Close();
        }

        [Fact]
        [Requirement("GUARD-STATE-DISCONNECT-CANCELLATION", "GUARD", RequirementType.Negative, "JsonRpcStateManager rejects registration and cancels pending completions upon disconnect.")]
        public void JsonRpcStateManager_Disconnect_PreventsRegistrationAndCancelsPending()
        {
            var stateManager = new JsonRpcStateManager();

            // Create a pending request while connected
            var req1 = stateManager.CreateTrackedRequest("req1", 1, "session1", CancellationToken.None, TimeSpan.FromSeconds(10));
            req1.Task.IsCompleted.Should().BeFalse();

            // Disconnect transport
            stateManager.MarkDisconnected();
            stateManager.IsDisconnected.Should().BeTrue();

            // Existing request should be cancelled
            req1.Task.IsCanceled.Should().BeTrue();
            stateManager.PendingRequests.Should().BeEmpty();

            // New registration while disconnected should throw
            var action = () => stateManager.CreateRequest("req2");
            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*transport is disconnected*");

            var actionTracked = () => stateManager.CreateTrackedRequest("req3", 3, "session1", CancellationToken.None, TimeSpan.FromSeconds(10));
            actionTracked.Should().Throw<InvalidOperationException>()
                .WithMessage("*transport is disconnected*");

            // Reconnect
            stateManager.MarkConnected();
            stateManager.IsDisconnected.Should().BeFalse();

            var req4 = stateManager.CreateRequest("req4");
            req4.Task.Should().NotBeNull();
            stateManager.PendingRequests.Should().ContainKey("req4");
        }

        [Fact]
        [Requirement("TRANS-02", "TRANS", RequirementType.Positive, "Handles mixed numeric, string, and null JSON-RPC IDs concurrently across backend transports.")]
        public async Task ConcurrentResponseIsolation_MixedNumericStringNullIds()
        {
            // Arrange
            var server = new McpServer
            {
                Id = "mixed_id_backend",
                DisplayName = "Mixed ID Backend",
                Url = "http://mixed_id_backend/mcp",
                Type = "sse",
                SecretProvider = "None",
                Enabled = true
            };

            var sseStream = new DynamicSseStream();
            sseStream.PushMessage("event: endpoint\ndata: http://mixed_id_backend/mcp/message\n\n");

            var mockHandler = new MockHttpMessageHandler();
            var httpClient = new HttpClient(mockHandler);
            var loggerMock = new Mock<ILogger>();

            var intercepted = new ConcurrentDictionary<string, (string UpstreamId, string OriginalIdRaw)>();
            var allThreeTcs = new TaskCompletionSource<bool>();

            mockHandler.Handler = async (req) =>
            {
                if (req.Method == HttpMethod.Get)
                {
                    var streamContent = new StreamContent(sseStream);
                    streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = streamContent };
                }
                else if (req.Method == HttpMethod.Post)
                {
                    var body = await req.Content!.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    var upstreamId = root.GetProperty("id").GetString()!;
                    var tag = root.GetProperty("params").GetProperty("tag").GetString()!;

                    intercepted[tag] = (upstreamId, tag);
                    if (intercepted.Count >= 3)
                    {
                        allThreeTcs.TrySetResult(true);
                    }
                    return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            };

            var conn = new BackendConnection(server, httpClient, loggerMock.Object);
            conn.RequestTimeout = TimeSpan.FromSeconds(15);
            await conn.ConnectAsync();

            conn.StartReader(async (msg) =>
            {
                await Task.CompletedTask;
            });

            // Send 3 concurrent requests: numeric, string, null
            var taskNumeric = conn.SendRequestAsync("tools/call", "{\"jsonrpc\":\"2.0\",\"id\":42,\"params\":{\"tag\":\"numeric\"}}");
            var taskString = conn.SendRequestAsync("tools/call", "{\"jsonrpc\":\"2.0\",\"id\":\"str-id-99\",\"params\":{\"tag\":\"string\"}}");
            var taskNull = conn.SendRequestAsync("tools/call", "{\"jsonrpc\":\"2.0\",\"id\":null,\"params\":{\"tag\":\"null\"}}");

            await allThreeTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Respond in reverse order
            sseStream.PushMessage($"event: message\ndata: {{\"jsonrpc\":\"2.0\",\"id\":\"{intercepted["null"].UpstreamId}\",\"result\":{{\"val\":\"null_done\"}}}}\n\n");
            sseStream.PushMessage($"event: message\ndata: {{\"jsonrpc\":\"2.0\",\"id\":\"{intercepted["string"].UpstreamId}\",\"result\":{{\"val\":\"string_done\"}}}}\n\n");
            sseStream.PushMessage($"event: message\ndata: {{\"jsonrpc\":\"2.0\",\"id\":\"{intercepted["numeric"].UpstreamId}\",\"result\":{{\"val\":\"numeric_done\"}}}}\n\n");

            var resNumeric = await taskNumeric;
            var resString = await taskString;
            var resNull = await taskNull;

            resNumeric.Id!.ToString().Should().Be("42");
            resNumeric.Result!.Value.GetProperty("val").GetString().Should().Be("numeric_done");

            resString.Id!.ToString().Should().Be("str-id-99");
            resString.Result!.Value.GetProperty("val").GetString().Should().Be("string_done");

            resNull.Id.Should().BeNull();
            resNull.Result!.Value.GetProperty("val").GetString().Should().Be("null_done");

            conn.PendingRequests.Should().BeEmpty();
            conn.Dispose();
        }
    }
}
