# AppKey & OAuth Client Last-Used Tracking and Safe Key Rotation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide visibility into credential utilization by tracking `LastUsedAt` with a 60-second in-memory debounce, and replace the non-functional "Config" button with an explicit, unclosable-until-acknowledged "Rotate Key" workflow containing an interactive Config Generator.

**Architecture:** Database schema migrations add `LastUsedAt` to `AppKeys` and `OAuthClients`. An in-memory debounced `IActivityTracker` updates timestamps on successful authentications without DB lock contention. `ICredentialService.RotateCredentialAsync` generates new credentials while preserving metadata. In the React frontend, `AppKeyRotateModal` displays a two-stage warning and configuration generator with modal click-away protection.

**Tech Stack:** C# .NET 10, ASP.NET Core, Dapper, SQLite/MySQL/MSSQL, React 19, TypeScript, Vite, Vitest, xUnit, Moq.

## Global Constraints

- Mandatory version bump: Target version is `5.20.0`.
- All tests must use formal requirements taxonomy (`[Requirement("AUTH-...", "AUTH", RequirementType.Positive, "...")]` / `@requirement`).
- Requirements catalog must be regenerated and verified (`dotnet run --project scripts/CatalogGenerator -- --verify-only`).
- Zero-knowledge at rest: Keys and secrets remain one-way SHA-256 hashes.
- In-memory activity tracking debounce must be 60 seconds (`TimeSpan.FromSeconds(60)`).
- Modal backdrop dismissal must be disabled during key reveal screens.

---

### Task 1: Database Migration & Entity Models (`LastUsedAt`)

**Files:**
- Modify: `Components/AppKeys/AppKey.cs:1-20`
- Modify: `Components/Clients/OAuthClient.cs:1-21`
- Modify: `Infrastructure/Persistence/DatabaseSeederService.cs:510-1520`
- Modify: `Infrastructure/Persistence/Repositories.cs:80-600`
- Create: `ModelContextGateway.Tests/AppKeyLastUsedPersistenceTests.cs`

**Interfaces:**
- Consumes: `IDbConnectionFactory`, Dapper `ExecuteAsync`, `QueryFirstOrDefaultAsync`
- Produces: `AppKey.LastUsedAt`, `OAuthClient.LastUsedAt`, `IAppKeyRepository.UpdateLastUsedAsync(string id, DateTime lastUsedAt)`, `IOAuthClientRepository.UpdateLastUsedAsync(string clientId, DateTime lastUsedAt)`

- [ ] **Step 1: Write the failing tests for schema migration and entity LastUsedAt persistence**

```csharp
// ModelContextGateway.Tests/AppKeyLastUsedPersistenceTests.cs
using System;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextGateway.Components.AppKeys;
using ModelContextGateway.Components.Clients;
using ModelContextGateway.Infrastructure.Persistence;
using ModelContextGateway.Tests.Attributes;
using Xunit;

namespace ModelContextGateway.Tests
{
    public class AppKeyLastUsedPersistenceTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly IDbConnectionFactory _dbFactory;

        public AppKeyLastUsedPersistenceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"test_mcg_lastused_{Guid.NewGuid():N}.db");
            _dbFactory = new SqliteDbConnectionFactory($"Data Source={_dbPath}");
            var seeder = new DatabaseSeederService(_dbFactory, NullLogger<DatabaseSeederService>.Instance);
            seeder.EnsureDatabaseSeeded();
        }

        public void Dispose()
        {
            if (File.Exists(_dbPath))
            {
                try { File.Delete(_dbPath); } catch { }
            }
        }

        [Fact]
        [Requirement("AUTH-118", "AUTH", RequirementType.Positive, "AppKey table contains LastUsedAt column and repository updates and queries it accurately.")]
        public async Task AppKeyRepository_UpdatesAndReads_LastUsedAt()
        {
            var repo = new DatabaseRepository(_dbFactory);
            var now = DateTime.UtcNow;

            using (var conn = _dbFactory.CreateConnection())
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO AppKeys (Id, Name, Username, OwnerSid, KeyType, KeyPrefix, EncryptedKey, ScopesJson, CreatedAt)
                    VALUES ('key1', 'Test Key', 'alice', 'sid1', 'personal', 'mcp-usr-abc', 'hash123', '[""all""]', @CreatedAt)",
                    new { CreatedAt = now });
            }

            var before = await repo.GetAppKeyByIdAsync("key1");
            Assert.NotNull(before);
            Assert.Null(before.LastUsedAt);

            var lastUsed = DateTime.UtcNow.AddMinutes(5);
            await repo.UpdateLastUsedAsync("key1", lastUsed);

            var after = await repo.GetAppKeyByIdAsync("key1");
            Assert.NotNull(after);
            Assert.NotNull(after.LastUsedAt);
            Assert.Equal(lastUsed.ToString("yyyy-MM-dd HH:mm:ss"), after.LastUsedAt.Value.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        [Fact]
        [Requirement("AUTH-119", "AUTH", RequirementType.Positive, "OAuthClients table contains LastUsedAt column and repository updates and queries it accurately.")]
        public async Task OAuthClientRepository_UpdatesAndReads_LastUsedAt()
        {
            var repo = new DatabaseRepository(_dbFactory);
            var client = new OAuthClient
            {
                ClientId = "client1",
                ClientName = "Test Client",
                ClientType = "confidential",
                ClientSecretHash = "secrethash",
                CreatedAt = DateTime.UtcNow
            };

            await repo.SaveOAuthClientAsync(client);

            var before = await repo.GetOAuthClientByIdAsync("client1");
            Assert.NotNull(before);
            Assert.Null(before.LastUsedAt);

            var lastUsed = DateTime.UtcNow.AddMinutes(10);
            await repo.UpdateLastUsedAsync("client1", lastUsed);

            var after = await repo.GetOAuthClientByIdAsync("client1");
            Assert.NotNull(after);
            Assert.NotNull(after.LastUsedAt);
            Assert.Equal(lastUsed.ToString("yyyy-MM-dd HH:mm:ss"), after.LastUsedAt.Value.ToString("yyyy-MM-dd HH:mm:ss"));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ModelContextGateway.slnx --filter "FullyQualifiedName~AppKeyLastUsedPersistenceTests"`
Expected: Compilation failure or missing member `LastUsedAt` / `UpdateLastUsedAsync`.

- [ ] **Step 3: Implement minimal entity, migration, and repository changes**

1. In `Components/AppKeys/AppKey.cs`, add:
   ```csharp
   public DateTime? LastUsedAt { get; set; }
   ```
2. In `Components/Clients/OAuthClient.cs`, add:
   ```csharp
   public DateTime? LastUsedAt { get; set; }
   ```
3. In `Infrastructure/Persistence/DatabaseSeederService.cs`:
   - In SQLite table creation: add `LastUsedAt DATETIME NULL` to `AppKeys` and `OAuthClients` table definitions.
   - In SQLite migration section: check `PRAGMA table_info(AppKeys)` and `PRAGMA table_info(OAuthClients)` for `LastUsedAt`, if missing execute `ALTER TABLE AppKeys ADD COLUMN LastUsedAt DATETIME NULL;` and `ALTER TABLE OAuthClients ADD COLUMN LastUsedAt DATETIME NULL;`.
   - In MySQL and MSSQL migrations: add corresponding `IF NOT EXISTS` column additions.
4. In `Infrastructure/Persistence/Repositories.cs`:
   - In `IAppKeyRepository`, add `Task UpdateLastUsedAsync(string id, DateTime lastUsedAt);`
   - In `IOAuthClientRepository`, add `Task UpdateLastUsedAsync(string clientId, DateTime lastUsedAt);`
   - In `DatabaseRepository`, implement `UpdateLastUsedAsync` for AppKeys and OAuthClients, and update SQL queries in `GetAppKeysAsync`, `GetAppKeyByIdAsync`, `GetOAuthClientsAsync`, and `GetOAuthClientByIdAsync` to include `LastUsedAt`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ModelContextGateway.slnx --filter "FullyQualifiedName~AppKeyLastUsedPersistenceTests"`
Expected: PASS (2 tests pass).

- [ ] **Step 5: Commit**

```bash
git add Components/AppKeys/AppKey.cs Components/Clients/OAuthClient.cs Infrastructure/Persistence/DatabaseSeederService.cs Infrastructure/Persistence/Repositories.cs ModelContextGateway.Tests/AppKeyLastUsedPersistenceTests.cs
git commit -m "feat(auth): add LastUsedAt column and repository updates for AppKeys and OAuthClients"
```

---

### Task 2: Activity Tracking Service & Throttled Request Recording

**Files:**
- Create: `Components/Activity/IActivityTracker.cs`
- Create: `Components/Activity/ActivityTracker.cs`
- Modify: `Extensions/ServiceCollectionExtensions.cs:90-120`
- Modify: `Middleware/AppKeyAuthenticationHandler.cs:60-140`
- Modify: `Components/Clients/AuthorizationController.cs:135-190`
- Create: `ModelContextGateway.Tests/ActivityTrackerTests.cs`

**Interfaces:**
- Consumes: `IAppKeyRepository`, `IOAuthClientRepository`, `ILogger<ActivityTracker>`
- Produces: `IActivityTracker.RecordAppKeyUsageAsync(string keyId)`, `IActivityTracker.RecordClientUsageAsync(string clientId)`

- [ ] **Step 1: Write failing tests for ActivityTracker throttling**

```csharp
// ModelContextGateway.Tests/ActivityTrackerTests.cs
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextGateway.Components.Activity;
using ModelContextGateway.Infrastructure.Persistence;
using ModelContextGateway.Tests.Attributes;
using Moq;
using Xunit;

namespace ModelContextGateway.Tests
{
    public class ActivityTrackerTests
    {
        [Fact]
        [Requirement("AUTH-120", "AUTH", RequirementType.Positive, "ActivityTracker throttles database updates to at most once per 60 seconds per key.")]
        public async Task RecordAppKeyUsage_DebouncesWithinWindow()
        {
            var appKeyRepoMock = new Mock<IAppKeyRepository>();
            var clientRepoMock = new Mock<IOAuthClientRepository>();

            var tracker = new ActivityTracker(
                appKeyRepoMock.Object,
                clientRepoMock.Object,
                NullLogger<ActivityTracker>.Instance,
                throttleWindow: TimeSpan.FromSeconds(60));

            // First call triggers DB update
            await tracker.RecordAppKeyUsageAsync("key-123");
            // Immediate subsequent calls within 60s should be debounced
            await tracker.RecordAppKeyUsageAsync("key-123");
            await tracker.RecordAppKeyUsageAsync("key-123");

            appKeyRepoMock.Verify(r => r.UpdateLastUsedAsync("key-123", It.IsAny<DateTime>()), Times.Once);

            // A different key triggers its own update
            await tracker.RecordAppKeyUsageAsync("key-456");
            appKeyRepoMock.Verify(r => r.UpdateLastUsedAsync("key-456", It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        [Requirement("AUTH-121", "AUTH", RequirementType.Positive, "ActivityTracker throttles OAuth client updates to at most once per 60 seconds per client.")]
        public async Task RecordClientUsage_DebouncesWithinWindow()
        {
            var appKeyRepoMock = new Mock<IAppKeyRepository>();
            var clientRepoMock = new Mock<IOAuthClientRepository>();

            var tracker = new ActivityTracker(
                appKeyRepoMock.Object,
                clientRepoMock.Object,
                NullLogger<ActivityTracker>.Instance,
                throttleWindow: TimeSpan.FromSeconds(60));

            await tracker.RecordClientUsageAsync("client-abc");
            await tracker.RecordClientUsageAsync("client-abc");

            clientRepoMock.Verify(r => r.UpdateLastUsedAsync("client-abc", It.IsAny<DateTime>()), Times.Once);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ModelContextGateway.slnx --filter "FullyQualifiedName~ActivityTrackerTests"`
Expected: FAIL (missing `IActivityTracker`).

- [ ] **Step 3: Implement ActivityTracker and wire into authentication pipeline**

1. Create `Components/Activity/IActivityTracker.cs`:
   ```csharp
   using System.Threading.Tasks;

   namespace ModelContextGateway.Components.Activity
   {
       public interface IActivityTracker
       {
           Task RecordAppKeyUsageAsync(string keyId);
           Task RecordClientUsageAsync(string clientId);
       }
   }
   ```
2. Create `Components/Activity/ActivityTracker.cs`:
   - Store `ConcurrentDictionary<string, DateTime> _lastRecordedTimes`.
   - Provide constructor with optional `TimeSpan throttleWindow = default` (defaults to `TimeSpan.FromSeconds(60)`).
   - In `RecordAppKeyUsageAsync`, check if `now - lastTime < _throttleWindow`. If not, update dictionary and fire `_appKeyRepo.UpdateLastUsedAsync(keyId, now)` in background or await safely.
   - In `RecordClientUsageAsync`, same logic with `_clientRepo.UpdateLastUsedAsync(clientId, now)`.
3. Register singleton in `Extensions/ServiceCollectionExtensions.cs`:
   ```csharp
   builder.Services.AddSingleton<IActivityTracker, ActivityTracker>();
   ```
4. In `Middleware/AppKeyAuthenticationHandler.cs`:
   - Inject `IActivityTracker`.
   - After `isValid` passes:
     ```csharp
     _ = _activityTracker.RecordAppKeyUsageAsync(appKey.Id);
     ```
5. In `Components/Clients/AuthorizationController.cs`:
   - Inject `IActivityTracker`.
   - In `Exchange()` after successful client authentication, call `_ = _activityTracker.RecordClientUsageAsync(client.ClientId);`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test ModelContextGateway.slnx --filter "FullyQualifiedName~ActivityTrackerTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Components/Activity/ Extensions/ServiceCollectionExtensions.cs Middleware/AppKeyAuthenticationHandler.cs Components/Clients/AuthorizationController.cs ModelContextGateway.Tests/ActivityTrackerTests.cs
git commit -m "feat(activity): implement throttled LastUsedAt activity tracking for AppKeys and OAuth clients"
```

---

### Task 3: AppKey & Client Secret Rotation Endpoints

**Files:**
- Modify: `Components/Clients/CredentialService.cs:10-50`
- Modify: `Components/AppKeys/AppKeysController.cs:310-360`
- Modify: `Components/Clients/ClientsController.cs:180-260`
- Create: `ModelContextGateway.Tests/AppKeyRotationControllerTests.cs`

**Interfaces:**
- Consumes: `ICredentialService.RotateCredentialAsync(string id)`, `IAppKeyRepository.GetAppKeyByIdAsync`, `IOAuthClientRepository`
- Produces: `POST /api/appkeys/{id}/rotate` -> `{ appKey, plaintextKey }`, `POST /api/clients/{id}/rotate-secret` -> `{ clientId, clientSecret }`

- [ ] **Step 1: Write failing tests for AppKey and Client secret rotation**

```csharp
// ModelContextGateway.Tests/AppKeyRotationControllerTests.cs
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextGateway.Components.AppKeys;
using ModelContextGateway.Components.Clients;
using ModelContextGateway.Infrastructure.Logging;
using ModelContextGateway.Infrastructure.Persistence;
using ModelContextGateway.Security;
using ModelContextGateway.Tests.Attributes;
using Moq;
using Xunit;

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
            services.AddSingleton(new CompositeIdentityProvider(Array.Empty<IAuthenticationProvider>()));
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
        [Requirement("AUTH-123", "GUARD", RequirementType.Guardrail, "RotateAppKey forbids non-owner non-admin user from rotating another user's key.")]
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
            services.AddSingleton(new CompositeIdentityProvider(Array.Empty<IAuthenticationProvider>()));
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ModelContextGateway.slnx --filter "FullyQualifiedName~AppKeyRotationControllerTests"`
Expected: FAIL (missing `RotateAppKey`).

- [ ] **Step 3: Implement credential rotation logic and controller endpoints**

1. In `Components/Clients/CredentialService.cs`:
   - Add to `ICredentialService`:
     ```csharp
     Task<(AppKey AppKey, string PlaintextKey)> RotateCredentialAsync(string id);
     ```
   - Implement `RotateCredentialAsync(string id)`:
     - Query existing AppKey by ID.
     - Generate new `selector` (8 Base62 chars) and `secret` (16 Base62 chars) with appropriate prefix.
     - Hash with SHA-256 for `EncryptedKey`.
     - Update `AppKeys SET KeyPrefix = @KeyPrefix, EncryptedKey = @EncryptedKey, LastUsedAt = NULL WHERE Id = @Id;`.
     - Return updated `(appKey, plaintextKey)`.
2. In `Components/AppKeys/AppKeysController.cs`:
   - Add `[HttpPost("{id}/rotate")]` `public async Task<IActionResult> RotateAppKey(string id)`.
   - Verify identity (owner or admin). If neither, return `Forbid()`.
   - Call `_credentialService.RotateCredentialAsync(id)`.
   - Log admin audit action `appkey.rotate`.
   - Return `Ok(new { appKey.Id, appKey.Name, appKey.Username, appKey.KeyType, appKey.KeyPrefix, PlaintextKey = plaintextKey, Scopes = DeserializeScopes(appKey.ScopesJson), appKey.ExpiresAt, appKey.CreatedAt, LastUsedAt = (DateTime?)null })`.
3. In `Components/Clients/ClientsController.cs`:
   - Add `[HttpPost("{id}/rotate-secret")]` `public async Task<IActionResult> RotateSecret(string id)`.
   - Verify caller is admin (or client creator).
   - Generate new 32-character Base62 secret, compute SHA-256 hash.
   - Update `OAuthClients SET ClientSecretHash = @Hash, LastUsedAt = NULL WHERE ClientId = @Id;`.
   - Log audit action `client.rotate_secret`.
   - Return `Ok(new { clientId = id, clientSecret = plaintextSecret })`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ModelContextGateway.slnx --filter "FullyQualifiedName~AppKeyRotationControllerTests"`
Expected: PASS (2 tests pass).

- [ ] **Step 5: Commit**

```bash
git add Components/Clients/CredentialService.cs Components/AppKeys/AppKeysController.cs Components/Clients/ClientsController.cs ModelContextGateway.Tests/AppKeyRotationControllerTests.cs
git commit -m "feat(auth): implement AppKey and OAuthClient secret rotation endpoints"
```

---

### Task 4: Frontend Store & Modal Implementation (`AppKeyRotateModal` & Config Generator)

**Files:**
- Modify: `frontend/src/stores/useAppKeyStore.ts`
- Modify: `frontend/src/stores/useClientStore.ts`
- Create: `frontend/src/components/clients/AppKeyRotateModal.tsx`
- Modify: `frontend/src/components/clients/AppKeyModal.tsx`
- Create: `frontend/src/test/components/AppKeyRotateModal.test.tsx`

**Interfaces:**
- Consumes: `useAppKeyStore.rotateAppKey(id)`, `showToast`
- Produces: `AppKeyRotateModal` component with 2-stage workflow: Warning -> Reveal & Config Generator (Claude Desktop, Cursor, Windsurf, generic JSON) with backdrop dismissal protection.

- [ ] **Step 1: Write failing tests for AppKeyRotateModal**

```typescript
// frontend/src/test/components/AppKeyRotateModal.test.tsx
import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import AppKeyRotateModal from '../../components/clients/AppKeyRotateModal';
import { useAppKeyStore } from '../../stores/useAppKeyStore';

vi.mock('../../stores/useAppKeyStore', () => ({
  useAppKeyStore: vi.fn(),
}));

describe('AppKeyRotateModal', () => {
  const mockRotateAppKey = vi.fn();
  const mockOnClose = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    (useAppKeyStore as any).mockReturnValue({
      rotateAppKey: mockRotateAppKey,
      isRotating: false,
    });
  });

  /**
   * @requirement UI-135
   * @category UI
   * @type PositiveFeature
   * @description Renders confirmation warning on open, confirms rotation, and displays new key with Config Generator.
   */
  it('renders confirmation warning, rotates key, and shows config generator', async () => {
    mockRotateAppKey.mockResolvedValueOnce({
      id: 'key-1',
      name: 'Personal CLI',
      keyPrefix: 'mcp-usr-new',
      plaintextKey: 'mcp-usr-new-secret456',
    });

    render(
      <AppKeyRotateModal
        isOpen={true}
        keyId="key-1"
        keyName="Personal CLI"
        onClose={mockOnClose}
      />
    );

    // Stage 1: Warning must be visible
    expect(screen.getByText(/Rotating this App Key will immediately invalidate/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /confirm & rotate key/i })).toBeInTheDocument();

    // Confirm rotation
    fireEvent.click(screen.getByRole('button', { name: /confirm & rotate key/i }));

    await waitFor(() => {
      expect(mockRotateAppKey).toHaveBeenCalledWith('key-1');
    });

    // Stage 2: Key & Config Generator visible
    expect(await screen.findByText('mcp-usr-new-secret456')).toBeInTheDocument();
    expect(screen.getByText(/Claude Desktop/i)).toBeInTheDocument();
    expect(screen.getByText(/Cursor/i)).toBeInTheDocument();

    // Close button dismisses modal
    const closeBtn = screen.getByRole('button', { name: /close/i });
    fireEvent.click(closeBtn);
    expect(mockOnClose).toHaveBeenCalled();
  });

  /**
   * @requirement UI-136
   * @category UI
   * @type FailClosedGuardrail
   * @description Disables backdrop click dismissal on the key rotation modal to protect plaintext credentials.
   */
  it('prevents backdrop click dismissal', () => {
    render(
      <AppKeyRotateModal
        isOpen={true}
        keyId="key-1"
        keyName="Personal CLI"
        onClose={mockOnClose}
      />
    );

    const backdrop = screen.getByTestId('modal-backdrop');
    fireEvent.click(backdrop);

    expect(mockOnClose).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm --prefix frontend test frontend/src/test/components/AppKeyRotateModal.test.tsx`
Expected: FAIL (missing component `AppKeyRotateModal`).

- [ ] **Step 3: Implement stores and modal components**

1. In `frontend/src/stores/useAppKeyStore.ts`:
   - Add `LastUsedAt?: string | null;` to `AppKey` interface.
   - Add `rotateAppKey: (id: string) => Promise<{ id: string; name: string; keyPrefix: string; plaintextKey: string }>` to store. Calls `POST /api/appkeys/${id}/rotate`. Updates local list item's `keyPrefix` and `lastUsedAt = null`.
2. In `frontend/src/stores/useClientStore.ts`:
   - Add `lastUsedAt?: string | null;` to `RegisteredClient` interface.
   - Add `rotateClientSecret: (id: string) => Promise<{ clientId: string; clientSecret: string }>`. Calls `POST /api/clients/${id}/rotate-secret`.
3. Create `frontend/src/components/clients/AppKeyRotateModal.tsx`:
   - Accepts `isOpen`, `keyId`, `keyName`, `onClose`.
   - Stage state: `'warning' | 'revealed'`.
   - Backdrop click: `onClick={(e) => e.stopPropagation()}` (no dismissal on overlay click).
   - In `'warning'` stage: Alert icon, explicit warning text, "Cancel" and "Confirm & Rotate Key" buttons.
   - In `'revealed'` stage:
     - Plaintext key display box with copy button.
     - Tabs for config generation: `Claude Desktop`, `Cursor`, `Windsurf`, `Generic JSON`.
     - Code block showing formatted config snippet containing `${window.location.origin}/sse` and the full plaintext key in header `"X-App-Key": "${result.plaintextKey}"`.
     - "Copy Configuration" button with toast notification.
     - Primary "Close" button to safely exit.
4. Update `frontend/src/components/clients/AppKeyModal.tsx`:
   - In the creation success screen, embed the same tabbed Config Generator so users creating a key immediately get ready-to-use snippets with their plaintext key.
   - Disable backdrop click dismissal (`e.stopPropagation()`).

- [ ] **Step 4: Run test to verify it passes**

Run: `npm --prefix frontend test frontend/src/test/components/AppKeyRotateModal.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/stores/ frontend/src/components/clients/AppKeyRotateModal.tsx frontend/src/components/clients/AppKeyModal.tsx frontend/src/test/components/AppKeyRotateModal.test.tsx
git commit -m "feat(ui): implement AppKeyRotateModal with warning, config generator, and backdrop protection"
```

---

### Task 5: Frontend Cards Integration (`AppKeysCard` & `RegisteredClientsCard`)

**Files:**
- Modify: `frontend/src/components/clients/AppKeysCard.tsx`
- Modify: `frontend/src/components/clients/RegisteredClientsCard.tsx`
- Modify: `frontend/src/test/components/AppKeysCard.test.tsx`
- Modify: `frontend/src/test/components/RegisteredClientsCard.test.tsx`

**Interfaces:**
- Consumes: `AppKey.lastUsedAt`, `RegisteredClient.lastUsedAt`, `AppKeyRotateModal`
- Produces: Updated tables showing "Last Used" column, "Rotate Key" button replacing old "Config" button, and "Rotate Secret" for OAuth clients.

- [ ] **Step 1: Write failing tests for AppKeysCard and RegisteredClientsCard updates**

In `frontend/src/test/components/AppKeysCard.test.tsx`:
Add test for "Last Used" header and column rendering (e.g. `Never` or relative date), and clicking "Rotate" opens `AppKeyRotateModal`.
```typescript
/**
 * @requirement UI-137
 * @category UI
 * @type PositiveFeature
 * @description Renders Last Used column and replaces static config button with Rotate Key action opening rotation modal.
 */
it('renders Last Used column and triggers Rotate Key modal', () => { ... });
```

In `frontend/src/test/components/RegisteredClientsCard.test.tsx`:
Add test for "Last Used" column and "Rotate Secret" action.

- [ ] **Step 2: Run test to verify it fails**

Run: `npm --prefix frontend test frontend/src/test/components/AppKeysCard.test.tsx`
Expected: FAIL.

- [ ] **Step 3: Implement changes in AppKeysCard and RegisteredClientsCard**

1. In `frontend/src/components/clients/AppKeysCard.tsx`:
   - Add `<th>Last Used</th>` table header before `Actions`.
   - Render `<td>` with formatted relative time or `<span className="badge badge-secondary">Never</span>` if null.
   - Replace old `copyConfigSnippet` button with:
     ```tsx
     <button
       className="btn btn-secondary btn-sm"
       data-testid="btn-rotate-key"
       onClick={() => openRotateModal(key.id, key.name)}
       title="Rotate Key & Generate Config"
     >
       <i className="fa-solid fa-arrows-rotate"></i> Rotate
     </button>
     ```
   - Render `<AppKeyRotateModal ... />`.
2. In `frontend/src/components/clients/RegisteredClientsCard.tsx`:
   - Add `<th>Last Used</th>` table header before `Actions`.
   - Render `<td>` with formatted relative time or `Never`.
   - Add "Rotate Secret" button in actions column.

- [ ] **Step 4: Run test to verify it passes**

Run: `npm --prefix frontend test frontend/src/test/components/AppKeysCard.test.tsx frontend/src/test/components/RegisteredClientsCard.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/components/clients/AppKeysCard.tsx frontend/src/components/clients/RegisteredClientsCard.tsx frontend/src/test/components/AppKeysCard.test.tsx frontend/src/test/components/RegisteredClientsCard.test.tsx
git commit -m "feat(ui): add Last Used column and replace static config button with Rotate Key action"
```

---

### Task 6: Version Bump, Catalog Generator & Full Verification

**Files:**
- Modify: `ModelContextGateway.csproj:9-11` (`5.20.0`)
- Modify: `frontend/src/stores/useUserStore.ts:16` (`5.20.0`)
- Modify: `CHANGELOG.md`
- Modify: `README.md`
- Run: `dotnet run --project scripts/CatalogGenerator`
- Run: `dotnet test ModelContextGateway.slnx`
- Run: `npm --prefix frontend test`

**Interfaces:**
- Consumes: All updated tests with requirement annotations
- Produces: Updated requirements catalog (`docs/software-requirements-and-test-catalog.md`, `docs/requirements-catalog.json`), verified zero-drift.

- [ ] **Step 1: Bump version numbers across the 4 mandatory files**
  - Update `ModelContextGateway.csproj` to `<Version>5.20.0</Version>`, `<AssemblyVersion>5.20.0</AssemblyVersion>`, `<FileVersion>5.20.0</FileVersion>`.
  - Update fallback version in `frontend/src/stores/useUserStore.ts` to `'5.20.0'`.
  - Add `5.20.0` entry to `CHANGELOG.md`.
  - Update top-5 release table in `README.md`.

- [ ] **Step 2: Regenerate and verify requirements catalog**
  Run: `dotnet run --project scripts/CatalogGenerator`
  Run: `dotnet run --project scripts/CatalogGenerator -- --verify-only`
  Expected: Success with 0 drift.

- [ ] **Step 3: Run full backend and frontend test suites**
  Run: `dotnet test ModelContextGateway.slnx`
  Run: `npm --prefix frontend test`
  Expected: All tests pass.

- [ ] **Step 4: Commit**
```bash
git add ModelContextGateway.csproj frontend/src/stores/useUserStore.ts CHANGELOG.md README.md docs/
git commit -m "chore(release): bump version to 5.20.0 and update requirements catalog"
```
