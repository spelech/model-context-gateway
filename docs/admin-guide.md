# Model Context Gateway (MCG) Administrator Guide

Welcome to the **Model Context Gateway (MCG)** Administrator Guide.

This guide explains how to manage backend servers, configure access policies, store credentials securely, and automate operations.

---

## 1. Administration Interfaces

You can administer Model Context Gateway in two ways:

1. **Web Dashboard (`http://localhost:8080/`)**: A visual dark-mode web application for human administrators. Use this interface to inspect servers, view logs, generate keys, and test tools.
2. **Admin MCP Server (`/admin` or `/mcg-admin`)**: An in-process MCP server with 10 tools. Use this interface to allow AI assistants (like Claude Desktop, Cursor, or Antigravity) and automated scripts to manage the gateway.

---

## 2. Admin MCP Tools Reference

The Admin MCP Server provides 10 tools that cover all administrative operations:

| Tool Name | Supported Actions | Description | Key Parameters |
| :--- | :--- | :--- | :--- |
| **`manage_servers`** | `list`, `get`, `create`, `update`, `delete`, `toggle`, `reconnect`, `reconnect_all` | Manage backend MCP servers and connections. | `id`, `name`, `url`, `type`, `alias`, `category`, `enabled`, `secret_provider`, `secret_key` |
| **`manage_appkeys`** | `list`, `get_limits`, `create`, `revoke` | Manage user and application API keys and quotas. | `name`, `username`, `scopes`, `expiresInDays`, `id` |
| **`manage_clients`** | `list`, `register`, `delete` | Manage OAuth 2.0 dynamic client applications. | `client_name`, `redirect_uris`, `scope`, `client_id` |
| **`manage_policies`** | `list`, `save`, `delete` | Manage role-based access control (RBAC) rules. | `target_id`, `required_group`, `is_allowed`, `id` |
| **`manage_group_mappings`** | `list`, `save`, `delete` | Map external identity groups to internal gateway roles. | `external_group`, `internal_role`, `id` |
| **`manage_providers`** | `list`, `save_secret`, `test_vault`, `save_auth`, `test_ldap` | Configure secret stores (Vault) and identity providers (Active Directory). | `providerName`, `displayName`, `configJson`, `isEnabled`, `address` |
| **`manage_settings`** | `get`, `update` | Manage global settings, vector models, and AppKey limits. | `settings` JSON object |
| **`manage_custom_files`** | `list`, `get`, `save`, `delete` | Manage prompt templates and resource files in `./data`. | `file_name`, `content`, `category` |
| **`manage_system`** | `diagnostics`, `get_logs`, `clear_logs`, `query_audit` | View memory, handle counts, recent logs, and audit trails. | `limit`, `user_filter`, `server_filter` |
| **`test_tool_call`** | *(execution)* | Execute a backend tool directly through the gateway test harness. | `server_id`, `tool_name`, `arguments` |

---

## 3. Server Management

The gateway connects to multiple downstream MCP servers and presents them to your AI assistants.

### Adding a Server
You can add servers through the Web Dashboard (**+ Add Server**) or via `manage_servers`:
1. **Display Name**: Enter a clear name (for example: `Home Assistant`).
2. **Connection URL**: Enter the endpoint URL or command string:
   - For `sse`: `http://10.0.0.5:8086/sse`
   - For `http`: `http://10.0.0.5:8086/mcp`
   - For `stdio`: `npx -y @modelcontextprotocol/server-postgres`
3. **Transport Type**: Select `sse`, `http`, or `stdio`.
4. **Alias**: Enter an optional short namespace prefix (for example: `ha`). The gateway exposes tools as `{alias}/{tool_name}` (such as `ha/light_turn_on`).
5. **Secret Provider**: Select how the gateway fetches credentials (`None`, `Environment`, `Vault`, or `WindowsRegistry`).
6. Click **Save Server**. The gateway initializes the connection immediately.

### Enabling, Disabling, and Reconnecting
* **Toggle**: Disabling a server stops traffic to that backend without deleting its configuration.
* **Reconnect**: Forces the gateway to test health and refresh tool schemas for that server.
* **Reconnect All**: Refreshes connections to all enabled backend servers at once.

---

## 4. Access Control & Security Policies

Model Context Gateway enforces Role-Based Access Control (RBAC) on every request.

### How Policy Evaluation Works
The authorization engine evaluates permissions in four strict stages:
1. **Explicit Deny**: If any matching policy sets `IsAllowed = false`, the gateway rejects the request immediately.
2. **Explicit Allow**: If a matching policy sets `IsAllowed = true` for the caller's group or SID, the gateway permits the request.
3. **AppKey Scope**: If no database policy matches, the gateway verifies that the caller's AppKey permits the target (for example: `*`, `category:smarthome`, or `server:docker`).
4. **Default Policy**: If no rule matches, the gateway fails closed and denies access (`403 Forbidden`).

### Creating an Access Policy
1. In the Web Dashboard, click **Settings** &rarr; **Access Control**.
2. Click **+ Add Policy**.
3. Specify:
   * **Target Type**: Choose `server`, `tool`, or wildcard `*`.
   * **Target ID**: Enter the target server ID or tool name.
   * **Required Group**: Enter the group name, role, or Windows SID (for example: `full_admin` or `S-1-5-32-544`).
   * **Access Type**: Select `Allow` or `Deny`.
4. Click **Save Policy**.

### External Group Mappings
If you use Active Directory or an OIDC identity provider (such as Authentik or Keycloak):
1. Go to **Settings** &rarr; **Access Control** &rarr; **Group Mappings**.
2. Click **+ Add Mapping**.
3. Map the external group (for example: `CN=DevOps,OU=Groups,DC=company,DC=com` or `devops-team`) to an internal role (such as `Administrator` or `Operator`).
4. Users presenting that external group claim receive the mapped permissions automatically.

---

## 5. Secret Providers & Credential Storage

The gateway protects backend credentials so clients never handle plaintext secrets.

| Provider | Storage Method | Best For |
| :--- | :--- | :--- |
| **Built-in Database** | Encrypted in SQLite/SQL using AES-256-GCM. | Standalone deployments and home labs. |
| **Environment** | Loaded from host environment variables (`ENV:VAR_NAME`). | Containerized infrastructure (Docker Compose, Kubernetes). |
| **HashiCorp Vault** | Retrieved dynamically from Vault KV v2 with token renewal. | Enterprise production environments. |
| **Windows Registry** | Encrypted with Windows DPAPI machine keys (`HKLM`). | Windows Server IIS hosting. |
| **UserProvided (BYOK)** | Stored per-user in encrypted DB or HashiCorp Vault. | Multi-user environments with personal tokens (Slack, GitHub, etc.). |
| **TokenExchange** | Dynamic RFC 8693 token exchange via IdP. | Microservices requiring audience-scoped downstream JWTs. |

### User Secret Storage (BYOK)
Configure user secret storage in **Settings &rarr; Secret Providers &rarr; User Secret Storage (BYOK)**:
- **Database (Encrypted Storage)**: Saves secrets in the local database encrypted with AES-256-GCM.
- **HashiCorp Vault (KV v2)**: Saves secrets in Vault KV v2 using a customizable path template (such as `{Company}/mcgateway/{User}/{Server}`). Supported tokens: `{Company}`, `{User}`, `{Server}`.

### In-House Identity Provider / External JWT Bearer
Configure inbound Bearer JWT validation in **Settings &rarr; Identity & Authentication &rarr; In-House IdP / External JWT Bearer**:
- **Authority / Discovery URL**: Provider discovery URL (such as `https://idp.corp.internal/auth/realms/corp/.well-known/openid-configuration`).
- **Audience**: Expected JWT audience (such as `model-context-gateway`).
- **Issuer**: Expected JWT issuer URL.

### Configuration Guardrails
- **Kerberos Impersonation**: Outbound calls execute via `WindowsIdentity.RunImpersonated`. The UI locks Secret Provider to `None` and disables static API keys.
- **User-Provided (BYOK)**: The gateway resolves user credentials dynamically. The UI disables static server API keys.
- **Vault User Storage Dependency**: The user store requires an active Vault provider. If Vault is disabled, user queries fail closed.

For setup details, read the [Secret Providers Guide](secret-providers.md) and [Authentication Support Matrix](auth-flows/auth-support-matrix.md).

---

## 6. System Diagnostics & Audit Logs

Administrators can monitor gateway health and inspect usage:

### Runtime Diagnostics
The **`manage_system(action="diagnostics")`** tool and the Web Dashboard display:
* Current memory usage (MB).
* Active client sessions and background connections.
* Open operating system handles.
* System uptime and version.

### Audit Logging
The gateway logs every administrative action and tool execution to the database (`AuditLogs` table):
* Records caller identity, timestamp, server, tool name, and execution status.
* Automatically redacts Bearer tokens, passwords, and sensitive keys before writing logs.
* Query audit logs via the dashboard or with `manage_system(action="query_audit", limit=50)`.

---

## 7. Automated Administration

You can automate all administration tasks using AI agents or shell scripts:
* **AI Agent Skills**: Equip your AI coding assistant with the `mcg-admin` skill located in `.agents/skills/mcg-admin/SKILL.md`.
* **Automation Playbooks**: For complete cURL, PowerShell, and Python automation recipes, see the [Admin MCP Automation Guide](admin-mcp-automation-guide.md).

---

## Related Documentation

* [**Active Directory & RBAC Guide**](active-directory-and-rbac-guide.md) — Windows Kerberos, LDAPS `tokenGroups` recursive resolution, and multi-level group policies.
* [**OIDC & SSO Reverse Proxy Guide**](oidc-and-sso-guide.md) — External JWT validation, header SSO, and downstream token exchange.
* [**Admin MCP Automation Guide**](admin-mcp-automation-guide.md) — Automation playbooks, JSON payloads, and AI agent skills.
* [**Operations Runbook**](runbook.md) — Production operations, backups, and disaster recovery.
* [**AppKey Scopes & Authorization Guide**](appkey-scopes.md) — Granular scope rules and personas.
* [**Troubleshooting & RCA Guide**](mcp-routing-and-admin-issues.md) — Root cause analysis for common issues.


