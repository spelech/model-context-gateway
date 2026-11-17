-- ============================================================================
-- Enterprise MCP Gateway Database Tables (MS SQL Server)
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'McpEnterpriseDb')
BEGIN
    CREATE DATABASE [McpEnterpriseDb];
END;
GO

USE [McpEnterpriseDb];
GO

-- 1. Registered MCP Servers
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Servers')
BEGIN
    CREATE TABLE [dbo].[Servers] (
        [Id]                VARCHAR(100) PRIMARY KEY,
        [DisplayName]       NVARCHAR(200) NOT NULL,
        [Url]               VARCHAR(500) NOT NULL,
        [Enabled]           BIT NOT NULL DEFAULT 1,
        [Hidden]            BIT NOT NULL DEFAULT 0,
        [Type]              VARCHAR(20) NOT NULL DEFAULT 'sse',
        [SecretProvider]    VARCHAR(50) NOT NULL DEFAULT 'None',
        [SecretItemKey]     VARCHAR(100) NULL,
        [SecretMount]       VARCHAR(100) NULL,
        [SecretPath]        VARCHAR(250) NULL,
        [SecretField]       VARCHAR(100) NULL,
        [AuthShape]         VARCHAR(20) NOT NULL DEFAULT 'bearer',
        [CustomHeaderName]  VARCHAR(100) NULL,
        [Categories]        NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        [ApiKey]            NVARCHAR(MAX) NULL,
        [HeadersJson]       NVARCHAR(MAX) NULL,
        [AutoDiscovered]    BIT NOT NULL DEFAULT 0,
        [RequestTimeoutSeconds] INT NULL,
        [MaxConcurrentRequests] INT NULL,
        [ConcurrencyKeyField] VARCHAR(100) NULL
    );
END;
GO

-- 2. Settings Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Settings')
BEGIN
    CREATE TABLE [dbo].[Settings] (
        [Id]                      VARCHAR(50) PRIMARY KEY,
        [EmbeddingProvider]       VARCHAR(50) NULL,
        [EmbeddingApiUrl]         VARCHAR(500) NULL,
        [EmbeddingApiKey]         NVARCHAR(MAX) NULL,
        [EmbeddingApiModel]       VARCHAR(100) NULL,
        [EmbeddingModelDir]       VARCHAR(500) NULL,
        [RequireManualApproval]   BIT NOT NULL DEFAULT 0,
        [GlobalMaxKeys]           INT NOT NULL DEFAULT 100,
        [UserMaxKeys]             INT NOT NULL DEFAULT 5,
        [UserSecretStorage]       VARCHAR(50) NOT NULL DEFAULT 'Database',
        [AllowOpenClientRegistration] BIT NOT NULL DEFAULT 1,
        [ToolSearchDefaultLimit]  INT NOT NULL DEFAULT 5,
        [ToolSearchMinScore]      FLOAT NOT NULL DEFAULT 0.35,
        [ToolSearchScoreDropoffRatio] FLOAT NOT NULL DEFAULT 0.60,
        [EnableCodeMode]          BIT NOT NULL DEFAULT 1,
        [CodeModeExecutionTimeoutSeconds] INT NOT NULL DEFAULT 120,
        [ToolExecutionTimeoutSeconds] INT NOT NULL DEFAULT 120,
        [MaxToolOutputChars]      INT NOT NULL DEFAULT 0,
        [CodeModeMaxToolCalls]    INT NOT NULL DEFAULT 50,
        [CodeModeMaxMemoryMb]     INT NOT NULL DEFAULT 64,
        [CodeModeMaxStatements]   INT NOT NULL DEFAULT 100000,
        [CodeModeAllowedGroups]   NVARCHAR(MAX) NOT NULL DEFAULT ''
    );
END;
GO

-- 7. App Keys Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AppKeys')
BEGIN
    CREATE TABLE [dbo].[AppKeys] (
        [Id]           VARCHAR(100) PRIMARY KEY,
        [Name]         NVARCHAR(200) NOT NULL,
        [Username]     NVARCHAR(256) NOT NULL,
        [OwnerSid]     NVARCHAR(200) NOT NULL DEFAULT '',
        [KeyType]      VARCHAR(50) NOT NULL DEFAULT 'personal',
        [KeyPrefix]    VARCHAR(50) NOT NULL,
        [EncryptedKey] NVARCHAR(MAX) NOT NULL,
        [ScopesJson]   NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        [ExpiresAt]    DATETIME2 NULL,
        [CreatedAt]    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
GO

-- Idempotent: add OwnerSid to pre-existing AppKeys tables so app-key calls are attributable.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'OwnerSid' AND Object_ID = Object_ID(N'dbo.AppKeys'))
    ALTER TABLE [dbo].[AppKeys] ADD [OwnerSid] NVARCHAR(200) NOT NULL DEFAULT '';
GO

-- Idempotent: add KeyType to pre-existing AppKeys tables
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'KeyType' AND Object_ID = Object_ID(N'dbo.AppKeys'))
    ALTER TABLE [dbo].[AppKeys] ADD [KeyType] VARCHAR(50) NOT NULL DEFAULT 'personal';
GO

-- Idempotent: add unique index on KeyPrefix for high-entropy lookup and collision prevention
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE Name = N'UQ_AppKeys_KeyPrefix' AND Object_ID = Object_ID(N'dbo.AppKeys'))
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_AppKeys_KeyPrefix] ON [dbo].[AppKeys] ([KeyPrefix]);
GO

-- 3. Secret Providers Configuration Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SecretProviders')
BEGIN
    CREATE TABLE [dbo].[SecretProviders] (
        [ProviderId]          INT IDENTITY(1,1) PRIMARY KEY,
        [ProviderName]        VARCHAR(50) NOT NULL UNIQUE, -- 'Vault', 'WindowsRegistry', 'Environment'
        [DisplayName]         NVARCHAR(100) NOT NULL,
        [EncryptedConfigJson] NVARCHAR(MAX) NULL,
        [IsEnabled]           BIT NOT NULL DEFAULT 1,
        [UpdatedAt]           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
GO

-- 4. Identity & Auth Providers Configuration Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuthProviderConfigs')
BEGIN
    CREATE TABLE [dbo].[AuthProviderConfigs] (
        [AuthId]              INT IDENTITY(1,1) PRIMARY KEY,
        [ProviderName]        VARCHAR(50) NOT NULL UNIQUE, -- 'ActiveDirectory', 'PocketID_TinyAuth'
        [DisplayName]         NVARCHAR(100) NOT NULL,
        [UserHeader]          VARCHAR(100) NULL DEFAULT 'Remote-User',
        [GroupsHeader]        VARCHAR(100) NULL DEFAULT 'Remote-Groups',
        [EncryptedConfigJson] NVARCHAR(MAX) NULL,
        [IsEnabled]           BIT NOT NULL DEFAULT 1,
        [UpdatedAt]           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
GO

-- 5. User & Group Security Groups
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AdGroups')
BEGIN
    CREATE TABLE [dbo].[AdGroups] (
        [GroupId]           INT IDENTITY(1,1) PRIMARY KEY,
        [ObjectSid]         VARCHAR(180) NOT NULL UNIQUE,
        [GroupName]         NVARCHAR(256) NOT NULL,
        [Description]       NVARCHAR(500) NULL,
        [IsActive]          BIT NOT NULL DEFAULT 1,
        [CreatedAt]         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
GO

-- 6. Tools Registry & Access Control
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Tools')
BEGIN
    CREATE TABLE [dbo].[Tools] (
        [ToolId]            INT IDENTITY(1,1) PRIMARY KEY,
        [ServerId]          VARCHAR(100) NOT NULL FOREIGN KEY REFERENCES [dbo].[Servers]([Id]),
        [ToolName]          VARCHAR(150) NOT NULL,
        [Description]       NVARCHAR(MAX) NULL,
        [InputSchemaJson]   NVARCHAR(MAX) NULL,
        [VaultSecretPath]   VARCHAR(250) NULL,
        [SecretProvider]    VARCHAR(50) NOT NULL DEFAULT 'None',
        [IsEnabled]         BIT NOT NULL DEFAULT 1,
        [CreatedAt]         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [UQ_Server_ToolName] UNIQUE ([ServerId], [ToolName])
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ToolAccessPolicies')
BEGIN
    CREATE TABLE [dbo].[ToolAccessPolicies] (
        [ToolPolicyId]      INT IDENTITY(1,1) PRIMARY KEY,
        [ToolId]            INT NOT NULL FOREIGN KEY REFERENCES [dbo].[Tools]([ToolId]),
        [GroupId]           INT NOT NULL FOREIGN KEY REFERENCES [dbo].[AdGroups]([GroupId]),
        [IsAllowed]         BIT NOT NULL DEFAULT 1,
        [RateLimitPerMin]   INT NOT NULL DEFAULT 60,
        [CreatedAt]         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [UQ_Tool_Group] UNIQUE ([ToolId], [GroupId])
    );
END;
GO

-- AccessPolicies Generic Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AccessPolicies')
BEGIN
    CREATE TABLE [dbo].[AccessPolicies] (
        [Id]            VARCHAR(100) PRIMARY KEY,
        [TargetId]      VARCHAR(250) NOT NULL,
        [RequiredGroup] NVARCHAR(256) NOT NULL,
        [IsAllowed]     BIT NOT NULL DEFAULT 1,
        [CreatedAt]     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
GO

-- Group Mappings Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GroupMappings')
BEGIN
    CREATE TABLE [dbo].[GroupMappings] (
        [Id]             VARCHAR(100) PRIMARY KEY,
        [ExternalId]     VARCHAR(256) NOT NULL,
        [InternalGroup]  NVARCHAR(256) NOT NULL,
        [CreatedAt]      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
GO

-- 8. Audit Logging Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
BEGIN
    CREATE TABLE [dbo].[AuditLogs] (
        [AuditId]           BIGINT IDENTITY(1,1) PRIMARY KEY,
        [RequestId]         VARCHAR(64) NOT NULL,
        [UserPrincipalName] NVARCHAR(256) NOT NULL,
        [UserSid]           VARCHAR(180) NOT NULL,
        [ServerCodeName]    VARCHAR(100) NOT NULL,
        [ItemName]          VARCHAR(150) NULL,
        [RequestMethod]     VARCHAR(50) NOT NULL,
        [ExecutionTimeMs]   INT NOT NULL,
        [StatusCode]        INT NOT NULL,
        [RequestPayload]    NVARCHAR(MAX) NULL,
        [ResponsePayload]   NVARCHAR(MAX) NULL,
        [ErrorMessage]      NVARCHAR(MAX) NULL,
        [Timestamp]         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
GO

-- 9. Admin Audit Logging Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AdminAuditLogs')
BEGIN
    CREATE TABLE [dbo].[AdminAuditLogs] (
        [Id]           VARCHAR(50) NOT NULL PRIMARY KEY,
        [Username]     NVARCHAR(256) NOT NULL,
        [Action]       VARCHAR(100) NOT NULL,
        [Target]       VARCHAR(256) NOT NULL,
        [Details]      NVARCHAR(MAX) NULL,
        [Success]      BIT NOT NULL,
        [ErrorMessage] NVARCHAR(MAX) NULL,
        [Timestamp]    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
GO

-- 10. User Quotas Table
IF OBJECT_ID('dbo.UserQuotas', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserQuotas] (
        [Username]     NVARCHAR(256) PRIMARY KEY,
        [MaxKeys]      INT NOT NULL DEFAULT 5,
        [CreatedAt]    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt]    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
GO

-- 11. OAuth Clients Table (RFC 7591 Dynamic Client Registration)
IF OBJECT_ID('dbo.OAuthClients', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OAuthClients] (
        [ClientId]         VARCHAR(100) PRIMARY KEY,
        [ClientSecretHash] VARCHAR(256) NOT NULL DEFAULT '',
        [ClientName]       NVARCHAR(200) NOT NULL,
        [ClientType]       VARCHAR(50) NOT NULL DEFAULT 'confidential',
        [RedirectUrisJson] NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        [GrantTypesJson]   NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        [ScopesJson]       NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        [OwnerSid]         NVARCHAR(200) NOT NULL DEFAULT '',
        [CreatedBy]        NVARCHAR(256) NOT NULL DEFAULT '',
        [ExpiresAt]        DATETIME2 NULL,
        [CreatedAt]        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
GO

