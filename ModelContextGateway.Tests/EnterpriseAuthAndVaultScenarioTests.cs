using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Security.Principal;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;
using VaultSharp;
using VaultSharp.V1;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;
using VaultSharp.V1.SecretsEngines.KeyValue;
using VaultSharp.V1.SecretsEngines.KeyValue.V2;

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

        private static (Mock<IVaultClient> client, Mock<IKeyValueSecretsEngineV2> kv2) CreateMockVault()
        {
            var mockVaultClient = new Mock<IVaultClient>();
            var mockV1 = new Mock<IVaultClientV1>();
            var mockSecrets = new Mock<ISecretsEngine>();
            var mockKv = new Mock<IKeyValueSecretsEngine>();
            var mockKv2 = new Mock<IKeyValueSecretsEngineV2>();

            mockKv.Setup(k => k.V2).Returns(mockKv2.Object);
            mockSecrets.Setup(s => s.KeyValue).Returns(mockKv.Object);
            mockV1.Setup(v => v.Secrets).Returns(mockSecrets.Object);
            mockVaultClient.Setup(c => c.V1).Returns(mockV1.Object);

            return (mockVaultClient, mockKv2);
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

        #region 2. HashiCorp Vault Path Templating & Full CRUD Support

        [Fact]
        [Requirement("SEC-02", "SEC", RequirementType.Positive, "VaultUserSecretStore saves user secret to Vault KV v2 with unpacked JSON properties.")]
        public async Task VaultUserSecretStore_SaveSecretAsync_WritesToVaultKv2()
        {
            var (mockClient, mockKv2) = CreateMockVault();
            IDictionary<string, object>? capturedData = null;
            string? capturedPath = null;

            mockKv2.Setup(k => k.WriteSecretAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), null, "secret"))
                .Callback<string, IDictionary<string, object>, int?, string>((path, data, cas, mount) =>
                {
                    capturedPath = path;
                    capturedData = data;
                })
                .ReturnsAsync(new Secret<CurrentSecretMetadata>());

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Vault:Address", "http://vault.local:8200" }
            }).Build();

            var retriever = new VaultSecretRetriever(config, new MemoryCache(new MemoryCacheOptions()), () => mockClient.Object);
            var store = new VaultUserSecretStore(retriever, config);

            var payload = "{\"client_id\":\"slack-123\",\"client_secret\":\"slack-secret-456\",\"access_token\":\"xoxp-steve\"}";
            await store.SaveSecretAsync("steve", "slack", payload);

            capturedPath.Should().Be("users/steve/slack");
            capturedData.Should().NotBeNull();
            capturedData!["client_id"].Should().Be("slack-123");
            capturedData["client_secret"].Should().Be("slack-secret-456");
            capturedData["access_token"].Should().Be("xoxp-steve");
            capturedData["secret"].Should().Be(payload);
        }

        [Fact]
        [Requirement("SEC-02", "SEC", RequirementType.Positive, "VaultUserSecretStore deletes user secret from Vault KV v2.")]
        public async Task VaultUserSecretStore_DeleteSecretAsync_DeletesFromVaultKv2()
        {
            var (mockClient, mockKv2) = CreateMockVault();
            string? deletedPath = null;

            mockKv2.Setup(k => k.DeleteSecretAsync(It.IsAny<string>(), "secret"))
                .Callback<string, string>((path, mount) => { deletedPath = path; })
                .Returns(Task.CompletedTask);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Vault:Address", "http://vault.local:8200" }
            }).Build();

            var retriever = new VaultSecretRetriever(config, new MemoryCache(new MemoryCacheOptions()), () => mockClient.Object);
            var store = new VaultUserSecretStore(retriever, config);

            await store.DeleteSecretAsync("steve", "slack");
            deletedPath.Should().Be("users/steve/slack");
        }

        [Fact]
        [Requirement("SEC-02", "SEC", RequirementType.Positive, "VaultUserSecretStore lists user server IDs from Vault KV v2 paths.")]
        public async Task VaultUserSecretStore_GetServerIdsAsync_ListsPathsFromVaultKv2()
        {
            var (mockClient, mockKv2) = CreateMockVault();

            var pathData = new Secret<ListInfo>
            {
                Data = new ListInfo
                {
                    Keys = new List<string> { "slack", "github", "jira/" }
                }
            };

            mockKv2.Setup(k => k.ReadSecretPathsAsync("users/steve", "secret", null))
                .ReturnsAsync(pathData);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Vault:Address", "http://vault.local:8200" }
            }).Build();

            var retriever = new VaultSecretRetriever(config, new MemoryCache(new MemoryCacheOptions()), () => mockClient.Object);
            var store = new VaultUserSecretStore(retriever, config);

            var serverIds = await store.GetServerIdsAsync("steve");
            serverIds.Should().Contain(new[] { "slack", "github", "jira" });
        }

        [Fact]
        [Requirement("SEC-02", "SEC", RequirementType.Positive, "VaultUserSecretStore resolves enterprise multi-tenant path template '{company}/mcgateway/{user}/{app}'.")]
        public async Task VaultUserSecretStore_Resolves_Enterprise_Path_Template_And_Discrete_KVs()
        {
            var (mockClient, mockKv2) = CreateMockVault();

            var secretData = new Secret<SecretData>
            {
                Data = new SecretData
                {
                    Data = new Dictionary<string, object>
                    {
                        { "client_id", "slack-id-99" },
                        { "client_secret", "slack-sec-99" },
                        { "access_token", "xoxp-steve-enterprise" }
                    }
                }
            };

            mockKv2.Setup(k => k.ReadSecretAsync("acme/mcgateway/steve/slack", null, "secret", null))
                .ReturnsAsync(secretData);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Vault:Address", "http://vault.local:8200" },
                { "Vault:UserSecretPathTemplate", "{company}/mcgateway/{user}/{app}" },
                { "Vault:Company", "acme" }
            }).Build();

            var retriever = new VaultSecretRetriever(config, new MemoryCache(new MemoryCacheOptions()), () => mockClient.Object);
            var store = new VaultUserSecretStore(retriever, config);

            var secret = await store.GetSecretAsync("steve", "slack");
            secret.Should().NotBeNull();
            // Discrete KVs containing access_token return access_token or full serialized json
            secret.Should().Be("xoxp-steve-enterprise");
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

        #region 4. Pluggable IUserSecretStore DI & External JWT Authentication

        [Fact]
        [Requirement("AUTH-02", "AUTH", RequirementType.Positive, "ServiceCollection dynamically resolves VaultUserSecretStore when Secrets:UserStore:Provider is 'Vault'.")]
        public void ServiceCollection_Resolves_VaultUserSecretStore_WhenConfigured()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Secrets:UserStore:Provider", "Vault" },
                { "Vault:Address", "http://vault.local:8200" },
                { "Vault:Token", "test-token" }
            }).Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddMemoryCache();
            services.AddLogging();
            services.AddSingleton<VaultSecretRetriever>();
            services.AddSingleton<VaultUserSecretStore>();
            services.AddSingleton<DatabaseUserSecretStore>(sp => new DatabaseUserSecretStore(null!, config));
            services.AddSingleton<IUserSecretStore>(sp =>
            {
                var cfg = sp.GetService<IConfiguration>();
                var provider = cfg?["Secrets:UserStore:Provider"] ?? "Database";
                if (string.Equals(provider, "Vault", StringComparison.OrdinalIgnoreCase))
                {
                    return sp.GetRequiredService<VaultUserSecretStore>();
                }
                return sp.GetRequiredService<DatabaseUserSecretStore>();
            });

            var provider = services.BuildServiceProvider();
            var resolvedStore = provider.GetRequiredService<IUserSecretStore>();

            resolvedStore.Should().BeOfType<VaultUserSecretStore>();
        }

        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly string _responseContent;
            public MockHttpMessageHandler(string responseContent) => _responseContent = responseContent;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_responseContent, System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(resp);
            }
        }

        [Fact]
        [Requirement("AUTH-02", "AUTH", RequirementType.Positive, "ExternalJwtAuthenticationHandler validates RS256 Bearer JWT and establishes authenticated user principal.")]
        public async Task ExternalJwtAuthenticationHandler_Authenticates_Valid_Bearer_Jwt()
        {
            using var rsa = System.Security.Cryptography.RSA.Create(2048);
            var securityKey = new RsaSecurityKey(rsa) { KeyId = "test-key-1" };
            var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(securityKey);
            var jwks = new JsonWebKeySet();
            jwks.Keys.Add(jwk);
            var jwksJson = JsonSerializer.Serialize(jwks);

            var tokenHandler = new JsonWebTokenHandler();
            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = "http://mock-idp.corp.local",
                Audience = "internal-mcp",
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("preferred_username", "steve"),
                    new Claim("roles", "MCP Developers"),
                    new Claim("sid", "S-1-5-21-1001")
                }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = signingCredentials
            };
            var jwt = tokenHandler.CreateToken(descriptor);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Identity:Jwt:Authority", "http://mock-idp.corp.local" },
                { "Identity:Jwt:Audience", "internal-mcp" }
            }).Build();

            var mockHttpFactory = new Mock<IHttpClientFactory>();
            mockHttpFactory.Setup(f => f.CreateClient("McpClient"))
                .Returns(new HttpClient(new MockHttpMessageHandler(jwksJson)));

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var optionsMonitor = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
            optionsMonitor.Setup(o => o.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());

            var handler = new ExternalJwtAuthenticationHandler(
                optionsMonitor.Object,
                NullLoggerFactory.Instance,
                UrlEncoder.Default,
                config,
                mockHttpFactory.Object,
                memoryCache);

            var context = new DefaultHttpContext();
            context.Request.Headers.Authorization = $"Bearer {jwt}";

            await handler.InitializeAsync(new AuthenticationScheme("ExternalJwt", "ExternalJwt", typeof(ExternalJwtAuthenticationHandler)), context);
            var result = await handler.AuthenticateAsync();

            result.Succeeded.Should().BeTrue();
            result.Principal.Should().NotBeNull();
            result.Principal!.Identity!.Name.Should().Be("steve");
            result.Principal.HasClaim(ClaimTypes.Role, "MCP Developers").Should().BeTrue();
            result.Principal.HasClaim(ClaimTypes.PrimarySid, "S-1-5-21-1001").Should().BeTrue();
        }

        #endregion
    }
}
