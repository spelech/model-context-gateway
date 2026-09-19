# Server Management & Secret Providers

The **Model Context Gateway (MCG)** manages connections to downstream Model Context Protocol (MCP) servers. It supports multiple transport types (`SSE`, `HTTP`, `STDIO`) and integrates with enterprise secret providers to eliminate hardcoded plaintext credentials.

> [!TIP]
> **Need a configuration recipe for your server?** Check the [**MCP Server Authentication & Integration Cookbook**](../mcp-server-auth-cookbook.md) for quick decision tables and configuration examples for Docker, Home Assistant, Postgres, Kubernetes, and more.

---

## ➕ Registering a New Backend Server

![Add Server Registration Modal](../assets/add_server_modal.jpg)

Click the **`+ Add Server`** button in the dashboard toolbar to open the registration modal:

```
+-------------------------------------------------------------------------------+
| ➕ Add New MCP Server                                                      [X] |
+-------------------------------------------------------------------------------+
| Server Identifier:   [ docker                                              ]  |
| Display Name:        [ Docker Infrastructure Daemon                        ]  |
| Transport Type:      (•) SSE Stream   ( ) HTTP JSON-RPC   ( ) STDIO CLI       |
| Endpoint / Command:  [ http://docker-mcp:8080/sse                          ]  |
| Categories (comma):  [ Infrastructure, DevOps                              ]  |
|                                                                               |
| Secret Provider:     [ HashiCorp Vault (KV v2) ▾                           ]  |
|   Vault Mount:       [ secret                                              ]  |
|   Secret Path:       [ homelab/docker                                      ]  |
|   Secret Field:      [ api_token                                           ]  |
|                                                                               |
| Custom Headers:      [ {"X-Custom-Header": "value"}                        ]  |
|                                                                               |
| [ Cancel ]                                                    [ Save Server ] |
+-------------------------------------------------------------------------------+
```

### Core Configuration Parameters

| Field | Description | Example |
| :--- | :--- | :--- |
| **Server Identifier (`id`)** | Unique identifier string. Used to namespace tools (`{id}__{tool}` or `{id}/{tool}`) and route requests (`/{id}`). | `docker`, `homeassistant`, `plex` |
| **Display Name** | Human-readable label displayed across status cards and test bench selectors. | `Docker Infrastructure Daemon` |
| **Transport Type** | Communication protocol: `SSE`, `HTTP`, or `STDIO`. | `SSE` |
| **Endpoint / Command** | Downstream URL (for `SSE` / `HTTP`) or local CLI executable path (for `STDIO`). | `http://docker-mcp:8080/sse` or `npx` |
| **Categories** | Comma-separated tags used for catalog grouping and category-scoped AppKey evaluation. | `Infrastructure, Smart Home` |
| **Secret Provider** | Secret resolution strategy: `None`, `Environment`, `Vault`, or `WindowsRegistry`. | `Vault` |
| **Custom Headers** | Optional JSON key-value map of HTTP headers injected into downstream requests. | `{"Authorization": "Bearer token"}` |

---

## 🚀 Transport Protocols & Lifecycle Behaviors

> [!TIP]
> For complete technical specifications and child process lifecycle policies, see the [**Transport Capability & Configuration Guide**](../transports.md).

### 1. Server-Sent Events (`SSE`)
* **Usage**: Stateful streaming connection for real-time notifications, progress events, and long-running tool executions.
* **Endpoint Pattern**: `http://host:port/sse`
* **Session Lifecycle**: MCG maintains a persistent SSE stream to the backend. It tracks session IDs and routes JSON-RPC messages bi-directionally between connected AI clients and the downstream server.

### 2. HTTP JSON-RPC (`HTTP` / `Streamable`)
* **Usage**: Stateless HTTP POST communication using standard JSON-RPC 2.0 payloads.
* **Endpoint Pattern**: `http://host:port/mcp` or `http://host:port/v1/jsonrpc`
* **Session Lifecycle**: The gateway executes an independent HTTP request for each tool call, prompt evaluation, or resource read. Ideal for containerized microservices and stateless functions.

### 3. Local Subprocess (`STDIO`)
* **Usage**: Spawns local CLI tools, Node.js scripts, Python binaries, or container wrappers communicating over standard input/output (`stdin`/`stdout`).
* **Command Syntax**: Executable name with command-line arguments (for example, `npx -y @modelcontextprotocol/server-filesystem /shared/data`).
* **Secret Protection**: MCG injects resolved credentials directly into the child process environment variables. Secrets never appear in command-line arguments or system process listings (`ps aux`).

---

## 🔐 Enterprise Secret Providers

MCG provides pluggable secret resolution strategies to ensure no credentials are stored in plaintext:

```mermaid
flowchart TD
    Record["<b>McpServer Record</b><br><i>(Encrypted in DB with AES-256-GCM)</i>"]
    Resolver["<b>SecretProvider Strategy Resolver</b><br><i>Dynamic runtime provider dispatch</i>"]
    Env["<b>Environment Variables</b><br><i>Host / Container OS Variables</i>"]
    Vault["<b>HashiCorp Vault KV v2</b><br><i>AppRole / Token JIT Lease</i>"]
    WinReg["<b>Windows Registry</b><br><i>DPAPI Machine Encryption</i>"]

    Record --> Resolver
    Resolver --> Env
    Resolver --> Vault
    Resolver --> WinReg

    classDef record fill:#161b22,stroke:#ff5f1f,stroke-width:1.5px,color:#fff;
    classDef resolver fill:#0f2e1b,stroke:#00c853,stroke-width:2px,color:#fff;
    classDef provider fill:#161b22,stroke:#30363d,stroke-width:1px,color:#e6edf3;
    class Record record;
    class Resolver resolver;
    class Env,Vault,WinReg provider;
```

### 1. Direct Static Key (`None`)
* Credentials are stored directly in the server configuration.
* All sensitive strings are encrypted at rest with AES-256-GCM envelope encryption.
* Recommended for local testing, public APIs, or unauthenticated internal networks.

### 2. Environment Variables (`Environment` / `Env`)
* Resolves secrets dynamically from environment variables on the gateway host or container.
* **Configuration**: Specify the environment variable name (e.g. `HOME_ASSISTANT_LONG_LIVED_TOKEN`).
* Supports prefix notation (e.g. `ENV:DOCKER_SECRET_KEY`).

### 3. HashiCorp Vault KV v2 (`Vault` / `HashiCorpVault`)
* Integrates directly with HashiCorp Vault Key-Value Version 2 (`kv-v2`) engines.
* **Authentication**: Supports AppRole authentication (`roleId` and `secretId`) or direct Vault tokens.
* **Features**:
  * **Automatic Token Renewal**: Checks token time-to-live before each request. Automatically renews authentication if less than 5 minutes remain.
  * **In-Memory Cache**: Caches retrieved secrets securely in memory for 10 minutes to minimize Vault traffic.
  * **Parameters**:
    * **Secret Mount**: Mount path of the KV v2 engine (default: `secret`).
    * **Secret Path**: Path to the secret document (e.g. `homelab/services/radarr`).
    * **Secret Field**: Specific key name in the secret document (e.g. `api_key`).

### 4. Windows Registry DPAPI (`WindowsRegistry` / `Registry`)
* Resolves credentials from Windows Registry keys (`HKLM` or `HKCU`).
* **DPAPI Decryption**: Automatically detects and decrypts DPAPI-encrypted machine or user data blobs.
* **Parameters**:
  * **Secret Path**: Subkey registry path (e.g. `SOFTWARE\Homelab\McpSecrets`).
  * **Secret Field**: Registry value name (e.g. `PlexToken`).
* *Note: Returns `null` on Linux containers.*

---

## 👁️ Inspecting Server Capabilities (Inspect Modal)

![Server Capabilities Inspect Modal](../assets/server_inspect_modal.jpg)

Click **`Inspect`** on any server card on the Overview dashboard to examine downstream capabilities:

* **Tools Tab**: Discovered tools, names, descriptions, and dynamic JSON Schema parameter requirements.
* **Resources Tab**: Virtual resource URIs (e.g. `mcp://docker/containers/list`), MIME types, and descriptions.
* **Prompts Tab**: Prompt templates and declared arguments.
* **Raw Schema**: Full JSON-RPC capabilities payload returned during protocol handshake.

---

## 🔗 Connected Accounts & Personal Egress OAuth (3LO)

When connecting downstream SaaS MCP servers (e.g. **GitHub**, **Slack**, **Jira / Atlassian**, **Linear**), organizations require users to execute actions as *themselves* rather than using a shared static service account.

The **Personal Egress 3LO OAuth Engine ("Connected Accounts")** enables seamless user authorization without distributing API keys or Personal Access Tokens (PATs) across developer laptops:

```mermaid
sequenceDiagram
    autonumber
    actor Dev as Developer / Browser
    participant UI as MCG Dashboard (My MCP Servers)
    participant GW as Model Context Gateway
    participant Cache as IMemoryCache (State Vault)
    participant SaaS as Third-Party OAuth (GitHub / Jira)
    participant Store as IUserSecretStore (AES-256-GCM)

    Dev->>UI: Click "Connect Account" on GitHub Server
    UI->>GW: GET /api/oauth/egress/authorize/{serverId}
    GW->>Cache: Store cryptographic state (username, serverId, 15m TTL)
    GW-->>Dev: 302 Redirect to SaaS Authorization URL (client_id, redirect_uri, scopes, state)
    Dev->>SaaS: Complete Out-of-Band OAuth Consent
    SaaS-->>GW: GET /api/oauth/egress/callback?code=...&state=...
    GW->>Cache: Validate & Consume state parameter
    GW->>SaaS: POST /login/oauth/access_token (grant_type=authorization_code)
    SaaS-->>GW: Return access_token, refresh_token, expires_in
    GW->>Store: Encrypt & Vault tokens for (username, serverId)
    GW-->>Dev: 302 Redirect to /my-servers?connected={serverId}
    Note over Dev,GW: On subsequent tool execution, MCG injects the user's vaulted token automatically
```

### Operator Setup: Enabling 3LO on an MCP Server
1. In the Dashboard or Admin MCP, select your backend server and open the configuration modal.
2. Enable **Enable OAuth (3LO)**.
3. Configure the OAuth client credentials registered with your SaaS provider:
   * **Client ID (`oauthClientId`)**: The OAuth Application Client ID.
   * **Client Secret (`oauthClientSecret`)**: The OAuth Application Secret (encrypted at rest).
   * **Authorization URL (`oauthAuthorizationUrl`)**: e.g. `https://github.com/login/oauth/authorize`.
   * **Token URL (`oauthTokenUrl`)**: e.g. `https://github.com/login/oauth/access_token`.
   * **Requested Scopes (`oauthScopes`)**: Comma/space-delimited scopes (e.g. `repo,read:org` or `read:jira-work,write:jira-work`).
   * **Redirect URI (`oauthRedirectUri`)**: (Optional) Defaults to `{gateway}/api/oauth/egress/callback`.
4. Click **Save Server**.

### Developer Experience: Connecting Personal Accounts
1. Log into the MCG Dashboard and open the **My MCP Servers** view.
2. For any server with OAuth enabled, a **`Connect Account`** button is displayed.
3. Click **Connect Account**. MCG initiates the third-party OAuth flow in your browser.
4. After approval, the gateway vaults the access and refresh tokens. The status card updates to **`Connected (OAuth)`**.
5. When your AI assistant (Claude Code, Cursor, Windsurf) invokes tools on this server:
   * The assistant authenticates to the gateway using its gateway AppKey or corporate JWT.
   * The gateway strips the incoming gateway credential and dynamically injects `Authorization: Bearer <user_saas_token>`.
   * When tokens near expiry, MCG uses the stored `refresh_token` to automatically renew credentials in the background with zero user intervention.

---

## 📄 Custom Tool JSON Specifications

For services that do not natively support the MCP protocol, you can register virtual tools and resources using custom JSON definitions:

1. Navigate to **Settings** -> **Prompts & Resources** (or **Custom Files**).
2. Click **+ Add Custom File**.
3. Select the file type (`Tools`, `Prompts`, or `Resources`), specify a filename, and enter valid JSON:

```json
[
  {
    "name": "network_ping_host",
    "description": "Sends an ICMP ping to a target IP or hostname",
    "parameters": {
      "type": "object",
      "properties": {
        "host": {
          "type": "string",
          "description": "Target hostname or IPv4 address"
        },
        "count": {
          "type": "integer",
          "default": 4,
          "description": "Number of packets to send"
        }
      },
      "required": ["host"]
    }
  }
]
```

4. Click **Save File**. MCG immediately indexes the virtual definitions into the catalog and updates the semantic vector search engine.
