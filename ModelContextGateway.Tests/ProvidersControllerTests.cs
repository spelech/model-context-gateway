using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Moq;

namespace ModelContextGateway.Tests
{
    public class ProvidersControllerTests : IDisposable
    {
        private const string ConnectionString = "Data Source=InMemoryProvidersDb;Mode=Memory;Cache=Shared";
        private readonly SqliteConnection _masterConnection;
        private readonly IDbConnectionFactory _dbFactory;
        private readonly DatabaseRepository _dbRepo;

        public ProvidersControllerTests()
        {
            _masterConnection = new SqliteConnection(ConnectionString);
            _masterConnection.Open();

            _masterConnection.Execute(@"
                CREATE TABLE IF NOT EXISTS SecretProviders (
                    ProviderName TEXT PRIMARY KEY,
                    DisplayName TEXT,
                    EncryptedConfigJson TEXT,
                    IsEnabled INTEGER
                );
                CREATE TABLE IF NOT EXISTS AuthProviderConfigs (
                    ProviderName TEXT PRIMARY KEY,
                    DisplayName TEXT,
                    UserHeader TEXT,
                    GroupsHeader TEXT,
                    EncryptedConfigJson TEXT,
                    IsEnabled INTEGER
                );");

            var mockFactory = new Mock<IDbConnectionFactory>();
            mockFactory.Setup(f => f.CreateConnection()).Returns(() =>
            {
                var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                return conn;
            });
            mockFactory.Setup(f => f.ProviderName).Returns("sqlite");
            _dbFactory = mockFactory.Object;
            _dbRepo = new DatabaseRepository(_dbFactory);
        }

        public void Dispose()
        {
            _masterConnection.Dispose();
        }

        [Fact]
        [Requirement("AUTH-03", "AUTH", RequirementType.Positive, "ProvidersController returns combined list of Secret and Auth providers.")]
        public async Task GetAllProviders_ReturnsOkWithSecretAndAuthProviders()
        {
            var controller = new ProvidersController(_dbRepo, _dbRepo);
            var result = await controller.GetAllProviders() as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        [Requirement("GUARD-04", "GUARD", RequirementType.Negative, "ProvidersController returns 500 when retrieving all providers encounters database failure.")]
        public async Task GetAllProviders_Returns500_OnDbException()
        {
            var mockFailingFactory = new Mock<IDbConnectionFactory>();
            mockFailingFactory.Setup(f => f.CreateConnection()).Throws(new Exception("DB Connection Failed"));

            var failingRepo = new DatabaseRepository(mockFailingFactory.Object);
            var controller = new ProvidersController(failingRepo, failingRepo);
            var result = await controller.GetAllProviders() as ObjectResult;

            Assert.NotNull(result);
            Assert.Equal(500, result.StatusCode);
        }

        [Fact]
        [Requirement("SEC-02", "SEC", RequirementType.Positive, "ProvidersController returns secret providers list.")]
        public async Task GetSecretProviders_ReturnsOkWithList()
        {
            var controller = new ProvidersController(_dbRepo, _dbRepo);
            var result = await controller.GetSecretProviders() as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        [Requirement("GUARD-PROVIDER-MISSING-SECRET-NAME", "GUARD", RequirementType.Negative, "ProvidersController rejects saving secret provider without providerName with BadRequest.")]
        public async Task SaveSecretProvider_ReturnsBadRequest_WhenProviderNameMissing()
        {
            var mockAudit = new Mock<IAuditLogger>();
            var controller = new ProvidersController(_dbRepo, _dbRepo);
            var dto = new SecretProviderDto { ProviderName = "" };

            var result = await controller.SaveSecretProvider(dto, mockAudit.Object) as BadRequestObjectResult;
            Assert.NotNull(result);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        [Requirement("GUARD-05", "GUARD", RequirementType.Negative, "ProvidersController rejects external HTTP URLs for secret providers with BadRequest.")]
        public async Task SaveSecretProvider_ReturnsBadRequest_WhenHttpUrlPassedInConfig()
        {
            var mockAudit = new Mock<IAuditLogger>();
            var controller = new ProvidersController(_dbRepo, _dbRepo);
            var dto = new SecretProviderDto
            {
                ProviderName = "Vault",
                DisplayName = "HashiCorp Vault",
                ConfigJson = "{\"url\":\"http://insecure-vault.local:8200\"}"
            };

            var result = await controller.SaveSecretProvider(dto, mockAudit.Object) as BadRequestObjectResult;
            Assert.NotNull(result);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        [Requirement("SEC-02", "SEC", RequirementType.Positive, "ProvidersController saves secret provider successfully.")]
        public async Task SaveSecretProvider_SavesSuccessfully()
        {
            var mockAudit = new Mock<IAuditLogger>();
            var controller = new ProvidersController(_dbRepo, _dbRepo);
            var dto = new SecretProviderDto
            {
                ProviderName = "Vault",
                DisplayName = "HashiCorp Vault",
                ConfigJson = "{\"url\":\"https://vault.local:8200\"}",
                IsEnabled = true
            };

            var result = await controller.SaveSecretProvider(dto, mockAudit.Object) as OkObjectResult;
            Assert.NotNull(result);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        [Requirement("GUARD-04", "GUARD", RequirementType.Negative, "ProvidersController returns 500 when saving secret provider encounters repository exception.")]
        public async Task SaveSecretProvider_Returns500_WhenRepositoryThrows()
        {
            var mockFailingFactory = new Mock<IDbConnectionFactory>();
            mockFailingFactory.Setup(f => f.CreateConnection()).Throws(new Exception("DB write crash"));

            var mockAudit = new Mock<IAuditLogger>();
            var failingRepo = new DatabaseRepository(mockFailingFactory.Object);
            var controller = new ProvidersController(failingRepo, failingRepo);
            var dto = new SecretProviderDto
            {
                ProviderName = "Vault",
                ConfigJson = "{\"url\":\"https://vault.local:8200\"}"
            };

            var result = await controller.SaveSecretProvider(dto, mockAudit.Object) as ObjectResult;
            Assert.NotNull(result);
            Assert.Equal(500, result.StatusCode);
        }

        [Fact]
        [Requirement("AUTH-03", "AUTH", RequirementType.Positive, "ProvidersController returns auth providers list.")]
        public async Task GetAuthProviders_ReturnsOkWithList()
        {
            var controller = new ProvidersController(_dbRepo, _dbRepo);
            var result = await controller.GetAuthProviders() as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        [Requirement("GUARD-PROVIDER-MISSING-AUTH-NAME", "GUARD", RequirementType.Negative, "ProvidersController rejects saving auth provider without providerName with BadRequest.")]
        public async Task SaveAuthProvider_ReturnsBadRequest_WhenProviderNameMissing()
        {
            var mockAudit = new Mock<IAuditLogger>();
            var controller = new ProvidersController(_dbRepo, _dbRepo);
            var dto = new AuthProviderDto { ProviderName = "" };

            var result = await controller.SaveAuthProvider(dto, mockAudit.Object) as BadRequestObjectResult;
            Assert.NotNull(result);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        [Requirement("AUTH-03", "AUTH", RequirementType.Positive, "ProvidersController saves auth provider configuration successfully.")]
        public async Task SaveAuthProvider_SavesSuccessfully()
        {
            var mockAudit = new Mock<IAuditLogger>();
            var controller = new ProvidersController(_dbRepo, _dbRepo);
            var dto = new AuthProviderDto
            {
                ProviderName = "PocketID",
                DisplayName = "PocketID OIDC",
                UserHeader = "Remote-User",
                GroupsHeader = "Remote-Groups",
                ConfigJson = "{\"authority\":\"https://sso.local\"}",
                IsEnabled = true
            };

            var result = await controller.SaveAuthProvider(dto, mockAudit.Object) as OkObjectResult;
            Assert.NotNull(result);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        [Requirement("GUARD-04", "GUARD", RequirementType.Negative, "ProvidersController returns 500 when saving auth provider encounters repository exception.")]
        public async Task SaveAuthProvider_Returns500_WhenRepositoryThrows()
        {
            var mockFailingFactory = new Mock<IDbConnectionFactory>();
            mockFailingFactory.Setup(f => f.CreateConnection()).Throws(new Exception("DB write crash"));

            var mockAudit = new Mock<IAuditLogger>();
            var failingRepo = new DatabaseRepository(mockFailingFactory.Object);
            var controller = new ProvidersController(failingRepo, failingRepo);
            var dto = new AuthProviderDto
            {
                ProviderName = "PocketID",
                ConfigJson = "{\"authority\":\"https://sso.local\"}"
            };

            var result = await controller.SaveAuthProvider(dto, mockAudit.Object) as ObjectResult;
            Assert.NotNull(result);
            Assert.Equal(500, result.StatusCode);
        }

        [Fact]
        [Requirement("GUARD-04", "GUARD", RequirementType.Negative, "ProvidersController returns 500 when secret provider retrieval throws database exception.")]
        public async Task GetSecretProviders_Returns500_OnDbException()
        {
            var mockFailingFactory = new Mock<IDbConnectionFactory>();
            mockFailingFactory.Setup(f => f.CreateConnection()).Throws(new Exception("DB Connection Failed"));

            var failingRepo = new DatabaseRepository(mockFailingFactory.Object);
            var controller = new ProvidersController(failingRepo, failingRepo);
            var result = await controller.GetSecretProviders() as ObjectResult;

            Assert.NotNull(result);
            Assert.Equal(500, result.StatusCode);
        }

        [Fact]
        [Requirement("GUARD-04", "GUARD", RequirementType.Negative, "ProvidersController returns 500 when auth provider retrieval throws database exception.")]
        public async Task GetAuthProviders_Returns500_OnDbException()
        {
            var mockFailingFactory = new Mock<IDbConnectionFactory>();
            mockFailingFactory.Setup(f => f.CreateConnection()).Throws(new Exception("DB Connection Failed"));

            var failingRepo = new DatabaseRepository(mockFailingFactory.Object);
            var controller = new ProvidersController(failingRepo, failingRepo);
            var result = await controller.GetAuthProviders() as ObjectResult;

            Assert.NotNull(result);
            Assert.Equal(500, result.StatusCode);
        }

        [Fact]
        [Requirement("SEC-02", "SEC", RequirementType.Positive, "ProvidersController validates Vault test connections and handles connection errors gracefully.")]
        public async Task TestVaultConnection_ValidatesInputAndHandlesFailureGracefully()
        {
            var controller = new ProvidersController(_dbRepo, _dbRepo);

            // 1. Missing address
            var badReq1 = await controller.TestVaultConnection(new TestVaultRequest { Address = "" }) as BadRequestObjectResult;
            Assert.NotNull(badReq1);
            Assert.Equal(400, badReq1.StatusCode);

            // 2. AppRole missing secretId
            var badReq2 = await controller.TestVaultConnection(new TestVaultRequest
            {
                Address = "https://vault.local:8200",
                AuthMethod = "approle",
                RoleId = "some-role"
            }) as BadRequestObjectResult;
            Assert.NotNull(badReq2);
            Assert.Equal(400, badReq2.StatusCode);

            // 3. Token auth missing token
            var badReq3 = await controller.TestVaultConnection(new TestVaultRequest
            {
                Address = "https://vault.local:8200",
                AuthMethod = "token",
                Token = ""
            }) as BadRequestObjectResult;
            Assert.NotNull(badReq3);
            Assert.Equal(400, badReq3.StatusCode);

            // 4. Invalid/Unreachable vault address returns Ok(success = false)
            var failResult = await controller.TestVaultConnection(new TestVaultRequest
            {
                Address = "https://127.0.0.1:19999",
                Token = "test-token"
            }) as OkObjectResult;
            Assert.NotNull(failResult);
            Assert.Equal(200, failResult.StatusCode);
        }

        [Fact]
        [Requirement("AUTH-04", "AUTH", RequirementType.Positive, "ProvidersController validates LDAP test connections and handles connection errors gracefully.")]
        public async Task TestLdapConnection_ValidatesInputAndHandlesFailureGracefully()
        {
            var controller = new ProvidersController(_dbRepo, _dbRepo);

            // 1. Missing Server
            var badReq1 = await controller.TestLdapConnection(new TestLdapRequest { Server = "" }) as BadRequestObjectResult;
            Assert.NotNull(badReq1);
            Assert.Equal(400, badReq1.StatusCode);

            // 2. Plaintext port 389 with useSsl = false rejected
            var badReq2 = await controller.TestLdapConnection(new TestLdapRequest
            {
                Server = "ad.local",
                Port = 389,
                UseSsl = false
            }) as BadRequestObjectResult;
            Assert.NotNull(badReq2);
            Assert.Equal(400, badReq2.StatusCode);

            // 3. Unreachable host on port 636 returns Ok(success = false)
            var failResult = await controller.TestLdapConnection(new TestLdapRequest
            {
                Server = "127.0.0.1",
                Port = 19998,
                UseSsl = true
            }) as OkObjectResult;
            Assert.NotNull(failResult);
            Assert.Equal(200, failResult.StatusCode);
        }

        [Fact]
        [Requirement("GUARD-05", "GUARD", RequirementType.Negative, "Batch save of authentication providers must fail closed if all providers are disabled")]
        public async Task SaveAuthProvidersBatch_ReturnsBadRequest_WhenAllProvidersDisabled()
        {
            var mockAudit = new Mock<IAuditLogger>();
            var controller = new ProvidersController(_dbRepo, _dbRepo);

            var dtos = new System.Collections.Generic.List<AuthProviderDto>
            {
                new AuthProviderDto { ProviderName = "ad", IsEnabled = false },
                new AuthProviderDto { ProviderName = "oidc", IsEnabled = false }
            };

            var result = await controller.SaveAuthProvidersBatch(dtos, mockAudit.Object) as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        [Requirement("SEC-02", "SEC", RequirementType.Positive, "ProvidersController allows HTTP URLs for localhost and 127.0.0.1 in development setups.")]
        public async Task SaveSecretProvider_HttpUrl_AllowedForLocalhost()
        {
            var mockAudit = new Mock<IAuditLogger>();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(x => x.GetService(typeof(System.Collections.Generic.IEnumerable<ModelContextGateway.Infrastructure.Secrets.ISecretRetriever>)))
                .Returns(new System.Collections.Generic.List<ModelContextGateway.Infrastructure.Secrets.ISecretRetriever>());
            var controller = new ProvidersController(_dbRepo, _dbRepo);
            var dto = new SecretProviderDto
            {
                ProviderName = "Vault",
                IsEnabled = true,
                ConfigJson = "{\"url\":\"http://localhost:8200\"}"
            };

            var result = await controller.SaveSecretProvider(dto, mockAudit.Object, mockServiceProvider.Object);
            if (result is ObjectResult br && br.StatusCode != 200)
            {
                throw new Exception("ObjectResult: " + System.Text.Json.JsonSerializer.Serialize(br.Value));
            }
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        [Requirement("SEC-02", "SEC", RequirementType.Positive, "ProvidersController allows HTTP URLs for intranet simple hostnames.")]
        public async Task SaveSecretProvider_HttpUrl_AllowedForSimpleHost()
        {
            var mockAudit = new Mock<IAuditLogger>();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(x => x.GetService(typeof(System.Collections.Generic.IEnumerable<ModelContextGateway.Infrastructure.Secrets.ISecretRetriever>)))
                .Returns(new System.Collections.Generic.List<ModelContextGateway.Infrastructure.Secrets.ISecretRetriever>());
            var controller = new ProvidersController(_dbRepo, _dbRepo);
            var dto = new SecretProviderDto
            {
                ProviderName = "Vault",
                IsEnabled = true,
                ConfigJson = "{\"url\":\"http://vault:8200\"}"
            };

            var result = await controller.SaveSecretProvider(dto, mockAudit.Object, mockServiceProvider.Object);
            if (result is ObjectResult br && br.StatusCode != 200)
            {
                throw new Exception("ObjectResult: " + System.Text.Json.JsonSerializer.Serialize(br.Value));
            }
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        [Requirement("GUARD-05", "GUARD", RequirementType.Negative, "ProvidersController rejects HTTP URLs for external FQDN hostnames under security policy.")]
        public async Task SaveSecretProvider_HttpUrl_RejectedForExternal()
        {
            var mockAudit = new Mock<IAuditLogger>();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(x => x.GetService(typeof(System.Collections.Generic.IEnumerable<ModelContextGateway.Infrastructure.Secrets.ISecretRetriever>)))
                .Returns(new System.Collections.Generic.List<ModelContextGateway.Infrastructure.Secrets.ISecretRetriever>());
            var controller = new ProvidersController(_dbRepo, _dbRepo);
            var dto = new SecretProviderDto
            {
                ProviderName = "Vault",
                IsEnabled = true,
                ConfigJson = "{\"url\":\"http://vault.external.com:8200\"}"
            };

            var result = await controller.SaveSecretProvider(dto, mockAudit.Object, mockServiceProvider.Object);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("must use the HTTPS scheme", badRequest.Value!.ToString());
        }
    }
}
