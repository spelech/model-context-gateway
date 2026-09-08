# Model Context Gateway Features Guide

This guide details the core capabilities and operational modes of **Model Context Gateway (MCG)**. Model Context Gateway routes and secures communication for the Model Context Protocol (MCP).

---

## 1. Dynamic Server Management

Model Context Gateway supports four methods to configure and manage backend MCP servers:

### Method A: Web UI Dashboard (Recommended)
Manage servers dynamically without restarting the gateway:
1. Open the gateway dashboard in a web browser.
2. Select **+ Add Server** in the top right corner.
3. Complete the fields in the **Add MCP Server** modal:
   - **Display Name**: Enter a user-friendly label (for example, `Home Assistant`).
   - **URL**: Provide the backend Server-Sent Events (SSE) endpoint or HTTP server address (such as `http://ha-mcp:8086/mcp`).
   - **Transport Type**: Select `sse` (stateful stream), `http` (stateless request), or `stdio` (local subprocess).
   - **Alias**: Optional short namespace prefix for tools (for example, `ha` instead of `homeassistant`).
   - **Category**: Group the server by function (such as `homecontrol`, `infrastructure`, or `development`).
   - **API Token/Key**: Credentials for downstream authentication.
   - **Secret Provider**: Choose how to retrieve secrets (`None`, `Vault`, `WindowsRegistry`, or `Environment`). See [Pluggable Secret Retrievers](#6-pluggable-secret-retrievers).
4. Select **Save Server**. The gateway registers the server and starts connections immediately.

![Add MCP Server Modal](assets/add_server_modal.jpg)

### Method B: Static JSON Seeding (`custom_servers.json`)
For declarative configuration files:
1. Create `custom_servers.json` in the `/app/data/` directory.
2. Use this structure:
   ```json
   [
     {
       "id": "my-mcp-server",
       "displayName": "My Custom Server",
       "alias": "custom",
       "url": "http://10.0.0.15:3000/sse",
       "type": "sse",
       "category": "infrastructure",
       "enabled": true,
       "hidden": false,
       "apiKey": "optional-bearer-or-api-key",
       "headersJson": "{\"Custom-Header-Name\": \"Header-Value\"}"
     }
   ]
   ```
3. The gateway imports these entries into the database during startup.

### Method C: Environment Seed Migration
The gateway auto-seeds common services on first startup if environment variables exist (such as `HOMEASSISTANT_TOKEN`, `PLEX_TOKEN`, or `SEERR_API_KEY`).

### Method D: Dynamic Docker Label Discovery (`mcp.*` labels)
When the gateway has access to the Docker socket (`/var/run/docker.sock`), it registers containers labeled `mcp.enabled=true`.

```yaml
services:
  my-service-mcp:
    image: ghcr.io/org/my-service-mcp:latest
    container_name: my-service-mcp
    restart: unless-stopped
    networks:
      - net_mcp
    labels:
      - mcp.enabled=true
      - mcp.id=myservice
      - mcp.alias=srv
      - mcp.displayName=My Custom Service
      - mcp.port=8080
      - mcp.type=sse
      - mcp.path=/sse
      - mcp.categories=infrastructure,custom
```

#### Supported Docker Labels

| Label | Required | Default | Description |
| :--- | :--- | :--- | :--- |
| `mcp.enabled` | **Yes** | `false` | Enables gateway auto-discovery. Set to `"true"`. |
| `mcp.id` | **Yes** | — | Unique server identifier (for example, `myservice`). |
| `mcp.alias` | No | — | Optional namespace prefix for tool names (such as `ha`). Also supports `mcp.namespace`. |
| `mcp.port` | **Yes** | — | Internal container port (for example, `8080` or `3000`). |
| `mcp.displayName`| No | Value of `mcp.id` | Readable name shown in the dashboard. |
| `mcp.type` | No | `sse` | Transport type (`sse`, `http`, or `stdio`). |
| `mcp.path` | No | `/sse` (or `/mcp`) | Message dispatch route. |
| `mcp.categories` | No | `general` | Comma-separated category tags for access policies. |
| `mcp.authType` | No | `none` | Header format (`bearer`, `x-api-key`, `custom-header`). |
| `mcp.secretProvider`| No | `none` | Secret retriever backend (`vault`, `env`, `none`). |
| `mcp.secretKey` | No | — | Vault path or environment variable name for the API key. |

---

## 2. Routing Modes

Connect client applications through these SSE endpoints:

| Route Path | Mode | Description |
| :--- | :--- | :--- |
| `/sse` or `/sse?meta=true` | **Meta-Mode (Default)** | Hides underlying tools during startup. Exposes only `search_tools` and `execute_tool`. This reduces token usage in the LLM context window. |
| `/sse?meta=false` | **Full-List Mode** | Exposes all tools from all connected backend servers in one list. |
| `/{targetServerId}` | **Target-Specific Proxying** | Proxies requests directly to one backend server (for example, `/docker` or `/ha`). |
| `/admin` or `/mcg-admin` | **Admin MCP Server** | Exposes 10 consolidated administrative tools. AI coding agents use this interface to manage the gateway. |

### Slash Formatting and Dual-Key Routing

The gateway presents tools to AI models with modern forward-slash formatting:
- **Default Format**: `{namespace}/{tool_name}` (such as `docker/list_containers` or `ha/turn_on`). If configured, the gateway uses the server `Alias` as the namespace.
- **Dual-Key Routing**: The router accepts both `{namespace}/{tool_name}` and canonical `{serverId}__{toolName}` formats for all tool calls and access checks.
- **Collision Prevention**: If a client requests a bare tool name (`list_containers`) without a namespace, the router executes it if unambiguous. If multiple servers offer that tool, the router rejects the call and lists the namespaced options.

> For transport protocol details (`sse`, `http`, `stdio`), concurrency, and error handling, read the [**Transport Capability & Configuration Guide**](transports.md).

### Gateway Client Configuration Examples (`/sse`)

#### Claude Desktop Configuration (`config.json`)
```json
{
  "mcpServers": {
    "mcg": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/client-sse", "http://localhost:8080/sse"]
    }
  }
}
```

#### Antigravity CLI Configuration (`.gemini/settings.json`)
```json
{
  "mcpServers": {
    "mcg": {
      "url": "http://localhost:8080/sse",
      "type": "sse",
      "trust": true,
      "serverUrl": "http://localhost:8080/sse"
    }
  }
}
```

---

## 3. Admin MCP Server and Autonomous Agent Administration

The **Admin MCP Server** (`/admin`, `/admin/sse`, `/mcg-admin`) runs as an in-process MCP server. It provides 10 consolidated administrative tools. Autonomous AI agents (such as Claude Desktop, Cursor, Cline, Windsurf, and Antigravity) use these tools to configure the gateway programmatically.

### Consolidated Admin Tools Reference

| Tool Name | Actions | Description | Key Parameters |
| :--- | :--- | :--- | :--- |
| `manage_servers` | `list`, `get`, `create`, `update`, `delete`, `toggle`, `reconnect`, `reconnect_all` | Manage backend MCP server records, endpoints, transports, categories, aliases, and secret providers. | `action`, `id`, `name`, `url`, `type`, `alias`, `category`, `enabled`, `secret_provider`, `secret_key` |
| `manage_appkeys` | `list`, `get_limits`, `create`, `revoke` | Issue and revoke API AppKeys, set expiration dates, configure scopes, and check key quotas. | `action`, `name`, `scopes`, `expires_in_days`, `prefix` |
| `manage_clients` | `list`, `register`, `delete` | Manage dynamic OAuth 2.0 client registrations. | `action`, `client_id`, `client_name`, `redirect_uris`, `grant_types`, `scopes` |
| `manage_policies` | `list`, `save`, `delete` | Configure Role-Based Access Control (RBAC) rules across servers and tool categories. | `action`, `policy_id`, `role_name`, `server_id`, `category`, `allowed`, `priority` |
| `manage_group_mappings` | `list`, `save`, `delete` | Map external Active Directory SIDs or OIDC Single Sign-On (SSO) groups to internal roles. | `action`, `id`, `source_type`, `external_identifier`, `role_name`, `priority` |
| `manage_providers` | `list`, `save_secret`, `test_vault`, `save_auth`, `test_ldap` | Configure and test HashiCorp Vault, Windows DPAPI, environment variables, Active Directory LDAP, and OIDC providers. | `action`, `provider_type`, `vault_address`, `vault_token`, `ldap_server`, `bind_dn` |
| `manage_settings` | `get`, `update` | Update dashboard UI branding (title, icon, colors) and configure vector embedding models. | `action`, `dashboard_title`, `dashboard_icon`, `embedding_provider`, `embedding_model` |
| `manage_custom_files` | `list`, `get`, `save`, `delete` | Manage prompt templates and resource files stored in persistent storage (`/app/data/`). | `action`, `file_type` (`prompts` or `resources`), `filename`, `content` |
| `manage_system` | `diagnostics`, `get_logs`, `clear_logs`, `query_audit` | View server diagnostics, inspect live logs, and query audit log history. | `action`, `limit`, `level`, `category`, `source_user`, `start_date`, `end_date` |
| `test_tool_call` | `execute` | Test tool execution against downstream MCP servers using the internal test engine. | `action`, `server_id`, `tool_name`, `arguments` |

### Admin MCP Server Client Configuration Examples

#### Claude Desktop (`claude_desktop_config.json`)
```json
{
  "mcpServers": {
    "mcg-admin": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/client-sse", "http://localhost:8080/admin"]
    }
  }
}
```

#### Cursor (`~/.cursor/mcp.json`) / Windsurf / Cline (`cline_mcp_settings.json`)
```json
{
  "mcpServers": {
    "mcg-admin": {
      "url": "http://localhost:8080/admin",
      "headers": {
        "Authorization": "Bearer mcp-adm-Xk9L2mPq-7vN3wZ8aB1cE4fG9"
      }
    }
  }
}
```

#### Antigravity CLI (`.gemini/settings.json`)
```json
{
  "mcpServers": {
    "mcg-admin": {
      "url": "http://localhost:8080/admin",
      "type": "sse",
      "trust": true,
      "serverUrl": "http://localhost:8080/admin"
    }
  }
}
```

---

## 4. Semantic Search

In **Meta-Mode**, clients search for relevant tools before calling them.

### Search Flow:
1. **Tool Query**: The client calls `search_tools(query: "restart actual budget container")`.
2. **Hybrid Scoring Engine**:
   - Evaluates tool similarity with a **local ONNX model** (`all-MiniLM-L6-v2`) or **LiteLLM / OpenAI APIs**. ONNX stands for Open Neural Network Exchange.
   - Adds **Keyword Boosting** (+2.0 for exact phrase matches, +1.0 or +0.5 for word matches).
3. **Execution**: The client runs the selected tool (such as `docker__restart_container`) through `execute_tool`.

### Embeddings Configuration:
Configure embedding providers in the Settings view:
* **Local ONNX (In-Process)**: Offline calculations using `Microsoft.ML.OnnxRuntime`. Downloads weights to `/app/data/` on first use.
* **OpenAI API or LiteLLM Provider**: Uses remote inference APIs. The gateway encrypts API keys at rest in the database.

---

## 5. Authentication, Group Mapping, and Capability Authorization

Model Context Gateway implements a **Unified Authorization Pipeline** across all MCP capabilities:
- **Tools**: `tools/list`, `tools/call`
- **Prompts**: `prompts/list`, `prompts/get`
- **Resources**: `resources/list`, `resources/read`, `resources/templates/list`
- **Completions**: `completion/complete`

Every request moves through this pipeline:
1. **AppKey Scope Validation**: Checks permissions (`*`, `all`, `server:{id}`, `tool:{id}`, `prompt:{id}`, `resource:{id}`, `resource_template:{id}`, `completion:{id}`).
2. **Administrator SID Verification**: Compares caller Security Identifiers (SIDs) against `Admin:GroupSid` (for example, `S-1-5-32-544`).
3. **Database Policy Evaluation**: Runs `sp_EvaluateUserAccess` against `AccessPolicies` to check group and user permissions.
4. **Discovery Filtering**: Automatically hides unauthorized tools, prompts, and resources from list endpoints.
5. **Fail-Closed Security**: Rejects unknown capabilities with an audited HTTP 403 Forbidden response.

### Identity Providers
- **Active Directory (Kerberos / NTLM)**: Identifies callers by Active Directory SIDs using `WindowsIdentity`.
- **OIDC Header Proxy**: Reads OpenID Connect (OIDC) headers (such as `Remote-User` and `Remote-Groups`) passed by reverse proxies.

### Group and SID Mapping Policies
Map external groups to internal roles in the `GroupMappings` table (Settings -> Identity & Auth):
1. **Create Mapping**: Link an AD SID or OIDC group to an internal security role (`admin`, `operator`, `readonly`).
2. **Evaluate Access**: The gateway checks mapped roles during each capability request.

### Standalone Network and Hybrid Authorization (`AdminPolicy`)
The gateway supports a hybrid administration security model:
1. **Enterprise Mode (with Active Directory or OIDC)**:
   - Evaluates caller groups against `Admin:GroupSid` (such as `S-1-5-32-544`), `Admin:Groups` (such as `["full_admin", "Administrator"]`), or `GroupMappings`.
   - Admin AppKeys with `all` or `admin` scopes owned by an administrator receive `Administrator` privileges.
2. **Standalone Mode (No External IDP Configured)**:
   - When no external Identity Provider (IDP) is active, administrative endpoints permit requests from approved IP subnets (`Admin:StandaloneAllowedNetworks`).
   - Default allowed network: Loopback (`127.0.0.1`, `::1`).
   - You can configure local subnets (for example, `10.0.0.0/8` or `192.168.1.0/24`) in `appsettings.json` or environment variables:
     ```json
     {
       "Admin": {
         "StandaloneAllowedNetworks": [
           "127.0.0.1",
           "::1",
           "192.168.1.0/24"
         ]
       }
     }
     ```
     Or through environment variables:
     `ADMIN__STANDALONE_ALLOWED_NETWORKS__0="127.0.0.1"`
     `ADMIN__STANDALONE_ALLOWED_NETWORKS__1="192.168.1.0/24"`
   - Callers from non-listed networks must provide an AppKey with administrative scopes.

### AppKey Credentials and Compact Taxonomy

The gateway issues cryptographically secure **Base62 AppKeys** (~32–34 characters) with recognizable prefixes.

#### AppKey Taxonomy Table

| Key Type | Prefix | Description | Example |
| :--- | :--- | :--- | :--- |
| **Admin Key** | `mcp-adm-` | Administrator access with complete control (`all`, `admin`) | `mcp-adm-Xk9L2mPq-7vN3wZ8aB1cE4fG9` |
| **Global Key** | `mcp-glb-` | Gateway-wide execution across all servers (`all`, `*`) | `mcp-glb-R4t8W1yU-9pM2nQ6sD8fH3jK5` |
| **Domain Scoped** | `mcp-{domain}-` or `mcp-grp-` | Restricted to a specific group or category (such as `group:devops`) | `mcp-devops-T5v7P2mX-3kL9aB1cE4fG8hJ` |
| **Personal / User** | `mcp-usr-` | Personal key bound to a specific user name or SID | `mcp-usr-A7d9F2kL-8xP1mC3vT5bN6mQ2` |
| **Server Scoped** | `mcp-srv-` | Restricted to a single target backend server | `mcp-srv-docker-K4m8X2pL-9vN3wZ8aB1cE` |

#### Cryptographic Design and Fast Lookup
- **Token Format**: `{prefix}{selector_8chars}-{secret_16chars}`.
- **Base62 Character Set**: Uses alphanumeric characters (`0-9`, `A-Z`, `a-z`) generated with `RandomNumberGenerator`.
- **Entropy**: Delivers approximately **143 bits of entropy** (8-char selector ~48 bits + 16-char secret ~95 bits).
- **Fast Prefix Indexing**: The `KeyPrefix` database column indexes `{prefix}{selector_8chars}` for fast lookups without table scans.
- **Constant-Time Verification**: Compares key hashes with `CryptographicOperations.FixedTimeEquals` against SHA-256 values in `AppKeys.EncryptedKey`.
- **Declarative Seeding**: Administrators can seed admin keys on startup with `MCG_ADMIN_AUTH_KEY` or `MCG_ADMIN_KEY`.

#### Scope Granularity
- `all` / `*` / `mcp_client`: Unrestricted access across all backend servers.
- `server:<serverId>` / `<serverId>`: Access restricted to one backend server.
- `category:<name>` / `group:<name>`: Access to all servers within that category.
- `tool:<name>`, `prompt:<name>`, `resource:<uri>`: Access restricted to a specific capability.
- **Dynamic Membership**: Category permissions update immediately when servers change categories.
- **Scope Validation**: The gateway checks category names during key generation. Unknown categories return a 400 Bad Request error unless created by administrators.

### Master Key Lifecycle and Dynamic Database Re-Encryption

The router tracks the encryption key lifecycle through these stages:

1. **Key Source Detection (`KeySource`)**:
   - `KeySource.External`: Provided through Vault (`VAULT_ADDR`), an environment variable (`MCG_MASTER_KEY`), or a secret file (`MCG_MASTER_KEY_FILE`). Key edits in the Web UI are disabled.
   - `KeySource.Configured`: Set explicitly by an administrator and saved to `./data/.master.key`.
   - `KeySource.AutoGenerated`: Generated on initial startup and saved to `./data/.master.key`. The Web UI shows a warning banner.
2. **Dynamic In-Place Database Re-Encryption**:
   Administrators can set a custom Master Key in the Web UI (`POST /api/config/master-key`) or through the Admin MCP Server (`manage_system(action: "set_master_key", newKey: "...")`). The gateway:
   - Decrypts all provider settings, server credentials, and secrets with the active key.
   - Re-encrypts all database records with the new master key.
   - Saves the new key to `./data/.master.key` and sets `KeySource` to `Configured`.

For additional details, read the [**AppKey Scopes & Authorization Guide**](appkey-scopes.md).

### CORS and Cross-Origin Security Configuration
By default, the gateway allows Cross-Origin Resource Sharing (CORS) only for local development ports (`http://localhost:3000`, `http://localhost:5000`, `https://localhost:5001`).

In production, configure allowed origins with the `CORS_ALLOWED_ORIGINS` environment variable:
- **`CORS_ALLOWED_ORIGINS`**: Delimited list of approved URLs (such as `https://mcp.internal.example.com`).

---

## 6. Pluggable Secret Retrievers

The gateway resolves downstream API keys and passwords dynamically to avoid storing plaintext credentials in the database.

The `CompositeSecretRetriever` resolves credentials through:
1. **HashiCorp Vault (KV v2)**: Reads secrets from paths (such as `/secret/data/mcp/plex`) with AppRole or Token authentication and automatic token renewal.
2. **Windows Registry (DPAPI)**: Reads DPAPI-encrypted values from local machine registry hives (`HKLM`).
3. **Environment Variables**: Reads credentials from container environment variables (such as `env:MY_SECRET`).

> [!TIP]
> For configuration recipes, AppRole policies, and AES-256-GCM encryption architecture, read [**docs/secret-providers.md**](secret-providers.md).

### Configuration Steps
1. Store the secret in your provider (for example, set `DOCKER_API_KEY=my-secret`).
2. In the Add Server form, choose `Environment` for **Secret Provider** and enter `DOCKER_API_KEY` for **SecretItemKey**.
3. The gateway retrieves, decrypts, and caches the secret in memory (`IMemoryCache` with rolling TTL) when executing tools.

---

## 7. Developer Test Bench and Diagnostics

The Web Dashboard includes an interactive test bench to test and verify configurations:

1. **Interactive Form Builder**: Creates input forms that match the JSON schemas of registered tools.
2. **Live Logs Console**: Displays real-time JSON-RPC messages, session IDs, and security events.
3. **Search Simulator**: Evaluates test queries against the semantic search engine and displays relevance scores.
4. **Direct JSON-RPC Console**: Allows sending raw JSON-RPC 2.0 payloads to the gateway.
5. **Resource and Prompt Testers**: Dedicated interfaces to read virtual resources and test prompt templates.

![Test Bench View](assets/test_bench_view.jpg)

---

## 8. Database Engine Support and Deployment

For SQLite, Microsoft SQL Server, and MySQL dialects, the 12-table [**Entity-Relationship Diagram (ERD)**](database-providers.md#unified-database-entity-relationship-diagram-erd), stored procedures, AES-256-GCM encryption, and Docker Compose templates, read the [**Database Provider Support & Deployment Matrix**](database-providers.md).

---

## 9. Software Requirements Specification and Automated Test Catalog

For requirements traceability, test proofs, guardrails, and automated verification matrices, reference:
* [**Software Requirements Specification (SRS) & Test Verification Catalog**](software-requirements-and-test-catalog.md)
* [**Test Catalog & Annotation Developer Guide**](test-catalog-guide.md)

---

## 10. Universal Setup Skill (`mcg-setup`)

The `mcg-setup` skill follows the [AgentSkills.io](https://agentskills.io) specification. AI coding assistants (such as Antigravity, Claude Code, Cursor, Cline, and Windsurf) can use it to guide operators through installing and configuring **Model Context Gateway** in any workspace without cloning source code.

### Installation Command
To install the skill into a project workspace:

```bash
mkdir -p .agents/skills/mcg-setup && curl -fsSL https://raw.githubusercontent.com/spelech/model-context-gateway/main/skills/mcg-setup/SKILL.md -o .agents/skills/mcg-setup/SKILL.md
```

### Guided 6-Phase Deployment Workflow
When triggered (for example: *"Set up Model Context Gateway for my environment"*), the skill follows this 6-phase sequence:

1. **Environment Inspection**: Checks the host operating system, Docker socket (`/var/run/docker.sock`), HashiCorp Vault (`VAULT_ADDR`), and Active Directory domain (`USERDNSDOMAIN`).
2. **Hosting Platform Selection**: Recommends **Docker Compose** (Linux, macOS, WSL2) or **Windows Server IIS / Windows Service** (with in-process ANCM and DPAPI).
3. **Configuration Strategy**: Helps administrators choose between **Environment Variables** (immutable `.env` files) and **Web UI & Database** (runtime dynamic configuration).
4. **Identity and Networking**:
   - *Standalone Mode*: Configures SQLite (`data/mcg.db`) and loopback or local subnet access (`Admin:StandaloneAllowedNetworks`).
   - *Enterprise Mode*: Configures Active Directory LDAP or OIDC forward-auth proxies (such as Authentik, PocketID, Authelia, or Keycloak) with MSSQL, MySQL, or Vault.
5. **File Generation and Key Creation**:
   - Generates a 256-bit `MCG_MASTER_KEY` (`openssl rand -base64 32`).
   - Creates `docker-compose.yml`, `web.config` with unbuffered SSE (`responseBufferLimit="0"`), `.env`, and `appsettings.Production.json`.
6. **Health Check and Client Configuration**:
   - Verifies gateway health endpoints (`GET /health` and `GET /sse`).
   - Generates ready-to-copy client configuration JSON for Claude Desktop, Cursor, Cline, and Windsurf.

### Bundled Scaffold Templates
The skill includes templates under `skills/mcg-setup/templates/`:
- `docker-compose.yml`: Production container setup with persistent SQLite storage and Docker socket mounts.
- `web.config`: IIS ASP.NET Core module configuration with unbuffered SSE streaming.
- `.env.example`: Environment variable template with encryption keys, provider settings, and network controls.
- `appsettings.Production.json.example`: Production ASP.NET Core configuration file.

---

## 11. Universal Admin MCP Automation Skill (`mcg-admin`)

The `mcg-admin` skill enables AI assistants and automation scripts to connect directly to the **Admin MCP Server** (`/admin/sse` or `/mcg-admin/sse`). Agents can configure, provision, and verify a new gateway without manual dashboard steps.

### Installation Command
```bash
mkdir -p .agents/skills/mcg-admin && curl -fsSL https://raw.githubusercontent.com/spelech/model-context-gateway/main/skills/mcg-admin/SKILL.md -o .agents/skills/mcg-admin/SKILL.md
```

### 7-Phase Administration Engine
1. **Gateway Diagnostics**: Authenticates with `Authorization: Bearer <admin-key>` and runs `manage_system(action: "diagnostics")`.
2. **Secret Provider Setup**: Configures HashiCorp Vault KV v2 (with `test_vault` validation) or the built-in AES-256-GCM database key.
3. **Identity Provider Setup**: Configures Authentik, Authelia, Keycloak OIDC, Microsoft Entra ID, or Active Directory LDAP (with `test_ldap`).
4. **RBAC and Group Mappings**: Maps external SSO groups and SIDs to internal roles (`manage_group_mappings`) and creates allow/deny rules (`manage_policies`).
5. **Semantic Search Configuration**: Sets up OpenAI, Azure OpenAI, or local embedding models (`manage_settings`).
6. **Backend Servers and AppKeys**: Registers backend tools (`manage_servers`), creates developer AppKeys (`manage_appkeys`), and registers OAuth clients (`manage_clients`).
7. **End-to-End Testing**: Executes tool calls through the router (`test_tool_call`) and checks security audit records (`manage_system(action: "query_audit")`).

> [!TIP]
> For architecture diagrams, JSON examples, and automation scripts, read the [**Admin MCP Automation & Provider Configuration Guide**](admin-mcp-automation-guide.md).

---

## 12. Observability and PII Audit Logging

Model Context Gateway includes observability features designed for privacy and security.

### Personally Identifiable Information (PII) Sanitization
The `PiiSanitizer` removes sensitive data from logs automatically:
- **Masked Data**: Replaces Bearer tokens, API keys, passwords, and authorization headers with `[REDACTED]`.
- **Safe Logs**: Prevents downstream credentials from leaking into log aggregators.

### Audit Logging
The gateway saves administrative actions and tool calls using database stored procedures (such as `sp_InsertAuditLog`):
- **Traceability**: Records user identities (or AppKey owners), target servers, called tools, execution latency, and timestamps.
- **Audit Tool**: The Admin MCP Server provides the `query_audit` action in `manage_system`. Administrators and agents can inspect historical events programmatically.

---

## 13. Enterprise Identity Delegation

In enterprise networks, the gateway passes user identities downstream so backend systems can apply Row-Level Security (RLS).

### Delegation Strategies
1. **X-Forwarded-User Header Propagation**:
   The gateway passes the authenticated username (from OIDC or Active Directory) to downstream HTTP and SSE backends.
2. **Kerberos / NTLM Impersonation**:
   On Windows IIS, the gateway uses `S4U2Proxy` to assume the user's Active Directory identity during backend calls.
3. **OAuth2 On-Behalf-Of Delegation**:
   The gateway exchanges tokens with identity providers (such as Microsoft Entra ID, Okta, or Authentik) on behalf of the user.
4. **Dynamic Authentication Pass-Through**:
   When downstream services return HTTP 401 challenges, the gateway forwards the challenge prompt to the client IDE or AI assistant to collect credentials.

---

## 14. Batteries-Included Docker Environment

The full Docker container simplifies running STDIO subprocess tools without extra container networking.

- **Container Image**: `ghcr.io/spelech/model-context-gateway:latest-full`
- **Included Runtimes**: Ships with pre-installed Node.js, Python 3, the `uv` package manager, and `bun`.
- **Primary Use Case**: Executes STDIO scripts (such as `npx -y @modelcontextprotocol/server-postgres`) directly inside the gateway container.

---

## 15. Model Context Protocol Specification Alignment (MCP 2026-07-28)

Model Context Gateway conforms to the MCP 2026-07-28 standard:

### Required `resultType` Field
Successful JSON-RPC responses return a top-level `resultType` property:
- `"complete"`: The operation finished with final tool output or resource data.
- `"input_required"`: Signals an interactive multi-turn request that requires user input.

### Multi Round-Trip Requests (MRTR)
When backend tools require multi-step user confirmation or Two-Factor Authentication (2FA) codes:
- The gateway returns `resultType: "input_required"` with an array of `inputRequests` (`id`, `type`, `message`, `required`, `schema`).
- The client responds via `execute_tool` using the `inputResponses` parameter for each requested ID.

### Cacheable Results Metadata
Read endpoints (`tools/list`, `resources/list`, `resources/read`, `prompts/list`) return caching metadata:
- `ttlMs`: Cache lifetime in milliseconds (default: `300000` / 5 minutes).
- `cacheScope`: Scope boundary (`"session"` or `"global"`).

### MCP 2026-07-28 Error Codes
Error handling adheres to standard MCP taxonomy:
- `-32602` (Invalid Params): Used when parameters or requested resources are invalid or missing.
- `-32020` (Connection Closed): Downstream connection dropped.
- `-32021` (Request Cancelled): Client or session cancelled the active request.
- `-32022` (Capability Missing): Client or server lacks the required declared capability.

### Request Header Annotations
HTTP POST endpoints support standard MCP headers:
- `Mcp-Method`: Target RPC method (such as `tools/call` or `resources/read`).
- `Mcp-Name`: Target tool, prompt, or resource name.
- Fallback logic ensures full compatibility with older client payloads.

### Deprecated Features Cleanup
In line with the 2026-07-28 specification roadmap:
- Clients set per-request log levels through `_meta.io.modelcontextprotocol/logLevel`.
- Deprecated methods (`logging/setLevel`, `roots/list`, `sampling/createMessage`, and Admin `ping`) return Method Not Found (`-32601`).
- The `Mcp-Session-Id` header is deprecated for HTTP+SSE in favor of URL session routing.

---

## 16. OAuth 2.1 Dynamic Client Registration and RFC 7591 Security

The built-in OAuth 2.0 and 2.1 authorization server complies with RFC 7591 and OAuth 2.1 rules:
- **Mandatory `application_type`**: Client registration (`/api/register`) requires an explicit `application_type` (`"web"` or `"native"`). Public native clients use Proof Key for Code Exchange (PKCE) with `token_endpoint_auth_method: "none"`.
- **Issuer Validation**: The gateway validates all requests against its canonical issuer URL (`iss`).
- **One-Way Secret Hashing**: Client secrets are hashed with SHA-256 before storage across SQLite, MSSQL, and MySQL databases.
- **Resource Metadata**: RFC 9728 discovery endpoints expose `authorization_servers` and `resource` parameters automatically.


