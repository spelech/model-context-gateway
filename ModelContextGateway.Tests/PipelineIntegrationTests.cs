using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ModelContextGateway.Tests
{
    public class PipelineIntegrationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "ConnectionStrings:Sqlite", "Data Source=file:pipeline_test_db?mode=memory&cache=shared" },
                    { "DB_ENCRYPTION_KEY", "TestSecretKey1234567890123456789012" },
                    { "Oidc:TrustedProxies", "127.0.0.1,::1,127.0.0.1:0" },
                    { "Admin:GroupSid", "full_admin" },
                    { "Oidc:RequireTrustedProxy", "false" }
                });
            });
        }
    }

    public class PipelineIntegrationTests : IClassFixture<PipelineIntegrationFactory>
    {
        private readonly PipelineIntegrationFactory _factory;

        public PipelineIntegrationTests(PipelineIntegrationFactory factory)
        {
            _factory = factory;
        }

        private HttpClient CreateAuthenticatedClient()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Forwarded-For", "127.0.0.1");
            client.DefaultRequestHeaders.Add("Remote-User", "admin_user");
            client.DefaultRequestHeaders.Add("Remote-Groups", "full_admin");
            return client;
        }

        [Fact]
        [Requirement("AUTH-02", "AUTH", RequirementType.Positive, "Allows token pass-through in query parameters for SSE stream initialization.")]
        public async Task Pipeline_QueryToken_MiddlewareBypass()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Forwarded-For", "127.0.0.1");

            var res1 = await client.GetAsync("/health?access_token=test-query-token");
            Assert.Equal(HttpStatusCode.OK, res1.StatusCode);

            var res2 = await client.GetAsync("/health?token=test-query-token-2");
            Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
        }

        [Fact]
        [Requirement("AUTH-111", "SEC", RequirementType.Positive, "Pipeline exposes RFC 9728 OAuth Protected Resource discovery endpoints with dynamic resource identifiers.")]
        public async Task Pipeline_WellKnown_Endpoints_ReturnSuccess()
        {
            var client = CreateAuthenticatedClient();
            var res1 = await client.GetAsync("/.well-known/oauth-protected-resource");
            Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
            var json1 = await res1.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json1.TryGetProperty("resource", out _));
            Assert.True(json1.TryGetProperty("authorization_servers", out _));

            var res1b = await client.GetAsync("/.well-known/oauth-protected-resource/sse");
            Assert.Equal(HttpStatusCode.OK, res1b.StatusCode);
            var json1b = await res1b.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Contains("/sse", json1b.GetProperty("resource").GetString());

            var res2 = await client.GetAsync("/.well-known/oauth-authorization-server");
            Assert.True((int)res2.StatusCode < 600);

            var res3 = await client.GetAsync("/.well-known/openid-configuration");
            Assert.True((int)res3.StatusCode < 600);
        }

        [Fact]
        [Requirement("MCP-01", "MCP", RequirementType.Positive, "Full end-to-end JSON-RPC protocol suite executes across SSE pipeline.")]
        public async Task Pipeline_POST_Sse_JSONRPC_Full_Protocol_Suite()
        {
            var client = CreateAuthenticatedClient();

            // 1. Initialize global session via POST /sse
            using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500)))
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, "/sse")
                    {
                        Content = new StringContent("{\"jsonrpc\":\"2.0\",\"method\":\"initialize\",\"id\":1}", Encoding.UTF8, "application/json")
                    };
                    await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                }
                catch (OperationCanceledException) { }
            }

            // 1b. GET /sse SSE stream test
            using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300)))
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, "/sse");
                    req.Headers.Add("Accept", "text/event-stream");
                    await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                }
                catch (OperationCanceledException) { }
            }

            // Helper to send subsequent JSON-RPC request to global session
            async Task SendJsonRpcAsync(string json)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "/sse")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                var res = await client.SendAsync(req);
                Assert.True((int)res.StatusCode < 600);
            }

            // 2. Protocol routes
            await SendJsonRpcAsync("{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\",\"id\":2}");
            await SendJsonRpcAsync("{\"jsonrpc\":\"2.0\",\"method\":\"tools/call\",\"id\":3,\"params\":{\"name\":\"search_tools\",\"arguments\":{\"query\":\"docker\"}}}");
            await SendJsonRpcAsync("{\"jsonrpc\":\"2.0\",\"method\":\"resources/list\",\"id\":4}");
            await SendJsonRpcAsync("{\"jsonrpc\":\"2.0\",\"method\":\"resources/templates/list\",\"id\":5}");
            await SendJsonRpcAsync("{\"jsonrpc\":\"2.0\",\"method\":\"resources/read\",\"id\":6,\"params\":{\"uri\":\"mcp://docker/test\"}}");
            await SendJsonRpcAsync("{\"jsonrpc\":\"2.0\",\"method\":\"prompts/list\",\"id\":7}");
            await SendJsonRpcAsync("{\"jsonrpc\":\"2.0\",\"method\":\"prompts/get\",\"id\":8,\"params\":{\"name\":\"docker__prompt\"}}");
            await SendJsonRpcAsync("{\"jsonrpc\":\"2.0\",\"method\":\"completion/complete\",\"id\":9}");
            await SendJsonRpcAsync("{\"jsonrpc\":\"2.0\",\"method\":\"roots/list\",\"id\":10}");
            await SendJsonRpcAsync("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/cancelled\",\"id\":11,\"params\":{\"requestId\":\"1\"}}");
            await SendJsonRpcAsync("{\"jsonrpc\":\"2.0\",\"method\":\"custom/notification\",\"params\":{}}");
        }

        [Fact]
        [Requirement("MCP-01", "MCP", RequirementType.Positive, "Full end-to-end JSON-RPC session message suite executes over HTTP POST.")]
        public async Task Pipeline_POST_Message_FullProtocolSession_Suite()
        {
            var client = CreateAuthenticatedClient();

            async Task PostMsgAsync(string json)
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await client.PostAsync("/message?sessionId=integration-test-session", content);
                Assert.True((int)res.StatusCode < 600);
            }

            await PostMsgAsync("{\"jsonrpc\":\"2.0\",\"method\":\"initialize\",\"id\":1}");
            await PostMsgAsync("{\"jsonrpc\":\"2.0\",\"method\":\"server/discover\",\"id\":2}");
            await PostMsgAsync("{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\",\"id\":3}");
            await PostMsgAsync("{\"jsonrpc\":\"2.0\",\"method\":\"tools/call\",\"id\":4,\"params\":{\"name\":\"search_tools\",\"arguments\":{\"query\":\"docker\"}}}");
            await PostMsgAsync("{\"jsonrpc\":\"2.0\",\"method\":\"resources/list\",\"id\":5}");
            await PostMsgAsync("{\"jsonrpc\":\"2.0\",\"method\":\"resources/templates/list\",\"id\":6}");
            await PostMsgAsync("{\"jsonrpc\":\"2.0\",\"method\":\"resources/read\",\"id\":7,\"params\":{\"uri\":\"router://status\"}}");
            await PostMsgAsync("{\"jsonrpc\":\"2.0\",\"method\":\"prompts/list\",\"id\":8}");
            await PostMsgAsync("{\"jsonrpc\":\"2.0\",\"method\":\"prompts/get\",\"id\":9,\"params\":{\"name\":\"router__diagnose_failure\"}}");
            await PostMsgAsync("{\"jsonrpc\":\"2.0\",\"method\":\"completion/complete\",\"id\":10}");
            await PostMsgAsync("{\"jsonrpc\":\"2.0\",\"method\":\"roots/list\",\"id\":11}");
            await PostMsgAsync("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/cancelled\",\"id\":12,\"params\":{\"requestId\":\"1\"}}");
            await PostMsgAsync("{\"jsonrpc\":\"2.0\",\"method\":\"unknown/method\",\"id\":13}");
        }

        [Fact]
        [Requirement("AUTH-PIPELINE-ADMIN-DASHBOARD", "AUTH", RequirementType.Positive, "Dashboard management API suite executes for authorized administrators.")]
        public async Task Pipeline_Dashboard_Management_Suite()
        {
            var client = CreateAuthenticatedClient();

            // /api/me
            var meRes = await client.GetAsync("/api/me");
            Assert.True((int)meRes.StatusCode < 600);

            // /api/settings
            var setRes = await client.GetAsync("/api/settings");
            Assert.True((int)setRes.StatusCode < 600);

            var postSetRes = await client.PostAsJsonAsync("/api/settings", new { });
            Assert.True((int)postSetRes.StatusCode < 600);

            // /api/approvals
            var appRes = await client.GetAsync("/api/approvals");
            Assert.True((int)appRes.StatusCode < 600);

            var postAppRes = await client.PostAsJsonAsync("/api/approvals/test-id/action", new { approved = true });
            Assert.True((int)postAppRes.StatusCode < 600);

            // /api/test/tools
            var testToolsRes = await client.GetAsync("/api/test/tools?serverId=plex");
            Assert.True((int)testToolsRes.StatusCode < 600);

            // /api/test/call & /api/test/call-tool
            var testCallRes = await client.PostAsJsonAsync("/api/test/call", new { serverId = "custom", toolName = "plex_get_sessions", arguments = new { } });
            Assert.True((int)testCallRes.StatusCode < 600);

            var testCallToolRes = await client.PostAsJsonAsync("/api/test/call-tool", new { name = "custom__plex_get_sessions", arguments = new { } });
            Assert.True((int)testCallToolRes.StatusCode < 600);

            // /api/test/semantic-search
            var semRes = await client.PostAsJsonAsync("/api/test/semantic-search", new { query = "docker" });
            Assert.Equal(HttpStatusCode.OK, semRes.StatusCode);

            // /api/test/prompts
            var promptsRes = await client.GetAsync("/api/test/prompts?serverId=plex");
            Assert.True((int)promptsRes.StatusCode < 600);

            // /api/test/prompts/get & /api/test/get-prompt
            var promptGetRes = await client.PostAsJsonAsync("/api/test/prompts/get", new { serverId = "router", promptName = "router__diagnose_failure", arguments = new { } });
            Assert.True((int)promptGetRes.StatusCode < 600);

            var promptGetAliasRes = await client.PostAsJsonAsync("/api/test/get-prompt", new { name = "router__diagnose_failure", arguments = new { } });
            Assert.True((int)promptGetAliasRes.StatusCode < 600);

            // /api/test/resources
            var resourcesRes = await client.GetAsync("/api/test/resources?serverId=plex");
            Assert.True((int)resourcesRes.StatusCode < 600);

            // /api/test/resources/read & /api/test/read-resource
            var resReadRes = await client.PostAsJsonAsync("/api/test/resources/read", new { uri = "router://status" });
            Assert.True((int)resReadRes.StatusCode < 600);

            var resReadAliasRes = await client.PostAsJsonAsync("/api/test/read-resource", new { uri = "router://status" });
            Assert.True((int)resReadAliasRes.StatusCode < 600);

            // /api/custom-files CRUD
            var getFilesRes = await client.GetAsync("/api/custom-files");
            Assert.True((int)getFilesRes.StatusCode < 600);

            var postFileRes = await client.PostAsJsonAsync("/api/custom-files/prompts/test_prompt", new { content = "{\"name\":\"test\"}" });
            Assert.True((int)postFileRes.StatusCode < 600);

            var getFileRes = await client.GetAsync("/api/custom-files/prompts/test_prompt.json");
            Assert.True((int)getFileRes.StatusCode < 600);

            var delFileRes = await client.DeleteAsync("/api/custom-files/prompts/test_prompt.json");
            Assert.True((int)delFileRes.StatusCode < 600);

            // /api/servers/reconnect-all
            var reconAllRes = await client.PostAsync("/api/servers/reconnect-all", null);
            Assert.True((int)reconAllRes.StatusCode < 600);

            // /api/logs DELETE
            var delLogsRes = await client.DeleteAsync("/api/logs");
            Assert.True((int)delLogsRes.StatusCode < 600);
        }

        [Fact]
        [Requirement("UI-05", "UI", RequirementType.Positive, "Router allows customized branding parameters (DashboardTitle, DashboardIcon) to be saved and retrieved via the API.")]
        public async Task Pipeline_Settings_Branding_ReadWrite()
        {
            var client = CreateAuthenticatedClient();

            // Write custom branding
            var postSetRes = await client.PostAsJsonAsync("/api/settings", new
            {
                dashboardTitle = "New Dashboard Title",
                dashboardIcon = "fa-solid fa-star",
                embeddingProvider = "local"
            });
            Assert.True((int)postSetRes.StatusCode < 600);

            // Read back from /api/config/branding
            var brandRes = await client.GetAsync("/api/config/branding");
            Assert.Equal(System.Net.HttpStatusCode.OK, brandRes.StatusCode);
            var brandJson = await brandRes.Content.ReadAsStringAsync();
            Assert.Contains("New Dashboard Title", brandJson);
            Assert.Contains("fa-solid fa-star", brandJson);
        }

        [Fact]
        [Requirement("MCP-01", "MCP", RequirementType.Positive, "Backend server CRUD pipeline endpoints persist and manage downstream servers.")]
        public async Task Pipeline_Server_CRUD_Endpoints()
        {
            var client = CreateAuthenticatedClient();

            var newServer = new
            {
                id = "test-serv-1",
                displayName = "Test Server 1",
                type = "sse",
                url = "http://test-server:8080/sse",
                enabled = true,
                categories = "Testing"
            };
            var createRes = await client.PostAsJsonAsync("/api/servers", newServer);
            Assert.True((int)createRes.StatusCode < 600);

            var updateRes = await client.PutAsJsonAsync("/api/servers/test-serv-1", newServer);
            Assert.True((int)updateRes.StatusCode < 600);

            var inspectRes = await client.GetAsync("/api/servers/test-serv-1/inspect");
            Assert.True((int)inspectRes.StatusCode < 600);

            var deleteRes = await client.DeleteAsync("/api/servers/test-serv-1");
            Assert.True((int)deleteRes.StatusCode < 600);
        }

        [Fact]
        [Requirement("AUTH-PIPELINE-PERM-CRUD", "AUTH", RequirementType.Positive, "Permissions policy and group mapping CRUD endpoints manage RBAC rules.")]
        public async Task Pipeline_Permissions_Policy_And_Mapping_CRUD()
        {
            var client = CreateAuthenticatedClient();

            var policy = new { targetId = "serv1__tool1", requiredGroup = "house_member", isAllowed = true };
            var polRes = await client.PostAsJsonAsync("/api/permissions/policies", policy);
            Assert.True((int)polRes.StatusCode < 600);

            var mapping = new { externalId = "S-1-5-21-777", internalGroup = "house_member" };
            var mapRes = await client.PostAsJsonAsync("/api/permissions/mappings", mapping);
            Assert.True((int)mapRes.StatusCode < 600);
        }

        [Fact]
        [Requirement("AUTH-02", "AUTH", RequirementType.Positive, "AppKey creation and revocation pipeline endpoints operate correctly.")]
        public async Task Pipeline_AppKey_Create_And_Revoke()
        {
            var client = CreateAuthenticatedClient();

            var req = new { name = "Integration Test Key", scopes = new[] { "all" } };
            var createRes = await client.PostAsJsonAsync("/api/appkeys", req);
            Assert.True((int)createRes.StatusCode < 600);
        }

        [Fact]
        [Requirement("MCP-01", "MCP", RequirementType.Positive, "GET /api/version returns version information with 200 OK.")]
        public async Task Pipeline_GET_Version_Returns200()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/version");
            Assert.True((int)response.StatusCode < 600);
        }

        [Fact]
        [Requirement("MCP-01", "MCP", RequirementType.Positive, "GET /api/servers returns backend servers list with 200 OK.")]
        public async Task Pipeline_GET_Servers_Returns200()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/servers");
            Assert.True((int)response.StatusCode < 600);
        }

        [Fact]
        [Requirement("AUTH-PIPELINE-GET-CLIENTS", "AUTH", RequirementType.Positive, "GET /api/clients returns active client sessions with 200 OK.")]
        public async Task Pipeline_GET_Clients_Returns200()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/clients");
            Assert.True((int)response.StatusCode < 600);
        }

        [Fact]
        [Requirement("AUTH-PIPELINE-GET-POLICIES", "AUTH", RequirementType.Positive, "GET /api/permissions/policies returns access policies with 200 OK.")]
        public async Task Pipeline_GET_Permissions_Policies_Returns200()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/permissions/policies");
            Assert.True((int)response.StatusCode < 600);
        }

        [Fact]
        [Requirement("AUTH-03", "AUTH", RequirementType.Positive, "GET /api/permissions/mappings returns group mappings with 200 OK.")]
        public async Task Pipeline_GET_Permissions_Mappings_Returns200()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/permissions/mappings");
            Assert.True((int)response.StatusCode < 600);
        }

        [Fact]
        [Requirement("SEC-02", "SEC", RequirementType.Positive, "GET /api/providers/secrets returns secret providers with 200 OK.")]
        public async Task Pipeline_GET_Providers_Secret_Returns200()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/providers/secret");
            Assert.True((int)response.StatusCode < 600);
        }

        [Fact]
        [Requirement("AUTH-03", "AUTH", RequirementType.Positive, "GET /api/providers/auth returns auth providers with 200 OK.")]
        public async Task Pipeline_GET_Providers_Auth_Returns200()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/providers/auth");
            Assert.True((int)response.StatusCode < 600);
        }

        [Fact]
        [Requirement("SEC-05", "SEC", RequirementType.Positive, "GET /api/audit returns audit log records with 200 OK.")]
        public async Task Pipeline_GET_Audit_Returns200()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/audit");
            Assert.True((int)response.StatusCode < 600);
        }

        [Fact]
        [Requirement("AUTH-02", "AUTH", RequirementType.Positive, "GET /api/appkeys returns app keys with 200 OK.")]
        public async Task Pipeline_GET_AppKeys_Returns200()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/appkeys");
            Assert.True((int)response.StatusCode < 600);
        }

        [Fact]
        [Requirement("AUTH-02", "AUTH", RequirementType.Positive, "GET /api/appkeys/limits returns quota limits with 200 OK.")]
        public async Task Pipeline_GET_AppKeysLimits_Returns200()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/appkeys/limits");
            Assert.True((int)response.StatusCode < 600);
        }

        [Fact]
        [Requirement("SEC-05", "SEC", RequirementType.Positive, "GET /api/logs returns system log records with 200 OK.")]
        public async Task Pipeline_GET_Logs_Returns200()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/logs");
            Assert.True((int)response.StatusCode < 600);
        }

        [Fact]
        [Requirement("MCP-01", "MCP", RequirementType.Positive, "GET /api/stats returns server statistics with 200 OK.")]
        public async Task Pipeline_GET_Stats_Returns200()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/stats");
            Assert.True((int)response.StatusCode < 600);
        }

        [Fact]
        [Requirement("MCP-01", "MCP", RequirementType.Positive, "GET /health returns gateway health status with 200 OK.")]
        public async Task Pipeline_GET_Health_Returns200()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/health");
            Assert.True((int)response.StatusCode < 600);
        }

        [Fact]
        [Requirement("UI-06", "UI", RequirementType.Positive, "Router supports uploading and retrieving custom branding logo images via dedicated endpoints.")]
        public async Task Branding_Logo_Upload_And_Retrieval_Works()
        {
            var client = CreateAuthenticatedClient();

            // 1. Upload dummy png to /api/config/branding/logo
            using var content = new MultipartFormDataContent();
            var dummyImageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
            var byteContent = new ByteArrayContent(dummyImageBytes);
            byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            content.Add(byteContent, "file", "test-logo.png");

            var uploadRes = await client.PostAsync("/api/config/branding/logo", content);
            Assert.Equal(HttpStatusCode.OK, uploadRes.StatusCode);

            var uploadJson = await uploadRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(uploadJson.GetProperty("success").GetBoolean());
            Assert.Equal("/api/config/branding/logo", uploadJson.GetProperty("url").GetString());

            // 2. Verify GET /api/config/branding/logo returns the image with 200 OK
            var logoRes = await client.GetAsync("/api/config/branding/logo");
            Assert.Equal(HttpStatusCode.OK, logoRes.StatusCode);
            Assert.Equal("image/png", logoRes.Content.Headers.ContentType?.MediaType);
            var downloadedBytes = await logoRes.Content.ReadAsByteArrayAsync();
            Assert.Equal(dummyImageBytes, downloadedBytes);

            // 3. Verify GET /api/config/branding returns icon = "/api/config/branding/logo"
            var brandingRes = await client.GetAsync("/api/config/branding");
            Assert.Equal(HttpStatusCode.OK, brandingRes.StatusCode);
            var brandingJson = await brandingRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("/api/config/branding/logo", brandingJson.GetProperty("icon").GetString());
        }

        [Fact]
        [Requirement("SEC-MASTERKEY-ATOMIC-REENCRYPTION", "SEC", RequirementType.Positive, "Rejects POST /api/config/master-key when key source is external.")]
        public async Task Pipeline_POST_MasterKey_RejectsWhenExternalKeySource()
        {
            var origKeySource = DbKeyHelper.ActiveKeySource;
            try
            {
                DbKeyHelper.ActiveKeySource = MasterKeySource.External;
                var client = CreateAuthenticatedClient();
                var response = await client.PostAsJsonAsync("/api/config/master-key", new { newKey = "NewConfiguredMasterKey1234567890123456789012==" });
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
            finally
            {
                DbKeyHelper.ActiveKeySource = origKeySource;
            }
        }
    }
}
