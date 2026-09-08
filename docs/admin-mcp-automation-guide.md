# Admin MCP Automation & Provider Configuration Guide

This guide provides instructions for developers, DevOps engineers, and AI coding agents. It explains how to configure and provision **Model Context Gateway (MCG)** from a clean setup.

---

## 🚀 1. Architecture & Safe Defaults

When **Model Context Gateway** starts in a clean environment without an existing database, it creates a safe default configuration:

```
+---------------------------------------------------------------------------------------+
|                                Model Context Gateway (MCG)                            |
+---------------------------------------------------------------------------------------+
|  Out-of-the-Box Safe Defaults:                                                        |
|  - Database: SQLite (./data/mcg.db auto-created & migrated)                           |
|  - Encryption: AES-256-GCM using MCG_MASTER_KEY                                       |
|  - Network Trust: Loopback only (127.0.0.1, ::1) via Admin:StandaloneAllowedNetworks  |
|  - Admin Key: mcp-adm-prod-bootstrap-token-99 (Owner: admin, Scopes: ["all"])        |
|  - Admin Endpoint: http://<host>:8080/admin/sse (JSON-RPC 2.0)                        |
+---------------------------------------------------------------------------------------+
                                           |
                                           v
+---------------------------------------------------------------------------------------+
|                                Autonomous Automation                                  |
|  - AI Agent Skill: .agents/skills/mcg-admin/SKILL.md                                  |
|  - Non-Interactive Scripts: cURL (Bash), PowerShell (Windows), Python                 |
|  - 10 Consolidated MCP Tools covering 100% of Gateway Admin Operations                |
+---------------------------------------------------------------------------------------+
```

### Safe Defaults Reference Matrix

| Parameter | Default Value | Description |
| :--- | :--- | :--- |
| **`DB_PROVIDER`** | `sqlite` | Default storage provider. Creates `./data/mcg.db` automatically. |
| **Master Encryption Key** | `./data/.master.key` (Auto-Generated) or `MCG_MASTER_KEY` / `MCG_MASTER_KEY_FILE` | Encrypts stored provider credentials and API tokens at rest with AES-256-GCM. |
| **`Admin:StandaloneAllowedNetworks`** | `127.0.0.1, ::1` | CIDR allowlist for admin endpoints when no external identity provider is configured. |
| **Default Admin Key** | Seeded Base62 token (`mcp-adm-...`) | Created in the database on first boot. Allows immediate administrative connection. |
| **`CORS_ALLOWED_ORIGINS`** | `http://localhost:3000, http://localhost:8080` | Allowed web browser origins for dashboard requests. |

---

## 🔑 2. Connecting to the Admin MCP Server

The Admin MCP Server listens on `/admin/sse` or `/mcg-admin/sse`. It receives messages on `/admin/message`.

### Bearer Tokens Explained
A bearer token is a secret security key. Send the token in the `Authorization: Bearer <token>` header to authenticate your requests.

### AI Agent Configuration (Claude, Cursor, Cline, Windsurf, Antigravity)

Add this block to your AI client configuration file:

```json
{
  "mcpServers": {
    "mcg-admin": {
      "url": "http://localhost:8080/admin/sse",
      "headers": {
        "Authorization": "Bearer mcp-adm-bootstrap-token-99"
      }
    }
  }
}
```

### JSON-RPC 2.0 Direct HTTP Dispatch

You can send tool execution requests directly with an HTTP `POST` request to `/admin`:

```bash
curl -X POST http://localhost:8080/admin \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer mcp-global-admin-default-cli-key-99" \
  -d '{
    "jsonrpc": "2.0",
    "id": "1",
    "method": "tools/call",
    "params": {
      "name": "manage_system",
      "arguments": {
        "action": "diagnostics"
      }
    }
  }'
```

---

## 🛠️ 3. Provider Configuration Cookbooks

Configure all identity and secret providers dynamically using the **`manage_providers`** tool.

### 3.1 Authentication Providers (`save_auth` & `test_ldap`)

#### A. Authentik / Authelia / Forward-Auth Reverse Proxy
Use this setup when the gateway runs behind a reverse proxy (such as Nginx, Traefik, or Caddy) that verifies user identity:

```json
{
  "name": "manage_providers",
  "arguments": {
    "action": "save_auth",
    "providerName": "HeaderAuth",
    "displayName": "Authentik SSO",
    "userHeader": "Remote-User",
    "groupsHeader": "Remote-Groups",
    "isEnabled": true,
    "configJson": "{\"trustedProxies\":[\"127.0.0.1\",\"10.0.0.0/8\",\"172.16.0.0/12\",\"192.168.0.0/16\"],\"requireTrustedProxy\":true}"
  }
}
```

#### B. Keycloak / OIDC Realm
```json
{
  "name": "manage_providers",
  "arguments": {
    "action": "save_auth",
    "providerName": "Keycloak",
    "displayName": "Keycloak Realm",
    "userHeader": "X-Forwarded-User",
    "groupsHeader": "X-Forwarded-Groups",
    "isEnabled": true,
    "configJson": "{\"authority\":\"https://keycloak.internal.corp/realms/master\",\"clientId\":\"mcg\",\"requireHttps\":true,\"groupClaim\":\"groups\"}"
  }
}
```

#### C. Microsoft Entra ID (Azure AD)
```json
{
  "name": "manage_providers",
  "arguments": {
    "action": "save_auth",
    "providerName": "EntraID",
    "displayName": "Microsoft Entra ID",
    "userHeader": "Remote-User",
    "groupsHeader": "Remote-Groups",
    "isEnabled": true,
    "configJson": "{\"tenantId\":\"00000000-0000-0000-0000-000000000000\",\"clientId\":\"11111111-1111-1111-1111-111111111111\",\"groupClaim\":\"groups\"}"
  }
}
```

#### D. Active Directory / LDAP (LDAPS Port 636)
> [!IMPORTANT]
> The gateway rejects plaintext LDAP on port 389 for security. Always configure LDAPS on port 636 or set `useSsl=true`.

1. **Test LDAP Connection & Bind**:
Send this tool call to test credentials:
```json
{
  "name": "manage_providers",
  "arguments": {
    "action": "test_ldap",
    "server": "dc01.internal.corp",
    "port": 636,
    "useSsl": true,
    "bindDn": "CN=svc-mcg,OU=ServiceAccounts,DC=internal,DC=corp",
    "bindPassword": "ServiceAccountPassword123!"
  }
}
```

2. **Save Active Directory Provider**:
Send this tool call to save the provider:
```json
{
  "name": "manage_providers",
  "arguments": {
    "action": "save_auth",
    "providerName": "ActiveDirectory",
    "displayName": "Corporate Active Directory",
    "isEnabled": true,
    "configJson": "{\"server\":\"dc01.internal.corp\",\"port\":636,\"useSsl\":true,\"domain\":\"INTERNAL\",\"baseDn\":\"DC=internal,DC=corp\",\"bindDn\":\"CN=svc-mcg,OU=ServiceAccounts,DC=internal,DC=corp\",\"bindPassword\":\"ServiceAccountPassword123!\"}"
  }
}
```

---

### 3.2 Secret Providers (`save_secret` & `test_vault`)

#### A. Built-in AES-256-GCM Master Key (Default)
The gateway encrypts all backend credentials at rest in the database using the 256-bit `MCG_MASTER_KEY`.

#### B. HashiCorp Vault KV v2 (AppRole Authentication)
1. **Test Vault Connection**:
Send this tool call to test Vault credentials:
```json
{
  "name": "manage_providers",
  "arguments": {
    "action": "test_vault",
    "address": "https://vault.internal.corp:8200",
    "authMethod": "approle",
    "roleId": "11111111-2222-3333-4444-555555555555",
    "secretId": "66666666-7777-8888-9999-000000000000"
  }
}
```

2. **Save Vault Secret Provider**:
Send this tool call to save the Vault configuration:
```json
{
  "name": "manage_providers",
  "arguments": {
    "action": "save_secret",
    "providerName": "HashiCorpVault",
    "displayName": "Enterprise Vault KV",
    "isEnabled": true,
    "configJson": "{\"address\":\"https://vault.internal.corp:8200\",\"authMethod\":\"approle\",\"roleId\":\"11111111-2222-3333-4444-555555555555\",\"secretId\":\"66666666-7777-8888-9999-000000000000\",\"mountPath\":\"secret\"}"
  }
}
```

---

## 👥 4. Group Mappings & Access Policies

### 4.1 Group Mappings (`manage_group_mappings`)
Map external SSO groups, roles, or Active Directory domain SIDs to internal gateway roles:

```json
{
  "name": "manage_group_mappings",
  "arguments": {
    "action": "save",
    "externalId": "S-1-5-21-1234567890-123456789-123456789-512",
    "internalGroup": "full_admin"
  }
}
```

### 4.2 Access Policies (`manage_policies`)
Control access to specific backend servers or tools:

```json
{
  "name": "manage_policies",
  "arguments": {
    "action": "save",
    "targetId": "docker",
    "requiredGroup": "devops",
    "isAllowed": true
  }
}
```

---

## 🔍 5. Semantic Search & Embedding Providers

Configure semantic tool discovery using **`manage_settings`**:

### OpenAI Embeddings
```json
{
  "name": "manage_settings",
  "arguments": {
    "action": "update",
    "embeddingProvider": "OpenAI",
    "embeddingApiUrl": "https://api.openai.com/v1",
    "embeddingApiKey": "sk-proj-...",
    "embeddingApiModel": "text-embedding-3-small"
  }
}
```

### Local Ollama Embeddings
```json
{
  "name": "manage_settings",
  "arguments": {
    "action": "update",
    "embeddingProvider": "Ollama",
    "embeddingApiUrl": "http://ollama:11434/api/embeddings",
    "embeddingApiModel": "nomic-embed-text"
  }
}
```

---

## 🖥️ 6. Backend Servers & Client AppKeys

### 6.1 Register Backend Server (`manage_servers`)
Register a new downstream MCP server:

```json
{
  "name": "manage_servers",
  "arguments": {
    "action": "create",
    "id": "github",
    "displayName": "GitHub Integration MCP",
    "url": "http://github-mcp:8080/sse",
    "type": "sse",
    "enabled": true,
    "categories": ["source-control", "ci-cd"]
  }
}
```

### 6.2 Issue Developer AppKey (`manage_appkeys`)
Issue an AppKey for a user or agent:

```json
{
  "name": "manage_appkeys",
  "arguments": {
    "action": "create",
    "name": "Developer Personal Key",
    "username": "steve",
    "scopes": ["all"],
    "expiresInDays": 90
  }
}
```

---

## 🧪 7. Live Tool Verification & Diagnostics

### 7.1 Test Backend Tool Dispatch (`test_tool_call`)
Run a tool call directly through the gateway to verify downstream connectivity:

```json
{
  "name": "test_tool_call",
  "arguments": {
    "serverId": "github",
    "toolName": "search_repositories",
    "arguments": {
      "query": "model-context-gateway"
    }
  }
}
```

### 7.2 View Audit Logs (`manage_system`)
Query recent administrative and execution audit records:

```json
{
  "name": "manage_system",
  "arguments": {
    "action": "query_audit",
    "take": 50
  }
}
```

---

## 📋 8. Admin MCP Tool Reference

| Tool Name | Action | Key Parameters | Purpose |
| :--- | :--- | :--- | :--- |
| **`manage_servers`** | `list`, `get`, `create`, `update`, `delete`, `toggle`, `reconnect`, `reconnect_all` | `id`, `displayName`, `url`, `type`, `enabled`, `secretProvider` | Manages downstream MCP server lifecycles. |
| **`manage_appkeys`** | `list`, `get_limits`, `create`, `revoke` | `name`, `username`, `scopes`, `expiresInDays`, `id` | Provisions and revokes client API keys. |
| **`manage_clients`** | `list`, `register`, `delete` | `displayName`, `scopes`, `expiresInDays`, `id` | Manages dynamic OAuth 2.0 clients. |
| **`manage_policies`** | `list`, `save`, `delete` | `targetId`, `requiredGroup`, `isAllowed`, `id` | Manages RBAC target access rules. |
| **`manage_group_mappings`** | `list`, `save`, `delete` | `externalId`, `internalGroup`, `id` | Maps SSO groups and domain SIDs to internal roles. |
| **`manage_providers`** | `list`, `save_secret`, `test_vault`, `save_auth`, `test_ldap` | `providerName`, `displayName`, `configJson`, `isEnabled`, `address`, `server` | Configures identity and secret providers. Tests connections live. |
| **`manage_settings`** | `get`, `update` | `dashboardTitle`, `embeddingProvider`, `embeddingApiUrl`, `globalMaxKeys` | Updates system branding and semantic embeddings. |
| **`manage_custom_files`** | `list`, `get`, `save`, `delete` | `type`, `name`, `content` | Manages virtual prompts and resource files. |
| **`manage_system`** | `diagnostics`, `get_logs`, `clear_logs`, `query_audit` | `limit`, `user`, `server`, `since`, `take`, `skip` | Inspects gateway diagnostics, server logs, and audit trails. |
| **`test_tool_call`** | *(default)* | `serverId`, `toolName`, `arguments` | Executes test tool calls on downstream servers. |
