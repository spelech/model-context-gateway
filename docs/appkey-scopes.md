# AppKey Scopes & Authorization Guide

This document explains the **AppKey Scoping and Authorization Engine** in the **Model Context Gateway (MCG)**. It covers scope syntax, normalization rules, pipeline stages, capability matrices, key lifecycle management, and least-privilege configuration recipes.

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

The Model Context Gateway (MCG) aggregates Model Context Protocol (MCP) servers (Docker, Home Assistant, Plex, databases) into a single secure endpoint.

External callers authenticate through two methods:
- **Interactive SSO / Forward-Auth Sessions**: Web users authenticate through reverse-proxy headers (`Remote-User`, `Remote-Groups`, `Remote-User-Sid`) or Active Directory Windows SIDs.
- **Machine Clients & Automated Agents (AppKeys)**: IDEs (Cursor, VS Code), autonomous coding agents (Claude Desktop, OpenClaw, Antigravity CLI), and CI/CD pipelines authenticate with high-entropy **AppKeys** (`mcp-*-*-*`).

### Core Concepts for Beginners

- **AppKey (Bearer Token)**: A high-entropy secret string. A client sends this key with HTTP requests. Whoever holds ("bears") the key receives access.
- **Scope**: A permission rule bound to a key. Scopes limit which servers, categories, tools, or resources an AppKey can access.
- **Reverse Proxy**: A server that sits in front of the gateway. It authenticates users with Single Sign-On (SSO) and passes user identity headers to the gateway.

### Defense-in-Depth Model

The gateway enforces four concentric security boundaries:

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

AppKeys store assigned scopes in a JSON array (`ScopesJson`). Scopes define the outer permission perimeter for the token.

### Scope Grammar & Taxonomy

| Scope Pattern | Type | Description | Example |
| :--- | :--- | :--- | :--- |
| `*`<br>`all`<br>`mcp_client` | **Global Wildcard** | Grants access to all capabilities across all registered backend servers. | `"*"` |
| `server:<serverId>`<br>`<serverId>` | **Server-Level** | Grants access to all tools, prompts, resources, and templates on the specified server. | `"server:ha"`<br>`"docker"` |
| `category:<name>`<br>`group:<name>` | **Category-Level** | Dynamically authorizes all servers classified under the given category name. | `"category:smarthome"`<br>`"group:infrastructure"` |
| `tool:<toolName>` | **Tool Capability** | Grants access to a specific namespaced tool or native router tool. | `"tool:ha__turn_on"`<br>`"tool:docker__ps"` |
| `prompt:<promptName>` | **Prompt Capability** | Grants access to a specific namespaced prompt template. | `"prompt:ha__diagnose_device"` |
| `resource:<uri>` | **Resource Capability** | Grants access to a specific virtualized resource URI. | `"resource:mcp://ha/states"`<br>`"resource:router://status"` |
| `resource_template:<uri>`<br>`template:<uri>` | **Template Capability** | Grants access to a specific resource URI template. | `"resource_template:mcp://ha/sensor/{id}"` |
| `completion:<target>` | **Completion Capability** | Grants access to auto-completion references for prompts or resource templates. | `"completion:ha__summary"` |

### Normalization & Parsing Rules

The scope engine in `ClientSession.Authorization.cs` runs these normalization steps:

1. **Trimming & Lowercase Conversion**: The engine trims whitespace from all scope strings and converts characters to lowercase (`s.Trim().ToLowerInvariant()`).
2. **Server ID Extraction**: The engine extracts the root `serverId` across multiple URI formats:
   - `mcp://{serverId}/{path}` $\rightarrow$ `{serverId}` (e.g. `mcp://ha/states` $\rightarrow$ `ha`)
   - `logs://{serverId}/{path}` $\rightarrow$ `{serverId}`
   - `router://{path}` $\rightarrow$ `router`
   - `server:{serverId}` $\rightarrow$ `{serverId}`
   - `{serverId}__{toolName}` $\rightarrow$ `{serverId}` (e.g. `docker__list_containers` $\rightarrow$ `docker`)
   - Native prefixes: `plex_*` $\rightarrow$ `plex`, `seerr_*` $\rightarrow$ `seerr`
3. **Meta-Mode Built-In Passthrough**: Discovery and execution tools (`search_tools` and `execute_tool`) can always run. When `execute_tool` runs, the engine checks the target tool against caller scopes and RBAC rules.
4. **Dynamic Category Resolution**:
   - For `category:<name>` and `group:<name>` scopes, the gateway reads server categories from the database (`SELECT Categories FROM Servers WHERE Id = @Id`).
   - Categories can be formatted as JSON arrays or comma-separated lists.
   - When an administrator updates a category tag on a server, all category-scoped AppKeys update their access immediately.
5. **Scope Creation Validation**:
   - When non-admin users create AppKeys via `POST /api/appkeys` or `POST /api/clients`, the gateway verifies each `category:<name>` scope against registered categories.
   - If a category is empty or unknown, the gateway returns `400 Bad Request`.
   - Administrators can pre-provision keys for future categories.

---

## 3. Multi-Stage Authorization Pipeline

Every incoming MCP request undergoes deterministic evaluation through `IsUserAuthorizedAsync(requestMethod, targetId, httpContext)`:

### Stage 1: AppKey Scope Validation
If the request authenticates with an AppKey (`context.Items["AppKeyUsed"] == true`):
1. Parse the JSON scope list from `context.Items["AppKeyScopes"]`. If the JSON is missing or malformed, the pipeline fails closed and denies access.
2. Compare the requested target with the scope rules:
   - Wildcard (`*`, `all`, `mcp_client`) $\rightarrow$ Proceed to Stage 2.
   - `server:{serverId}` or `{serverId}` $\rightarrow$ Proceed to Stage 2.
   - `category:{category}` or `group:{category}` where the server belongs to that category $\rightarrow$ Proceed to Stage 2.
   - Specific `tool:{targetId}`, `prompt:{targetId}`, `resource:{targetId}`, `template:{targetId}`, `completion:{targetId}` $\rightarrow$ Proceed to Stage 2.
3. If no scope matches the target, the gateway logs a warning and returns `403 Forbidden`.

### Stage 2: Identity Resolution & Group Mapping
The gateway resolves caller identity into a `UserIdentityContext`:
1. Claims and headers supply `Username`, `GroupNames` (from `Remote-Groups`, `roles`, `groups`), and `Sids` (from `Remote-User-Sid`, `Sid`, `GroupSid`).
2. The gateway queries the `GroupMappings` database table for external identifiers (`ExternalId IN @ExternalIds`).
3. Mapped `InternalGroup` entries join the active group set for the user.

### Stage 3: Administrative SID Bypass
The gateway checks whether the caller is an administrator:
- `SecurityValidationHelper.IsAdmin(identity, config)` inspects `identity.AllSids`.
- If the identity contains the administrative SID in `Admin:GroupSid` (default: `S-1-5-32-544`) or the `full_admin` claim, the gateway grants access immediately.
- Role names alone (such as `Administrator` in `ClaimTypes.Role`) do not grant bypass without a verified SID or group mapping.

### Stage 4: RBAC Policy Evaluation (Fail-Closed)
For non-admin callers, the gateway checks database access policies against the target:
1. Generates target keys:
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
2. **Deny Precedence**: The gateway checks for explicit deny rules (`IsAllowed = 0`) where `TargetId IN @TargetKeys AND RequiredGroup IN @UserGroups`. If any explicit deny exists, access is **DENIED**.
3. **Allow Matching**: The gateway checks for explicit allow rules (`IsAllowed = 1`) where `TargetId IN @TargetKeys AND RequiredGroup IN @UserGroups`. If at least one matching allow policy exists, access is **GRANTED**.
4. **Default Deny (Fail-Closed)**: If no policy matches the target and user groups, access is **DENIED**.

### Creator Ownership Decoupling
When an administrator creates an AppKey or Machine Client for another user:
- The gateway sets `OwnerSid` to the target user's SID or leaves it empty (`""`).
- The gateway removes the administrator's SID (`S-1-5-32-544`) from the key.
- Machine tokens never inherit administrator permissions or access to `AdminPolicy` endpoints (`/api/*`).

---

## 4. Capability Authorization Matrix

This table shows how each MCP method and router capability maps to scopes, RBAC evaluation keys, and filtering behavior:

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

Model Context Gateway (MCG) uses token hashing, high-entropy selectors, and constant-time authentication to protect machine credentials:

### 1. Token Structure & Entropy Specification

Generated AppKeys follow a structured, multi-segment format:

$$\text{Token Format} = \underbrace{\texttt{mcp}}_{\text{Scheme}}-\underbrace{\texttt{\{scopeSlug\}}}_{\text{Scope Hint}}-\underbrace{\texttt{\{selector\}}_{32\text{ hex}}}_{\text{128-bit Selector}}-\underbrace{\texttt{\{secret\}}_{64\text{ hex}}}_{\text{256-bit CSPRNG Secret}}$$

- **Scope Slug**: Derived from the primary assigned scope (`global`, `server`, `group`, or `tool`).
- **Selector (128-bit entropy / 16 bytes)**: Generated using cryptographically secure random number generation (`RandomNumberGenerator.GetBytes`). The database indexes this prefix for fast lookup ($O(1)$).
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

- **One-Way SHA-256 Hash**: The database stores only the SHA-256 hash digest of the key in the `EncryptedKey` column.
- **One-Time Secret Presentation**: The API returns the plaintext key **exactly once** upon creation. The gateway never saves plaintext keys. You cannot recover a lost key.
- **Sanitized Management APIs**: `GET /api/appkeys` sanitizes the response. It returns only `Id`, `Name`, `Username`, `KeyPrefix`, `Scopes`, `ExpiresAt`, and `CreatedAt`. It never exposes cipher hashes.

### 3. Constant-Time Authentication Flow

Incoming requests supply the token in HTTP headers or query parameters:
1. `Authorization: Bearer mcp-...`
2. `X-App-Key: mcp-...`
3. `X-Api-Key: mcp-...`
4. Query string: `?app_key=mcp-...`, `?api_key=mcp-...`, or `?key=mcp-...`

`AppKeyAuthenticationHandler` validates the credential:
1. Extracts the selector prefix (`mcp-{scopeSlug}-{selector}`).
2. Executes an indexed database query against `KeyPrefix`.
3. Computes the SHA-256 hash of the incoming token string.
4. Performs constant-time byte comparison using `CryptographicOperations.FixedTimeEquals`:
   ```csharp
   bool isValid = CryptographicOperations.FixedTimeEquals(
       Encoding.UTF8.GetBytes(appKey.EncryptedKey.ToLowerInvariant()),
       Encoding.UTF8.GetBytes(computedHash)
   );
   ```
   This prevents timing attacks.
5. Verifies the `ExpiresAt` timestamp against UTC time.
6. Attaches `ClaimsPrincipal` with `ClaimTypes.Name` and role `McpClient`. Sets `HttpContext.Items["AppKeyUsed"] = true` and `HttpContext.Items["AppKeyScopes"] = appKey.ScopesJson`.

### 4. Quotas, Expiration & Revocation

- **Key Quotas**: Configured in the `Settings` table (`GlobalMaxKeys`, default 0; `UserMaxKeys`, default 0; `0` = Unlimited). Non-admin users who exceed an administrator-configured limit receive `400 Bad Request`.
- **Expiration**: Keys support optional expiration (`ExpiresInDays`). Expired keys fail authentication with an audited `App Key has expired` error.
- **Revocation**: You can revoke keys via `DELETE /api/appkeys/{id}` or `DELETE /api/clients/{id}` (`sp_DeleteAppKey`). Revocations take effect immediately.
- **Audit Logging**: The gateway records key creations, revocations, and authentication failures in the audit log database table.

---

## 6. Least-Privilege Personas & Configuration Recipes

Use these configurations to apply least-privilege access:

### Persona 1: Read-Only Discovery Agent
Use this setup for documentation bots, indexers, or status monitors. The key allows inspecting server metadata and reading logs without running operational tools.

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
Use this setup for a developer working only on Docker infrastructure. The key denies access to Home Assistant or Media servers.

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
Use this setup for an agent that manages smart home hardware. The key uses a dynamic category scope. Any future server tagged with `smarthome` becomes available immediately.

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
Use this setup for an agent that needs all media servers and a single Docker inspection tool (`docker__ps`). The key forbids destructive commands.

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
Use this setup for scheduled cluster maintenance jobs in CI/CD. The key grants access across all servers and tools.

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
| `401 Unauthorized`<br>`Invalid App Key prefix.` | The key format is invalid or does not match any registered `KeyPrefix` in the database. | Copy the full token without spaces. Keep the `mcp-*` prefix intact. |
| `401 Unauthorized`<br>`Invalid App Key.` | The selector matched a database row, but the secret portion failed constant-time SHA-256 verification. | The key secret contains a typing error. Create a new key. |
| `401 Unauthorized`<br>`App Key has expired.` | The `ExpiresAt` date is in the past. | Revoke the expired key. Create a new key with a future expiration date. |
| `403 Forbidden`<br>`AppKey rejected: requested target '{target}' is outside the key's allowed scopes` | The key is valid, but its `Scopes` array does not include the requested target. | Update client settings to stay within scope, or create a key with the needed `server:<id>` or `category:<cat>` scope. |
| `403 Forbidden`<br>`User does not have permission to execute tool '{tool}'` | The AppKey scope allowed the target, but RBAC policies deny access or lack an Allow rule. | Open **Policy** in the Dashboard. Add an Allow rule for the caller group. |
| `400 Bad Request`<br>`Category '{cat}' does not exist among registered servers.` | A non-admin tried to create an AppKey with an unknown category scope. | Register a server with that category first, or ask an administrator to create the key. |

### Real-Time Diagnostics & Auditing

1. **Invocation Audit Logs**: The gateway records every execution in the `AuditLogs` table with username, server, method, run time, status code, and sanitized parameters.
2. **Dashboard Logs Console**: Open the **Logs** tab in the Web Dashboard to view real-time color-coded invocation traces, request IDs, and security verdicts.
3. **Interactive Test Bench**: Open the **Test Bench** view in the Web Dashboard to run test calls and inspect response headers and status codes.
