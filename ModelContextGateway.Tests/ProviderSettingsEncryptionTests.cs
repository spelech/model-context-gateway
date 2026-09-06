using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ModelContextGateway.Tests
{
    public class ProviderSettingsEncryptionTests : IDisposable
    {
        private const string ConnectionString = "Data Source=InMemoryEncryptionDb;Mode=Memory;Cache=Shared";
        private readonly SqliteConnection _masterConnection;
        private readonly IDbConnectionFactory _dbFactory;
        private readonly IConfiguration _config;
        private readonly DatabaseRepository _repo;

        public ProviderSettingsEncryptionTests()
        {
            _masterConnection = new SqliteConnection(ConnectionString);
            _masterConnection.Open();

            _masterConnection.Execute(@"
                CREATE TABLE IF NOT EXISTS SecretProviders (
                    ProviderName TEXT PRIMARY KEY,
                    DisplayName TEXT,
                    EncryptedConfigJson TEXT,
                    IsEnabled INTEGER DEFAULT 1
                );
                CREATE TABLE IF NOT EXISTS AuthProviderConfigs (
                    ProviderName TEXT PRIMARY KEY,
                    DisplayName TEXT,
                    UserHeader TEXT,
                    GroupsHeader TEXT,
                    EncryptedConfigJson TEXT,
                    IsEnabled INTEGER DEFAULT 1
                );");

            _config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DB_ENCRYPTION_KEY", "TestSecretKey1234567890123456789012" },
                { "MCG_MASTER_KEY", "TestSecretKey1234567890123456789012" }
            }).Build();

            DbKeyHelper.ResetCache();

            var mockFactory = new Mock<IDbConnectionFactory>();
            mockFactory.Setup(f => f.CreateConnection()).Returns(() =>
            {
                var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                return conn;
            });
            mockFactory.Setup(f => f.ProviderName).Returns("sqlite");
            _dbFactory = mockFactory.Object;

            _repo = new DatabaseRepository(_dbFactory, _config);
        }

        public void Dispose()
        {
            _masterConnection.Dispose();
            DbKeyHelper.ResetCache();
        }

        [Fact]
        [Requirement("SEC-01", "SEC", RequirementType.Positive, "Encrypts SecretProvider configuration JSON at rest in database using Master Key.")]
        public async Task SaveSecretProvider_EncryptsConfigJson_AtRestInDatabase()
        {
            var plainConfig = "{\"address\":\"https://vault.corp.local:8200\",\"token\":\"s.SuperSecretVaultToken12345\"}";
            var dto = new SecretProviderDto
            {
                ProviderName = "Vault",
                DisplayName = "HashiCorp Vault",
                ConfigJson = plainConfig,
                IsEnabled = true
            };

            await _repo.SaveSecretProviderAsync(dto);

            // Directly inspect raw database row
            using var conn = _dbFactory.CreateConnection();
            var rawEncrypted = await conn.ExecuteScalarAsync<string>("SELECT EncryptedConfigJson FROM SecretProviders WHERE ProviderName = 'Vault';");

            rawEncrypted.Should().NotBeNullOrEmpty();
            rawEncrypted.Should().NotContain("SuperSecretVaultToken12345");
            rawEncrypted.Should().NotContain("vault.corp.local");

            // Verify repository transparently decrypts
            var retrieved = (await _repo.GetSecretProvidersAsync()).FirstOrDefault(p => p.ProviderName == "Vault");
            retrieved.Should().NotBeNull();
            retrieved!.ConfigJson.Should().Be(plainConfig);
        }

        [Fact]
        [Requirement("SEC-01", "SEC", RequirementType.Positive, "Encrypts AuthProvider configuration JSON at rest in database using Master Key.")]
        public async Task SaveAuthProvider_EncryptsConfigJson_AtRestInDatabase()
        {
            var plainConfig = "{\"server\":\"ldaps.corp.local\",\"bindPassword\":\"P@ssw0rdServiceAccount999!\",\"useSsl\":true}";
            var dto = new AuthProviderDto
            {
                ProviderName = "ActiveDirectory",
                DisplayName = "Active Directory LDAP",
                UserHeader = "Remote-User",
                GroupsHeader = "Remote-Groups",
                ConfigJson = plainConfig,
                IsEnabled = true
            };

            await _repo.SaveAuthProviderAsync(dto);

            // Directly inspect raw database row
            using var conn = _dbFactory.CreateConnection();
            var rawEncrypted = await conn.ExecuteScalarAsync<string>("SELECT EncryptedConfigJson FROM AuthProviderConfigs WHERE ProviderName = 'ActiveDirectory';");

            rawEncrypted.Should().NotBeNullOrEmpty();
            rawEncrypted.Should().NotContain("P@ssw0rdServiceAccount999!");
            rawEncrypted.Should().NotContain("ldaps.corp.local");

            // Verify repository transparently decrypts
            var retrieved = (await _repo.GetAuthProvidersAsync()).FirstOrDefault(p => p.ProviderName == "ActiveDirectory");
            retrieved.Should().NotBeNull();
            retrieved!.ConfigJson.Should().Be(plainConfig);
        }

        [Fact]
        [Requirement("SEC-PROVIDER-MASK-SECRETS-GET", "SEC", RequirementType.Positive, "ProvidersController GET endpoints mask sensitive tokens and passwords as asterisks.")]
        public async Task ProvidersController_GetEndpoints_RedactSensitiveSecrets()
        {
            var secretDto = new SecretProviderDto
            {
                ProviderName = "Vault",
                DisplayName = "HashiCorp Vault",
                ConfigJson = "{\"address\":\"https://vault.corp.local\",\"token\":\"s.ActualSecretToken\",\"secretId\":\"sec-xyz-987\"}",
                IsEnabled = true
            };
            await _repo.SaveSecretProviderAsync(secretDto);

            var authDto = new AuthProviderDto
            {
                ProviderName = "ActiveDirectory",
                DisplayName = "AD LDAP",
                ConfigJson = "{\"server\":\"ldaps.corp.local\",\"bindPassword\":\"SuperSecretLdapPass\",\"clientSecret\":\"oidc-secret-123\"}",
                IsEnabled = true
            };
            await _repo.SaveAuthProviderAsync(authDto);

            var controller = new ProvidersController(_repo, _repo);

            // Test GET /api/providers/secrets
            var secretsResult = await controller.GetSecretProviders() as OkObjectResult;
            secretsResult.Should().NotBeNull();
            var secretList = (secretsResult!.Value as IEnumerable<SecretProviderDto>)?.ToList();
            secretList.Should().NotBeNull();
            var vaultItem = secretList!.First(p => p.ProviderName == "Vault");
            vaultItem.ConfigJson.Should().Contain("\"token\":\"********\"");
            vaultItem.ConfigJson.Should().Contain("\"secretId\":\"********\"");
            vaultItem.ConfigJson.Should().NotContain("ActualSecretToken");
            vaultItem.ConfigJson.Should().NotContain("sec-xyz-987");

            // Test GET /api/providers/auth
            var authResult = await controller.GetAuthProviders() as OkObjectResult;
            authResult.Should().NotBeNull();
            var authList = (authResult!.Value as IEnumerable<AuthProviderDto>)?.ToList();
            authList.Should().NotBeNull();
            var adItem = authList!.First(p => p.ProviderName == "ActiveDirectory");
            adItem.ConfigJson.Should().Contain("\"bindPassword\":\"********\"");
            adItem.ConfigJson.Should().Contain("\"clientSecret\":\"********\"");
            adItem.ConfigJson.Should().NotContain("SuperSecretLdapPass");
            adItem.ConfigJson.Should().NotContain("oidc-secret-123");

            // Test GET /api/providers & /api/admin/providers
            var allResult = await controller.GetAllProviders() as OkObjectResult;
            allResult.Should().NotBeNull();
            var json = JsonSerializer.Serialize(allResult!.Value);
            json.Should().NotContain("ActualSecretToken");
            json.Should().NotContain("SuperSecretLdapPass");
            json.Should().Contain("********");
        }

        [Fact]
        [Requirement("SEC-PROVIDER-REDACT-AUDIT-PAYLOADS", "SEC", RequirementType.Positive, "ProvidersController redacts sensitive secrets in administrative audit logs.")]
        public async Task ProvidersController_SaveEndpoints_RedactAuditLogPayloads()
        {
            var loggedActions = new List<(string Action, string Target, string Details, bool Success)>();
            var mockAudit = new Mock<IAuditLogger>();
            mockAudit.Setup(a => a.LogAdminActionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>()
            )).Callback<string, string, string, string, bool, string?>((user, action, target, details, success, err) =>
            {
                loggedActions.Add((action, target, details, success));
            }).Returns(Task.CompletedTask);

            var controller = new ProvidersController(_repo, _repo);

            var secretDto = new SecretProviderDto
            {
                ProviderName = "Vault",
                DisplayName = "HashiCorp Vault",
                ConfigJson = "{\"address\":\"https://vault.corp.local\",\"token\":\"s.SuperSecretVaultToken12345\"}",
                IsEnabled = true
            };

            var result = await controller.SaveSecretProvider(secretDto, mockAudit.Object);
            result.Should().BeOfType<OkObjectResult>();

            loggedActions.Should().HaveCount(1);
            var log = loggedActions[0];
            log.Action.Should().Be("SaveSecretProvider");
            log.Details.Should().Contain("\"token\":\"********\"");
            log.Details.Should().NotContain("SuperSecretVaultToken12345");
        }

        [Fact]
        [Requirement("SEC-01", "SEC", RequirementType.Positive, "Preserves existing stored secret ciphertext when masked placeholder string is submitted in updates.")]
        public async Task ProvidersController_MaskPreserving_PreservesExistingDecryptedSecret_WhenMaskSubmitted()
        {
            // Seed initial config
            var initialDto = new SecretProviderDto
            {
                ProviderName = "Vault",
                DisplayName = "HashiCorp Vault",
                ConfigJson = "{\"address\":\"https://vault.corp.local:8200\",\"token\":\"s.InitialHighEntropyToken999\",\"mountPath\":\"secret\"}",
                IsEnabled = true
            };
            await _repo.SaveSecretProviderAsync(initialDto);

            var controller = new ProvidersController(_repo, _repo);
            var mockAudit = new Mock<IAuditLogger>();

            // Admin updates address and mountPath, leaving token as "********"
            var updateDto = new SecretProviderDto
            {
                ProviderName = "Vault",
                DisplayName = "HashiCorp Vault Primary",
                ConfigJson = "{\"address\":\"https://vault-updated.corp.local:8200\",\"token\":\"********\",\"mountPath\":\"kv-v2\"}",
                IsEnabled = true
            };

            var result = await controller.SaveSecretProvider(updateDto, mockAudit.Object);
            result.Should().BeOfType<OkObjectResult>();

            // Decrypted repository view must still contain original token
            var updated = (await _repo.GetSecretProvidersAsync()).First(p => p.ProviderName == "Vault");
            updated.DisplayName.Should().Be("HashiCorp Vault Primary");
            updated.ConfigJson.Should().Contain("s.InitialHighEntropyToken999");
            updated.ConfigJson.Should().Contain("https://vault-updated.corp.local:8200");
            updated.ConfigJson.Should().Contain("kv-v2");
            updated.ConfigJson.Should().NotContain("********");
        }

        [Fact]
        [Requirement("GUARD-05", "GUARD", RequirementType.Negative, "Fails closed with HTTP 400 when invalid JSON or insecure HTTP/LDAP URLs are submitted.")]
        public async Task FailClosedValidation_RejectsInvalidJson_AndInsecureUrls()
        {
            var mockAudit = new Mock<IAuditLogger>();
            var controller = new ProvidersController(_repo, _repo);

            // 1. Invalid JSON
            var invalidJsonDto = new SecretProviderDto
            {
                ProviderName = "Vault",
                ConfigJson = "{ not-valid-json }"
            };
            var result1 = await controller.SaveSecretProvider(invalidJsonDto, mockAudit.Object) as BadRequestObjectResult;
            result1.Should().NotBeNull();
            result1!.StatusCode.Should().Be(400);

            // 2. Insecure HTTP URL
            var insecureUrlDto = new SecretProviderDto
            {
                ProviderName = "Vault",
                ConfigJson = "{\"address\":\"http://plain-vault.insecure.com:8200\"}"
            };
            var result2 = await controller.SaveSecretProvider(insecureUrlDto, mockAudit.Object) as BadRequestObjectResult;
            result2.Should().NotBeNull();
            result2!.StatusCode.Should().Be(400);

            // 3. LDAP plaintext port 389 without SSL
            var insecureLdapDto = new AuthProviderDto
            {
                ProviderName = "ActiveDirectory",
                ConfigJson = "{\"server\":\"ldap.insecure.com\",\"port\":389,\"useSsl\":false}"
            };
            var result3 = await controller.SaveAuthProvider(insecureLdapDto, mockAudit.Object) as BadRequestObjectResult;
            result3.Should().NotBeNull();
            result3!.StatusCode.Should().Be(400);
        }

        [Fact]
        [Requirement("SEC-02", "SEC", RequirementType.Positive, "VaultSecretRetriever dynamically loads, applies, and reloads Vault configurations from database repository.")]
        public async Task VaultSecretRetriever_DynamicallyLoadsAndAppliesDbConfig_WithReload()
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            var staticConfig = new ConfigurationBuilder().Build();

            // 1. Initially no DB config -> retriever returns null client
            var retriever = new VaultSecretRetriever(staticConfig, cache, _repo);
            var clientInitial = await retriever.EnsureVaultClientAsync();
            clientInitial.Should().BeNull();

            // 2. Save Vault provider config to DB
            var dbDto = new SecretProviderDto
            {
                ProviderName = "Vault",
                DisplayName = "HashiCorp Vault",
                ConfigJson = "{\"address\":\"https://vault-dynamic.corp.local:8200\",\"token\":\"s.DynamicVaultToken12345\"}",
                IsEnabled = true
            };
            await _repo.SaveSecretProviderAsync(dbDto);

            // Reload and ensure client
            await retriever.ReloadConfigAsync();
            var clientLoaded = await retriever.EnsureVaultClientAsync();
            clientLoaded.Should().NotBeNull();

            // 3. Disable Vault in DB
            dbDto.IsEnabled = false;
            await _repo.SaveSecretProviderAsync(dbDto);
            await retriever.ReloadConfigAsync();

            var clientDisabled = await retriever.EnsureVaultClientAsync();
            clientDisabled.Should().BeNull();
        }

        [Fact]
        [Requirement("AUTH-03", "AUTH", RequirementType.Positive, "HeaderIdentityProvider dynamically applies database-configured SSO proxy headers.")]
        public async Task HeaderIdentityProvider_DynamicallyLoadsAndAppliesDbConfig()
        {
            var headerProvider = new HeaderIdentityProvider(new ConfigurationBuilder().Build(), _repo);

            // Configure custom headers in DB for PocketID_TinyAuth
            var authDto = new AuthProviderDto
            {
                ProviderName = "PocketID_TinyAuth",
                DisplayName = "Custom Reverse Proxy",
                UserHeader = "X-Custom-User",
                GroupsHeader = "X-Custom-Groups",
                IsEnabled = true
            };
            await _repo.SaveAuthProviderAsync(authDto);

            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
            httpContext.Request.Headers["X-Custom-User"] = "jane_doe";
            httpContext.Request.Headers["X-Custom-Groups"] = "devops,engineering";

            var identity = await headerProvider.ResolveIdentityAsync(httpContext);
            identity.Username.Should().Be("jane_doe");
            identity.GroupNames.Should().Contain("devops");
            identity.GroupNames.Should().Contain("engineering");

            // When disabled in DB, fallback to guest
            authDto.IsEnabled = false;
            await _repo.SaveAuthProviderAsync(authDto);

            var disabledIdentity = await headerProvider.ResolveIdentityAsync(httpContext);
            disabledIdentity.Username.Should().Be("guest");
        }

        [Fact]
        [Requirement("AUTH-04", "AUTH", RequirementType.Positive, "LdapActiveDirectoryService returns empty SIDs when disabled in database.")]
        public async Task LdapActiveDirectoryService_RespectsDisabledStatusInDatabase()
        {
            var authDto = new AuthProviderDto
            {
                ProviderName = "ActiveDirectory",
                DisplayName = "Active Directory",
                ConfigJson = "{\"server\":\"ldaps.corp.local:636\",\"useSsl\":true}",
                IsEnabled = false
            };
            await _repo.SaveAuthProviderAsync(authDto);

            var ldapService = new LdapActiveDirectoryService(
                _config,
                NullLogger<LdapActiveDirectoryService>.Instance,
                null,
                _repo
            );

            var sids = await ldapService.ResolveUserSidsAsync("testuser");
            sids.Should().BeEmpty();
        }
        [Fact]
        [Requirement("SEC-PROVIDER-GUARD-CORRUPT-ENCRYPTED-FIELD", "SEC", RequirementType.Negative, "Router must not overwrite corrupt encrypted database fields if an update occurs without user reset.")]
        public async Task SaveSecretProvider_WhenDecryptionFailed_DoesNotOverwriteCorruptPayload()
        {
            // 1. Save valid config
            var validDto = new SecretProviderDto { ProviderName = "Vault", DisplayName = "Vault", ConfigJson = "{\"valid\":true}" };
            await _repo.SaveSecretProviderAsync(validDto);

            // 2. Corrupt DB record
            using var conn = _dbFactory.CreateConnection();
            var validEncrypted = await conn.ExecuteScalarAsync<string>("SELECT EncryptedConfigJson FROM SecretProviders WHERE ProviderName = 'Vault';");
            await conn.ExecuteAsync("UPDATE SecretProviders SET EncryptedConfigJson = 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==' WHERE ProviderName = 'Vault';");

            // 3. Read back - should have IsDecryptionFailed = true
            var retrieved = (await _repo.GetSecretProvidersAsync()).First(p => p.ProviderName == "Vault");
            retrieved.IsDecryptionFailed.Should().BeTrue();
            retrieved.ConfigJson.Should().Be("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==");

            // 4. Save with IsDecryptionFailed = true (e.g. user toggled IsEnabled)
            retrieved.IsEnabled = false;
            await _repo.SaveSecretProviderAsync(retrieved);

            // 5. Verify corrupt payload is retained
            var finalEncrypted = await conn.ExecuteScalarAsync<string>("SELECT EncryptedConfigJson FROM SecretProviders WHERE ProviderName = 'Vault';");
            finalEncrypted.Should().Be("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==");
        }
    }
}
