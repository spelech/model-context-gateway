using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ModelContextGateway.Tests
{
    public class TransportsAuthShapeTests
    {
        private async Task InvokeApplyAuthAndCustomHeadersAsync(object transport, HttpRequestMessage request)
        {
            var method = transport.GetType().GetMethod("ApplyAuthAndCustomHeadersAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var task = (Task)method.Invoke(transport, new object[] { request })!;
            await task;
        }

        [Theory]
        [InlineData("bearer", "secret123", "Authorization", "Bearer secret123")]
        [InlineData("basic", "secret123", "Authorization", "Basic secret123")]
        [InlineData("raw", "secret123", "Authorization", "secret123")]
        [InlineData("x-api-key", "secret123", "X-API-Key", "secret123")]
        [Requirement("TRANS-AUTH-SHAPES-STANDARD-HEADERS", "TRANS", RequirementType.Positive, "SseTransport formats standard authorization shapes (bearer, basic, raw, x-api-key) into HTTP headers.")]
        public async Task SseTransport_ApplyAuthAndCustomHeaders_Formats_Standard_Headers(string authShape, string token, string expectedHeaderKey, string expectedHeaderValue)
        {
            var server = new McpServer
            {
                Id = "srv1",
                Url = "http://localhost:5000/sse",
                SecretProvider = "None",
                ApiKey = token,
                AuthShape = authShape
            };
            var httpClient = new HttpClient();
            var logger = NullLogger<SseTransport>.Instance;
            var transport = new SseTransport(server, httpClient, logger, null!);

            var request = new HttpRequestMessage(HttpMethod.Get, server.Url);
            await InvokeApplyAuthAndCustomHeadersAsync(transport, request);

            Assert.True(request.Headers.Contains(expectedHeaderKey));
            var val = string.Join(" ", request.Headers.GetValues(expectedHeaderKey));
            Assert.Equal(expectedHeaderValue, val);
        }

        [Fact]
        [Requirement("TRANS-AUTH-SHAPES-CUSTOM-HEADER", "TRANS", RequirementType.Positive, "SseTransport applies custom header names for proprietary target backend authentication.")]
        public async Task SseTransport_ApplyAuthAndCustomHeaders_Formats_CustomHeader()
        {
            var server = new McpServer
            {
                Id = "slack",
                Url = "http://localhost:5000/sse",
                SecretProvider = "None",
                ApiKey = "xoxb-test-token",
                AuthShape = "custom-header",
                CustomHeaderName = "Slack-Bot-Token"
            };
            var transport = new SseTransport(server, new HttpClient(), NullLogger<SseTransport>.Instance, null!);

            var request = new HttpRequestMessage(HttpMethod.Get, server.Url);
            await InvokeApplyAuthAndCustomHeadersAsync(transport, request);

            Assert.True(request.Headers.Contains("Slack-Bot-Token"));
            Assert.Equal("xoxb-test-token", string.Join("", request.Headers.GetValues("Slack-Bot-Token")));
        }

        [Fact]
        [Requirement("TRANS-AUTH-SHAPES-QUERY-PARAM", "TRANS", RequirementType.Positive, "SseTransport appends authentication tokens as URL query parameters when query auth shape is configured.")]
        public async Task SseTransport_ApplyAuthAndCustomHeaders_Appends_QueryParameter()
        {
            var server = new McpServer
            {
                Id = "query-srv",
                Url = "http://localhost:5000/sse?existing=1",
                SecretProvider = "None",
                ApiKey = "query-token-123",
                AuthShape = "query",
                CustomHeaderName = "api_key"
            };
            var transport = new SseTransport(server, new HttpClient(), NullLogger<SseTransport>.Instance, null!);

            var request = new HttpRequestMessage(HttpMethod.Get, server.Url);
            await InvokeApplyAuthAndCustomHeadersAsync(transport, request);

            Assert.NotNull(request.RequestUri);
            Assert.Contains("api_key=query-token-123", request.RequestUri.Query);
            Assert.Contains("existing=1", request.RequestUri.Query);
        }

        [Fact]
        [Requirement("TRANS-HTTP-AUTH-CUSTOM-HEADER", "TRANS", RequirementType.Positive, "HttpTransport formats custom header authentication for target servers.")]
        public async Task HttpTransport_ApplyAuthAndCustomHeaders_Formats_CustomHeader()
        {
            var server = new McpServer
            {
                Id = "custom-http",
                Url = "http://localhost:5000/mcp",
                SecretProvider = "None",
                ApiKey = "http-secret-key",
                AuthShape = "custom-header",
                CustomHeaderName = "X-Service-Auth"
            };
            var transport = new HttpTransport(server, new HttpClient(), NullLogger<HttpTransport>.Instance);

            var request = new HttpRequestMessage(HttpMethod.Post, server.Url);
            await InvokeApplyAuthAndCustomHeadersAsync(transport, request);

            Assert.True(request.Headers.Contains("X-Service-Auth"));
            Assert.Equal("http-secret-key", string.Join("", request.Headers.GetValues("X-Service-Auth")));
        }

        [Fact]
        [Requirement("TRANS-AUTH-SHAPES-HEADERS-JSON", "TRANS", RequirementType.Positive, "SseTransport parses and applies extra request headers from HeadersJson configuration.")]
        public async Task SseTransport_ApplyAuthAndCustomHeaders_Parses_HeadersJson()
        {
            var server = new McpServer
            {
                Id = "headers-json",
                Url = "http://localhost:5000/sse",
                SecretProvider = "None",
                HeadersJson = "{\"X-Custom-Env\": \"production\", \"X-Agent\": \"antigravity\"}"
            };
            var transport = new SseTransport(server, new HttpClient(), NullLogger<SseTransport>.Instance, null!);

            var request = new HttpRequestMessage(HttpMethod.Get, server.Url);
            await InvokeApplyAuthAndCustomHeadersAsync(transport, request);

            Assert.True(request.Headers.Contains("X-Custom-Env"));
            Assert.Equal("production", string.Join("", request.Headers.GetValues("X-Custom-Env")));
            Assert.True(request.Headers.Contains("X-Agent"));
            Assert.Equal("antigravity", string.Join("", request.Headers.GetValues("X-Agent")));
        }

        [Fact]
        [Requirement("SEC-VAULT-CUSTOM-MOUNT-PATH", "SEC", RequirementType.Positive, "SseTransport resolves dynamic secrets from Vault using custom mounts, paths, and secret fields.")]
        public async Task SseTransport_ResolveTokenAsync_Uses_Custom_Path_Field_And_Mount()
        {
            var server = new McpServer
            {
                Id = "vault-srv",
                Url = "http://localhost:5000/sse",
                SecretProvider = "Vault",
                SecretMount = "kv-custom",
                SecretPath = "apps/my-app",
                SecretField = "token-key"
            };

            var mockRetriever = new Mock<ISecretRetriever>();
            mockRetriever.Setup(r => r.GetSecretAsync("kv-custom:apps/my-app", "token-key"))
                         .ReturnsAsync("resolved-vault-secret-999");

            var transport = new SseTransport(server, new HttpClient(), NullLogger<SseTransport>.Instance, null!, mockRetriever.Object);

            var token = await transport.ResolveTokenAsync();

            Assert.Equal("resolved-vault-secret-999", token);
            mockRetriever.Verify(r => r.GetSecretAsync("kv-custom:apps/my-app", "token-key"), Times.Once);
        }

        [Fact]
        [Requirement("SEC-HTTP-SECRET-URL-FALLBACK", "SEC", RequirementType.Positive, "HttpTransport falls back to URL and SecretItemKey when specific secret paths are unconfigured.")]
        public async Task HttpTransport_ResolveTokenAsync_Defaults_To_Url_And_ApiKey_When_Not_Configured()
        {
            var server = new McpServer
            {
                Id = "vault-default-http",
                Url = "http://localhost:5000/mcp",
                SecretProvider = "Vault",
                SecretItemKey = "MyKey"
            };

            var mockRetriever = new Mock<ISecretRetriever>();
            mockRetriever.Setup(r => r.GetSecretAsync("http://localhost:5000/mcp", "MyKey"))
                         .ReturnsAsync("resolved-default-secret-123");

            var transport = new HttpTransport(server, new HttpClient(), NullLogger<HttpTransport>.Instance, mockRetriever.Object);

            var token = await transport.ResolveTokenAsync();

            Assert.Equal("resolved-default-secret-123", token);
            mockRetriever.Verify(r => r.GetSecretAsync("http://localhost:5000/mcp", "MyKey"), Times.Once);
        }

        [Fact]
        [Requirement("GUARD-03", "GUARD", RequirementType.Negative, "SseTransport fails closed and throws SecurityException when Vault secret resolution returns null.")]
        public async Task SseTransport_ResolveTokenAsync_FailsClosed_WhenVaultResolvesNull()
        {
            var server = new McpServer
            {
                Id = "srv-null-vault",
                Url = "http://localhost:5000/sse",
                SecretProvider = "Vault",
                ApiKey = "fallback-key-should-never-be-used",
                SecretMount = "kv",
                SecretPath = "missing",
                SecretField = "token"
            };

            var mockRetriever = new Mock<ISecretRetriever>();
            mockRetriever.Setup(r => r.GetSecretAsync(It.IsAny<string>(), It.IsAny<string>()))
                         .ReturnsAsync((string?)null);

            var transport = new SseTransport(server, new HttpClient(), NullLogger<SseTransport>.Instance, null!, mockRetriever.Object);

            await Assert.ThrowsAsync<System.Security.SecurityException>(() => transport.ResolveTokenAsync());
        }

        [Fact]
        [Requirement("AUTH-06", "AUTH", RequirementType.Positive, "Transports use passThroughToken when AllowPassThroughAuth is true")]
        public async Task Transports_Use_PassThroughToken_If_Allowed()
        {
            var server = new McpServer { Id = "test", AllowPassThroughAuth = true };
            var transport = new HttpTransport(server, new HttpClient(), NullLogger<HttpTransport>.Instance, null, "secret-token-123");
            var token = await transport.ResolveTokenAsync();
            Assert.Equal("secret-token-123", token);
        }

        [Fact]
        [Requirement("GUARD-02", "GUARD", RequirementType.Negative, "HttpTransport fails closed with InvalidOperationException when Kerberos impersonation lacks WindowsIdentity context.")]
        public async Task HttpTransport_SendRequestAsync_Throws_When_Impersonation_Missing_WindowsIdentity()
        {
            var server = new McpServer
            {
                Id = "impersonate-srv",
                Url = "http://localhost:5000/mcp",
                AuthShape = "impersonation"
            };
            var transport = new HttpTransport(server, new HttpClient(), NullLogger<HttpTransport>.Instance, null, null, null);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => transport.SendRequestAsync("ping", "{\"jsonrpc\":\"2.0\",\"method\":\"ping\",\"id\":1}"));
            Assert.Contains("Kerberos impersonation failed", ex.Message);
        }
    }
}
