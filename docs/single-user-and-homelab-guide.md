# Single-User & Home-Lab Setup Guide

This guide details how to install, configure, and operate the **Model Context Gateway (MCG)** for single users, home-lab operators, and local developers.

```
┌────────────────────────────────────────────────────────────────────────┐
│               Single-User / Homelab Setup Architecture                 │
└────────────────────────────────────────────────────────────────────────┘
                                   │
              ┌────────────────────┴────────────────────┐
              ▼                                         ▼
   [Local Web UI / Admin]                     [Multiple AI Clients]
   • Trusted LAN / Loopback Subnets           • Claude Desktop -> Scope: "all"
   • Zero-Auth Standalone Admin               • Cursor -> Scope: "server:docker"
   • Port 8080                                • Open WebUI -> Scope: "category:media"
   • + Generate Key Modal                     • Antigravity -> Scope: "all,admin"
              │                                         │
              └────────────────────┬────────────────────┘
                                   ▼
          ┌───────────────────────────────────────────────────┐
          │        Model Context Gateway (MCG Container)      │
          │                                                   │
          │  1. Zero-Config Safe Defaults                     │
          │     • Standalone AppKeys: ENABLED (Default)       │
          │     • Built-in DB Secrets (AES-GCM): ENABLED      │
          │     • Active Directory / LDAP: DISABLED (Default) │
          │     • Vault / Windows Registry: DISABLED (Default)│
          │  2. Zero-Cert OpenIddict Bootstrapping            │
          │     (Auto-generates PFX or dev certs in Standalone)│
          │  3. AppKey Authentication Handler                 │
          │     (Validates mcp-adm-*, mcp-glb-*, mcp-usr-*,   │
          │      mcp-srv-*, mcp-grp-*)                        │
          │  4. Granular Scope Enforcement                    │
          │     • Global: "all", "*"                          │
          │     • Server: "server:docker", "server:postgres"  │
          │     • Category: "category:media", "group:devops"  │
          │     • Tool: "tool:docker__list_containers"        │
          │  5. Flexible Multi-Key Provisioning               │
          │     • Web UI (+ Generate Key)                     │
          │     • MCG_CLIENT_APP_KEYS environment variable     │
          │     • Admin MCP tool: create_app_key              │
          │     • Auto-seeded .admin.key and .client.key      │
          └────────────────────────┬──────────────────────────┘
                                   │
              ┌────────────────────┴────────────────────┐
              ▼                                         ▼
   [Docker Socket MCP Discovery]              [Local / Remote MCP Servers]
   • Labeled: mcp.enabled=true                • Python / FastMCP, Node, Postgres,
   • Auto-registers container tools             Home Assistant, Filesystem, Git
```

---

## 60-Second Zero-Config Quickstart

You can start Model Context Gateway with **zero environment variables** and **zero certificate configuration**.

### 1. Launch with Docker Compose
Create a `docker-compose.yml` file:

```yaml
services:
  mcg:
    image: ghcr.io/spelech/model-context-gateway:latest
    container_name: mcg
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      - STANDALONE_ALLOWED_NETWORKS=127.0.0.1,::1,192.168.0.0/16,10.0.0.0/8
    volumes:
      - ./data:/app/data
      - /var/run/docker.sock:/var/run/docker.sock
```

Run:
```bash
docker compose up -d
```

### 2. Retrieve Your Auto-Generated Keys
On first startup, MCG automatically creates the SQLite database and persists your keys into `./data/`:

```bash
# General tool calling key for AI clients (Cursor, Claude, Cline, etc.)
cat ./data/.client.key
# -> mcp-glb-R4t8W1yU-9pM2nQ6sD8fH3jK5

# Admin key for gateway management and AI Agent configuration (/admin/sse)
cat ./data/.admin.key
# -> mcp-adm-Xk9L2mPq-7vN3wZ8aB1cE4fG9
```

### 3. Open the Web Dashboard
Open `http://localhost:8080/` in your browser. From localhost or your local LAN subnet, you have immediate full administrator access without any login prompts.

---

## AppKey Scoping & Multi-Client Isolation

Single users can create multiple individualized AppKeys to control exactly which tools each AI assistant or script can invoke.

### Scope Types & Granularity

| Scope Type | Example Syntax | Behavior | Ideal Client |
| :--- | :--- | :--- | :--- |
| **Global Access** | `all` or `*` | Full access to all backend servers and tools | Claude Desktop, Antigravity |
| **Server Scoped** | `server:docker`, `server:postgres` | Restricts access exclusively to tools from that specific backend server | Cursor, VS Code / Cline |
| **Category Scoped** | `category:devops`, `category:media` | Restricts access to all servers tagged with that category | Domain-specific assistants |
| **Tool Scoped** | `tool:docker__list_containers` | Restricts access to a single specific tool | Automated webhooks or scripts |
| **Capability Scoped**| `resources:read`, `prompts:read` | Read-only context lookup without tool execution | Research and docs assistants |
| **System Admin** | `admin` | Full gateway management via Admin MCP server (`/admin/sse`) | Autonomous admin subagents |

---

## Managing AppKeys

### Option A: Interactive Web UI Dashboard
1. Open `http://localhost:8080/` and navigate to **App Keys**.
2. Click **+ Generate App Key**.
3. Choose a descriptive name (e.g. `Cursor (Docker Tools Only)`), select your desired scope (e.g. `server:docker`), and click **Create Key**.
4. Switch to the **Client Setup Guide** tab to view pre-filled configuration snippets for all major AI clients with your new key.

### Option B: Declarative via `.env` / Docker Compose
Pre-seed your keys upfront during container launch using `MCG_CLIENT_APP_KEYS`:

```ini
MCG_CLIENT_APP_KEYS=mcp-glb-claudeFull123:ClaudeDesktop:all,mcp-srv-cursorDocker456:Cursor:server:docker,mcp-grp-openWebUI789:OpenWebUI:category:media;category:homecontrol
```

### Option C: Autonomous AI Agent Management
AI coding agents (such as Antigravity) connected to `/admin/sse` can call the `create_app_key` tool dynamically:

```json
{
  "name": "Cline Scratchpad Key",
  "scopes": ["server:filesystem", "server:git"]
}
```

---

## Built-in SQLite Secret Storage (Cross-Platform)

Model Context Gateway includes a native **AES-256-GCM Envelope Encryption Engine** (`DatabaseUserSecretStore`).

* **Zero External Dependencies**: You do not need HashiCorp Vault, Windows DPAPI/Registry, or cloud secret managers.
* **Encrypted At Rest**: Every API key, bearer token, password, or downstream header entered in the Web UI or Admin MCP is automatically encrypted before being written to `./data/mcg.db`.
* **Just-In-Time Downstream Injection**: When an AI client invokes a tool, MCG decrypts the server's credential in-memory and injects the header (e.g., `Authorization: Bearer <key>`) downstream.
* **Auto-Generated Master Key**: The 256-bit encryption key is automatically generated and safely stored in `./data/.master.key` (with `0600` permissions).

---

## AI Client Configuration Snippets

### 1. Claude Desktop (`claude_desktop_config.json`)
```json
{
  "mcpServers": {
    "mcg": {
      "command": "npx",
      "args": ["-y", "mcp-proxy", "http://localhost:8080/sse"],
      "env": {
        "MCP_PROXY_HEADER_AUTHORIZATION": "Bearer mcp-glb-R4t8W1yU-9pM2nQ6sD8fH3jK5"
      }
    }
  }
}
```

### 2. Cursor (`.cursor/mcp.json`)
```json
{
  "mcpServers": {
    "mcg": {
      "url": "http://localhost:8080/sse",
      "headers": {
        "Authorization": "Bearer mcp-glb-R4t8W1yU-9pM2nQ6sD8fH3jK5"
      }
    }
  }
}
```

### 3. VS Code / Cline / Roo-Code (`cline_mcp_settings.json`)
```json
{
  "mcpServers": {
    "mcg": {
      "url": "http://localhost:8080/sse",
      "transport": "sse",
      "headers": {
        "Authorization": "Bearer mcp-glb-R4t8W1yU-9pM2nQ6sD8fH3jK5"
      }
    }
  }
}
```

### 4. Windsurf (`mcp_config.json`)
```json
{
  "mcpServers": {
    "mcg": {
      "serverUrl": "http://localhost:8080/sse",
      "headers": {
        "Authorization": "Bearer mcp-glb-R4t8W1yU-9pM2nQ6sD8fH3jK5"
      }
    }
  }
}
```

### 5. Autonomous Admin Agent (`Admin MCP Server`)
To allow an AI assistant (such as Antigravity) to manage backend servers and configuration dynamically:
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

---

## Homelab Docker MCP Auto-Discovery

When you mount `/var/run/docker.sock:/var/run/docker.sock`, MCG automatically discovers other containers running on your host:

1. Add the label `mcp.enabled=true` to any container in your Docker Compose file:
```yaml
services:
  postgres-mcp:
    image: cschreib/postgres-mcp:latest
    labels:
      - "mcp.enabled=true"
      - "mcp.name=PostgreSQL Database"
      - "mcp.category=databases"
    environment:
      - DATABASE_URL=postgresql://user:pass@db:5432/mydb
```
2. MCG detects the container, registers its SSE/HTTP endpoint, and immediately makes its tools available through the gateway.
