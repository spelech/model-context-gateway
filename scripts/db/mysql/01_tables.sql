-- ============================================================================
-- Enterprise MCP Gateway Database Tables (MySQL / MariaDB)
-- ============================================================================

CREATE DATABASE IF NOT EXISTS `McpEnterpriseDb`;
USE `McpEnterpriseDb`;

-- 1. Registered MCP Servers
CREATE TABLE IF NOT EXISTS `Servers` (
    `Id`                VARCHAR(100) PRIMARY KEY,
    `DisplayName`       VARCHAR(200) NOT NULL,
    `Url`               VARCHAR(500) NOT NULL,
    `Enabled`           TINYINT(1) NOT NULL DEFAULT 1,
    `Hidden`            TINYINT(1) NOT NULL DEFAULT 0,
    `Type`              VARCHAR(20) NOT NULL DEFAULT 'sse',
    `SecretProvider`    VARCHAR(50) NOT NULL DEFAULT 'None',
    `SecretItemKey`     VARCHAR(100) NULL,
    `SecretMount`       VARCHAR(100) NULL,
    `SecretPath`        VARCHAR(250) NULL,
    `SecretField`       VARCHAR(100) NULL,
    `AuthShape`         VARCHAR(20) NOT NULL DEFAULT 'bearer',
    `CustomHeaderName`  VARCHAR(100) NULL,
    `Categories`        LONGTEXT NOT NULL,
    `ApiKey`            LONGTEXT NULL,
    `HeadersJson`       LONGTEXT NULL,
    `AutoDiscovered`    TINYINT(1) NOT NULL DEFAULT 0,
    `RequestTimeoutSeconds` INT NULL,
    `MaxConcurrentRequests` INT NULL,
    `ConcurrencyKeyField` VARCHAR(100) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 2. Settings Table
CREATE TABLE IF NOT EXISTS `Settings` (
    `Id`                      VARCHAR(50) PRIMARY KEY,
    `EmbeddingProvider`       VARCHAR(50) NULL,
    `EmbeddingApiUrl`         VARCHAR(500) NULL,
    `EmbeddingApiKey`         LONGTEXT NULL,
    `EmbeddingApiModel`       VARCHAR(100) NULL,
    `EmbeddingModelDir`       VARCHAR(500) NULL,
    `RequireManualApproval`   TINYINT(1) NOT NULL DEFAULT 0,
    `GlobalMaxKeys`           INT NOT NULL DEFAULT 100,
    `UserMaxKeys`             INT NOT NULL DEFAULT 5,
    `UserSecretStorage`       VARCHAR(50) NOT NULL DEFAULT 'Database',
    `AllowOpenClientRegistration` TINYINT(1) NOT NULL DEFAULT 1,
    `ToolSearchDefaultLimit`  INT NOT NULL DEFAULT 5,
    `ToolSearchMinScore`      DOUBLE NOT NULL DEFAULT 0.35,
    `ToolSearchScoreDropoffRatio` DOUBLE NOT NULL DEFAULT 0.60,
    `EnableCodeMode`          TINYINT(1) NOT NULL DEFAULT 1,
    `CodeModeExecutionTimeoutSeconds` INT NOT NULL DEFAULT 120,
    `ToolExecutionTimeoutSeconds` INT NOT NULL DEFAULT 120,
    `MaxToolOutputChars`      INT NOT NULL DEFAULT 0,
    `CodeModeMaxToolCalls`    INT NOT NULL DEFAULT 50,
    `CodeModeMaxMemoryMb`     INT NOT NULL DEFAULT 64,
    `CodeModeMaxStatements`   INT NOT NULL DEFAULT 100000,
    `CodeModeAllowedGroups`   LONGTEXT NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 3. Secret Providers Configuration Table
CREATE TABLE IF NOT EXISTS `SecretProviders` (
    `ProviderId`          INT AUTO_INCREMENT PRIMARY KEY,
    `ProviderName`        VARCHAR(50) NOT NULL UNIQUE,
    `DisplayName`         VARCHAR(100) NOT NULL,
    `EncryptedConfigJson` LONGTEXT NULL,
    `IsEnabled`           TINYINT(1) NOT NULL DEFAULT 1,
    `UpdatedAt`           DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 4. Identity & Auth Providers Configuration Table
CREATE TABLE IF NOT EXISTS `AuthProviderConfigs` (
    `AuthId`              INT AUTO_INCREMENT PRIMARY KEY,
    `ProviderName`        VARCHAR(50) NOT NULL UNIQUE,
    `DisplayName`         VARCHAR(100) NOT NULL,
    `UserHeader`          VARCHAR(100) NULL DEFAULT 'Remote-User',
    `GroupsHeader`        VARCHAR(100) NULL DEFAULT 'Remote-Groups',
    `EncryptedConfigJson` LONGTEXT NULL,
    `IsEnabled`           TINYINT(1) NOT NULL DEFAULT 1,
    `UpdatedAt`           DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 5. User & Group Security Groups
CREATE TABLE IF NOT EXISTS `AdGroups` (
    `GroupId`           INT AUTO_INCREMENT PRIMARY KEY,
    `ObjectSid`         VARCHAR(180) NOT NULL UNIQUE,
    `GroupName`         VARCHAR(256) NOT NULL,
    `Description`       VARCHAR(500) NULL,
    `IsActive`          TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt`         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 6. Tools Registry & Access Control
CREATE TABLE IF NOT EXISTS `Tools` (
    `ToolId`            INT AUTO_INCREMENT PRIMARY KEY,
    `ServerId`          VARCHAR(100) NOT NULL,
    `ToolName`          VARCHAR(150) NOT NULL,
    `Description`       TEXT NULL,
    `InputSchemaJson`   LONGTEXT NULL,
    `VaultSecretPath`   VARCHAR(250) NULL,
    `SecretProvider`    VARCHAR(50) NOT NULL DEFAULT 'None',
    `IsEnabled`         TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt`         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT `FK_Tools_Servers` FOREIGN KEY (`ServerId`) REFERENCES `Servers` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `UQ_Server_ToolName` UNIQUE (`ServerId`, `ToolName`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `ToolAccessPolicies` (
    `ToolPolicyId`      INT AUTO_INCREMENT PRIMARY KEY,
    `ToolId`            INT NOT NULL,
    `GroupId`           INT NOT NULL,
    `IsAllowed`         TINYINT(1) NOT NULL DEFAULT 1,
    `RateLimitPerMin`   INT NOT NULL DEFAULT 60,
    `CreatedAt`         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT `FK_ToolAccessPolicies_Tools` FOREIGN KEY (`ToolId`) REFERENCES `Tools` (`ToolId`) ON DELETE CASCADE,
    CONSTRAINT `FK_ToolAccessPolicies_AdGroups` FOREIGN KEY (`GroupId`) REFERENCES `AdGroups` (`GroupId`) ON DELETE CASCADE,
    CONSTRAINT `UQ_Tool_Group` UNIQUE (`ToolId`, `GroupId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- AccessPolicies Generic Table
CREATE TABLE IF NOT EXISTS `AccessPolicies` (
    `Id`            VARCHAR(100) PRIMARY KEY,
    `TargetId`      VARCHAR(250) NOT NULL,
    `RequiredGroup` VARCHAR(256) NOT NULL,
    `IsAllowed`     TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt`     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Group Mappings Table
CREATE TABLE IF NOT EXISTS `GroupMappings` (
    `Id`             VARCHAR(100) PRIMARY KEY,
    `ExternalId`     VARCHAR(256) NOT NULL,
    `InternalGroup`  VARCHAR(256) NOT NULL,
    `CreatedAt`      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 7. Audit Logging Table
CREATE TABLE IF NOT EXISTS `AuditLogs` (
    `AuditId`           BIGINT AUTO_INCREMENT PRIMARY KEY,
    `RequestId`         VARCHAR(64) NOT NULL,
    `UserPrincipalName` VARCHAR(256) NOT NULL,
    `UserSid`           VARCHAR(180) NOT NULL,
    `ServerCodeName`    VARCHAR(100) NOT NULL,
    `ItemName`          VARCHAR(150) NULL,
    `RequestMethod`     VARCHAR(50) NOT NULL,
    `ExecutionTimeMs`   INT NOT NULL,
    `StatusCode`        INT NOT NULL,
    `RequestPayload`    LONGTEXT NULL,
    `ResponsePayload`   LONGTEXT NULL,
    `ErrorMessage`      LONGTEXT NULL,
    `Timestamp`         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 8. App Keys Table
CREATE TABLE IF NOT EXISTS `AppKeys` (
    `Id`           VARCHAR(100) PRIMARY KEY,
    `Name`         VARCHAR(200) NOT NULL,
    `Username`     VARCHAR(256) NOT NULL,
    `OwnerSid`     VARCHAR(200) NOT NULL DEFAULT '',
    `KeyType`      VARCHAR(50) NOT NULL DEFAULT 'personal',
    `KeyPrefix`    VARCHAR(50) NOT NULL,
    `EncryptedKey` LONGTEXT NOT NULL,
    `ScopesJson`   LONGTEXT NOT NULL,
    `ExpiresAt`    DATETIME NULL,
    `CreatedAt`    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY `UQ_AppKeys_KeyPrefix` (`KeyPrefix`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 9. Admin Audit Logging Table
CREATE TABLE IF NOT EXISTS `AdminAuditLogs` (
    `Id`           VARCHAR(50) NOT NULL PRIMARY KEY,
    `Username`     VARCHAR(256) NOT NULL,
    `Action`       VARCHAR(100) NOT NULL,
    `Target`       VARCHAR(256) NOT NULL,
    `Details`      LONGTEXT NULL,
    `Success`      TINYINT(1) NOT NULL,
    `ErrorMessage` LONGTEXT NULL,
    `Timestamp`    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 10. User Quotas Table
CREATE TABLE IF NOT EXISTS `UserQuotas` (
    `Username`     VARCHAR(256) PRIMARY KEY,
    `MaxKeys`      INT NOT NULL DEFAULT 5,
    `CreatedAt`    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 11. OAuth Clients Table (RFC 7591 Dynamic Client Registration)
CREATE TABLE IF NOT EXISTS `OAuthClients` (
    `ClientId`         VARCHAR(100) PRIMARY KEY,
    `ClientSecretHash` VARCHAR(256) NOT NULL DEFAULT '',
    `ClientName`       VARCHAR(200) NOT NULL,
    `ClientType`       VARCHAR(50) NOT NULL DEFAULT 'confidential',
    `RedirectUrisJson` LONGTEXT NOT NULL,
    `GrantTypesJson`   LONGTEXT NOT NULL,
    `ScopesJson`       LONGTEXT NOT NULL,
    `OwnerSid`         VARCHAR(200) NOT NULL DEFAULT '',
    `CreatedBy`        VARCHAR(256) NOT NULL DEFAULT '',
    `ExpiresAt`        DATETIME NULL,
    `CreatedAt`        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

