using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Moq;

namespace ModelContextGateway.Tests
{
    public class ClientsControllerTests
    {
        private (SqliteConnection conn, IDbConnectionFactory factory, IOAuthClientRepository repo) CreateDbEnvironment()
        {
            var dbName = $"Data Source=ClientsControllerTests_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            var connection = new SqliteConnection(dbName);
            connection.Open();

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS OAuthClients (
                    ClientId TEXT PRIMARY KEY,
                    ClientSecretHash TEXT DEFAULT '',
                    ClientName TEXT NOT NULL,
                    ClientType TEXT DEFAULT 'confidential',
                    RedirectUrisJson TEXT DEFAULT '[]',
                    GrantTypesJson TEXT DEFAULT '[]',
                    ScopesJson TEXT DEFAULT '[]',
                    OwnerSid TEXT DEFAULT '',
                    CreatedBy TEXT DEFAULT '',
                    ExpiresAt TEXT NULL,
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );
                CREATE TABLE IF NOT EXISTS Servers (
                    Id TEXT PRIMARY KEY,
                    Categories TEXT,
                    Enabled INTEGER DEFAULT 1
                );
            ");

            var mockDbFactory = new Mock<IDbConnectionFactory>();
            mockDbFactory.Setup(f => f.CreateConnection()).Returns(() => new SqliteConnection(dbName));
            mockDbFactory.Setup(f => f.ProviderName).Returns("sqlite");

            var repo = new DatabaseRepository(mockDbFactory.Object);
            return (connection, mockDbFactory.Object, repo);
        }

        [Fact]
        [Requirement("AUTH-110", "AUTH", RequirementType.Positive, "GetClients returns list of OAuthClient records without secret hashes")]
        public async Task GetClients_ReturnsOk_WithClientsAndMappedProperties()
        {
            var (_, dbFactory, repo) = CreateDbEnvironment();
            await repo.SaveOAuthClientAsync(new OAuthClient
            {
                ClientId = "client-1",
                ClientName = "Client One",
                ClientSecretHash = "hash123",
                ScopesJson = "[\"mcp_client\"]",
                GrantTypesJson = "[\"authorization_code\"]",
                RedirectUrisJson = "[\"https://app.example.com/cb\"]"
            });

            var mockAudit = new Mock<IAuditLogger>();
            var controller = new ClientsController(repo, mockAudit.Object, dbFactory);

            var result = await controller.GetClients();
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var list = (okResult.Value as IEnumerable<object>)?.ToList();

            list.Should().NotBeNull();
            list.Should().HaveCount(1);

            var item = list![0];
            var json = JsonSerializer.Serialize(item);
            json.Should().NotContain("hash123");
            json.Should().NotContain("ClientSecretHash");
            json.Should().Contain("client-1");
            json.Should().Contain("Client One");
        }

        [Fact]
        [Requirement("AUTH-111", "AUTH", RequirementType.Positive, "CreateClient persists OAuthClient with SHA-256 secret hash and returns plaintext secret")]
        public async Task CreateClient_ReturnsOk_WithGeneratedCredentials()
        {
            var (_, dbFactory, repo) = CreateDbEnvironment();
            var mockAudit = new Mock<IAuditLogger>();
            var controller = new ClientsController(repo, mockAudit.Object, dbFactory);

            var model = new ClientsController.CreateClientModel
            {
                DisplayName = "Test CLI",
                Scopes = new List<string> { "custom_scope" }
            };

            var result = await controller.CreateClient(model);
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var value = okResult.Value;

            value.Should().NotBeNull();
            var displayNameProp = value!.GetType().GetProperty("DisplayName")?.GetValue(value, null) as string;
            var clientIdProp = value.GetType().GetProperty("ClientId")?.GetValue(value, null) as string;
            var clientSecretProp = value.GetType().GetProperty("ClientSecret")?.GetValue(value, null) as string;

            displayNameProp.Should().Be("Test CLI");
            clientIdProp.Should().NotBeNullOrEmpty();
            clientSecretProp.Should().NotBeNullOrEmpty();

            // Verify stored hash matches SHA-256 of plaintext secret
            var savedClient = await repo.GetOAuthClientByIdAsync(clientIdProp!);
            savedClient.Should().NotBeNull();
            savedClient!.ClientName.Should().Be("Test CLI");

            var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(clientSecretProp!))).ToLowerInvariant();
            savedClient.ClientSecretHash.Should().Be(expectedHash);
        }

        [Fact]
        [Requirement("AUTH-112", "AUTH", RequirementType.Positive, "DeleteClient removes OAuthClient via repository")]
        public async Task DeleteClient_ReturnsNoContent_WhenAppExists()
        {
            var (_, dbFactory, repo) = CreateDbEnvironment();
            await repo.SaveOAuthClientAsync(new OAuthClient
            {
                ClientId = "123",
                ClientName = "Client"
            });

            var mockAudit = new Mock<IAuditLogger>();
            var controller = new ClientsController(repo, mockAudit.Object, dbFactory);
            var result = await controller.DeleteClient("123");

            result.Should().BeOfType<NoContentResult>();

            var lookup = await repo.GetOAuthClientByIdAsync("123");
            lookup.Should().BeNull();
        }

        [Fact]
        [Requirement("AUTH-112", "AUTH", RequirementType.Negative, "DeleteClient returns NotFound when client does not exist")]
        public async Task DeleteClient_ReturnsNotFound_WhenAppDoesNotExist()
        {
            var (_, dbFactory, repo) = CreateDbEnvironment();
            var mockAudit = new Mock<IAuditLogger>();
            var controller = new ClientsController(repo, mockAudit.Object, dbFactory);

            var result = await controller.DeleteClient("nonexistent");
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        [Requirement("AUTH-111", "SEC", RequirementType.Positive, "Plaintext secret is never persisted in OAuthClients repository")]
        public async Task DatabaseAssertion_PlaintextNotPersisted()
        {
            var (conn, dbFactory, repo) = CreateDbEnvironment();
            var mockAudit = new Mock<IAuditLogger>();
            var controller = new ClientsController(repo, mockAudit.Object, dbFactory);

            var createModel = new ClientsController.CreateClientModel
            {
                DisplayName = "Secure App",
                Scopes = new List<string> { "custom_scope" }
            };
            var createResult = await controller.CreateClient(createModel);
            var okResult = createResult.Should().BeOfType<OkObjectResult>().Subject;
            var responseValue = okResult.Value;
            var clientSecret = responseValue!.GetType().GetProperty("ClientSecret")?.GetValue(responseValue, null) as string;

            // Assert that plaintext is not saved in SQLite
            var storedClient = conn.QueryFirstOrDefault<OAuthClient>("SELECT * FROM OAuthClients;");
            storedClient.Should().NotBeNull();
            storedClient!.ClientSecretHash.Should().NotBe(clientSecret);
            storedClient.ClientSecretHash.Should().HaveLength(64); // SHA-256 hex is 64 characters
            storedClient.ClientSecretHash.Should().NotContain("+").And.NotContain("/").And.NotContain("=");
        }

        [Fact]
        [Requirement("AUTH-111", "SEC", RequirementType.Positive, "Client credentials do not inherit administrative SID or privileges")]
        public async Task CreateClient_AdminCreator_DoesNotInheritAdminSid()
        {
            var (conn, dbFactory, repo) = CreateDbEnvironment();
            var mockAudit = new Mock<IAuditLogger>();
            var controller = new ClientsController(repo, mockAudit.Object, dbFactory);

            var adminSid = "S-1-5-32-544";
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "admin_user"),
                new Claim("Sid", adminSid)
            };
            var adminPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, "OidcHeader"));

            var httpContext = new DefaultHttpContext();
            httpContext.User = adminPrincipal;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            var createModel = new ClientsController.CreateClientModel
            {
                DisplayName = "Machine Client App",
                Scopes = new List<string> { "all" }
            };
            var createResult = await controller.CreateClient(createModel);
            var okResult = createResult.Should().BeOfType<OkObjectResult>().Subject;
            var responseValue = okResult.Value;
            var clientId = responseValue!.GetType().GetProperty("ClientId")?.GetValue(responseValue, null) as string;

            // Verify DB record has EMPTY OwnerSid, NOT admin's SID
            var storedClient = conn.QueryFirstOrDefault<OAuthClient>("SELECT * FROM OAuthClients WHERE ClientId = @ClientId;", new { ClientId = clientId });
            storedClient.Should().NotBeNull();
            storedClient!.OwnerSid.Should().BeEmpty();
        }

        [Fact]
        [Requirement("AUTH-111", "AUTH", RequirementType.Positive, "CreateClient sets expiration timestamp when ExpiresInDays is specified")]
        public async Task CreateClient_WithExpiresInDays_SetsExpiration()
        {
            var (_, dbFactory, repo) = CreateDbEnvironment();
            var mockAudit = new Mock<IAuditLogger>();
            var controller = new ClientsController(repo, mockAudit.Object, dbFactory);

            var createModel = new ClientsController.CreateClientModel
            {
                DisplayName = "Expiring App",
                Scopes = new List<string> { "all" },
                ExpiresInDays = 30
            };
            var createResult = await controller.CreateClient(createModel);
            var okResult = createResult.Should().BeOfType<OkObjectResult>().Subject;
            var responseValue = okResult.Value;
            var expiresAt = responseValue!.GetType().GetProperty("ExpiresAt")?.GetValue(responseValue, null) as DateTime?;

            expiresAt.Should().NotBeNull();
            expiresAt.Value.Should().BeAfter(DateTime.UtcNow.AddDays(29));
        }

        [Fact]
        [Requirement("AUTH-110", "SEC", RequirementType.Positive, "GetClients never leaks secret hashes or credentials")]
        public async Task GetClients_NeverLeaksRawBearerSecretOrHash()
        {
            var (_, dbFactory, repo) = CreateDbEnvironment();
            var mockAudit = new Mock<IAuditLogger>();
            var controller = new ClientsController(repo, mockAudit.Object, dbFactory);

            var createModel = new ClientsController.CreateClientModel
            {
                DisplayName = "Leak Prevention App",
                Scopes = new List<string> { "all" }
            };
            var createResult = await controller.CreateClient(createModel);
            var okResult = createResult.Should().BeOfType<OkObjectResult>().Subject;
            var rawSecret = okResult.Value!.GetType().GetProperty("ClientSecret")?.GetValue(okResult.Value, null) as string;

            var listResult = await controller.GetClients();
            var listOk = listResult.Should().BeOfType<OkObjectResult>().Subject;
            var list = (listOk.Value as IEnumerable<object>)?.ToList();

            list.Should().NotBeNull();
            list.Should().HaveCount(1);

            var item = list![0];
            var json = JsonSerializer.Serialize(item);

            json.Should().NotContain(rawSecret!);
            json.Should().NotContain("ClientSecretHash");
            json.Should().NotContain("ClientSecret");
        }

        [Fact]
        [Requirement("GUARD-CLIENT-MISSING-NAME", "GUARD", RequirementType.Negative, "CreateClient fails closed when DisplayName is missing")]
        public async Task CreateClient_ReturnsBadRequest_WhenDisplayNameMissing()
        {
            var (_, dbFactory, repo) = CreateDbEnvironment();
            var mockAudit = new Mock<IAuditLogger>();
            var controller = new ClientsController(repo, mockAudit.Object, dbFactory);

            var model = new ClientsController.CreateClientModel { DisplayName = "" };
            var result = await controller.CreateClient(model);
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        [Requirement("GUARD-CLIENT-EMPTY-SCOPE", "GUARD", RequirementType.Negative, "CreateClient fails closed when category scope is empty")]
        public async Task CreateClient_ReturnsBadRequest_WhenCategoryScopeEmpty()
        {
            var (_, dbFactory, repo) = CreateDbEnvironment();
            var mockAudit = new Mock<IAuditLogger>();
            var controller = new ClientsController(repo, mockAudit.Object, dbFactory);

            var model = new ClientsController.CreateClientModel
            {
                DisplayName = "Invalid Category App",
                Scopes = new List<string> { "category:" }
            };
            var result = await controller.CreateClient(model);
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        [Requirement("GUARD-CLIENT-CREATE-REPO-ERR", "GUARD", RequirementType.Negative, "CreateClient returns 500 when repository throws")]
        public async Task CreateClient_Returns500_WhenOAuthClientRepositoryThrows()
        {
            var (_, dbFactory, _) = CreateDbEnvironment();
            var mockAudit = new Mock<IAuditLogger>();
            var mockRepo = new Mock<IOAuthClientRepository>();
            mockRepo.Setup(c => c.SaveOAuthClientAsync(It.IsAny<OAuthClient>()))
                .ThrowsAsync(new Exception("Database disk full"));

            var controller = new ClientsController(mockRepo.Object, mockAudit.Object, dbFactory);
            var model = new ClientsController.CreateClientModel { DisplayName = "Faulty App" };
            var result = await controller.CreateClient(model);
            var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
        }

        [Fact]
        [Requirement("GUARD-CLIENT-DELETE-REPO-ERR", "GUARD", RequirementType.Negative, "DeleteClient returns 500 when repository throws")]
        public async Task DeleteClient_Returns500_WhenOAuthClientRepositoryThrows()
        {
            var (_, dbFactory, _) = CreateDbEnvironment();
            var mockAudit = new Mock<IAuditLogger>();
            var mockRepo = new Mock<IOAuthClientRepository>();
            mockRepo.Setup(c => c.DeleteOAuthClientAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Database locked"));

            var controller = new ClientsController(mockRepo.Object, mockAudit.Object, dbFactory);
            var result = await controller.DeleteClient("client-123");
            var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
        }

        [Fact]
        [Requirement("AUTH-119", "SEC", RequirementType.Positive, "CleanupClients endpoint triggers DCR pruning and returns total cleaned client count.")]
        public async Task CleanupClients_CallsRepoAndReturnsCleanedCount()
        {
            var (_, dbFactory, _) = CreateDbEnvironment();
            var mockAudit = new Mock<IAuditLogger>();
            var mockRepo = new Mock<IOAuthClientRepository>();
            mockRepo.Setup(c => c.CleanupDcrClientsAsync(30))
                .ReturnsAsync(7);

            var controller = new ClientsController(mockRepo.Object, mockAudit.Object, dbFactory);
            var result = await controller.CleanupClients(30);

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var doc = JsonDocument.Parse(JsonSerializer.Serialize(okResult.Value));
            doc.RootElement.GetProperty("cleanedCount").GetInt32().Should().Be(7);
            mockRepo.Verify(c => c.CleanupDcrClientsAsync(30), Times.Once);
        }

        [Fact]
        [Requirement("GUARD-CLIENT-CLEANUP-REPO-ERR", "GUARD", RequirementType.Negative, "CleanupClients returns 500 when repository throws.")]
        public async Task CleanupClients_Returns500_WhenOAuthClientRepositoryThrows()
        {
            var (_, dbFactory, _) = CreateDbEnvironment();
            var mockAudit = new Mock<IAuditLogger>();
            var mockRepo = new Mock<IOAuthClientRepository>();
            mockRepo.Setup(c => c.CleanupDcrClientsAsync(It.IsAny<int>()))
                .ThrowsAsync(new Exception("Database locked"));

            var controller = new ClientsController(mockRepo.Object, mockAudit.Object, dbFactory);
            var result = await controller.CleanupClients(30);
            var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
        }
    }
}


