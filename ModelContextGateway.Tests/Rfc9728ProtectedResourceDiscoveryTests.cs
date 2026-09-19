using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ModelContextGateway.Tests
{
    public class CustomConfigPipelineFactory : WebApplicationFactory<Program>
    {
        private readonly Dictionary<string, string?> _customConfig;

        public CustomConfigPipelineFactory(Dictionary<string, string?> customConfig)
        {
            _customConfig = customConfig;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(_customConfig);
            });
        }
    }

    public class Rfc9728ProtectedResourceDiscoveryTests : IClassFixture<PipelineIntegrationFactory>
    {
        private readonly PipelineIntegrationFactory _factory;

        public Rfc9728ProtectedResourceDiscoveryTests(PipelineIntegrationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        [Requirement("AUTH-131", "AUTH", RequirementType.Positive, "RFC 9728 Protected Resource Metadata endpoint exposes discovery document with canonical resource URI, authorization servers, scopes, and documentation.")]
        public async Task ProtectedResourceDiscovery_ReturnsValidMetadataDocument()
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/.well-known/oauth-protected-resource");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var metadata = await response.Content.ReadFromJsonAsync<ProtectedResourceMetadata>();
            Assert.NotNull(metadata);
            Assert.False(string.IsNullOrWhiteSpace(metadata.Resource));
            Assert.NotEmpty(metadata.AuthorizationServers);
            Assert.Contains(metadata.ScopesSupported, s => s == "openid");
            Assert.Contains(metadata.ScopesSupported, s => s == "profile");
            Assert.Contains(metadata.ScopesSupported, s => s == "email");
            Assert.Contains(metadata.ScopesSupported, s => s == "mcp:access");
            Assert.Contains("header", metadata.BearerMethodsSupported);
            Assert.EndsWith("/docs", metadata.ResourceDocumentation);
        }

        [Fact]
        [Requirement("AUTH-131", "AUTH", RequirementType.Positive, "RFC 9728 Protected Resource Metadata endpoint supports path-aware target server metadata documents.")]
        public async Task ProtectedResourceDiscovery_TargetPath_ReturnsPathAwareMetadataDocument()
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/.well-known/oauth-protected-resource/test-target-server");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var metadata = await response.Content.ReadFromJsonAsync<ProtectedResourceMetadata>();
            Assert.NotNull(metadata);
            Assert.EndsWith("/test-target-server", metadata.Resource);
            Assert.NotEmpty(metadata.AuthorizationServers);
            Assert.Contains("header", metadata.BearerMethodsSupported);
            Assert.EndsWith("/docs", metadata.ResourceDocumentation);
        }

        [Fact]
        [Requirement("AUTH-131", "AUTH", RequirementType.Positive, "RFC 9728 Protected Resource Metadata respects explicit resource query parameter per specification.")]
        public async Task ProtectedResourceDiscovery_CustomResourceQuery_OverridesResourceUri()
        {
            var client = _factory.CreateClient();
            var explicitResource = "https://custom.gateway.internal/my-resource";
            var response = await client.GetAsync($"/.well-known/oauth-protected-resource?resource={Uri.EscapeDataString(explicitResource)}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var metadata = await response.Content.ReadFromJsonAsync<ProtectedResourceMetadata>();
            Assert.NotNull(metadata);
            Assert.Equal(explicitResource, metadata.Resource);
            Assert.NotEmpty(metadata.AuthorizationServers);
        }

        [Fact]
        [Requirement("AUTH-131", "AUTH", RequirementType.Positive, "RFC 9728 Protected Resource Metadata populates authorization servers and canonical URL from configuration.")]
        public async Task ProtectedResourceDiscovery_ConfiguredExternalIdp_ReturnsConfiguredAuthorizationServer()
        {
            using var customFactory = new CustomConfigPipelineFactory(new Dictionary<string, string?>
            {
                { "ConnectionStrings:Sqlite", "Data Source=file:prm_idp_test?mode=memory&cache=shared" },
                { "DB_ENCRYPTION_KEY", "TestSecretKey1234567890123456789012" },
                { "Gateway:PublicUrl", "https://mcg.example.internal" },
                { "Identity:Jwt:Authority", "https://authentik.corp.internal/application/o/mcp/" }
            });

            var client = customFactory.CreateClient();
            var response = await client.GetAsync("/.well-known/oauth-protected-resource");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var metadata = await response.Content.ReadFromJsonAsync<ProtectedResourceMetadata>();
            Assert.NotNull(metadata);
            Assert.Equal("https://mcg.example.internal", metadata.Resource);
            Assert.Contains("https://authentik.corp.internal/application/o/mcp", metadata.AuthorizationServers);
            Assert.Equal("https://mcg.example.internal/docs", metadata.ResourceDocumentation);
        }

        [Fact]
        [Requirement("AUTH-132", "AUTH", RequirementType.Positive, "Unauthenticated client requests to /sse endpoint return 401 Unauthorized with RFC 9728 WWW-Authenticate header containing realm and resource_metadata.")]
        public async Task UnauthenticatedMcpRequest_SseEndpoint_Returns401WithRfc9728WwwAuthenticateHeader()
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/sse");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(response.Headers.WwwAuthenticate.Any());

            var authHeader = response.Headers.WwwAuthenticate.ToString();
            Assert.Contains("Bearer", authHeader);
            Assert.Contains("realm=\"mcp\"", authHeader);
            Assert.Contains("resource_metadata=\"http://localhost/.well-known/oauth-protected-resource\"", authHeader);
        }

        [Fact]
        [Requirement("AUTH-132", "AUTH", RequirementType.Positive, "Unauthenticated client requests to target-specific MCP endpoints return 401 Unauthorized with path-aware resource_metadata.")]
        public async Task UnauthenticatedMcpRequest_TargetServerEndpoint_Returns401WithTargetResourceMetadata()
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/backend-service-one");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(response.Headers.WwwAuthenticate.Any());

            var authHeader = response.Headers.WwwAuthenticate.ToString();
            Assert.Contains("Bearer", authHeader);
            Assert.Contains("realm=\"mcp\"", authHeader);
            Assert.Contains("resource_metadata=\"http://localhost/.well-known/oauth-protected-resource/backend-service-one\"", authHeader);
        }

        [Fact]
        [Requirement("AUTH-132", "AUTH", RequirementType.Positive, "Unauthenticated client requests to /message session endpoint return 401 with RFC 9728 WWW-Authenticate header.")]
        public async Task UnauthenticatedMcpRequest_MessageEndpoint_Returns401WithRfc9728WwwAuthenticateHeader()
        {
            var client = _factory.CreateClient();
            var response = await client.PostAsync("/message?sessionId=unauth-test", null);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(response.Headers.WwwAuthenticate.Any());

            var authHeader = response.Headers.WwwAuthenticate.ToString();
            Assert.Contains("Bearer", authHeader);
            Assert.Contains("realm=\"mcp\"", authHeader);
            Assert.Contains("resource_metadata=\"http://localhost/.well-known/oauth-protected-resource\"", authHeader);
        }
    }
}
