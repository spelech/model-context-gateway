using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ModelContextGateway.Tests
{
    public class LdapActiveDirectoryServiceTests
    {
        [Theory]
        [InlineData("user*name", "user\\2aname")]
        [InlineData("user(name)", "user\\28name\\29")]
        [InlineData("user\\name", "user\\5cname")]
        [InlineData("user\0null", "user\\00null")]
        [InlineData("", "")]
        [Requirement("GUARD-02", "GUARD", RequirementType.Positive, "EscapeLdapFilter sanitizes and escapes special LDAP filter characters to prevent LDAP injection.")]
        public void EscapeLdapFilter_EscapesSpecialCharacters(string input, string expected)
        {
            var result = LdapActiveDirectoryService.EscapeLdapFilter(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        [Requirement("AUTH-04", "AUTH", RequirementType.Positive, "ConvertSidBytesToString accurately formats binary SID byte arrays to Windows SID string format.")]
        public void ConvertSidBytesToString_FormatsValidBinarySid()
        {
            // Binary representation of S-1-5-32-544 (Builtin Administrators)
            byte[] sidBytes = new byte[] { 1, 2, 0, 0, 0, 0, 0, 5, 32, 0, 0, 0, 32, 2, 0, 0 };
            var sidStr = LdapActiveDirectoryService.ConvertSidBytesToString(sidBytes);

            Assert.Equal("S-1-5-32-544", sidStr);
        }

        [Fact]
        [Requirement("AUTH-04", "AUTH", RequirementType.Positive, "ConvertSidBytesToString returns empty string for invalid binary SID buffers.")]
        public void ConvertSidBytesToString_ReturnsEmpty_OnInvalidBytes()
        {
            Assert.Equal(string.Empty, LdapActiveDirectoryService.ConvertSidBytesToString(null!));
            Assert.Equal(string.Empty, LdapActiveDirectoryService.ConvertSidBytesToString(new byte[4]));
        }

        [Fact]
        [Requirement("AUTH-04", "AUTH", RequirementType.Positive, "ResolveUserSidsAsync returns empty list when username is empty.")]
        public async Task ResolveUserSidsAsync_ReturnsEmpty_WhenUsernameEmpty()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
            var service = new LdapActiveDirectoryService(config, NullLogger<LdapActiveDirectoryService>.Instance);

            var sids = await service.ResolveUserSidsAsync("");
            Assert.Empty(sids);
        }

        [Fact]
        [Requirement("AUTH-04", "AUTH", RequirementType.Positive, "ResolveUserSidsAsync returns empty list when LDAP server is unconfigured.")]
        public async Task ResolveUserSidsAsync_ReturnsEmpty_WhenServerNotConfigured()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
            var service = new LdapActiveDirectoryService(config, NullLogger<LdapActiveDirectoryService>.Instance);

            var sids = await service.ResolveUserSidsAsync("alice");
            Assert.Empty(sids);
        }

        [Fact]
        [Requirement("GUARD-02", "GUARD", RequirementType.Negative, "ResolveUserSidsAsync rejects plaintext unencrypted LDAP connections with InvalidOperationException.")]
        public async Task ResolveUserSidsAsync_ThrowsInvalidOperation_WhenPlaintextLdapConfigured()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Ldap:Server", "ldap.internal" },
                { "Ldap:Port", "389" },
                { "Ldap:UseSsl", "false" }
            }).Build();
            var service = new LdapActiveDirectoryService(config, NullLogger<LdapActiveDirectoryService>.Instance);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResolveUserSidsAsync("alice"));
        }

        [Fact]
        [Requirement("AUTH-04", "AUTH", RequirementType.Positive, "ActiveDirectoryIdentityProvider returns anonymous identity context when request originates from untrusted proxy.")]
        public async Task ActiveDirectoryIdentityProvider_ReturnsAnonymous_WhenUntrustedProxy()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Oidc:RequireTrustedProxy", "true" }
            }).Build();

            var provider = new ActiveDirectoryIdentityProvider(config);
            var httpContext = new DefaultHttpContext();

            var identity = await provider.ResolveIdentityAsync(httpContext);
            Assert.Equal("anonymous", identity.Username);
            Assert.Equal("ActiveDirectory", identity.AuthenticationType);
        }

        [Fact]
        [Requirement("AUTH-04", "AUTH", RequirementType.Positive, "ActiveDirectoryIdentityProvider returns anonymous when request lacks Windows authentication credentials.")]
        public async Task ActiveDirectoryIdentityProvider_ReturnsAnonymous_WhenNotWindowsAuth()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Oidc:RequireTrustedProxy", "false" }
            }).Build();

            var provider = new ActiveDirectoryIdentityProvider(config);
            var httpContext = new DefaultHttpContext();

            var identity = await provider.ResolveIdentityAsync(httpContext);
            Assert.Equal("anonymous", identity.Username);
        }

        [Fact]
        [Requirement("AUTH-04", "AUTH", RequirementType.Positive, "ActiveDirectoryIdentityProvider delegates group SID resolution to ILdapService.")]
        public async Task ActiveDirectoryIdentityProvider_ResolvesLdapSids_WhenLdapServiceProvided()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Oidc:RequireTrustedProxy", "false" }
            }).Build();

            var mockLdap = new Mock<ILdapService>();
            mockLdap.Setup(l => l.ResolveUserSidsAsync(It.IsAny<string>()))
                    .ReturnsAsync(new List<string> { "S-1-5-21-100" });

            var provider = new ActiveDirectoryIdentityProvider(config, mockLdap.Object);
            var httpContext = new DefaultHttpContext();

            var identity = await provider.ResolveIdentityAsync(httpContext);
            Assert.NotNull(identity);
        }

        [Fact]
        [Requirement("GUARD-LDAP-FAIL-CLOSED", "AUTH", RequirementType.Negative, "LdapActiveDirectoryService fails closed with SecurityException when LDAP connection throws an exception.")]
        public async Task ResolveUserSidsAsync_ThrowsSecurityException_OnConnectionFailure()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Ldap:Server", "ldap.internal" },
                { "Ldap:Port", "636" },
                { "Ldap:UseSsl", "true" }
            }).Build();

            var mockConnection = new Mock<ILdapConnection>();
            mockConnection.Setup(c => c.Bind()).Throws(new System.DirectoryServices.Protocols.LdapException("Mock LDAP Bind Failure"));

            var mockFactory = new Mock<ILdapConnectionFactory>();
            mockFactory.Setup(f => f.CreateConnection(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<System.Net.NetworkCredential>(), It.IsAny<System.DirectoryServices.Protocols.AuthType>()))
                       .Returns(mockConnection.Object);

            var service = new LdapActiveDirectoryService(config, NullLogger<LdapActiveDirectoryService>.Instance, null, null, mockFactory.Object);

            var ex = await Assert.ThrowsAsync<System.Security.SecurityException>(() => service.ResolveUserSidsAsync("alice"));
            Assert.Contains("Fail-closed policy active", ex.Message);
        }
    }
}
