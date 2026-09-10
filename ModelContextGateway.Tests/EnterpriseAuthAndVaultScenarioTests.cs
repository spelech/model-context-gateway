using System.Reflection;
using System.Security.Claims;
using System.Security.Principal;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ModelContextGateway.Tests
{
    public class EnterpriseAuthAndVaultScenarioTests
    {
        private async Task InvokeApplyAuthAndCustomHeadersAsync(HttpTransport transport, HttpRequestMessage request)
        {
            var method = typeof(HttpTransport).GetMethod("ApplyAuthAndCustomHeadersAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            var task = (Task)method.Invoke(transport, new object[] { request })!;
            await task;
        }

        #region 1. Active Directory & LDAP SID Resolution (Scenario A & B)

        [Fact]
        [Requirement("AUTH-04", "AUTH", RequirementType.Positive, "ActiveDirectoryIdentityProvider resolves Steve Windows identity with primary SID and group SIDs.")]
        public async Task ActiveDirectoryIdentityProvider_Resolves_Steve_Identity_And_Sids()
        {
            var mockWindowsAccessor = new Mock<IWindowsIdentityAccessor>();
            string? steveSid = "S-1-5-21-1001";
            var steveGroups = new List<string> { "S-1-5-21-2001", "S-1-5-21-2002" }; // MCP Developers, Slack Users

            var identity = new GenericIdentity("CORP\\steve");
            mockWindowsAccessor.Setup(w => w.TryGetWindowsIdentityDetails(identity, out steveSid, out steveGroups))
                .Returns(true);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Oidc:RequireTrustedProxy", "false" }
            }).Build();

            var provider = new ActiveDirectoryIdentityProvider(
                configuration: config,
                ldapService: null,
                authRepo: null,
                windowsIdentityAccessor: mockWindowsAccessor.Object);

            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(identity);

            var context = await provider.ResolveIdentityAsync(httpContext);

            context.Should().NotBeNull();
            context.Username.Should().Be("CORP\\steve");
            context.Sid.Should().Be("S-1-5-21-1001");
            context.Sids.Should().Contain(new[] { "S-1-5-21-1001", "S-1-5-21-2001", "S-1-5-21-2002" });
        }

        [Fact]
        [Requirement("AUTH-04", "AUTH", RequirementType.Positive, "ActiveDirectoryIdentityProvider augments Steve identity with LDAP group SIDs.")]
        public async Task ActiveDirectoryIdentityProvider_Augments_Steve_With_LdapGroupSids()
        {
            var mockWindowsAccessor = new Mock<IWindowsIdentityAccessor>();
            string? steveSid = "S-1-5-21-1001";
            var winGroups = new List<string> { "S-1-5-21-2001" };

            var identity = new GenericIdentity("steve");
            mockWindowsAccessor.Setup(w => w.TryGetWindowsIdentityDetails(identity, out steveSid, out winGroups))
                .Returns(true);

            var mockLdap = new Mock<ILdapService>();
            mockLdap.Setup(l => l.ResolveUserSidsAsync("steve"))
                .ReturnsAsync(new List<string> { "S-1-5-21-2002", "S-1-5-32-544" }); // Augmented with Slack Users and Admin

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Oidc:RequireTrustedProxy", "false" }
            }).Build();

            var provider = new ActiveDirectoryIdentityProvider(
                configuration: config,
                ldapService: mockLdap.Object,
                authRepo: null,
                windowsIdentityAccessor: mockWindowsAccessor.Object);

            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(identity);

            var context = await provider.ResolveIdentityAsync(httpContext);

            context.Should().NotBeNull();
            context.Sids.Should().Contain(new[] { "S-1-5-21-1001", "S-1-5-21-2001", "S-1-5-21-2002", "S-1-5-32-544" });
        }

        #endregion

        #region 2. HashiCorp Vault Path & Store Shortcomings

        [Fact]
        [Requirement("SEC-02", "SEC", RequirementType.Positive, "VaultUserSecretStore queries hardcoded path 'users/{username}/{serverId}' with key 'secret'.")]
        public async Task VaultUserSecretStore_GetSecretAsync_Uses_Hardcoded_Path()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Vault:Address", "http://vault.local:8200" },
                { "Vault:Token", "test-token" }
            }).Build();

            var cache = new MemoryCache(new MemoryCacheOptions());
            // Pre-seed cache with expected hardcoded key: "vault:secret:users/steve/slack:secret"
            cache.Set("vault:secret:users/steve/slack:secret", "xoxp-steve-slack-token-999");

            var retriever = new VaultSecretRetriever(config, cache);
            var store = new VaultUserSecretStore(retriever);

            var secret = await store.GetSecretAsync("steve", "slack");
            secret.Should().Be("xoxp-steve-slack-token-999");
        }

        [Fact]
        [Requirement("SEC-02", "SEC", RequirementType.Negative, "VaultUserSecretStore throws NotImplementedException on SaveSecretAsync confirming Vault write limitation.")]
        public async Task VaultUserSecretStore_SaveSecretAsync_Throws_NotImplementedException()
        {
            var retriever = new VaultSecretRetriever(new ConfigurationBuilder().Build(), new MemoryCache(new MemoryCacheOptions()));
            var store = new VaultUserSecretStore(retriever);

            await Assert.ThrowsAsync<NotImplementedException>(() => store.SaveSecretAsync("steve", "slack", "{}"));
        }

        [Fact]
        [Requirement("SEC-02", "SEC", RequirementType.Negative, "VaultUserSecretStore throws NotImplementedException on DeleteSecretAsync confirming Vault delete limitation.")]
        public async Task VaultUserSecretStore_DeleteSecretAsync_Throws_NotImplementedException()
        {
            var retriever = new VaultSecretRetriever(new ConfigurationBuilder().Build(), new MemoryCache(new MemoryCacheOptions()));
            var store = new VaultUserSecretStore(retriever);

            await Assert.ThrowsAsync<NotImplementedException>(() => store.DeleteSecretAsync("steve", "slack"));
        }

        [Fact]
        [Requirement("SEC-02", "SEC", RequirementType.Negative, "VaultUserSecretStore throws NotImplementedException on GetServerIdsAsync confirming Vault list limitation.")]
        public async Task VaultUserSecretStore_GetServerIdsAsync_Throws_NotImplementedException()
        {
            var retriever = new VaultSecretRetriever(new ConfigurationBuilder().Build(), new MemoryCache(new MemoryCacheOptions()));
            var store = new VaultUserSecretStore(retriever);

            await Assert.ThrowsAsync<NotImplementedException>(() => store.GetServerIdsAsync("steve"));
        }

        #endregion

        #region 3. Downstream Auth Matrix Verification

        [Fact]
        [Requirement("TRANS-01", "TRANS", RequirementType.Positive, "HttpTransport formats X-API-Key header when downstream server AuthShape is 'x-api-key'.")]
        public async Task HttpTransport_Applies_XApiKey_AuthShape()
        {
            var server = new McpServer
            {
                Id = "internal-appkey-mcp",
                Url = "http://mock-downstream:8090/apikey/mcp",
                SecretProvider = "None",
                ApiKey = "corp-internal-key-456",
                AuthShape = "x-api-key"
            };

            var transport = new HttpTransport(server, new HttpClient(), NullLogger<HttpTransport>.Instance);
            var req = new HttpRequestMessage(HttpMethod.Post, server.Url);

            await InvokeApplyAuthAndCustomHeadersAsync(transport, req);

            req.Headers.Contains("X-API-Key").Should().BeTrue();
            req.Headers.GetValues("X-API-Key").First().Should().Be("corp-internal-key-456");
        }

        [Fact]
        [Requirement("TRANS-01", "TRANS", RequirementType.Positive, "HttpTransport formats proprietary CustomHeaderName when downstream server AuthShape is 'custom-header'.")]
        public async Task HttpTransport_Applies_CustomHeader_AuthShape()
        {
            var server = new McpServer
            {
                Id = "custom-header-mcp",
                Url = "http://mock-downstream:8090/customheader/mcp",
                SecretProvider = "None",
                ApiKey = "custom-corp-token-789",
                AuthShape = "custom-header",
                CustomHeaderName = "X-Internal-Token"
            };

            var transport = new HttpTransport(server, new HttpClient(), NullLogger<HttpTransport>.Instance);
            var req = new HttpRequestMessage(HttpMethod.Post, server.Url);

            await InvokeApplyAuthAndCustomHeadersAsync(transport, req);

            req.Headers.Contains("X-Internal-Token").Should().BeTrue();
            req.Headers.GetValues("X-Internal-Token").First().Should().Be("custom-corp-token-789");
        }

        [Fact]
        [Requirement("TRANS-01", "TRANS", RequirementType.Positive, "HttpTransport injects per-user Bearer token and X-Forwarded-User header when SecretProvider is 'UserProvided'.")]
        public async Task HttpTransport_Applies_Slack_PerUser_Token_And_ForwardedUser()
        {
            var server = new McpServer
            {
                Id = "slack",
                Url = "http://mock-downstream:8090/slack/mcp",
                SecretProvider = "UserProvided",
                AuthShape = "bearer"
            };

            // Simulating ClientSession passing Steve's resolved token and forwarded user identity
            var transport = new HttpTransport(
                server,
                new HttpClient(),
                NullLogger<HttpTransport>.Instance,
                secretRetriever: null,
                passThroughToken: "xoxp-steve-slack-token-999",
                forwardedUser: "steve");

            var req = new HttpRequestMessage(HttpMethod.Post, server.Url);

            await InvokeApplyAuthAndCustomHeadersAsync(transport, req);

            req.Headers.Authorization.Should().NotBeNull();
            req.Headers.Authorization!.Scheme.Should().Be("Bearer");
            req.Headers.Authorization!.Parameter.Should().Be("xoxp-steve-slack-token-999");
            req.Headers.Contains("X-Forwarded-User").Should().BeTrue();
            req.Headers.GetValues("X-Forwarded-User").First().Should().Be("steve");
        }

        #endregion
    }
}
