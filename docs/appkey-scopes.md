# AppKey Scopes & Authorization Guide

This document details the **AppKey Scoping and Authorization Engine** in the **Model Context Gateway (MCG)**, covering scope grammar, normalization rules, the multi-stage authorization pipeline, capability enforcement matrices, cryptographic key lifecycle management, and least-privilege configuration recipes.

---

## 📑 Table of Contents
1. [Overview & Security Architecture](#1-overview-security-architecture)
2. [Canonical Scope Syntax & Normalization](#2-canonical-scope-syntax-normalization)
3. [Multi-Stage Authorization Pipeline](#3-multi-stage-authorization-pipeline)
4. [Capability Authorization Matrix](#4-capability-authorization-matrix)
5. [Key Lifecycle & Cryptographic Architecture](#5-key-lifecycle-cryptographic-architecture)
6. [Least-Privilege Personas & Configuration Recipes](#6-least-privilege-personas-configuration-recipes)
7. [Diagnostics, Auditing & Troubleshooting](#7-diagnostics-auditing-troubleshooting)

---

## 1. Overview & Security Architecture

The Model Context Gateway (MCG) acts as an enterprise-grade gateway and semantic proxy that aggregates Model Context Protocol (MCP) servers (Docker, Home Assistant, Plex, Actual Budget, Databases, etc.) into a unified endpoint.

External callers authenticate via two primary vectors:
- **Interactive SSO / Forward-Auth Sessions**: Web UI and reverse-proxy users authenticate via OIDC/Proxy headers (`Remote-User`, `Remote-Groups`, `Remote-User-Sid`) or Active Directory Windows SIDs.
- **Machine Clients & Automated Agents (AppKeys)**: IDEs (Cursor, VS Code), autonomous coding agents (Claude Desktop, OpenClaw, Antigravity CLI), and CI/CD pipelines authenticate using high-entropy **AppKeys** (`mcp-*-*-*`).

### Defense-in-Depth Model

Authorization in Model Context Gateway (MCG) enforces four concentric security boundaries:

```
+-----------------------------------------------------------------------------------+
| 1. AppKey Scope Boundary (Fast-Path Key Filtering)                                |
|    Does the caller's AppKey allow the target server, category, or tool?           |
+-----------------------------------------------------------------------------------+
                                         │ Allowed
                                         ▼
+-----------------------------------------------------------------------------------+
| 2. Identity Resolution & Group Mapping                                            |
|    Resolve username, external SIDs, and map them to internal groups               |
+-----------------------------------------------------------------------------------+
                                         │
                                         ▼
+-----------------------------------------------------------------------------------+
| 3. Administrative Bypass Check                                                    |
|    Does the resolved principal possess the Admin SID (S-1-5-32-544 / Admin:GroupSid)?|
+-----------------------------------------------------------------------------------+
                    │ No                                  │ Yes (Admin Bypass)
                    ▼                                     ▼
+---------------------------------------------------+  +----------------------------+
| 4. RBAC Policy Evaluation (Fail-Closed)           |  | Authorized (200 OK)        |
|    - Explicit Deny overrides Allow                |  | Invocation Audit Logged    |
|    - Target/Group matching across categories      |  +----------------------------+
|    - Default: DENY                                |
+---------------------------------------------------+
                    │ Allowed
                    ▼
          +-------------------+
          | Authorized (200)  |
          +-------------------+
```

---

## 2. Canonical Scope Syntax & Normalization

AppKeys are bound to a JSON array of scope strings (`ScopesJson`). Scopes define the maximum outer capability perimeter granted to the bearer of that key.

### Scope Grammar & Taxonomy

| Scope Pattern | Type | Description | Example |
| :--- | :--- | :--- | :--- |
| `*`<br>`all`<br>`mcp_client` | **Global Wildcard** | Grants access to all capabilities across all registered backend servers. | `"*"` |
| `server:<serverId>`<br>`<serverId>` | **Server-Level** | Grants access to all tools, prompts, resources, and templates provided by the specified server. | `"server:ha"`<br>`"docker"` |
| `category:<name>`<br>`group:<name>` | **Category-Level** | Dynamically authorizes all servers classified under the given category name in the database. | `"category:smarthome"`<br>`"group:infrastructure"` |
| `tool:<toolName>` | **Tool Capability** | Grants access to a specific namespaced tool or native router tool. | `"tool:ha__turn_on"`<br>`"tool:docker__ps"` |
| `prompt:<promptName>` | **Prompt Capability** | Grants access to a specific namespaced prompt template. | `"prompt:ha__diagnose_device"` |
| `resource:<uri>` | **Resource Capability** | Grants access to a specific virtualized resource URI. | `"resource:mcp://ha/states"`<br>`"resource:router://status"` |
| `resource_template:<uri>`<br>`template:<uri>` | **Template Capability** | Grants access to a specific resource URI template. | `"resource_template:mcp://ha/sensor/{id}"` |
| `completion:<target>` | **Completion Capability** | Grants access to auto-completion references for prompts or resource templates. | `"completion:ha__summary"` |

### Normalization & Parsing Rules

The scope engine in `ClientSession.Authorization.cs` executes the following normalization steps during evaluation:

1. **Trimming & Case-Insensitivity**: All scope strings and target identifiers are trimmed and converted using invariant lower-case (`s.Trim().ToLowerInvariant()`).
2. **Server ID Extraction**: When evaluating targets, the router extracts the root `serverId` across multiple URI and delimiter syntaxes:
   - `mcp://{serverId}/{path}` $\rightarrow$ `{serverId}` (e.g. `mcp://ha/states` $\rightarrow$ `ha`)
   - `logs://{serverId}/{path}` $\rightarrow$ `{serverId}`
   - `router://{path}` $\rightarrow$ `router`
   - `server:{serverId}` $\rightarrow$ `{serverId}`
   - `{serverId}__{toolName}` $\rightarrow$ `{serverId}` (e.g. `docker__list_containers` $\rightarrow$ `docker`)
   - Native prefixes: `plex_*` $\rightarrow$ `plex`, `seerr_*` $\rightarrow$ `seerr`
3. **Meta-Mode Built-In Passthrough**: The router's built-in discovery and execution tools (`search_tools` and `execute_tool`) are permitted to execute the wrapper call; when `execute_tool` runs, the *inner* target tool is independently evaluated against the caller's scopes and RBAC.
4. **Dynamic Category Resolution**:
   - For `category:<name>` and `group:<name>` scopes, the router queries the `Servers` table in the database to retrieve the server's current categories (`SELECT Categories FROM Servers WHERE Id = @Id`).
   - Categories can be formatted as JSON arrays (e.g. `["smarthome","iot"]`) or comma-separated strings (`smarthome, iot`).
   - **Dynamic Membership**: If an administrator adds or removes a category tag on a server in the dashboard, access takes effect immediately across all existing category-scoped AppKeys without key re-issuance.
5. **Scope Creation Validation**:
   - When non-admin users create AppKeys via `POST /api/appkeys` or `POST /api/clients`, the router checks all `category:<name>` scopes against registered server categories in the database.
   - If an empty category (`category: `) or unknown category is specified, the request is rejected with `400 Bad Request`.
   - Administrators are permitted to pre-provision keys for future categories.

---

## 3. Multi-Stage Authorization Pipeline

Every incoming MCP request undergoes deterministic evaluation through `IsUserAuthorizedAsync(requestMethod, targetId, httpContext)`:

### Stage 1: AppKey Scope Validation
If the request authenticated using an AppKey (`context.Items["AppKeyUsed"] == true`):
1. Parse the JSON scope list from `context.Items["AppKeyScopes"]`. If the JSON is missing or malformed, the pipeline **fails closed** (returns `false`).
2. Match the requested target against the scope rules:
   - Matches wildcard (`*`, `all`, `mcp_client`) $\rightarrow$ Proceed to Stage 2.
   - Matches `server:{serverId}` or `{serverId}` $\rightarrow$ Proceed to Stage 2.
   - Matches `category:{category}` or `group:{category}` where the target server belongs to that category $\rightarrow$ Proceed to Stage 2.
   - Matches granular `tool:{targetId}`, `prompt:{targetId}`, `resource:{targetId}`, `template:{targetId}`, `completion:{targetId}` $\rightarrow$ Proceed to Stage 2.
3. If no scope matches the target, the request is rejected immediately with an audit log warning and `403 Forbidden` response.

### Stage 2: Identity Resolution & Group Mapping
The caller's identity is resolved into a unified `UserIdentityContext`:
1. Claims/headers provide `Username`, `GroupNames` (from `Remote-Groups`, `roles`, `groups`), and `Sids` (from `Remote-User-Sid`, `Sid`, `GroupSid`).
2. The router queries the `GroupMappings` table for all external identifiers (`ExternalId IN @ExternalIds`).
3. Mapped `InternalGroup` values are added to the user's active groups set.

### Stage 3: Administrative SID Bypass
The router checks if the caller is an administrator:
- `SecurityValidationHelper.IsAdmin(identity, config)` inspects `identity.AllSids`.
- If the principal contains the administrative SID configured in `Admin:GroupSid` (default: `S-1-5-32-544`) or the `full_admin` claim, access is **unconditionally granted**.
- **Important**: Role names alone (such as `Administrator` or `full_admin` in `ClaimTypes.Role`) do *not* grant bypass unless backed by the verified administrative SID or group mapping.

### Stage 4: RBAC Policy Evaluation (Fail-Closed)
For non-admin callers, access policies in the database are evaluated against the target:
1. Target keys are generated:
   - `{targetId}`
   - `tool:{targetId}`
   - `prompt:{targetId}`
   - `resource:{targetId}`
   - `resource_template:{targetId}`
   - `template:{targetId}`
   - `completion:{targetId}`
   - `server:{serverId}`
   - `category:{category}` (for all categories the server belongs to)
   - `group:{category}`
2. **Deny Precedence**: The router checks for explicit deny rules (`IsAllowed = 0`) where `TargetId IN @TargetKeys AND RequiredGroup IN @UserGroups`. If *any* explicit deny exists, access is **DENIED**.
3. **Allow Matching**: The router checks for explicit allow rules (`IsAllowed = 1`) where `TargetId IN @TargetKeys AND RequiredGroup IN @UserGroups`. If at least one matching allow policy exists, access is **GRANTED**.
4. **Default Deny (Fail-Closed)**: If no policy matches the target and user groups, the request is **DENIED**.

### Creator Ownership Decoupling
When an administrator provisions an AppKey or Machine Client on behalf of another user or automated service:
- The key's `OwnerSid` is set to the target user's resolved SID or left empty (`""`).
- The administrator's administrative SID (`S-1-5-32-544`) is **explicitly stripped** from the credential.
- Machine tokens authenticated with the generated secret never inherit administrative permissions or access to `AdminPolicy` endpoints (`/api/*`).

---

## 4. Capability Authorization Matrix

The table below outlines how each Model Context Protocol (MCP) method and Router capability maps to scopes, RBAC evaluation keys, and filtering behavior:

| MCP Method | Target Identifier Format | Allowed Scope Types | RBAC Target Keys Checked | List Filtering vs Invocation | Behavior on Unauthorized |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `tools/list` | *N/A (Aggregated list)* | `*`, `server:*`, `category:*`, `tool:*` | `tool:{toolName}`, `server:{serverId}`, `category:{cat}` | **Automatic List Filter**: Only permitted tools are returned to client. | Silent omission from returned tool catalog. |
| `tools/call` | `{serverId}__{toolName}` | `*`, `server:{id}`, `category:{cat}`, `tool:{name}` | `{name}`, `tool:{name}`, `server:{serverId}`, `category:{cat}` | **Invocation Guard**: Verifies caller permission before executing backend call. | Returns JSON-RPC error: `User does not have permission to execute tool '{name}'`. |
| `prompts/list` | *N/A (Aggregated list)* | `*`, `server:*`, `category:*`, `prompt:*` | `prompt:{promptName}`, `server:{serverId}`, `category:{cat}` | **Automatic List Filter**: Only permitted prompt templates are returned. | Silent omission from returned prompts array. |
| `prompts/get` | `{serverId}__{promptName}` | `*`, `server:{id}`, `category:{cat}`, `prompt:{name}` | `{name}`, `prompt:{name}`, `server:{serverId}`, `category:{cat}` | **Invocation Guard**: Verifies caller permission before rendering prompt. | Fails closed; returns empty or error response. |
| `resources/list` | *N/A (Aggregated list)* | `*`, `server:*`, `category:*`, `resource:*` | `resource:{uri}`, `server:{serverId}`, `category:{cat}` | **Automatic List Filter**: Only permitted static resource URIs are listed. | Silent omission from returned resources list. |
| `resources/read` | `mcp://{serverId}/{path}`<br>`router://{path}`<br>`logs://{serverId}/{path}` | `*`, `server:{id}`, `category:{cat}`, `resource:{uri}` | `{uri}`, `resource:{uri}`, `server:{serverId}`, `category:{cat}` | **Invocation Guard**: Checks read authorization on specific resource URI. | Throws `UnauthorizedAccessException`; returns error. |
| `resources/templates/list` | *N/A (Aggregated list)* | `*`, `server:*`, `category:*`, `resource_template:*` | `resource_template:{uri}`, `template:{uri}`, `server:{serverId}`, `category:{cat}` | **Automatic List Filter**: Only permitted URI templates are listed. | Silent omission from returned templates list. |
| `completion/complete` | Reference object: `ref/prompt` (`{name}`) or `ref/resource` (`{uri}`) | `*`, `server:{id}`, `category:{cat}`, `prompt:*`, `resource:*`, `completion:*` | `completion:{target}`, `prompt:{name}`, `resource:{uri}`, `server:{serverId}`, `category:{cat}` | **Invocation Guard & Value Filter**: Checks template permission and filters completion values by authorized server IDs. | Returns empty completion values array or throws error. |
| `search_tools` *(Meta-Mode)* | `"search_tools"` | Any active AppKey (wrapper); filters candidates | Evaluates candidate tool permissions before scoring | **Semantic Search Filter**: Candidate tools outside caller permissions are excluded from search results. | Excluded from returned search results. |
| `execute_tool` *(Meta-Mode)* | `"execute_tool"` (params specify inner target) | Any active AppKey (wrapper); inspects inner target | Evaluates inner tool: `{name}`, `tool:{name}`, `server:{serverId}`, `category:{cat}` | **Invocation Guard**: Enforces scope and RBAC on the inner target tool. | Returns JSON-RPC error: `User does not have permission to execute tool '{innerName}'`. |

---

## 5. Key Lifecycle & Cryptographic Architecture

Model Context Gateway (MCG) implements industry-standard token hashing, high-entropy selectors, and constant-time authentication to safeguard machine credentials:

### 1. Token Structure & Entropy Specification

Generated AppKeys follow a structured, multi-segment format:

$$\text{Token Format} = \underbrace{\texttt{mcp}}_{\text{Scheme}}-\underbrace{\texttt{\{scopeSlug\}}}_{\text{Scope Hint}}-\underbrace{\texttt{\{selector\}}_{32\text{ hex}}}_{\text{128-bit Selector}}-\underbrace{\texttt{\{secret\}}_{64\text{ hex}}}_{\text{256-bit CSPRNG Secret}}$$

- **Scope Slug**: Derived from the primary assigned scope (`global`, `server`, `group`, or `tool`).
- **Selector (128-bit entropy / 16 bytes)**: Generated using cryptographically secure random number generation (`RandomNumberGenerator.GetBytes`). Used for $O(1)$ database indexing and prefix lookup.
- **Secret (256-bit entropy / 32 bytes)**: High-entropy cryptographic secret.
- **Prefix Key**: Stored as `KeyPrefix` in the database: `mcp-{scopeSlug}-{selector}`.

### 2. Cryptographic Storage & One-Time Display

```
[Plaintext Key Generated] ────► SHA-256 Hash ────► EncryptedKey (64 hex characters stored in DB)
           │
           ├───────────────► Return to Client ONCE (JSON response)
           ▼
[Plaintext Discarded from Memory]
```

- **One-Way SHA-256 Hash**: The database stores only the one-way SHA-256 digest of the complete plaintext key in the `EncryptedKey` column.
- **One-Time Secret Presentation**: The plaintext key is returned in the API response **exactly once** upon creation. It is never stored in plaintext and cannot be recovered if lost.
- **Sanitized Management APIs**: `GET /api/appkeys` sanitizes the response, returning only `Id`, `Name`, `Username`, `KeyPrefix`, `Scopes`, `ExpiresAt`, and `CreatedAt`. Cipher hashes are never exposed over the API.

### 3. Constant-Time Authentication Flow

Incoming requests supply the token via HTTP headers or query parameters:
1. `Authorization: Bearer mcp-...`
2. `X-App-Key: mcp-...`
3. `X-Api-Key: mcp-...`
4. Query string: `?app_key=mcp-...`, `?api_key=mcp-...`, `?key=mcp-...`

`AppKeyAuthenticationHandler` validates the credential:
1. Extracts the selector prefix (`mcp-{scopeSlug}-{selector}`).
2. Executes an indexed database query against `KeyPrefix`.
3. Computes the SHA-256 hash of the incoming token string.
4. Executes constant-time byte comparison using `CryptographicOperations.FixedTimeEquals`:
   ```csharp
   bool isValid = CryptographicOperations.FixedTimeEquals(
       Encoding.UTF8.GetBytes(appKey.EncryptedKey.ToLowerInvariant()),
       Encoding.UTF8.GetBytes(computedHash)
   );
   ```
   *This completely eliminates side-channel timing attacks.*
5. Verifies `ExpiresAt` timestamp against UTC time.
6. Attaches `ClaimsPrincipal` with `ClaimTypes.Name`, role `McpClient`, and sets `HttpContext.Items["AppKeyUsed"] = true` and `HttpContext.Items["AppKeyScopes"] = appKey.ScopesJson`.

### 4. Quotas, Expiration & Revocation

- **Key Quotas**: Configured in `Settings` table (`GlobalMaxKeys`, default 0; `UserMaxKeys`, default 0, where `0` = Unlimited). Non-admin users who exceed an administrator-configured limit receive `400 Bad Request`.
- **Expiration**: Keys support optional expiration (`ExpiresInDays`). Expired keys fail authentication with an audited `App Key has expired` message.
- **Revocation**: Keys can be revoked via `DELETE /api/appkeys/{id}` or `DELETE /api/clients/{id}` (`sp_DeleteAppKey`). Revocations take effect immediately.
- **Audit Logging**: All administrative key creations, revocations, and authentication failures are recorded via `IAuditLogger.LogAdminActionAsync` and stored in the database audit log table.

---

## 6. Least-Privilege Personas & Configuration Recipes

Below are recommended configurations adhering strictly to the principle of least privilege:

### Persona 1: Read-Only Discovery Agent
*For documentation bots, vector indexers, or status aggregators that only need to inspect server metadata and read system logs without invoking operational tools.*

```json
{
  "name": "Documentation Indexer",
  "username": "doc_bot",
  "scopes": [
    "resource:router://status",
    "resource:logs://ha/today",
    "resource:logs://docker/today"
  ],
  "expiresInDays": 90
}
```

### Persona 2: Single-Server Developer IDE (Cursor / VS Code)
*For a developer working strictly on Docker infrastructure tools without access to Home Assistant or Media servers.*

**AppKey Request (`POST /api/appkeys`):**
```json
{
  "name": "Cursor IDE - DevOps",
  "scopes": [
    "server:docker"
  ],
  "expiresInDays": 365
}
```

**Client Configuration (`.cursor/mcp.json`):**
```json
{
  "mcpServers": {
    "mcg-docker": {
      "url": "http://localhost:8080/sse",
      "headers": {
        "X-App-Key": "mcp-server-a1b2c3d4e5f678901234567890abcdef-1234567890abcdef..."
      }
    }
  }
}
```

### Persona 3: Smart Home Automation Agent (Category-Scoped)
*For an agent managing smart home hardware. The key uses a dynamic category scope; any future server tagged with `smarthome` (e.g. Zigbee2MQTT, Homebox) becomes immediately accessible without updating the key.*

**AppKey Request (`POST /api/appkeys`):**
```json
{
  "name": "OpenClaw Home Assistant",
  "scopes": [
    "category:smarthome"
  ],
  "expiresInDays": 180
}
```

**Client Configuration (`claude_desktop_config.json`):**
```json
{
  "mcpServers": {
    "mcg-smarthome": {
      "command": "npx",
      "args": [
        "-y",
        "@modelcontextprotocol/client-sse",
        "http://localhost:8080/sse"
      ],
      "env": {
        "X_APP_KEY": "mcp-group-9876543210fedcba9876543210fedcba-fedcba9876543210..."
      }
    }
  }
}
```

### Persona 4: Fine-Grained Mixed-Capability Agent
*For an agent needing access to all Media servers plus a single pinpoint tool from Docker (`docker__ps`), but explicitly forbidden from invoking destructive commands.*

**AppKey Request (`POST /api/appkeys`):**
```json
{
  "name": "Media Agent with Container Inspection",
  "scopes": [
    "category:media",
    "tool:docker__ps"
  ],
  "expiresInDays": 30
}
```

### Persona 5: Administrative Automation Pipeline
*For scheduled cluster maintenance scripts running in CI/CD requiring access across all tools and servers.*

**AppKey Request (`POST /api/appkeys` with Admin Token):**
```json
{
  "name": "Cluster Maintenance CI Runner",
  "scopes": [
    "*"
  ],
  "expiresInDays": 30
}
```

---

## 7. Diagnostics, Auditing & Troubleshooting

### Common Error Responses & Resolution

| HTTP Status / Error Message | Root Cause | Remediation Step |
| :--- | :--- | :--- |
| `401 Unauthorized`<br>`Invalid App Key prefix.` | The key format is invalid or does not match any registered `KeyPrefix` in the database. | Verify that the full token (including `mcp-*` prefix) was copied without truncation or whitespace. |
| `401 Unauthorized`<br>`Invalid App Key.` | The selector matched a database record, but the secret portion failed constant-time SHA-256 verification. | The key secret has been mistyped or corrupted. Generate a new key. |
| `401 Unauthorized`<br>`App Key has expired.` | The key's `ExpiresAt` date is in the past. | Revoke the expired key and generate a fresh key with the desired expiration. |
| `403 Forbidden`<br>`AppKey rejected: requested target '{target}' is outside the key's allowed scopes` | The key is valid, but its assigned `Scopes` array does not include the requested server, category, or tool. | Update the client configuration to request within scope, or generate a key with the required `server:<id>` or `category:<cat>` scope. |
| `403 Forbidden`<br>`User does not have permission to execute tool '{tool}'` | The AppKey scope allowed the target, but the user's RBAC policies in the database have no matching Allow rule or an Explicit Deny rule. | In the Dashboard, open **Policy** on the target server/tool and ensure the caller's group is granted access. |
| `400 Bad Request`<br>`Category '{cat}' does not exist among registered servers.` | A non-admin attempted to create an AppKey with a non-existent category scope. | Register a server under that category first or create the key as an administrator. |

### Real-Time Diagnostics & Auditing

1. **Invocation Audit Logs**: Every execution is recorded in the `AuditLogs` table with `Username`, `ServerId`, `ItemName`, `RequestMethod`, `ExecutionTimeMs`, `StatusCode`, and sanitized payloads.
2. **Dashboard Logs Console**: Open the **Logs** tab in the Web Dashboard to view real-time color-coded invocation traces, request IDs, and security authorization verdicts.
3. **Interactive Test Bench**: Use the **Test Bench** view in the Web Dashboard to simulate tool executions and inspect response headers and authorization codes.
