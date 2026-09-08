# Single-User & Home-Lab Setup Guide

This guide details how to install, configure, and operate the **Model Context Gateway (MCG)** for single users, home-lab operators, and local developers.

```mermaid
flowchart TD
    subgraph Ingress ["<b>Ingress & Client Layer</b>"]
        UI["<b>Local Web UI / Admin</b><br>• Trusted LAN / Loopback Subnets<br>• Zero-Auth Standalone Admin<br>• Port 8080 | + Generate Key Modal"]
        Clients["<b>Multiple AI Clients</b><br>• Claude Desktop (Scope: 'all')<br>• Cursor (Scope: 'server:docker')<br>• Open WebUI (Scope: 'category:media')<br>• Antigravity (Scope: 'all,admin')"]
    end

    subgraph Core ["<b>Model Context Gateway (MCG Container)</b>"]
        direction TB
        C1["<b>Zero-Config Safe Defaults:</b> Standalone AppKeys & AES-GCM Encryption enabled"]
        C2["<b>Zero-Cert Bootstrapping:</b> Auto-generates OpenIddict signing credentials"]
        C3["<b>AppKey Auth & Scopes:</b> Validates compact keys with granular scope boundaries"]
        C4["<b>Multi-Key Provisioning:</b> Web UI, MCG_CLIENT_APP_KEYS, or manage_appkeys"]
        C1 --> C2 --> C3 --> C4
    end

    subgraph Backends ["<b>Downstream Capability Fleet</b>"]
        DockerSock["<b>Docker Socket Discovery</b><br>• Labeled: <code>mcp.enabled=true</code><br>• Auto-registers container tools"]
        Servers["<b>Local / Remote MCP Servers</b><br>• Python, FastMCP, Node, Postgres<br>• Home Assistant, Filesystem, Git"]
    end

    UI & Clients ==> Core
    Core ==> DockerSock & Servers

    classDef ingStyle fill:#161b22,stroke:#ff5f1f,stroke-width:1.5px,color:#fff;
    classDef coreStyle fill:#0f2e1b,stroke:#00c853,stroke-width:2px,color:#fff;
    classDef backStyle fill:#161b22,stroke:#30363d,stroke-width:1px,color:#e6edf3;
    class UI,Clients ingStyle;
    class C1,C2,C3,C4 coreStyle;
    class DockerSock,Servers backStyle;
```

---

## 60-Second Quickstart

Start Model Context Gateway with default settings:

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

### 2. Retrieve Your Keys
On first startup, MCG creates the SQLite database and writes initial keys to `./data/`:

```bash
# General tool calling key for AI clients (Cursor, Claude, Cline)
cat ./data/.client.key
# -> mcp-glb-R4t8W1yU-9pM2nQ6sD8fH3jK5

# Admin key for gateway administration and AI agent configuration (/admin)
cat ./data/.admin.key
# -> mcp-adm-Xk9L2mPq-7vN3wZ8aB1cE4fG9
```

### 3. Open the Web Dashboard
Open `http://localhost:8080/` in your browser. From localhost or your local LAN subnet, you have immediate administrator access without login prompts.

---

## AppKey Scoping & Permissions

You can create multiple AppKeys to control which tools each AI assistant or script can run.

### Scope Syntax & Rules

| Scope Type | Example Syntax | Description | Best For |
| :--- | :--- | :--- | :--- |
| **Global Access** | `all` or `*` | Full access to all backend servers and tools | Claude Desktop, Antigravity |
| **Server Scoped** | `server:docker`, `server:postgres` | Restricts access to one backend server | Cursor, VS Code / Cline |
| **Category Scoped** | `category:devops`, `category:media` | Restricts access to servers with that category | Specialized AI assistants |
| **Tool Scoped** | `tool:docker__list_containers` | Restricts access to a single specific tool | Scripts and webhooks |
| **Capability Scoped**| `resources:read`, `prompts:read` | Read-only context lookup without tool execution | Documentation assistants |
| **System Admin** | `admin` | Full gateway management via Admin MCP server (`/admin`) | Admin AI assistants |

---

## Managing AppKeys

### Option A: Web Dashboard
1. Open `http://localhost:8080/` and click **App Keys & Security**.
2. Click **+ Generate App Key**.
3. Enter a name (for example: `Cursor - Docker Only`), select a scope (such as `server:docker`), and click **Generate Key**.
4. The dashboard displays the secret key once. Copy it to your client settings.

### Option B: Docker Compose Environment Variable
Pre-seed keys on startup using `MCG_CLIENT_APP_KEYS`:

```ini
MCG_CLIENT_APP_KEYS=mcp-glb-claudeFull123:ClaudeDesktop:all,mcp-srv-cursorDocker456:Cursor:server:docker,mcp-grp-openWebUI789:OpenWebUI:category:media
```

### Option C: AI Agent Tool Call
AI coding assistants connected to `/admin` can call the `manage_appkeys` tool directly:

```json
{
  "action": "create",
  "name": "Cline Key",
  "scopes": ["server:filesystem", "server:git"]
}
```

---

## Built-in SQLite Secret Storage

Model Context Gateway includes built-in credential encryption using AES-256-GCM.

* **Zero External Dependencies**: You do not need Vault or external secret managers for personal use.
* **Encrypted at Rest**: Every API key or password entered in the dashboard is encrypted before being written to `./data/mcg.db`.
* **Just-In-Time Header Injection**: When an AI client runs a tool, MCG decrypts the server credential in memory and sends it to the backend server.
* **Auto-Generated Master Key**: The 256-bit encryption key is stored safely in `./data/.master.key` (with `0600` permissions).

---

## AI Client Configuration Snippets

### 1. Claude Desktop (`claude_desktop_config.json`)
```json
{
  "mcpServers": {
    "mcg": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/client-sse", "http://localhost:8080/sse"],
      "env": {
        "Authorization": "Bearer mcp-glb-R4t8W1yU-9pM2nQ6sD8fH3jK5"
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
