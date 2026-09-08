---
name: mcg-admin
description: Use when configuring, managing, provisioning, or automating Model Context Gateway (MCG) settings, authentication providers (Authentik, Keycloak, Entra ID, Active Directory, Cloudflare Access), secret providers (HashiCorp Vault, AES Master Key, DPAPI), access policies, group mappings, backend servers, or client keys via the Admin MCP Server (/admin/sse or /mcg-admin/sse).
---

# Universal Admin MCP Automation Skill (`mcg-admin`)

## Overview

**Model Context Gateway (MCG)** includes a built-in, encrypted database-backed **Admin MCP Server** (`/admin/sse` or `/mcg-admin/sse`) exposing 10 consolidated tools. This skill equips autonomous AI coding agents and DevOps engineers to configure, manage, and verify any gateway deployment from a blank slate without manual UI interaction.

---

## Blank-Slate Safe Defaults

When `Model Context Gateway (MCG)` starts in a new environment, it initializes with secure out-of-the-box defaults:

| Component | Default Configuration | Notes |
| :--- | :--- | :--- |
| **Database** | SQLite (`./data/mcg.db`) | Zero external database dependencies required. Auto-seeded on startup. |
| **Master Key** | `./data/.master.key` (Auto-Generated) or `MCG_MASTER_KEY` / `MCG_MASTER_KEY_FILE` | Encrypts sensitive credentials at rest in the DB (AES-256-GCM). Auto-generated if unset. |
| **Admin AppKey** | `mcp-adm-` (Compact Base62) or `MCG_ADMIN_AUTH_KEY` / `MCG_ADMIN_KEY` | Scoped to `["all", "admin"]`. Seeded automatically in the database or configured via `MCG_ADMIN_AUTH_KEY` / `MCG_ADMIN_KEY`. Legacy fallback: `mcp-global-admin-default-cli-key-99`. |
| **Network Trust** | `127.0.0.1, ::1` (Loopback) | Configurable via `Admin:StandaloneAllowedNetworks` for LAN/CIDR subnets. |
| **Admin Endpoint** | `http://<host>:8080/admin/sse` or `/mcg-admin/sse` | MCP SSE transport for administrative JSON-RPC tool calling. |

---

## Workflow: 7-Phase Autonomous Administration

```mermaid
flowchart TD
    Connect["<b>Connect to /admin/sse</b><br><i>(Bearer Admin AppKey)</i>"]
    P1["<b>Phase 1: Gateway Diagnostics</b><br><code>manage_system: diagnostics</code>"]
    P2["<b>Phase 2: Secret Provider Setup</b><br><code>manage_providers: save_secret</code><br>Vault KV v2 & Built-in AES-256-GCM"]
    P3["<b>Phase 3: Auth Provider Setup</b><br><code>manage_providers: save_auth</code><br>Authentik, Keycloak, Entra ID, AD LDAPS, Standalone"]
    P4["<b>Phase 4: RBAC & Group Mappings</b><br><code>manage_group_mappings</code> & <code>manage_policies</code>"]
    P5["<b>Phase 5: Embeddings & Search</b><br><code>manage_settings: update</code><br>FastEmbed/ONNX Local & OpenAI/Ollama"]
    P6["<b>Phase 6: Backend Servers & Clients</b><br><code>manage_servers</code>, <code>manage_appkeys</code>, <code>manage_clients</code>"]
    P7["<b>Phase 7: End-to-End Verification</b><br><code>test_tool_call</code> & <code>manage_system: query_audit</code>"]

    Connect --> P1 --> P2 --> P3 --> P4 --> P5 --> P6 --> P7

    classDef initStyle fill:#161b22,stroke:#ff5f1f,stroke-width:2px,color:#fff;
    classDef phaseStyle fill:#0f2e1b,stroke:#00c853,stroke-width:1.5px,color:#fff;
    class Connect initStyle;
    class P1,P2,P3,P4,P5,P6,P7 phaseStyle;
```

---

## Phase 1: Gateway Connection & Diagnostics

### 1.1 Connect to Admin MCP Server
Configure your client with the gateway's `/admin/sse` (or `/mcg-admin/sse`) endpoint and the admin bearer token (e.g. compact `mcp-adm-` key):
```json
{
  "mcpServers": {
    "mcg-admin": {
      "url": "http://localhost:8080/admin/sse",
      "headers": {
        "Authorization": "Bearer mcp-adm-Xk9L2mPq-7vN3wZ8aB1cE4fG9"
      }
    }
  }
}
```

### 1.2 Inspect Gateway Status
Call `manage_system`:
```json
{
  "tool": "manage_system",
  "arguments": {
    "action": "diagnostics"
  }
}
```
Inspect current active sessions, OS version, process uptime, and memory usage.

---

## Phase 2: Secret Provider Configuration

Configure where backend API keys, tokens, and credentials are encrypted and stored.

### Option A: Built-in Master Key (AES-256-GCM / Default)
No extra provider needed. All backend credentials stored via `manage_servers` are automatically encrypted at rest using `MCG_MASTER_KEY`.

### Option B: HashiCorp Vault KV v2 (Token Auth)
```json
{
  "tool": "manage_providers",
  "arguments": {
    "action": "save_secret",
    "providerName": "HashiCorpVault",
    "displayName": "Enterprise Vault KV",
    "isEnabled": true,
    "configJson": "{"address":"https://vault.internal.corp:8200","authMethod":"token","token":"s.yourVaultToken","mountPath":"secret"}"
  }
}
```

### Option C: HashiCorp Vault KV v2 (AppRole Auth)
```json
{
  "tool": "manage_providers",
  "arguments": {
    "action": "save_secret",
    "providerName": "HashiCorpVault",
    "displayName": "Production AppRole Vault",
    "isEnabled": true,
    "configJson": "{"address":"https://vault.internal.corp:8200","authMethod":"approle","roleId":"11111111-2222-3333-4444-555555555555","secretId":"66666666-7777-8888-9999-000000000000","mountPath":"secret"}"
  }
}
```

### 2.1 Test Vault Connection
```json
{
  "tool": "manage_providers",
  "arguments": {
    "action": "test_vault",
    "address": "https://vault.internal.corp:8200",
    "authMethod": "approle",
    "roleId": "11111111-2222-3333-4444-555555555555",
    "secretId": "66666666-7777-8888-9999-000000000000"
  }
}
```

### 2.2 Dynamic Master Key Setting & Database Re-Encryption
When running on an auto-generated keyfile (`./data/.master.key`), administrators can set a permanent custom Master Key with atomic database re-encryption:
```json
{
  "tool": "manage_system",
  "arguments": {
    "action": "set_master_key",
    "newKey": "YourSecure32CharacterMasterKeyHere123"
  }
}
```

---

## Phase 3: Authentication Provider Configuration

Configure single sign-on, reverse proxy forward-auth, or enterprise directory integration.

### Option A: Authentik / Authelia / Forward-Auth (Reverse Proxy Headers)
```json
{
  "tool": "manage_providers",
  "arguments": {
    "action": "save_auth",
    "providerName": "HeaderAuth",
    "displayName": "Authentik Forward-Auth",
    "userHeader": "Remote-User",
    "groupsHeader": "Remote-Groups",
    "isEnabled": true,
    "configJson": "{"trustedProxies":["127.0.0.1","10.0.0.0/8","172.16.0.0/12","192.168.0.0/16"],"requireTrustedProxy":true}"
  }
}
```

### Option B: Keycloak / OIDC SSO
```json
{
  "tool": "manage_providers",
  "arguments": {
    "action": "save_auth",
    "providerName": "Keycloak",
    "displayName": "Corporate Keycloak Realm",
    "userHeader": "X-Forwarded-User",
    "groupsHeader": "X-Forwarded-Groups",
    "isEnabled": true,
    "configJson": "{"authority":"https://keycloak.internal.corp/realms/master","clientId":"mcg","requireHttps":true}"
  }
}
```

### Option C: Microsoft Entra ID (Azure AD)
```json
{
  "tool": "manage_providers",
  "arguments": {
    "action": "save_auth",
    "providerName": "EntraID",
    "displayName": "Microsoft Entra ID",
    "userHeader": "Remote-User",
    "groupsHeader": "Remote-Groups",
    "isEnabled": true,
    "configJson": "{"tenantId":"00000000-0000-0000-0000-000000000000","clientId":"11111111-1111-1111-1111-111111111111","groupClaim":"groups"}"
  }
}
```

### Option D: Active Directory / LDAP (LDAPS Port 636)
```json
{
  "tool": "manage_providers",
  "arguments": {
    "action": "save_auth",
    "providerName": "ActiveDirectory",
    "displayName": "Corporate Active Directory",
    "isEnabled": true,
    "configJson": "{"server":"dc01.internal.corp","port":636,"useSsl":true,"domain":"INTERNAL","baseDn":"DC=internal,DC=corp","bindDn":"CN=svc-mcg,OU=ServiceAccounts,DC=internal,DC=corp","bindPassword":"StrongPassword123!"}"
  }
}
```

### 3.1 Test LDAP Connection
```json
{
  "tool": "manage_providers",
  "arguments": {
    "action": "test_ldap",
    "server": "dc01.internal.corp",
    "port": 636,
    "useSsl": true,
    "bindDn": "CN=svc-mcg,OU=ServiceAccounts,DC=internal,DC=corp",
    "bindPassword": "StrongPassword123!"
  }
}
```

---

## Phase 4: RBAC, Group Mappings & Access Policies

### 4.1 Create Group Mappings
Map external SSO roles or Active Directory SIDs to internal router roles:
```json
{
  "tool": "manage_group_mappings",
  "arguments": {
    "action": "save",
    "externalId": "S-1-5-21-1234567890-123456789-123456789-512",
    "internalGroup": "full_admin"
  }
}
```

### 4.2 Create Target Access Policies
Restrict backend MCP servers or tools to specific roles:
```json
{
  "tool": "manage_policies",
  "arguments": {
    "action": "save",
    "targetId": "github",
    "requiredGroup": "developer",
    "isAllowed": true
  }
}
```

---

## Phase 5: Dynamic Embeddings & Gateway Settings

Configure semantic tool search embeddings (OpenAI, Azure, Ollama, ONNX):

```json
{
  "tool": "manage_settings",
  "arguments": {
    "action": "update",
    "dashboardTitle": "Model Context Gateway",
    "embeddingProvider": "OpenAI",
    "embeddingApiUrl": "https://api.openai.com/v1",
    "embeddingApiKey": "sk-proj-...",
    "embeddingApiModel": "text-embedding-3-small",
    "globalMaxKeys": 250,
    "userMaxKeys": 20
  }
}
```

For local Ollama embeddings:
```json
{
  "tool": "manage_settings",
  "arguments": {
    "action": "update",
    "embeddingProvider": "Ollama",
    "embeddingApiUrl": "http://ollama:11434/api/embeddings",
    "embeddingApiModel": "nomic-embed-text"
  }
}
```

---

## Phase 6: Backend Servers & Client AppKeys

### 6.1 Register Backend MCP Server
```json
{
  "tool": "manage_servers",
  "arguments": {
    "action": "create",
    "id": "docker",
    "displayName": "Docker Host Engine",
    "url": "http://docker-mcp:8000/sse",
    "type": "sse",
    "enabled": true,
    "hidden": false,
    "categories": ["infrastructure", "devops"]
  }
}
```

### 6.2 Issue Client AppKey for Developer
```json
{
  "tool": "manage_appkeys",
  "arguments": {
    "action": "create",
    "name": "Dev User Key",
    "username": "steve",
    "scopes": ["all"],
    "expiresInDays": 90
  }
}
```
Returns a compact ~32-character Base62 AppKey with semantic prefix (e.g. `mcp-glb-R4t8W1yU-9pM2nQ6sD8fH3jK5`, `mcp-adm-...`, `mcp-devops-...`, `mcp-usr-...`, `mcp-srv-...`).

---

## Phase 7: Verification & Diagnostics

### 7.1 Test Backend Tool Dispatch
Execute a tool directly through the gateway to confirm routing:
```json
{
  "tool": "test_tool_call",
  "arguments": {
    "serverId": "docker",
    "toolName": "list_containers",
    "arguments": {}
  }
}
```

### 7.2 Inspect System Audit Logs
```json
{
  "tool": "manage_system",
  "arguments": {
    "action": "query_audit",
    "take": 20
  }
}
```

---

## Tool Reference Matrix

| Tool Name | Key Actions | Description |
| :--- | :--- | :--- |
| `manage_servers` | `list`, `get`, `create`, `update`, `delete`, `toggle`, `reconnect`, `reconnect_all` | Manages backend MCP servers and connection status. |
| `manage_appkeys` | `list`, `get_limits`, `create`, `revoke` | Issues and audits developer/agent API keys. |
| `manage_clients` | `list`, `register`, `delete` | Manages dynamic OAuth2 client credentials. |
| `manage_policies` | `list`, `save`, `delete` | Configures fine-grained RBAC access policies. |
| `manage_group_mappings` | `list`, `save`, `delete` | Maps external SSO groups/SIDs to internal roles. |
| `manage_providers` | `list`, `save_secret`, `test_vault`, `save_auth`, `test_ldap` | Configures auth & secret providers with live connection tests. |
| `manage_settings` | `get`, `update` | Controls branding, quotas, and semantic embedding providers. |
| `manage_custom_files` | `list`, `get`, `save`, `delete` | Manages virtual prompt and resource JSON files. |
| `manage_system` | `diagnostics`, `get_logs`, `clear_logs`, `query_audit` | Gateway health metrics, logs, and security audit trail. |
| `test_tool_call` | *(default)* | Sends a live test payload to any backend server. |
