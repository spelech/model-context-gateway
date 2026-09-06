using Microsoft.Extensions.Configuration;
using Moq;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ModelContextGateway.Tests
{
    public class DbKeyHelperTests : IDisposable
    {
        private readonly string _tempDataDir;

        public DbKeyHelperTests()
        {
            _tempDataDir = Path.Combine(Path.GetTempPath(), $"mcp_key_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDataDir);
            DbKeyHelper.ResetCache();
        }

        public void Dispose()
        {
            DbKeyHelper.ResetCache();
            if (Directory.Exists(_tempDataDir))
            {
                try { Directory.Delete(_tempDataDir, true); } catch { }
            }
        }

        [Fact]
        [Requirement("SEC-KEYFILE-ENV-PRECEDENCE", "SEC", RequirementType.Positive, "Explicit environment variables MCG_MASTER_KEY or MCG_SECRET take precedence over keyfiles.")]
        public void ResolveDbEncryptionKey_ReturnsConfiguredEnvKey_WhenPresent()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DATA_DIR", _tempDataDir },
                { "MCG_MASTER_KEY", "ConfiguredEnvMasterKey1234567890123456789012==" }
            }).Build();

            var resolvedKey = DbKeyHelper.ResolveDbEncryptionKey(config);

            Assert.Equal("ConfiguredEnvMasterKey1234567890123456789012==", resolvedKey);
            var keyFilePath = Path.Combine(_tempDataDir, ".master.key");
            Assert.False(File.Exists(keyFilePath), "Should not create a key file when configured in environment.");
        }

        [Fact]
        [Requirement("SEC-KEYFILE-FILE-SECRET", "SEC", RequirementType.Positive, "File-based secrets configured via MCG_MASTER_KEY_FILE or standard Docker secrets paths are resolved.")]
        public void ResolveDbEncryptionKey_ReturnsFileSecret_WhenKeyFileSpecified()
        {
            var secretFile = Path.Combine(_tempDataDir, "docker_secret.txt");
            File.WriteAllText(secretFile, "DockerMountedSecretKey12345678901234567890==");

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DATA_DIR", _tempDataDir },
                { "MCG_MASTER_KEY_FILE", secretFile }
            }).Build();

            var resolvedKey = DbKeyHelper.ResolveDbEncryptionKey(config);

            Assert.Equal("DockerMountedSecretKey12345678901234567890==", resolvedKey);
        }

        [Fact]
        [Requirement("SEC-KEYFILE-AUTOGEN", "SEC", RequirementType.Positive, "Blank-slate initialization auto-generates a 256-bit base64 master key and persists it to .master.key.")]
        public void ResolveDbEncryptionKey_AutoGeneratesAndPersistsKey_WhenBlankSlate()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DATA_DIR", _tempDataDir }
            }).Build();

            var resolvedKey = DbKeyHelper.ResolveDbEncryptionKey(config);

            Assert.False(string.IsNullOrWhiteSpace(resolvedKey));
            var keyBytes = Convert.FromBase64String(resolvedKey);
            Assert.Equal(32, keyBytes.Length); // 256-bit

            var keyFilePath = Path.Combine(_tempDataDir, ".master.key");
            Assert.True(File.Exists(keyFilePath), "Auto-generated key must be persisted to .master.key");
            Assert.Equal(resolvedKey, File.ReadAllText(keyFilePath).Trim());
        }

        [Fact]
        [Requirement("SEC-KEYFILE-RELOAD", "SEC", RequirementType.Positive, "Existing .master.key file is loaded across gateway restarts without key mutation.")]
        public void ResolveDbEncryptionKey_LoadsExistingKeyFile_OnSubsequentBoot()
        {
            var keyFilePath = Path.Combine(_tempDataDir, ".master.key");
            const string existingKey = "ExistingPersistentKeyFile1234567890123456==";
            File.WriteAllText(keyFilePath, existingKey);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DATA_DIR", _tempDataDir }
            }).Build();

            var resolvedKey = DbKeyHelper.ResolveDbEncryptionKey(config);

            Assert.Equal(existingKey, resolvedKey);
        }

        [Fact]
        [Requirement("SEC-KEYFILE-HIERARCHY-PRECEDENCE", "SEC", RequirementType.Positive, "Explicit environment variables take precedence over file secrets and keyfiles.")]
        public void ResolveDbEncryptionKey_EnvVarTakesPrecedenceOverFileSecretAndKeyFile()
        {
            var keyFilePath = Path.Combine(_tempDataDir, ".master.key");
            File.WriteAllText(keyFilePath, "KeyFileKey123456789012345678901234567890==");

            var secretFile = Path.Combine(_tempDataDir, "secret.txt");
            File.WriteAllText(secretFile, "FileSecretKey1234567890123456789012345678==");

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DATA_DIR", _tempDataDir },
                { "MCG_MASTER_KEY_FILE", secretFile },
                { "MCG_MASTER_KEY", "WinningEnvMasterKey12345678901234567890123==" }
            }).Build();

            var resolvedKey = DbKeyHelper.ResolveDbEncryptionKey(config);

            Assert.Equal("WinningEnvMasterKey12345678901234567890123==", resolvedKey);
        }

        [Fact]
        [Requirement("SEC-KEYFILE-FILE-OVER-KEYFILE", "SEC", RequirementType.Positive, "Explicit file secrets take precedence over persistent .master.key files.")]
        public void ResolveDbEncryptionKey_FileSecretTakesPrecedenceOverKeyFile()
        {
            var keyFilePath = Path.Combine(_tempDataDir, ".master.key");
            File.WriteAllText(keyFilePath, "KeyFileKey123456789012345678901234567890==");

            var secretFile = Path.Combine(_tempDataDir, "secret.txt");
            File.WriteAllText(secretFile, "WinningFileSecret123456789012345678901234==");

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DATA_DIR", _tempDataDir },
                { "MCG_MASTER_KEY_FILE", secretFile }
            }).Build();

            var resolvedKey = DbKeyHelper.ResolveDbEncryptionKey(config);

            Assert.Equal("WinningFileSecret123456789012345678901234==", resolvedKey);
        }

        [Fact]
        [Requirement("SEC-KEYSOURCE-DETECTION", "SEC", RequirementType.Positive, "Correctly identifies KeySource origin for environment, file, and auto-generated keys.")]
        public void ResolveDbEncryptionKey_IdentifiesKeySourceAccurately()
        {
            // 1. Environment / External
            DbKeyHelper.ResetCache();
            var envConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "MCG_MASTER_KEY", "TestEnvKey1234567890123456789012==" }
            }).Build();
            DbKeyHelper.ResolveDbEncryptionKey(envConfig);
            Assert.Equal(MasterKeySource.External, DbKeyHelper.ActiveKeySource);

            // 2. Secret File
            DbKeyHelper.ResetCache();
            var secretFile = Path.Combine(_tempDataDir, "secret_source.txt");
            File.WriteAllText(secretFile, "SecretFileKey1234567890123456789012==");
            var fileConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DATA_DIR", _tempDataDir },
                { "MCG_MASTER_KEY_FILE", secretFile }
            }).Build();
            DbKeyHelper.ResolveDbEncryptionKey(fileConfig);
            Assert.Equal(MasterKeySource.SecretFile, DbKeyHelper.ActiveKeySource);

            // 3. Existing Configured Key File (.master.key)
            DbKeyHelper.ResetCache();
            var keyFilePath = Path.Combine(_tempDataDir, ".master.key");
            File.WriteAllText(keyFilePath, "ExistingConfiguredKey1234567890123456==");
            var keyFileConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DATA_DIR", _tempDataDir }
            }).Build();
            DbKeyHelper.ResolveDbEncryptionKey(keyFileConfig);
            Assert.Equal(MasterKeySource.Configured, DbKeyHelper.ActiveKeySource);

            // 4. Blank-slate AutoGenerated
            DbKeyHelper.ResetCache();
            File.Delete(keyFilePath);
            var blankConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DATA_DIR", _tempDataDir }
            }).Build();
            DbKeyHelper.ResolveDbEncryptionKey(blankConfig);
            Assert.Equal(MasterKeySource.AutoGenerated, DbKeyHelper.ActiveKeySource);
        }

        [Fact]
        [Requirement("SEC-VAULT-BOOTSTRAPPING", "SEC", RequirementType.Positive, "Bootstraps master encryption key directly from HashiCorp Vault when VAULT_ADDR is configured.")]
        public void ResolveDbEncryptionKey_BootstrapsFromVault_WhenVaultConfigured()
        {
            DbKeyHelper.ResetCache();

            var mockVaultClient = new Moq.Mock<VaultSharp.IVaultClient>();
            var mockV1 = new Moq.Mock<VaultSharp.V1.IVaultClientV1>();
            var mockSecrets = new Moq.Mock<VaultSharp.V1.SecretsEngines.ISecretsEngine>();
            var mockKv = new Moq.Mock<VaultSharp.V1.SecretsEngines.KeyValue.IKeyValueSecretsEngine>();
            var mockKv2 = new Moq.Mock<VaultSharp.V1.SecretsEngines.KeyValue.V2.IKeyValueSecretsEngineV2>();

            var secretData = new VaultSharp.V1.Commons.Secret<VaultSharp.V1.Commons.SecretData>
            {
                Data = new VaultSharp.V1.Commons.SecretData
                {
                    Data = new Dictionary<string, object>
                    {
                        { "master_key", "VaultBootstrappedMasterKey12345678901234==" }
                    }
                }
            };

            mockKv2.Setup(k => k.ReadSecretAsync("mcg/master-key", null, "secret", null))
                .ReturnsAsync(secretData);
            mockKv.Setup(k => k.V2).Returns(mockKv2.Object);
            mockSecrets.Setup(s => s.KeyValue).Returns(mockKv.Object);
            mockV1.Setup(v => v.Secrets).Returns(mockSecrets.Object);
            mockVaultClient.Setup(c => c.V1).Returns(mockV1.Object);

            DbKeyHelper.SetVaultClientFactory(() => mockVaultClient.Object);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DATA_DIR", _tempDataDir },
                { "VAULT_ADDR", "https://vault.corp.local:8200" },
                { "VAULT_TOKEN", "test-token" }
            }).Build();

            var resolvedKey = DbKeyHelper.ResolveDbEncryptionKey(config);

            Assert.Equal("VaultBootstrappedMasterKey12345678901234==", resolvedKey);
            Assert.Equal(MasterKeySource.Vault, DbKeyHelper.ActiveKeySource);
        }

        [Fact]
        [Requirement("SEC-VAULT-CUSTOM-PATH", "SEC", RequirementType.Positive, "Bootstraps master key from Vault using custom mount path and secret key name.")]
        public void ResolveDbEncryptionKey_BootstrapsFromVault_WithCustomPathAndKeyName()
        {
            DbKeyHelper.ResetCache();

            var mockVaultClient = new Mock<VaultSharp.IVaultClient>();
            var mockV1 = new Mock<VaultSharp.V1.IVaultClientV1>();
            var mockSecrets = new Mock<VaultSharp.V1.SecretsEngines.ISecretsEngine>();
            var mockKv = new Mock<VaultSharp.V1.SecretsEngines.KeyValue.IKeyValueSecretsEngine>();
            var mockKv2 = new Mock<VaultSharp.V1.SecretsEngines.KeyValue.V2.IKeyValueSecretsEngineV2>();

            var secretData = new VaultSharp.V1.Commons.Secret<VaultSharp.V1.Commons.SecretData>
            {
                Data = new VaultSharp.V1.Commons.SecretData
                {
                    Data = new Dictionary<string, object>
                    {
                        { "custom_token_key", "CustomPathVaultMasterKey123456789==" }
                    }
                }
            };

            mockKv2.Setup(k => k.ReadSecretAsync("custom-apps/gateway", null, "custom-mount", null))
                .ReturnsAsync(secretData);
            mockKv.Setup(k => k.V2).Returns(mockKv2.Object);
            mockSecrets.Setup(s => s.KeyValue).Returns(mockKv.Object);
            mockV1.Setup(v => v.Secrets).Returns(mockSecrets.Object);
            mockVaultClient.Setup(c => c.V1).Returns(mockV1.Object);

            DbKeyHelper.SetVaultClientFactory(() => mockVaultClient.Object);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DATA_DIR", _tempDataDir },
                { "VAULT_ADDR", "https://vault.corp.local:8200" },
                { "VAULT_TOKEN", "test-token" },
                { "VAULT_MASTER_KEY_PATH", "custom-mount:custom-apps/gateway" },
                { "VAULT_MASTER_KEY_NAME", "custom_token_key" }
            }).Build();

            var resolvedKey = DbKeyHelper.ResolveDbEncryptionKey(config);

            Assert.Equal("CustomPathVaultMasterKey123456789==", resolvedKey);
            Assert.Equal(MasterKeySource.Vault, DbKeyHelper.ActiveKeySource);
        }

        [Fact]
        [Requirement("GUARD-02", "GUARD", RequirementType.Negative, "Throws InvalidOperationException when Vault master key retrieval fails.")]
        public void ResolveDbEncryptionKey_ThrowsInvalidOperationException_WhenVaultFails()
        {
            DbKeyHelper.ResetCache();

            var mockVaultClient = new Mock<VaultSharp.IVaultClient>();
            var mockV1 = new Mock<VaultSharp.V1.IVaultClientV1>();
            var mockSecrets = new Mock<VaultSharp.V1.SecretsEngines.ISecretsEngine>();
            var mockKv = new Mock<VaultSharp.V1.SecretsEngines.KeyValue.IKeyValueSecretsEngine>();
            var mockKv2 = new Mock<VaultSharp.V1.SecretsEngines.KeyValue.V2.IKeyValueSecretsEngineV2>();

            mockKv2.Setup(k => k.ReadSecretAsync(It.IsAny<string>(), null, It.IsAny<string>(), null))
                .ThrowsAsync(new System.Exception("Network error connecting to Vault"));
            mockKv.Setup(k => k.V2).Returns(mockKv2.Object);
            mockSecrets.Setup(s => s.KeyValue).Returns(mockKv.Object);
            mockV1.Setup(v => v.Secrets).Returns(mockSecrets.Object);
            mockVaultClient.Setup(c => c.V1).Returns(mockV1.Object);

            DbKeyHelper.SetVaultClientFactory(() => mockVaultClient.Object);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DATA_DIR", _tempDataDir },
                { "VAULT_ADDR", "https://vault.corp.local:8200" },
                { "VAULT_TOKEN", "test-token" }
            }).Build();

            Assert.Throws<InvalidOperationException>(() => DbKeyHelper.ResolveDbEncryptionKey(config));
        }

        [Fact]
        [Requirement("SEC-KEYSOURCE-SETCACHEDKEY", "SEC", RequirementType.Positive, "SetCachedKey sets in-memory encryption key and updates ActiveKeySource.")]
        public void SetCachedKey_UpdatesCachedKeyAndActiveKeySource()
        {
            DbKeyHelper.ResetCache();
            const string manualKey = "ManuallySetEncryptionKey123456789==";

            DbKeyHelper.SetCachedKey(manualKey, MasterKeySource.Configured);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DATA_DIR", _tempDataDir }
            }).Build();

            var resolvedKey = DbKeyHelper.ResolveDbEncryptionKey(config);

            Assert.Equal(manualKey, resolvedKey);
            Assert.Equal(MasterKeySource.Configured, DbKeyHelper.ActiveKeySource);
        }

        [Fact]
        [Requirement("SEC-DB-ENCRYPTION-KEY-AUTOGEN-FAIL", "DB", RequirementType.Negative, "ResolveDbEncryptionKey wraps file persistence errors in InvalidOperationException")]
        public void ResolveDbEncryptionKey_ThrowsInvalidOperationException_WhenAutoGenerationFails()
        {
            DbKeyHelper.ResetCache();

            // On Windows, read-only on directories doesn't prevent file creation.
            // But we can simulate a failure by using an invalid path character or standard UnauthorizedAccessException.
            // On Linux/macOS, we can remove write permissions.

            string uncreatableDir;
            if (OperatingSystem.IsWindows())
            {
                // In Windows, a path with invalid characters or trying to use a reserved name
                // or just relying on a file with the same name as the directory could work.
                // Creating a file where a directory is expected will cause Directory.CreateDirectory to fail.
                uncreatableDir = Path.Combine(_tempDataDir, "file_blocking_dir");
                File.WriteAllText(uncreatableDir, "blocking");
            }
            else
            {
                uncreatableDir = Path.Combine(_tempDataDir, "readonly_dir");
                Directory.CreateDirectory(uncreatableDir);
                File.SetUnixFileMode(uncreatableDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            }

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DATA_DIR", uncreatableDir }
            }).Build();

            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => DbKeyHelper.ResolveDbEncryptionKey(config));
                Assert.Contains("failed to persist auto-generated key", exception.Message);
            }
            finally
            {
                if (!OperatingSystem.IsWindows())
                {
                    // Restore permissions so it can be cleaned up
                    File.SetUnixFileMode(uncreatableDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
            }
        }
    }
}
