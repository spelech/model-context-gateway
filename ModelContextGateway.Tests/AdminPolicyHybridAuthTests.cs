using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace ModelContextGateway.Tests
{
    public class AdminPolicyHybridAuthTests
    {
        [Fact]
        [Requirement("AUTH-ADMIN-POLICY-ALLOW-GROUPNAME", "AUTH", RequirementType.Positive, "AdminPolicy allows principal with configured Admin Group Name (e.g., full_admin)")]
        public async Task AdminPolicy_Allows_Principal_With_AdminGroupName()
        {
            var services = new ServiceCollection();
            var mockEnv = new Mock<IHostEnvironment>();
            mockEnv.Setup(e => e.EnvironmentName).Returns("Development");

            var configDict = new Dictionary<string, string?>
            {
                { "Admin:GroupName", "full_admin" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

            services.AddSingleton<IConfiguration>(config);
            services.AddMcpOpenIddict(mockEnv.Object, config);

            var httpContext = new DefaultHttpContext();
            var serviceProvider = services.BuildServiceProvider();
            httpContext.RequestServices = serviceProvider;

            var authService = serviceProvider.GetRequiredService<IAuthorizationService>();

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "admin_user"),
                new Claim(ClaimTypes.Role, "full_admin")
            }, "OidcHeader");
            var principal = new ClaimsPrincipal(identity);

            var result = await authService.AuthorizeAsync(principal, httpContext, "AdminPolicy");
            Assert.True(result.Succeeded);
        }

        [Fact]
        [Requirement("AUTH-ADMIN-POLICY-ALLOW-SID", "AUTH", RequirementType.Positive, "AdminPolicy allows principal with configured Admin SID")]
        public async Task AdminPolicy_Allows_Principal_With_AdminSid()
        {
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

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "ad_admin"),
                new Claim("Sid", "S-1-5-32-544")
            }, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            var result = await authService.AuthorizeAsync(principal, httpContext, "AdminPolicy");
            Assert.True(result.Succeeded);
        }

        [Fact]
        [Requirement("AUTH-ADMIN-POLICY-ALLOW-GROUPS-ARRAY", "AUTH", RequirementType.Positive, "AdminPolicy allows principal with configured Admin Groups array")]
        public async Task AdminPolicy_Allows_Principal_With_ConfiguredAdminGroups()
        {
            var services = new ServiceCollection();
            var mockEnv = new Mock<IHostEnvironment>();
            mockEnv.Setup(e => e.EnvironmentName).Returns("Development");

            var configDict = new Dictionary<string, string?>
            {
                { "Admin:Groups:0", "mcp_superusers" },
                { "Admin:Groups:1", "cluster_admins" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

            services.AddSingleton<IConfiguration>(config);
            services.AddMcpOpenIddict(mockEnv.Object, config);

            var httpContext = new DefaultHttpContext();
            var serviceProvider = services.BuildServiceProvider();
            httpContext.RequestServices = serviceProvider;

            var authService = serviceProvider.GetRequiredService<IAuthorizationService>();

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "cluster_op"),
                new Claim(ClaimTypes.Role, "cluster_admins")
            }, "OidcHeader");
            var principal = new ClaimsPrincipal(identity);

            var result = await authService.AuthorizeAsync(principal, httpContext, "AdminPolicy");
            Assert.True(result.Succeeded);
        }

        [Fact]
        [Requirement("AUTH-ADMIN-POLICY-REJECT-REGULAR", "AUTH", RequirementType.Negative, "AdminPolicy rejects principal with unconfigured regular role without Admin SID or Admin Group")]
        public async Task AdminPolicy_Denies_StandardRole_WithoutAdminSidOrGroup()
        {
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

            var httpContext = new DefaultHttpContext();
            var serviceProvider = services.BuildServiceProvider();
            httpContext.RequestServices = serviceProvider;

            var authService = serviceProvider.GetRequiredService<IAuthorizationService>();

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "regular_user"),
                new Claim(ClaimTypes.Role, "house_member")
            }, "OidcHeader");
            var principal = new ClaimsPrincipal(identity);

            var result = await authService.AuthorizeAsync(principal, httpContext, "AdminPolicy");
            Assert.False(result.Succeeded);
        }
    }
}
