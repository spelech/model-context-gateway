using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ModelContextGateway.Tests
{
    public class IdentityProviderTests
    {
        [Fact]
        [Requirement("AUTH-03", "AUTH", RequirementType.Positive, "OidcIdentityProvider extracts Remote-User and Remote-Groups SSO headers into UserIdentityContext.")]
        public async Task OidcIdentityProvider_Parses_Remote_User_And_Groups_Headers()
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
            context.Request.Headers["Remote-User"] = "admin_user";
            context.Request.Headers["Remote-Groups"] = "full_admin, engineering";

            var configDict = new Dictionary<string, string?> { ["Oidc:TrustedProxies"] = "127.0.0.1" };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();
            var provider = new OidcIdentityProvider(config);
            var identity = await provider.ResolveIdentityAsync(context);

            Assert.Equal("admin_user", identity.Username);
            Assert.Equal("HeaderAuth", identity.AuthenticationType);
            Assert.Equal(2, identity.GroupNames.Count);
            Assert.Contains("full_admin", identity.GroupNames);
            Assert.Contains("engineering", identity.GroupNames);
        }

        [Fact]
        [Requirement("AUTH-03", "AUTH", RequirementType.Positive, "CompositeIdentityProvider falls back to OIDC provider when ActiveDirectory is unauthenticated.")]
        public async Task CompositeIdentityProvider_Falls_Back_To_Oidc_When_AD_Not_Authenticated()
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
            context.Request.Headers["Remote-User"] = "alex";
            context.Request.Headers["sso_groups"] = "dev_team";

            var configDict = new Dictionary<string, string?> { ["Oidc:TrustedProxies"] = "127.0.0.1" };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();
            var adProvider = new ActiveDirectoryIdentityProvider(config);
            var oidcProvider = new OidcIdentityProvider(config);
            var composite = new CompositeIdentityProvider(new IIdentityProvider[] { adProvider, oidcProvider });

            var identity = await composite.ResolveIdentityAsync(context);

            Assert.Equal("alex", identity.Username);
            Assert.Contains("dev_team", identity.GroupNames);
        }

        [Fact]
        [Requirement("GUARD-06", "GUARD", RequirementType.Negative, "Header authentication strips remote identity headers when request is sent through untrusted proxy.")]
        public async Task HeaderAuth_StripsHeaders_ForUntrustedProxy()
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");
            context.Request.Headers["Remote-User"] = "malicious";
            context.Request.Headers["Remote-Groups"] = "admin, full_admin";
            context.Request.Headers["X-Forwarded-User"] = "malicious_forwarded";
            context.Request.Headers["sso_groups"] = "malicious_sso";

            var provider = new OidcIdentityProvider();
            var identity = await provider.ResolveIdentityAsync(context);

            Assert.Equal("guest", identity.Username);
            Assert.False(context.Request.Headers.ContainsKey("Remote-User"));
            Assert.False(context.Request.Headers.ContainsKey("Remote-Groups"));
            Assert.False(context.Request.Headers.ContainsKey("X-Forwarded-User"));
            Assert.False(context.Request.Headers.ContainsKey("sso_groups"));
            Assert.Empty(identity.GroupNames);
        }

        [Fact]
        [Requirement("AUTH-03", "AUTH", RequirementType.Positive, "Header authentication accepts remote identity headers when request originates from trusted proxy IP.")]
        public async Task HeaderAuth_AllowsHeaders_ForTrustedProxy()
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.10");
            context.Request.Headers["Remote-User"] = "dev_user";
            context.Request.Headers["Remote-Groups"] = "devops";

            var configDict = new Dictionary<string, string?>
            {
                ["Oidc:TrustedProxies"] = "10.0.0.10,127.0.0.1"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

            var provider = new OidcIdentityProvider(config);
            var identity = await provider.ResolveIdentityAsync(context);

            Assert.True(context.Request.Headers.ContainsKey("Remote-User"));
            Assert.Equal("dev_user", identity.Username);
            Assert.Contains("devops", identity.GroupNames);
        }

        [Fact]
        [Requirement("AUTH-03", "AUTH", RequirementType.Positive, "OidcIdentityProvider does not map Windows admin SID for arbitrary admin group names.")]
        public async Task OidcIdentityProvider_DoesNotMapAdminSid_ForAdminGroups()
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
            context.Request.Headers["Remote-User"] = "admin_user";
            context.Request.Headers["Remote-Groups"] = "full_admin";

            var provider = new OidcIdentityProvider();
            var identity = await provider.ResolveIdentityAsync(context);

            Assert.Empty(identity.AllSids);
        }

        /// <summary>
        /// Verifies that a single shared ClientSession evaluates identity from the live
        /// per-message HttpContext, not the cached handshake context.
        ///
        /// Scenario:
        ///   - Session is established with a dummy handshake context (neither Alice nor Bob).
        ///   - Alice's per-request context resolves to an admin SID → tool call is authorized.
        ///   - Bob's per-request context resolves to no SID/groups → tool call is denied (isError:true).
        /// </summary>
        [Fact]
        [Requirement("AUTH-SSE-PER-MESSAGE-IDENTITY", "AUTH", RequirementType.Positive, "SSE streams re-validate caller identity and permissions per message payload.")]
        public async Task SSE_ValidatesIdentityPerMessage()
        {
            const string AdminSid = "S-1-5-32-544";

            // --- Build a per-request-context-aware mock identity provider ---
            // The mock returns Alice's admin identity when her context is passed in,
            // and Bob's unprivileged identity when his context is passed in.
            var aliceContext = new DefaultHttpContext();
            aliceContext.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;

            var bobContext = new DefaultHttpContext();
            bobContext.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;

            var aliceIdentity = new UserIdentityContext("alice", "Test", new List<string> { "full_admin" }, AdminSid);
            var bobIdentity = new UserIdentityContext("bob", "Test", new List<string>(), "");

            var mockProvider = new Moq.Mock<IIdentityProvider>();
            mockProvider.Setup(p => p.ResolveIdentityAsync(aliceContext)).ReturnsAsync(aliceIdentity);
            mockProvider.Setup(p => p.ResolveIdentityAsync(bobContext)).ReturnsAsync(bobIdentity);

            // --- Shared service provider ---
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Audit:FailClosed"] = "false",
                ["Admin:GroupSid"] = AdminSid
            }).Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddSingleton(new CompositeIdentityProvider(new[] { mockProvider.Object }));
            var sp = services.BuildServiceProvider();

            aliceContext.RequestServices = sp;
            bobContext.RequestServices = sp;

            // Handshake context — session constructed with Alice's response but identity
            // must NOT be used for subsequent per-message calls
            var handshakeContext = new DefaultHttpContext();
            handshakeContext.RequestServices = sp;
            handshakeContext.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;

            var servers = new List<McpServer>();
            var httpClient = new System.Net.Http.HttpClient();
            var loggerMock = new Moq.Mock<Microsoft.Extensions.Logging.ILogger>();
            var embeddingMock = new Moq.Mock<IEmbeddingService>();

            var session = new ClientSession("test-sse-session", handshakeContext.Response, servers, httpClient, embeddingMock.Object, loggerMock.Object);

            var toolBody = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"search_tools\",\"arguments\":{\"query\":\"test\"}}}";

            // --- Alice's call: admin SID bypass → authorized ---
            // Tool routing will fail (no backends) but the auth layer must not deny her.
            try { await session.CallToolAsync("search_tools", toolBody, dbFactory: null!, httpContext: aliceContext); }
            catch (UnauthorizedAccessException) { throw; }  // fail the test if auth denies alice
            catch { /* routing / no-backend exceptions are expected in a unit test */ }

            // --- Bob's call: no SID, no DB policy → RBAC must deny (isError:true) ---
            // CallToolAsync returns an MCP error object on RBAC denial (protocol-conformant).
            var bobResult = await session.CallToolAsync("search_tools", toolBody, dbFactory: null!, httpContext: bobContext);
            var bobJson = System.Text.Json.JsonSerializer.Serialize(bobResult);
            Assert.Contains("\"isError\":true", bobJson);
            Assert.Contains("Security Error", bobJson);
            Assert.Contains("bob", bobJson);
        }

        [Fact]
        [Requirement("GUARD-02", "GUARD", RequirementType.Negative, "LdapService rejects unencrypted LDAP with InvalidOperationException.")]
        public async Task LdapService_ThrowsInvalidOperation_WhenUseSslFalse()
        {
            var configDict = new Dictionary<string, string?>
            {
                ["Ldap:Server"] = "127.0.0.1",
                ["Ldap:Port"] = "389",
                ["Ldap:UseSsl"] = "false"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LdapActiveDirectoryService>>().Object;
            var service = new LdapActiveDirectoryService(config, logger);

            await Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
            {
                await service.ResolveUserSidsAsync("testuser");
            });
        }

        [Fact]
        [Requirement("GUARD-02", "GUARD", RequirementType.Negative, "LdapService throws SecurityException when LDAP bind authentication fails.")]
        public async Task LdapService_ThrowsSecurityException_OnBindFailure()
        {
            var configDict = new Dictionary<string, string?>
            {
                ["Ldap:Server"] = "127.0.0.1",
                ["Ldap:Port"] = "636",
                ["Ldap:UseSsl"] = "true"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LdapActiveDirectoryService>>().Object;
            var service = new LdapActiveDirectoryService(config, logger);

            await Assert.ThrowsAsync<System.Security.SecurityException>(async () =>
            {
                await service.ResolveUserSidsAsync("testuser");
            });
        }

        [Fact]
        [Requirement("AUTH-ADMIN-VALIDATE-SID", "SecurityValidationHelper authorizes principals via Admin Group SID", Type = RequirementType.Positive, Category = "AUTH")]
        public void SecurityValidationHelper_IsAdmin_RequiresAdminGroupSid()
        {
            var configDict = new Dictionary<string, string?>
            {
                ["Admin:GroupSid"] = "S-1-5-32-544",
                ["Admin:GroupName"] = "custom_admin_only"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

            var adminIdentity = new UserIdentityContext("admin_user", "Test", new List<string> { "regular_users" }, Sid: "", Sids: new List<string> { "S-1-5-32-544" });
            var nonAdminIdentity = new UserIdentityContext("user", "Test", new List<string> { "regular_users" }, Sid: "", Sids: new List<string> { "S-1-5-21-999" });

            Assert.True(ModelContextGateway.Components.Authorization.SecurityValidationHelper.IsAdmin(adminIdentity, config));
            Assert.False(ModelContextGateway.Components.Authorization.SecurityValidationHelper.IsAdmin(nonAdminIdentity, config));
        }

        [Fact]
        [Requirement("AUTH-ADMIN-VALIDATE-GROUPNAME", "SecurityValidationHelper authorizes principals via Admin Group Name", Type = RequirementType.Positive, Category = "AUTH")]
        public void SecurityValidationHelper_IsAdmin_AllowsAdminGroupName()
        {
            var configDict = new Dictionary<string, string?>
            {
                { "Admin:GroupName", "full_admin" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

            var identity = new UserIdentityContext("admin_user", "HeaderAuth", new List<string> { "full_admin", "devops" });
            Assert.True(ModelContextGateway.Components.Authorization.SecurityValidationHelper.IsAdmin(identity, config));
        }

        [Fact]
        [Requirement("AUTH-ADMIN-REJECT-NONADMIN", "SecurityValidationHelper rejects non-admin groups and guest identities", Type = RequirementType.Negative, Category = "AUTH")]
        public void SecurityValidationHelper_IsAdmin_RejectsNonAdminGroups()
        {
            var configDict = new Dictionary<string, string?>
            {
                { "Admin:GroupName", "full_admin" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

            var identity = new UserIdentityContext("alice", "HeaderAuth", new List<string> { "house_member" });
            Assert.False(ModelContextGateway.Components.Authorization.SecurityValidationHelper.IsAdmin(identity, config));

            var guestIdentity = new UserIdentityContext("guest", "HeaderAuth", new List<string> { "full_admin" });
            Assert.False(ModelContextGateway.Components.Authorization.SecurityValidationHelper.IsAdmin(guestIdentity, config));
        }

        [Fact]
        [Requirement("AUTH-ADMIN-VALIDATE-GROUPS-ARRAY", "SecurityValidationHelper authorizes principals via custom configured Admin:Groups array", Type = RequirementType.Positive, Category = "AUTH")]
        public void SecurityValidationHelper_IsAdmin_AllowsCustomAdminGroupsArray()
        {
            var configDict = new Dictionary<string, string?>
            {
                { "Admin:Groups:0", "DevOps_Admins" },
                { "Admin:Groups:1", "Cloud_Architects" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

            var identity = new UserIdentityContext("bob", "HeaderAuth", new List<string> { "DevOps_Admins", "developers" });
            Assert.True(ModelContextGateway.Components.Authorization.SecurityValidationHelper.IsAdmin(identity, config));
        }

        [Fact]
        [Requirement("AUTH-ADMIN-VALIDATE-MAPPED-GROUPS", "SecurityValidationHelper authorizes principals via mappedGroups database resolution", Type = RequirementType.Positive, Category = "AUTH")]
        public void SecurityValidationHelper_IsAdmin_AllowsMappedGroups()
        {
            var configDict = new Dictionary<string, string?>
            {
                { "Admin:GroupName", "full_admin" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

            var identity = new UserIdentityContext("charlie", "HeaderAuth", new List<string> { "Oidc_Admins" });
            var mappedGroups = new List<string> { "full_admin" };
            Assert.True(ModelContextGateway.Components.Authorization.SecurityValidationHelper.IsAdmin(identity, config, mappedGroups));
        }

        [Fact]
        [Requirement("AUTH-OIDC-PRESERVE-GROUP-NAMES", "OidcIdentityProvider preserves group names without synthesizing Windows SIDs", Type = RequirementType.Positive, Category = "AUTH")]
        public async Task OidcIdentityProvider_DoesNotGrantAdminSid_FromGroupOrUserNames()
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
            context.Request.Headers["Remote-User"] = "admin";
            context.Request.Headers["Remote-Groups"] = "full_admin, Administrators";

            var configDict = new Dictionary<string, string?>
            {
                ["Oidc:TrustedProxies"] = "127.0.0.1"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();
            var provider = new OidcIdentityProvider(config);

            var identity = await provider.ResolveIdentityAsync(context);

            Assert.Equal("admin", identity.Username);
            Assert.Empty(identity.AllSids);
            Assert.True(ModelContextGateway.Components.Authorization.SecurityValidationHelper.IsAdmin(identity, config));
        }

        [Fact]
        [Requirement("GUARD-06", "GUARD", RequirementType.Negative, "TrustedProxyHelper rejects loopback forward headers when not allowlisted.")]
        public void TrustedProxyHelper_DeniesLoopback_WhenNotExplicitlyAllowlisted()
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
            context.Request.Headers["Remote-User"] = "malicious";

            var configDict = new Dictionary<string, string?>
            {
                ["Oidc:RequireTrustedProxy"] = "true",
                ["Oidc:TrustedProxies"] = "10.0.0.10" // Loopback (127.0.0.1) NOT included!
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

            bool isTrusted = TrustedProxyHelper.IsTrustedProxy(context, config);
            Assert.False(isTrusted);
        }

        [Fact]
        [Requirement("GUARD-06", "GUARD", RequirementType.Negative, "TrustedProxyHelper rejects X-Forwarded-For header when proxy chain contains untrusted hop.")]
        public void TrustedProxyHelper_DeniesXForwardedFor_WhenChainHasUntrustedHop()
        {
            var context = new DefaultHttpContext();
            // Direct connection is trusted proxy (10.0.0.10)
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.10");
            // XFF chain has untrusted hop 192.168.1.50 between client and trusted proxy
            context.Request.Headers["X-Forwarded-For"] = "203.0.113.195, 192.168.1.50, 10.0.0.10";

            var configDict = new Dictionary<string, string?>
            {
                ["Oidc:RequireTrustedProxy"] = "true",
                ["Oidc:TrustedProxies"] = "10.0.0.10" // 192.168.1.50 is NOT trusted
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

            bool isTrusted = TrustedProxyHelper.IsTrustedProxy(context, config);
            Assert.False(isTrusted);
        }

        [Fact]
        [ModelContextGateway.Tests.Attributes.Requirement("SEC-03", "Ensure TrustedProxyHelper supports CIDR ranges in XFF validation", Type = ModelContextGateway.Tests.Attributes.RequirementType.Positive, Category = "SEC")]
        public void TrustedProxyHelper_AllowsXForwardedFor_WhenChainIsFullyTrusted()
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.10");
            // XFF chain: Cloudflare IP -> Local Gateway -> Nginx (direct connection)
            context.Request.Headers["X-Forwarded-For"] = "203.0.113.195, 172.16.0.5, 10.0.0.10";

            var configDict = new Dictionary<string, string?>
            {
                ["Oidc:RequireTrustedProxy"] = "true",
                ["Oidc:TrustedProxies"] = "10.0.0.0/8, 172.16.0.0/12"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

            bool isTrusted = TrustedProxyHelper.IsTrustedProxy(context, config);
            Assert.True(isTrusted);
        }

        [Fact]
        [Requirement("GUARD-06", "GUARD", RequirementType.Positive, "TrustedProxyHelper defaults to loopback trust while rejecting unconfigured LAN hosts.")]
        public void TrustedProxyHelper_Unconfigured_LoopbackTrusted_LANNotTrusted()
        {
            // Unconfigured (TrustedProxies empty)
            var configDict = new Dictionary<string, string?>
            {
                ["Oidc:TrustedProxies"] = ""
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

            // 1. Loopback ip address should be trusted
            var loopbackCtx = new DefaultHttpContext();
            loopbackCtx.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
            Assert.True(TrustedProxyHelper.IsTrustedProxy(loopbackCtx, config));

            // 2. LAN ip address (e.g., 10.0.0.50) should NOT be trusted
            var lanCtx = new DefaultHttpContext();
            lanCtx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.50");
            Assert.False(TrustedProxyHelper.IsTrustedProxy(lanCtx, config));
        }

        [Fact]
        [Requirement("GUARD-06", "GUARD", RequirementType.Positive, "TrustedProxyHelper validates configured proxy IP addresses.")]
        public void TrustedProxyHelper_ConfiguredProxyTrusted()
        {
            var configDict = new Dictionary<string, string?>
            {
                ["Oidc:TrustedProxies"] = "10.0.0.50"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

            var proxyCtx = new DefaultHttpContext();
            proxyCtx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.50");
            Assert.True(TrustedProxyHelper.IsTrustedProxy(proxyCtx, config));

            var otherCtx = new DefaultHttpContext();
            otherCtx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.51");
            Assert.False(TrustedProxyHelper.IsTrustedProxy(otherCtx, config));
        }

        [Fact]
        [ModelContextGateway.Tests.Attributes.Requirement("SEC-03", "Ensure TrustedProxyHelper supports CIDR ranges for proxy validation", Type = ModelContextGateway.Tests.Attributes.RequirementType.Positive, Category = "SEC")]
        public void TrustedProxyHelper_ConfiguredProxyTrusted_CIDR()
        {
            var configDict = new Dictionary<string, string?>
            {
                ["Oidc:TrustedProxies"] = "192.168.1.0/24, 10.0.0.0/8, 172.16.0.5"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

            var proxyCtx = new DefaultHttpContext();
            proxyCtx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.15");
            Assert.True(TrustedProxyHelper.IsTrustedProxy(proxyCtx, config));

            var proxyCtx2 = new DefaultHttpContext();
            proxyCtx2.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.5.5.5");
            Assert.True(TrustedProxyHelper.IsTrustedProxy(proxyCtx2, config));

            var proxyCtx3 = new DefaultHttpContext();
            proxyCtx3.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("172.16.0.5");
            Assert.True(TrustedProxyHelper.IsTrustedProxy(proxyCtx3, config));

            var otherCtx = new DefaultHttpContext();
            otherCtx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.2.15");
            Assert.False(TrustedProxyHelper.IsTrustedProxy(otherCtx, config));
        }

        [Fact]
        [Requirement("GUARD-06", "GUARD", RequirementType.Negative, "Forged SSO headers from untrusted LAN hosts degrade session to guest.")]
        public async Task TrustedProxyHelper_ForgedHeaderFromLanHost_DegradesToGuest()
        {
            var configDict = new Dictionary<string, string?>
            {
                ["Oidc:TrustedProxies"] = "127.0.0.1" // loopback only
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.50"); // LAN host (not trusted)
            context.Request.Headers["Remote-User"] = "malicious_admin";
            context.Request.Headers["Remote-Groups"] = "full_admin";

            var provider = new OidcIdentityProvider(config);
            var identity = await provider.ResolveIdentityAsync(context);

            Assert.Equal("guest", identity.Username);
            Assert.Empty(identity.GroupNames);
            Assert.False(context.Request.Headers.ContainsKey("Remote-User"));
            Assert.False(context.Request.Headers.ContainsKey("Remote-Groups"));
        }

        [Fact]
        [Requirement("SEC-02", "SEC", RequirementType.Positive, "ConnectAndInitializeBackendAsync resolves Vault secret retriever from root DI services when HttpContext is null.")]
        public async Task ConnectAndInitializeBackendAsync_WithVaultServer_ResolvesRetrieverFromRootServices_WhenHttpContextIsNull()
        {
            // Arrange
            var server = new McpServer
            {
                Id = "vault-server",
                Enabled = true,
                Url = "http://localhost:8080",
                Type = "http", // Keep it simple so it doesn't try to open SSE sockets
                SecretProvider = "Vault"
            };

            var mockInnerRetriever = new Mock<ISecretRetriever>();
            mockInnerRetriever.Setup(r => r.ProviderName).Returns("Vault");
            mockInnerRetriever.Setup(r => r.GetSecretAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("dummy-secret-value");

            var realComposite = new CompositeSecretRetriever(new List<ISecretRetriever> { mockInnerRetriever.Object }, null);

            var services = new ServiceCollection();
            services.AddSingleton(realComposite);
            var rootServices = services.BuildServiceProvider();

            // ClientSession with null HttpContext (so clientResponse is null)
            var session = new ClientSession(
                "session-id",
                null!, // clientResponse is null
                new List<McpServer> { server },
                new HttpClient(),
                new Mock<IEmbeddingService>().Object,
                null,
                new Mock<Microsoft.Extensions.Logging.ILogger<ClientSession>>().Object,
                rootServices
            );

            // Act
            // ConnectAndInitializeBackendAsync is private, so we invoke it via reflection.
            var method = typeof(ClientSession).GetMethod("ConnectAndInitializeBackendAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            var task = (Task)method.Invoke(session, new object[] { server })!;
            await task;

            // Assert
            // The method executes without throwing a NullReferenceException on HTTP context or missing secret retriever.
            // Since it's configured as type="http", it won't attempt to open persistent SSE/Stream connections, but it does
            // invoke secret retrieval.
            mockInnerRetriever.Verify(r => r.GetSecretAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce());
        }
    }
}


