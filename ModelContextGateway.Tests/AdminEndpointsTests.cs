using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ModelContextGateway.Tests
{
    public class AdminEndpointsTestFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "ConnectionStrings:Sqlite", $"Data Source=file:admin_endpoint_tests_{Guid.NewGuid():N}?mode=memory&cache=shared" },
                    { "DB_ENCRYPTION_KEY", "TestSecretKey1234567890123456789012" },
                    { "Oidc:TrustedProxies", "127.0.0.1,::1,127.0.0.1:0" },
                    { "Oidc:RequireTrustedProxy", "false" },
                    { "Admin:GroupSid", "S-1-5-32-544" },
                    { "Admin:GroupName", "full_admin" },
                    { "Admin:Groups:0", "full_admin" },
                    { "Admin:Groups:1", "Administrator" },
                    { "Admin:StandaloneAllowedNetworks:0", "127.0.0.1" },
                    { "Admin:StandaloneAllowedNetworks:1", "::1" },
                    { "Identity:ExternalIdpEnabled", "true" }
                });
            });
        }
    }

    public class AdminEndpointsTests : IClassFixture<AdminEndpointsTestFactory>
    {
        private readonly AdminEndpointsTestFactory _factory;

        public AdminEndpointsTests(AdminEndpointsTestFactory factory)
        {
            _factory = factory;
        }

        private HttpClient CreateAdminClient()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Forwarded-For", "127.0.0.1");
            client.DefaultRequestHeaders.Add("Remote-User", "admin_user");
            client.DefaultRequestHeaders.Add("Remote-Groups", "full_admin");
            return client;
        }

        private HttpClient CreateUnauthorizedClient()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.55");
            client.DefaultRequestHeaders.Add("Remote-User", "guest_user");
            client.DefaultRequestHeaders.Add("Remote-Groups", "standard_users");
            return client;
        }

        [Fact]
        [Requirement("MCP-ADMIN-ENDPOINT-SSE-HANDSHAKE", "MCP", RequirementType.Positive, "Admin endpoint /admin/sse performs initialize handshake with 2026-07-28 protocol version.")]
        public async Task AdminEndpoint_SseHandshake_NegotiatesProtocol()
        {
            var client = CreateAdminClient();

            // 1. Initiate SSE connection to /admin/sse
            using var sseCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var sseRequest = new HttpRequestMessage(HttpMethod.Get, "/admin/sse");
            sseRequest.Headers.Add("Accept", "text/event-stream");

            var sseResponse = await client.SendAsync(sseRequest, HttpCompletionOption.ResponseHeadersRead, sseCts.Token);
            Assert.Equal(HttpStatusCode.OK, sseResponse.StatusCode);
            Assert.Contains("text/event-stream", sseResponse.Content.Headers.ContentType?.MediaType);

            var sseStream = await sseResponse.Content.ReadAsStreamAsync(sseCts.Token);
            using var reader = new StreamReader(sseStream, Encoding.UTF8);

            // Read SSE lines until we obtain the endpoint URI
            string? endpointLine = null;
            while (endpointLine == null)
            {
                var line = await reader.ReadLineAsync(sseCts.Token);
                if (line == null)
                {
                    break;
                }

                if (line.StartsWith("data: "))
                {
                    endpointLine = line.Substring("data: ".Length).Trim();
                }
            }

            Assert.NotNull(endpointLine);
            Assert.Contains("/admin/message?sessionId=", endpointLine);

            var endpointUri = new Uri(endpointLine);
            var query = System.Web.HttpUtility.ParseQueryString(endpointUri.Query);
            var sessionId = query["sessionId"];
            Assert.False(string.IsNullOrWhiteSpace(sessionId));

            // 2. Post initialize request with protocolVersion 2026-07-28
            var initPayload = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2026-07-28",
                    capabilities = new { },
                    clientInfo = new { name = "AdminTestClient", version = "1.0.0" }
                }
            };

            var postContent = new StringContent(JsonSerializer.Serialize(initPayload), Encoding.UTF8, "application/json");
            var postResponse = await client.PostAsync($"/admin/message?sessionId={sessionId}", postContent);
            Assert.Equal(HttpStatusCode.Accepted, postResponse.StatusCode);

            // 3. Read initialize response event from the SSE stream
            string? messageData = null;
            while (messageData == null)
            {
                var line = await reader.ReadLineAsync(sseCts.Token);
                if (line == null)
                {
                    break;
                }

                if (line.StartsWith("data: "))
                {
                    messageData = line.Substring("data: ".Length).Trim();
                }
            }

            Assert.NotNull(messageData);
            using var doc = JsonDocument.Parse(messageData);
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("result", out var resultProp));
            Assert.True(resultProp.TryGetProperty("protocolVersion", out var protoProp));
            Assert.Equal("2026-07-28", protoProp.GetString());
            Assert.True(resultProp.TryGetProperty("serverInfo", out var serverInfoProp));
            Assert.True(serverInfoProp.TryGetProperty("name", out var nameProp));
            Assert.Equal("Model-Context-Gateway-Admin", nameProp.GetString());
        }

        [Fact]
        [Requirement("MCP-ADMIN-ENDPOINT-ROUTER-ADMIN-TARGET", "MCP", RequirementType.Positive, "Target proxy endpoint /router-admin routes directly to the Admin MCP server.")]
        public async Task TargetProxy_RouterAdmin_RoutesToAdminServer()
        {
            var client = CreateAdminClient();

            // POST /router-admin with tools/list
            var listToolsPayload = new
            {
                jsonrpc = "2.0",
                id = "list-tools-req",
                method = "tools/list",
                @params = new { }
            };

            var content = new StringContent(JsonSerializer.Serialize(listToolsPayload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/router-admin", content);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("result", out var resultProp));
            Assert.True(resultProp.TryGetProperty("tools", out var toolsProp));
            Assert.Equal(JsonValueKind.Array, toolsProp.ValueKind);

            var toolNames = toolsProp.EnumerateArray()
                .Select(t => t.GetProperty("name").GetString())
                .ToList();

            Assert.Contains("manage_servers", toolNames);
            Assert.Contains("manage_appkeys", toolNames);
            Assert.Contains("manage_clients", toolNames);
            Assert.Contains("manage_policies", toolNames);
            Assert.Contains("manage_group_mappings", toolNames);
            Assert.Contains("manage_providers", toolNames);
            Assert.Contains("manage_settings", toolNames);
            Assert.Contains("manage_custom_files", toolNames);
            Assert.Contains("manage_system", toolNames);
            Assert.Contains("test_tool_call", toolNames);
            Assert.Equal(10, toolNames.Count);
        }

        [Fact]
        [Requirement("GUARD-ADMIN-ENDPOINT-UNAUTHORIZED", "GUARD", RequirementType.Negative, "Unauthenticated / non-admin client request to /admin receives 403 Forbidden.")]
        public async Task AdminEndpoint_UnauthorizedCaller_Returns403()
        {
            var client = CreateUnauthorizedClient();

            // 1. Direct GET /admin/sse
            var sseRes = await client.GetAsync("/admin/sse");
            Assert.Equal(HttpStatusCode.Forbidden, sseRes.StatusCode);

            // 2. Direct POST /admin
            var postAdminRes = await client.PostAsync("/admin", new StringContent("{}", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.Forbidden, postAdminRes.StatusCode);

            // 3. Direct POST /router-admin
            var proxyRes = await client.PostAsync("/router-admin", new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.Forbidden, proxyRes.StatusCode);
        }

        [Fact]
        [Requirement("MCP-ADMIN-ENDPOINT-HEAD-REQUEST", "MCP", RequirementType.Positive, "Admin endpoint /admin handles HEAD request returning text/event-stream headers.")]
        public async Task AdminEndpoint_HeadRequest_ReturnsEventStreamHeaders()
        {
            var client = CreateAdminClient();
            using var req = new HttpRequestMessage(HttpMethod.Head, "/admin");
            var res = await client.SendAsync(req);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Contains("text/event-stream", res.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        [Requirement("MCP-ADMIN-ENDPOINT-LIST-TOOLS", "MCP", RequirementType.Positive, "Admin endpoint /admin/message executes tools/list over active SSE session and returns 10 admin tools.")]
        public async Task AdminEndpoint_SseSession_ListTools()
        {
            var client = CreateAdminClient();

            // 1. Connect GET /admin
            using var sseCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var sseRequest = new HttpRequestMessage(HttpMethod.Get, "/admin");
            sseRequest.Headers.Add("Accept", "text/event-stream");

            var sseResponse = await client.SendAsync(sseRequest, HttpCompletionOption.ResponseHeadersRead, sseCts.Token);
            Assert.Equal(HttpStatusCode.OK, sseResponse.StatusCode);

            var sseStream = await sseResponse.Content.ReadAsStreamAsync(sseCts.Token);
            using var reader = new StreamReader(sseStream, Encoding.UTF8);

            string? endpointLine = null;
            while (endpointLine == null)
            {
                var line = await reader.ReadLineAsync(sseCts.Token);
                if (line == null)
                {
                    break;
                }

                if (line.StartsWith("data: "))
                {
                    endpointLine = line.Substring("data: ".Length).Trim();
                }
            }
            Assert.NotNull(endpointLine);

            var endpointUri = new Uri(endpointLine);
            var sessionId = System.Web.HttpUtility.ParseQueryString(endpointUri.Query)["sessionId"];

            // 2. Post tools/list request to /admin/message
            var listReq = new
            {
                jsonrpc = "2.0",
                id = "list-tools-sse",
                method = "tools/list",
                @params = new { }
            };

            var postRes = await client.PostAsync($"/admin/message?sessionId={sessionId}", new StringContent(JsonSerializer.Serialize(listReq), Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.Accepted, postRes.StatusCode);

            // 3. Receive tools/list response on SSE stream
            string? messageData = null;
            while (messageData == null)
            {
                var line = await reader.ReadLineAsync(sseCts.Token);
                if (line == null)
                {
                    break;
                }

                if (line.StartsWith("data: "))
                {
                    messageData = line.Substring("data: ".Length).Trim();
                }
            }

            Assert.NotNull(messageData);
            using var doc = JsonDocument.Parse(messageData);
            var tools = doc.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToList();
            Assert.Equal(10, tools.Count);
        }

        [Fact]
        [Requirement("MCP-ADMIN-ENDPOINT-CALL-TOOL", "MCP", RequirementType.Positive, "Admin endpoint /admin/message executes tools/call for manage_system diagnostics.")]
        public async Task AdminEndpoint_SseSession_CallTool_ManageSystemDiagnostics()
        {
            var client = CreateAdminClient();

            // 1. Connect GET /admin/sse
            using var sseCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var sseRequest = new HttpRequestMessage(HttpMethod.Get, "/admin/sse");
            var sseResponse = await client.SendAsync(sseRequest, HttpCompletionOption.ResponseHeadersRead, sseCts.Token);
            Assert.Equal(HttpStatusCode.OK, sseResponse.StatusCode);

            var sseStream = await sseResponse.Content.ReadAsStreamAsync(sseCts.Token);
            using var reader = new StreamReader(sseStream, Encoding.UTF8);

            string? endpointLine = null;
            while (endpointLine == null)
            {
                var line = await reader.ReadLineAsync(sseCts.Token);
                if (line == null)
                {
                    break;
                }

                if (line.StartsWith("data: "))
                {
                    endpointLine = line.Substring("data: ".Length).Trim();
                }
            }
            Assert.NotNull(endpointLine);

            var sessionId = System.Web.HttpUtility.ParseQueryString(new Uri(endpointLine).Query)["sessionId"];

            // 2. Post tools/call for manage_system action diagnostics
            var callReq = new
            {
                jsonrpc = "2.0",
                id = "call-diagnostics-req",
                method = "tools/call",
                @params = new
                {
                    name = "manage_system",
                    arguments = new
                    {
                        action = "diagnostics"
                    }
                }
            };

            var postRes = await client.PostAsync($"/admin/message?sessionId={sessionId}", new StringContent(JsonSerializer.Serialize(callReq), Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.Accepted, postRes.StatusCode);

            // 3. Receive tool call response on SSE stream
            string? messageData = null;
            while (messageData == null)
            {
                var line = await reader.ReadLineAsync(sseCts.Token);
                if (line == null)
                {
                    break;
                }

                if (line.StartsWith("data: "))
                {
                    messageData = line.Substring("data: ".Length).Trim();
                }
            }

            Assert.NotNull(messageData);
            using var doc = JsonDocument.Parse(messageData);
            var content = doc.RootElement.GetProperty("result").GetProperty("content");
            Assert.Equal(JsonValueKind.Array, content.ValueKind);
            var text = content[0].GetProperty("text").GetString();
            Assert.Contains("activeSessions", text);
        }

        [Fact]
        [Requirement("MCP-21", "MCP", RequirementType.Positive, "Admin endpoint handles direct Streamable HTTP POST tools/list request returning JSON even with Accept text/event-stream header.")]
        public async Task AdminEndpoint_DirectPost_ToolsList_ReturnsJson_EvenWithSseAcceptHeader()
        {
            var client = CreateAdminClient();

            var req = new
            {
                jsonrpc = "2.0",
                id = "list-tools-req",
                method = "tools/list"
            };

            using var postReq = new HttpRequestMessage(HttpMethod.Post, "/admin/sse");
            postReq.Headers.Add("Accept", "text/event-stream, application/json");
            postReq.Content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");

            var res = await client.SendAsync(postReq);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal("application/json", res.Content.Headers.ContentType?.MediaType);

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
            Assert.Equal("list-tools-req", root.GetProperty("id").GetString());
            Assert.True(root.GetProperty("result").TryGetProperty("tools", out var tools));
            Assert.Equal(10, tools.GetArrayLength());
        }

        [Fact]
        [Requirement("MCP-21", "MCP", RequirementType.Positive, "Admin endpoint handles notifications/initialized returning 202 Accepted without opening a stream.")]
        public async Task AdminEndpoint_DirectPost_Notification_ReturnsAccepted()
        {
            var client = CreateAdminClient();

            var req = new
            {
                jsonrpc = "2.0",
                method = "notifications/initialized"
            };

            using var postReq = new HttpRequestMessage(HttpMethod.Post, "/admin/sse");
            postReq.Headers.Add("Accept", "text/event-stream, application/json");
            postReq.Content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");

            var res = await client.SendAsync(postReq);
            Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        }

        [Fact]
        [Requirement("MCP-21", "MCP", RequirementType.Positive, "Target proxy endpoint /router-admin handles direct Streamable HTTP POST tools/list.")]
        public async Task TargetAdminEndpoint_DirectPost_ToolsList_ReturnsJson()
        {
            var client = CreateAdminClient();

            var req = new
            {
                jsonrpc = "2.0",
                id = "target-tools-req",
                method = "tools/list"
            };

            using var postReq = new HttpRequestMessage(HttpMethod.Post, "/router-admin");
            postReq.Headers.Add("Accept", "text/event-stream, application/json");
            postReq.Content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");

            var res = await client.SendAsync(postReq);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal("application/json", res.Content.Headers.ContentType?.MediaType);

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
            Assert.Equal("target-tools-req", root.GetProperty("id").GetString());
            Assert.True(root.GetProperty("result").TryGetProperty("tools", out var tools));
            Assert.Equal(10, tools.GetArrayLength());
        }

        [Fact]
        [Requirement("MCP-ADMIN-ZOD-STRICT-RESPONSE", "MCP", RequirementType.Positive, "JsonRpcResponse serialization omits null result, error, and _meta fields completely to satisfy Zod strict validation.")]
        public void JsonRpcResponse_Serialization_OmitsNullFieldsCompletely()
        {
            var response = new JsonRpcResponse
            {
                Id = 1,
                Result = JsonSerializer.SerializeToElement(new { status = "ok" }),
                Error = null,
                Meta = null
            };

            var json = JsonSerializer.Serialize(response);

            Assert.DoesNotContain("\"error\"", json);
            Assert.DoesNotContain("\"_meta\"", json);
            Assert.Contains("\"result\"", json);
            Assert.Contains("\"id\":1", json);
            Assert.Contains("\"jsonrpc\":\"2.0\"", json);

            var errorResponse = new JsonRpcResponse
            {
                Id = "err-1",
                Result = null,
                Error = new JsonRpcError { Code = -32600, Message = "Invalid Request" },
                Meta = null
            };

            var errorJson = JsonSerializer.Serialize(errorResponse);
            Assert.DoesNotContain("\"result\"", errorJson);
            Assert.DoesNotContain("\"_meta\"", errorJson);
            Assert.Contains("\"error\"", errorJson);
            Assert.Contains("\"code\":-32600", errorJson);
        }

        [Fact]
        [Requirement("MCP-ADMIN-SSE-ZOD-COMPLIANT", "MCP", RequirementType.Positive, "Admin endpoint direct POST response does not serialize null error or _meta fields.")]
        public async Task AdminEndpoint_DirectPost_SerializesResponseWithoutNullErrorOrMeta()
        {
            var client = CreateAdminClient();

            var req = new
            {
                jsonrpc = "2.0",
                id = "tools-list-zod",
                method = "tools/list"
            };

            using var postReq = new HttpRequestMessage(HttpMethod.Post, "/admin/sse");
            postReq.Headers.Add("Accept", "application/json");
            postReq.Content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");

            var res = await client.SendAsync(postReq);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var json = await res.Content.ReadAsStringAsync();
            Assert.DoesNotContain("\"error\":null", json);
            Assert.DoesNotContain("\"_meta\":null", json);
            Assert.DoesNotContain("\"error\"", json);
            Assert.DoesNotContain("\"_meta\"", json);
        }

        [Fact]
        [Requirement("MCP-ADMIN-REVERSE-PROXY-X-FORWARDED-HOST", "MCP", RequirementType.Positive, "Admin SSE endpoint prioritizes X-Forwarded-Host header over Request.Host in endpoint URI advertising.")]
        public async Task AdminEndpoint_SseHandshake_UsesXForwardedHost()
        {
            var client = CreateAdminClient();

            using var sseCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var sseRequest = new HttpRequestMessage(HttpMethod.Get, "/admin/sse");
            sseRequest.Headers.Add("Accept", "text/event-stream");
            sseRequest.Headers.Add("X-Forwarded-Host", "mcp.wileyriley.com:443");
            sseRequest.Headers.Add("X-Forwarded-Proto", "https");

            var sseResponse = await client.SendAsync(sseRequest, HttpCompletionOption.ResponseHeadersRead, sseCts.Token);
            Assert.Equal(HttpStatusCode.OK, sseResponse.StatusCode);

            var sseStream = await sseResponse.Content.ReadAsStreamAsync(sseCts.Token);
            using var reader = new StreamReader(sseStream, Encoding.UTF8);

            string? endpointLine = null;
            while (endpointLine == null)
            {
                var line = await reader.ReadLineAsync(sseCts.Token);
                if (line == null)
                {
                    break;
                }
                if (line.StartsWith("data: "))
                {
                    endpointLine = line.Substring("data: ".Length).Trim();
                }
            }

            Assert.NotNull(endpointLine);
            Assert.StartsWith("https://mcp.wileyriley.com:443/admin/message?sessionId=", endpointLine);
        }

        [Fact]
        [Requirement("MCP-ADMIN-REQUEST-WITHOUT-ID-NOT-DROPPED", "MCP", RequirementType.Positive, "Admin endpoint executes requests without an ID and does not drop them as notifications.")]
        public async Task AdminEndpoint_DirectPost_RequestWithoutId_ExecutesSuccessfully()
        {
            var client = CreateAdminClient();

            var req = new
            {
                jsonrpc = "2.0",
                method = "tools/list"
            };

            using var postReq = new HttpRequestMessage(HttpMethod.Post, "/admin/sse");
            postReq.Headers.Add("Accept", "application/json");
            postReq.Content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");

            var res = await client.SendAsync(postReq);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("result", out var result));
            Assert.True(result.TryGetProperty("tools", out var tools));
            Assert.Equal(10, tools.GetArrayLength());
        }
    }
}
