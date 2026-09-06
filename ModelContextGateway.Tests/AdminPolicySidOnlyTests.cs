using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace ModelContextGateway.Tests
{
    public class AdminPolicySidOnlyTests
    {
        /// <summary>
        /// Verifies that users with standard/unconfigured role names lacking explicit Admin SID claim or Admin Group are denied by AdminPolicy.
        /// </summary>
        [Fact]
        [Requirement("AUTH-ADMIN-POLICY-REJECT-REGULAR", "AUTH", RequirementType.Negative, "AdminPolicy rejects principal with unconfigured regular role without Admin SID or Admin Group")]
        public async Task AdminPolicy_Denies_StandardRole_Without_AdminSid()
        {
            // Arrange
            var services = new ServiceCollection();
            var mockEnv = new Mock<IHostEnvironment>();
            mockEnv.Setup(e => e.EnvironmentName).Returns("Development");

            var configDict = new Dictionary<string, string?>
            {
                { "Admin:GroupSid", "S-1-5-32-544" },
                { "Admin:GroupName", "full_admin" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

            services.AddSingleton<IConfiguration>(config);
            services.AddMcpOpenIddict(mockEnv.Object, config);

            // Build HttpContext with request services to simulate resource
            var httpContext = new DefaultHttpContext();
            var serviceProvider = services.BuildServiceProvider();
            httpContext.RequestServices = serviceProvider;

            var authService = serviceProvider.GetRequiredService<IAuthorizationService>();

            // Principal with unconfigured role name
            var identityWithStandardRole = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Role, "house_member"),
                new Claim(ClaimTypes.Name, "testuser")
            }, "TestAuth");
            var principalWithStandardRole = new ClaimsPrincipal(identityWithStandardRole);

            // Act & Assert
            var resultStandardRole = await authService.AuthorizeAsync(principalWithStandardRole, httpContext, "AdminPolicy");
            Assert.False(resultStandardRole.Succeeded);
        }

        /// <summary>
        /// Verifies that principals with the configured Admin SID are granted administrative policy access.
        /// </summary>
        [Fact]
        [Requirement("AUTH-ADMIN-POLICY-ALLOW-SID", "AUTH", RequirementType.Positive, "AdminPolicy allows principal with configured Admin SID")]
        public async Task AdminPolicy_Allows_Principal_With_AdminSid()
        {
            // Arrange
            var services = new ServiceCollection();
            var mockEnv = new Mock<IHostEnvironment>();
            mockEnv.Setup(e => e.EnvironmentName).Returns("Development");

            var configDict = new Dictionary<string, string?>
            {
                { "Admin:GroupSid", "S-1-5-32-544" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

            services.AddSingleton<IConfiguration>(config);
            services.AddMcpOpenIddict(mockEnv.Object, config);

            var httpContext = new DefaultHttpContext();
            var serviceProvider = services.BuildServiceProvider();
            httpContext.RequestServices = serviceProvider;

            var authService = serviceProvider.GetRequiredService<IAuthorizationService>();

            // Principal with Admin SID claim
            var identityWithSid = new ClaimsIdentity(new[]
            {
                new Claim("Sid", "S-1-5-32-544"),
                new Claim(ClaimTypes.Name, "testuser")
            }, "TestAuth");
            var principalWithSid = new ClaimsPrincipal(identityWithSid);

            // Act
            var result = await authService.AuthorizeAsync(principalWithSid, httpContext, "AdminPolicy");

            // Assert
            Assert.True(result.Succeeded);
        }
    }
}
