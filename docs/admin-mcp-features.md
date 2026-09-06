# Admin MCP Server Features

Model Context Gateway (MCG) embeds a virtual, in-process **Admin MCP Server**. This allows administrators to manage the gateway's configuration, servers, clients, policies, and diagnostics using the standard Model Context Protocol (MCP), effectively treating the gateway administration interface as just another MCP server.

This server exposes 10 consolidated tools that cover 100% of the gateway's administration and diagnostics flows.

## Admin Tools Reference

### 1. `manage_servers`
Manage backend MCP server configurations and connectivity in the router gateway.
- **Actions**: `list`, `get`, `create`, `update`, `delete`, `toggle`, `reconnect`, `reconnect_all`
- **Key Parameters**: `id`, `name`, `url`, `type` (`sse`, `http`, `stdio`), `alias` (concise namespace alias for tool routing, e.g. `ha`), `category`, `enabled`, `secret_provider`, `secret_key`
- **Use Cases**: Provisioning new backend servers, updating connection settings (URLs, auth shapes, headers, aliases), toggling enabled states, and manually forcing health checks or reconnections.

### 2. `manage_appkeys`
Manage user and application API keys, quotas, and expiration.
- **Actions**: `list`, `get_limits`, `create`, `revoke`
- **Use Cases**: Enforcing limits on API keys, provisioning temporary or permanent AppKeys for scripts or developers, and revoking compromised keys.

### 3. `manage_clients`
Manage OAuth2 / dynamic client credentials.
- **Actions**: `list`, `register`, `delete`
- **Use Cases**: Registering new machine-to-machine OAuth2 clients, assigning display names and scopes, and rotating or deleting client credentials.

### 4. `manage_policies`
Manage role-based access control (RBAC) policies for MCP servers, tools, and resources.
- **Actions**: `list`, `save`, `delete`
- **Use Cases**: Controlling which users or groups can access specific backend servers or execute specific tools. 

### 5. `manage_group_mappings`
Manage external identity provider group to internal group mappings.
- **Actions**: `list`, `save`, `delete`
- **Use Cases**: Mapping an external Active Directory SID or Okta Group to an internal router role (e.g., mapping `S-1-5-21-...` to `admins`).

### 6. `manage_providers`
Manage secret providers (Vault, etc.) and authentication providers (LDAP/AD, OIDC).
- **Actions**: `list`, `save_secret`, `test_vault`, `save_auth`, `test_ldap`
- **Use Cases**: Configuring HashiCorp Vault integrations, saving LDAP/AD configuration details, and securely testing connections (e.g., LDAP bind or Vault AppRole auth) before enabling them.

### 7. `manage_settings`
Manage global router settings, embedding models, and UI configuration.
- **Actions**: `get`, `update`
- **Use Cases**: Modifying the dashboard title/icon, configuring semantic routing embedding providers (like ONNX or API-based embeddings), and enforcing global AppKey limits.

### 8. `manage_custom_files`
Manage custom prompt templates and local resource files in the router data directory.
- **Actions**: `list`, `get`, `save`, `delete`
- **Use Cases**: Creating or updating custom system prompts, uploading specialized `.json` prompt definitions, and managing raw resource text files used by the router.

### 9. `manage_system`
Router system diagnostics, runtime metrics, logs, and audit trail.
- **Actions**: `diagnostics`, `get_logs`, `clear_logs`, `query_audit`
- **Use Cases**: Querying memory and handle usage, retrieving recent system logs, checking active session counts, and querying the comprehensive audit trail by user, server, or timestamp.

### 10. `test_tool_call`
Test execution of a backend MCP tool directly via the router test bench.
- **Actions**: Execution (implied)
- **Use Cases**: Dispatching a raw JSON-RPC `tools/call` request to any connected backend server to verify its connectivity, protocol compatibility, and output formatting.
