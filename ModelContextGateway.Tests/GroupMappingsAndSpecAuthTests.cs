using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ModelContextGateway.Tests
{
    public class GroupMappingsAndSpecAuthTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly IDbConnectionFactory _dbFactory;

        public GroupMappingsAndSpecAuthTests()
        {
            var dbName = $"Data Source=GroupMappingDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            _connection = new SqliteConnection(dbName);
            _connection.Open();

            // Create AccessPolicies and GroupMappings tables
            _connection.Execute(@"
                CREATE TABLE IF NOT EXISTS AccessPolicies (
                    Id TEXT PRIMARY KEY,
                    TargetId TEXT,
                    RequiredGroup TEXT,
                    IsAllowed INTEGER DEFAULT 1
                );
                CREATE TABLE IF NOT EXISTS GroupMappings (
                    Id TEXT PRIMARY KEY,
                    ExternalId TEXT,
                    InternalGroup TEXT
                );");

            var mockFactory = new Mock<IDbConnectionFactory>();
            mockFactory.Setup(f => f.CreateConnection()).Returns(() => new SqliteConnection(dbName));
            mockFactory.Setup(f => f.ProviderName).Returns("sqlite");
            _dbFactory = mockFactory.Object;
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private ClientSession CreateSession(string username, List<string> groups, string sid = "")
        {
            var context = new DefaultHttpContext();
            var response = context.Response;

            var services = new ServiceCollection();
            services.AddSingleton(_dbFactory);

            var mockProvider = new Mock<IIdentityProvider>();
            mockProvider.Setup(p => p.ResolveIdentityAsync(It.IsAny<HttpContext>()))
                .ReturnsAsync(new UserIdentityContext(username, "Test", groups, sid));

            var composite = new CompositeIdentityProvider(new[] { mockProvider.Object });
            services.AddSingleton(composite);

            var realConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                { "Audit:FailClosed", "false" }
            }).Build();
            services.AddSingleton<IConfiguration>(realConfig);

            context.RequestServices = services.BuildServiceProvider();

            var httpClient = new HttpClient();
            var loggerMock = new Mock<ILogger>();
            var embeddingMock = new Mock<IEmbeddingService>();

            var servers = new List<McpServer>();
            return new ClientSession("test-session-mapping", response, servers, httpClient, embeddingMock.Object, loggerMock.Object);
        }

        private void SeedPolicy(string id, string targetId, string requiredGroup, bool isAllowed)
        {
            _connection.Execute(@"
                INSERT INTO AccessPolicies (Id, TargetId, RequiredGroup, IsAllowed)
                VALUES (@Id, @TargetId, @RequiredGroup, @IsAllowed);",
                new { Id = id, TargetId = targetId, RequiredGroup = requiredGroup, IsAllowed = isAllowed ? 1 : 0 });
        }

        private void SeedMapping(string id, string externalId, string internalGroup)
        {
            _connection.Execute(@"
                INSERT INTO GroupMappings (Id, ExternalId, InternalGroup)
                VALUES (@Id, @ExternalId, @InternalGroup);",
                new { Id = id, ExternalId = externalId, InternalGroup = internalGroup });
        }

        [Fact]
        [Requirement("AUTH-03", "AUTH", RequirementType.Positive, "GroupMappings resolves external Windows SIDs to internal security groups to satisfy access policies.")]
        public async Task GroupMapping_AllowsUser_WhenMappingResolvesToAllowedInternalGroup()
        {
            // Seed a policy requiring the internal group "database_users"
            SeedPolicy("pol-1", "tool:db__query", "database_users", true);

            // Seed a group mapping: external SID S-1-5-123 maps to internal database_users
            SeedMapping("map-1", "S-1-5-123", "database_users");

            // User has raw external group "Users" and SID "S-1-5-123"
            var session = CreateSession("alice", new List<string> { "Users" }, "S-1-5-123");

            // Evaluate authorization
            var authorized = await session.IsUserAuthorizedAsync("tools/call", "db__query");
            Assert.True(authorized);
        }

        [Fact]
        [Requirement("AUTH-03", "AUTH", RequirementType.Positive, "GroupMappings resolves external OIDC claim groups to internal security groups.")]
        public async Task GroupMapping_AllowsUser_WhenOidcGroupMapsToAllowedInternalGroup()
        {
            SeedPolicy("pol-2", "tool:ha__write", "smarthome_writers", true);
            SeedMapping("map-2", "oidc_admins", "smarthome_writers");

            // User has raw OIDC group oidc_admins
            var session = CreateSession("charlie", new List<string> { "oidc_admins" });

            var authorized = await session.IsUserAuthorizedAsync("tools/call", "ha__write");
            Assert.True(authorized);
        }

        [Fact]
        [Requirement("GUARD-MAPPING-UNMAPPED-DENY", "GUARD", RequirementType.Negative, "Fails closed and denies access when no valid group mapping exists for a restricted target.")]
        public async Task GroupMapping_RejectsUser_WhenNoMappingExistsForRestrictedTarget()
        {
            SeedPolicy("pol-3", "tool:ha__write", "smarthome_writers", true);

            // Charlie has raw OIDC group oidc_guests which is NOT mapped
            var session = CreateSession("charlie", new List<string> { "oidc_guests" });

            var authorized = await session.IsUserAuthorizedAsync("tools/call", "ha__write");
            Assert.False(authorized);
        }
    }
}
