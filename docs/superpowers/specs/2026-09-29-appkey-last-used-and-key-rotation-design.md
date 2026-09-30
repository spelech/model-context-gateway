# Design Specification: AppKey & OAuth Client Last-Used Tracking and Safe Key Rotation

**Date:** 2026-09-29  
**Status:** Approved by User  
**Scope:** Backend and Frontend components for Model Context Gateway (MCG) to track credential utilization and enable safe AppKey / OAuth Client secret rotation with an integrated configuration generator.

---

## 1. Problem Statement & Motivation

1. **Lack of Usage Visibility**: Neither AppKeys nor OAuth Client registrations record when they were last used. Administrators and users cannot distinguish between active credentials and abandoned or obsolete keys.
2. **Key Recovery & Broken Config Snippets**: Credentials are encrypted at rest via one-way cryptographic SHA-256 hashes for zero-knowledge security. Users cannot recover lost plaintext keys. The existing UI "Config" button copied a placeholder string (`${keyPrefix}...[YOUR_FULL_KEY]`), which caused confusion and provided non-functional configurations.
3. **Accidental Dismissal**: Key creation modals could be closed without saving the plaintext key, permanently losing access.

---

## 2. Goals & Non-Goals

### Goals
- Track `LastUsedAt` timestamps for both `AppKeys` and `OAuthClients` across SQLite, MySQL, and SQL Server databases.
- Throttle `LastUsedAt` database writes using an in-memory 60-second sliding debounce per key/client to eliminate write contention during high-frequency streaming or tool-calling workloads.
- Replace static, non-functional "Config" button in the AppKeys UI with a dedicated **"Rotate Key"** workflow.
- Implement an explicit confirmation warning before rotation explaining that existing clients will be disconnected.
- Upon successful rotation, generate and display the new plaintext credential once, alongside an interactive **Config Generator** pre-populated with the exact full key for standard IDEs (Claude Desktop, Cursor, Windsurf, generic MCP JSON).
- Prevent accidental dismissal of key display modals by disabling backdrop/overlay click-away and requiring explicit user acknowledgement ("Close / Done").
- Add "Last Used" column to both AppKeys and Registered OAuth Clients tables in the frontend dashboard.
- Provide a corresponding "Rotate Secret" workflow for Registered OAuth Clients.

### Non-Goals
- Reversible key encryption (AppKeys and OAuth secrets remain secured via one-way SHA-256 hashes to preserve zero-knowledge security at rest).
- Automatic scheduled key rotation without explicit user initiation.

---

## 3. Architecture & Data Flow

### 3.1 Database Schema Migrations

Automated migrations added to `DatabaseSeederService.cs`:
1. **`AppKeys` Table**:
   - `LastUsedAt DATETIME NULL`
   - Added via migration check for SQLite, MySQL, and Microsoft SQL Server.
2. **`OAuthClients` Table**:
   - `LastUsedAt DATETIME NULL`
   - Added via migration check for SQLite, MySQL, and Microsoft SQL Server.

### 3.2 Backend Implementation

#### Entity Updates
- `AppKey.cs`: Add `public DateTime? LastUsedAt { get; set; }`
- `OAuthClient.cs`: Add `public DateTime? LastUsedAt { get; set; }`

#### Repository & Persistence Layer
- `IAppKeyRepository` / `DatabaseRepository`:
  - Add `Task UpdateLastUsedAsync(string id, DateTime lastUsedAt)`
  - Update `GetAppKeysAsync` and `GetAppKeyByIdAsync` queries to include `LastUsedAt`.
- `IOAuthClientRepository` / `DatabaseRepository`:
  - Add `Task UpdateLastUsedAsync(string clientId, DateTime lastUsedAt)`
  - Update `GetOAuthClientsAsync` and `GetOAuthClientByIdAsync` queries to include `LastUsedAt`.

#### Activity Tracker & Throttling
- `IActivityTracker` interface and singleton implementation `ActivityTracker`:
  - Holds thread-safe in-memory cache: `ConcurrentDictionary<string, DateTime> _lastRecordedTimes`.
  - Method: `Task RecordAppKeyUsageAsync(string keyId, string keyPrefix)`
  - Method: `Task RecordClientUsageAsync(string clientId)`
  - Logic: Only issues database update if key/client has not been recorded within the last 60 seconds (`TimeSpan.FromSeconds(60)`). Executes DB write in background task without delaying HTTP/SSE response.
- Integrated into:
  - `AppKeyAuthenticationHandler.cs`: Records usage when AppKey authentication succeeds.
  - `AuthorizationController.cs`: Records usage when OAuth client exchanges credentials at `/connect/token` or `/connect/authorize`.

#### Rotation Endpoints
- **`POST /api/appkeys/{id}/rotate`**:
  - Requires user authentication.
  - Validates ownership (or admin privileges).
  - Calls `ICredentialService.RotateCredentialAsync(id)`.
  - Re-generates selector and secret, creates new Base62 key prefix and SHA-256 hash.
  - Updates DB with new `KeyPrefix`, `EncryptedKey`, and sets `LastUsedAt = null`.
  - Logs audit action: `appkey.rotate`.
  - Returns: `{ appKey, plaintextKey }`.
- **`POST /api/clients/{id}/rotate-secret`**:
  - Requires user authentication and admin privilege (or client ownership).
  - Generates new 32-character Base62 client secret, calculates SHA-256 hash.
  - Updates DB with new `ClientSecretHash`, and sets `LastUsedAt = null`.
  - Logs audit action: `client.rotate_secret`.
  - Returns: `{ clientId, clientSecret }`.

---

## 4. Frontend UI & UX

### 4.1 AppKeys Card (`AppKeysCard.tsx`)
- **"Last Used" Column**:
  - Added to the AppKeys table.
  - Formats relative time (e.g. `Just now`, `12m ago`, `2h ago`, `3d ago`) or displays a subtle `Never` badge if null.
- **"Rotate Key" Action**:
  - Replaces the old static `Config` button in the actions column.
  - Button label: `<i className="fa-solid fa-arrows-rotate"></i> Rotate`.

### 4.2 Key Rotation Modal (`AppKeyRotateModal.tsx`)
- **Stage 1 (Confirmation Warning)**:
  - Caution alert: *"Rotating this App Key will immediately invalidate the current credential. Any active clients, IDEs, or automated agents currently using this key will stop working until their configuration is updated."*
  - Actions: `Cancel` and `Confirm & Rotate Key` (warning/danger theme).
- **Stage 2 (Key Display & Config Generator)**:
  - Displays new full plaintext key in a highlighted copy block with a dedicated "Copy Key" button.
  - Integrated **Config Generator**:
    - Tabs for **Claude Desktop (`claude_desktop_config.json`)**, **Cursor (`settings.json`)**, **Windsurf**, and **Generic JSON (`mcp_config.json`)**.
    - Pre-populates the gateway endpoint URL (`window.location.origin + '/sse'`) and the exact new `X-App-Key` header with the plaintext key.
    - One-click "Copy Configuration" button for each format.
  - **Click-Away Guard**:
    - Modal backdrop dismissal is disabled (`backdrop="static"` / click-outside ignored).
    - Requires clicking the explicit "Close / I Have Saved My Key" button.

### 4.3 AppKey Creation Parity (`AppKeyModal.tsx`)
- When creating a brand new key, embed the same interactive Config Generator in the success screen alongside the plaintext key.
- Disable backdrop dismissal so new keys cannot be accidentally lost before copying.

### 4.4 Registered OAuth Clients Parity (`RegisteredClientsCard.tsx`)
- Add **"Last Used"** column.
- Add **"Rotate Secret"** action button opening a confirmation modal with the same backdrop dismissal protection and single-reveal secret display.

---

## 5. Security & Performance Considerations

- **Zero-Knowledge at Rest**: Stored keys and secrets remain one-way SHA-256 hashes. Plaintext keys exist in memory only during creation/rotation and are returned strictly once.
- **Timing Attacks**: Token and hash comparisons continue using `CryptographicOperations.FixedTimeEquals`.
- **Database Write Load**: In-memory 60-second sliding debounce guarantees that sustained streaming or concurrent tool calling will not exceed 1 DB write per key per minute.
- **Audit Logging**: All rotation events are logged to the persistent audit log via `IAuditLogger`.

---

## 6. Testing & Quality Requirements

- **Backend xUnit Tests**:
  - Test `LastUsedAt` update and throttling in `AppKeyAuthenticationHandler`.
  - Test `AppKeysController.RotateAppKey` (positive rotation, permissions, audit log).
  - Test `ClientsController.RotateSecret` (positive rotation, permissions, audit log).
  - Test `DatabaseSeederService` schema migrations across SQLite.
- **Frontend Vitest Tests**:
  - Test `AppKeysCard` renders `Last Used` column and triggers `AppKeyRotateModal`.
  - Test `AppKeyRotateModal` displays warning, confirms rotation, renders key & config snippets, and rejects backdrop click-away.
  - Test `RegisteredClientsCard` renders `Last Used` column and rotation modal.
- **Traceability**: All new tests annotated with formal requirements taxonomy (`[Requirement("AUTH-...", ...)]` / `@requirement`).
- **Catalog Synchronization**: Update `CatalogGenerator` and verify zero drift.
