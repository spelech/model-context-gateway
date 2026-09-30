using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextGateway.Infrastructure.Identity;
using ModelContextGateway.Infrastructure.Logging;
using ModelContextGateway.Infrastructure.Persistence;
using ModelContextGateway.Tests.Attributes;
using Moq;

namespace ModelContextGateway.Tests
{
    public class AppKeyRotationControllerTests
    {
        [Fact]
        [Requirement("AUTH-122", "AUTH", RequirementType.Positive, "RotateAppKey issues new plaintext key, updates database, resets LastUsedAt, and audits action.")]
        public async Task RotateAppKey_Success_ReturnsNewPlaintextKey()
        {
            var appKeyRepoMock = new Mock<IAppKeyRepository>();
            var settingRepoMock = new Mock<ISettingRepository>();
            var auditLoggerMock = new Mock<IAuditLogger>();
            var credServiceMock = new Mock<ICredentialService>();
            var config = new ConfigurationBuilder().Build();

            var existingKey = new AppKey
            {
                Id = "key-1",
                Name = "Personal CLI",
                Username = "alice",
                KeyPrefix = "mcp-usr-oldprefix",
                EncryptedKey = "oldhash",
                KeyType = "personal",
                LastUsedAt = DateTime.UtcNow
            };

            var rotatedKey = new AppKey
            {
                Id = "key-1",
                Name = "Personal CLI",
                Username = "alice",
                KeyPrefix = "mcp-usr-newprefix",
                EncryptedKey = "newhash",
                KeyType = "personal",
                LastUsedAt = null
            };

            appKeyRepoMock.Setup(r => r.GetAppKeyByIdAsync("key-1")).ReturnsAsync(existingKey);
            credServiceMock.Setup(c => c.RotateCredentialAsync("key-1"))
                .ReturnsAsync((rotatedKey, "mcp-usr-newprefix-newsecret123"));

            var controller = new AppKeysController(
                appKeyRepoMock.Object,
                settingRepoMock.Object,
                config,
                auditLoggerMock.Object,
                credServiceMock.Object);

            var services = new ServiceCollection();
            services.AddSingleton(new CompositeIdentityProvider(Array.Empty<IIdentityProvider>()));
            var serviceProvider = services.BuildServiceProvider();

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = serviceProvider,
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Name, "alice")
                    }, "TestAuth"))
                }
            };

            var result = await controller.RotateAppKey("key-1");
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic data = okResult.Value!;
            Assert.Equal("mcp-usr-newprefix-newsecret123", (string)data.PlaintextKey);
            Assert.Equal("mcp-usr-newprefix", (string)data.KeyPrefix);

            auditLoggerMock.Verify(a => a.LogAdminActionAsync(
                "alice", "appkey.rotate", "key-1", It.IsAny<string>(), true), Times.Once);
        }

        [Fact]
        [Requirement("AUTH-123", "GUARD", RequirementType.FailClosedGuardrail, "RotateAppKey forbids non-owner non-admin user from rotating another user's key.")]
        public async Task RotateAppKey_ForbiddenForDifferentUser()
        {
            var appKeyRepoMock = new Mock<IAppKeyRepository>();
            var credServiceMock = new Mock<ICredentialService>();
            var config = new ConfigurationBuilder().Build();

            var existingKey = new AppKey
            {
                Id = "key-2",
                Username = "bob",
                KeyType = "personal"
            };

            appKeyRepoMock.Setup(r => r.GetAppKeyByIdAsync("key-2")).ReturnsAsync(existingKey);

            var controller = new AppKeysController(
                appKeyRepoMock.Object,
                Mock.Of<ISettingRepository>(),
                config,
                Mock.Of<IAuditLogger>(),
                credServiceMock.Object);

            var services = new ServiceCollection();
            services.AddSingleton(new CompositeIdentityProvider(Array.Empty<IIdentityProvider>()));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = services.BuildServiceProvider(),
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Name, "charlie")
                    }, "TestAuth"))
                }
            };

            var result = await controller.RotateAppKey("key-2");
            Assert.IsType<ForbidResult>(result);
        }
    }
}
