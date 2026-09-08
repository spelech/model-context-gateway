---
name: mcg-setup
description: Use when installing, configuring, deploying, or bootstrapping the Model Context Gateway (MCG) in a new or existing environment (Docker, Docker Compose, or Windows IIS) for personal home-lab or enterprise use.
---

# Universal Model Context Gateway Setup (`mcg-setup`)

## Overview

**Model Context Gateway (MCG)** is a high-performance ASP.NET Core gateway and proxy for the Model Context Protocol (MCP). It aggregates multiple backend MCP servers, exposes a token-efficient **Meta-Mode** (`/sse`) with `search_tools` and `execute_tool` to prevent context window bloat, and provides an encrypted database-backed **Admin MCP Server** (`/admin` or `/mcg-admin`) and Web UI for hot-reloading configurations at runtime.

This skill provides an autonomous 6-phase decision and bootstrapping engine to install and configure the gateway in any environment without needing to clone or compile source code.

---

## When to Use

```mermaid
flowchart TD
    Start["<b>[Start Setup]</b>"]
    
    subgraph EnvSelection ["Phase 1 & 2: Environment Probing"]
        Docker["<b>Docker / Containers</b><br>Linux / WSL / Compose"]
        IIS["<b>Windows Server IIS</b><br>Native In-Process ANCM v2"]
    end

    subgraph ConfigParadigm ["Phase 3: Configuration Paradigm"]
        EnvConfig["<b>Environment Variables</b><br>(Static / 12-Factor)"]
        UIConfig["<b>Web UI & Database</b><br>(Dynamic Hot-Reload)"]
    end

    subgraph IdentityMode ["Phase 4: Identity & Network Mode"]
        Standalone["<b>Personal / Standalone</b><br>(Loopback / LAN CIDR)"]
        Enterprise["<b>Enterprise AD / OIDC</b><br>(SSO / Forward-Auth)"]
    end

    Artifacts["<b>Phase 5: Generate Artifacts</b><br><i>(256-bit Key, Compose / web.config)</i>"]
    Health["<b>Phase 6: Health & Client Setup</b><br><i>(Claude, Cursor, Cline, Windsurf)</i>"]

    Start --> Docker & IIS
    Docker & IIS --> EnvConfig & UIConfig
    EnvConfig & UIConfig --> Standalone & Enterprise
    Standalone & Enterprise --> Artifacts --> Health

    classDef startStyle fill:#161b22,stroke:#ff5f1f,stroke-width:2px,color:#fff;
    classDef optStyle fill:#161b22,stroke:#30363d,stroke-width:1px,color:#e6edf3;
    classDef endStyle fill:#0f2e1b,stroke:#00c853,stroke-width:2px,color:#fff;
    class Start startStyle;
    class Docker,IIS,EnvConfig,UIConfig,Standalone,Enterprise optStyle;
    class Artifacts,Health endStyle;
```

### Trigger Conditions & Use Cases
- User wants to install, deploy, or configure `Model Context Gateway (MCG)` from scratch.
- Deploying the gateway via Docker Compose, Docker CLI, or Windows IIS.
- Connecting AI IDEs (Claude Desktop, Cursor, Cline, Windsurf, Antigravity) to an aggregated MCP gateway.
- Setting up standalone home-lab routing or enterprise SSO/AD authentication.
- Troubleshooting missing `MCG_MASTER_KEY`, 403 network access, or client connection errors.

### When NOT to Use
- Connecting to an individual, standalone MCP server directly without an aggregator or gateway.
- Modifying gateway source code or writing C# unit tests (refer to repository developer guides instead).

---

## Phase 1: Automated Environment Probing

Before prompting the user, inspect the host environment to determine defaults and available capabilities.

### 1.1 Probing Commands

Run the appropriate detection commands for the platform:

```bash
# 1. OS & Architecture Detection
uname -s -m 2>/dev/null || echo ""

# 2. Docker Daemon Availability
docker info >/dev/null 2>&1 && echo "DOCKER_AVAILABLE=true" || echo "DOCKER_AVAILABLE=false"
test -e /var/run/docker.sock && echo "DOCKER_SOCK_FOUND=true" || echo "DOCKER_SOCK_FOUND=false"

# 3. Vault & Secret Store Detection
if [ -n "" ]; then echo "VAULT_DETECTED="; fi

# 4. Windows Active Directory Context (PowerShell / Windows)
# In PowerShell:
# if ($env:USERDNSDOMAIN) { Write-Output "AD_DOMAIN=$env:USERDNSDOMAIN" }
```

### 1.2 Probe Interpretation
- **Docker socket present & daemon running**: Recommend Docker Compose (Phase 2).
- **Windows host with `USERDNSDOMAIN` or IIS**: Recommend Windows IIS / Service track with Active Directory authentication.
- **`VAULT_ADDR` present**: Pre-populate HashiCorp Vault secret provider options.

---

## Phase 2: Hosting Platform Selection

Present the user with the two primary hosting platforms:

| Platform | Recommended When | Requirements |
| :--- | :--- | :--- |
| **Docker / Compose** *(Recommended)* | Linux, macOS, WSL2, Home-Lab servers, Kubernetes | Docker Engine 20.10+, Docker Compose v2+ |
| **Windows IIS / Service** | Enterprise Windows Server, native Windows Auth / DPAPI | Windows Server 2019+, IIS with ASP.NET Core Module v2 (in-process) |

---

## Phase 3: Configuration Paradigm (Env vs. UI & Database)

Explain the trade-offs between static environment variable configuration and dynamic database configuration:

| Dimension | Environment Variables (`.env`) | Web UI & Database (Dynamic) |
| :--- | :--- | :--- |
| **Management** | Static declarative files (`.env`, `docker-compose.yml`) | Browser Web UI or `/admin` MCP Server |
| **Updates** | Requires container/service restart | Zero-downtime dynamic hot-reloading |
| **Secret Storage** | Plaintext in environment / compose files | AES-256-GCM / SQLCipher encrypted at rest |
| **AI Agent Admin** | Manual human editing of files | Autonomous agent self-configuration via Admin MCP |
| **Multi-Tenancy** | Single shared gateway configuration | Role-Based Access Control & audit logging |
| **Best For** | CI/CD pipelines, GitOps, minimal static setups | Active multi-server gateways, teams, dynamic tools |

---

## Phase 4: Identity & Network Topology

Select the identity and network access tier based on deployment scope:

### Option A: Personal / Home-Lab (Standalone Mode & Granular AppKeys)
- **Zero-Config Safe Defaults**: SQLite database (`./data/mcg.db`) with built-in AES-256-GCM envelope encryption (`./data/.master.key`). Enterprise providers (Active Directory, LDAP, HeaderAuth, Vault) are disabled by default.
- **Auto-Generated & Scoped AppKeys**:
  - Auto-generates `./data/.admin.key` (Admin MCP server `/admin/sse` & settings management) and `./data/.client.key` (Global tool calling key for AI IDEs).
  - Supports multiple individualized AppKeys with custom granular scopes (`all`, `server:<name>`, `category:<group>`, `tool:<id>`) declared via `MCG_CLIENT_APP_KEYS` or generated in the Web UI.
- **Zero OpenSSL / Certificate Overhead**: Standalone mode auto-generates `.openiddict.pfx` or uses development signing certs without manual OpenSSL commands.
- **Network Restriction**: Restrict admin UI to loopback or local LAN CIDR subnets using `STANDALONE_ALLOWED_NETWORKS` (`Admin:StandaloneAllowedNetworks`). Example: `STANDALONE_ALLOWED_NETWORKS=127.0.0.1,::1,192.168.0.0/16,10.0.0.0/8`.
- **Deployment Template**: Use [templates/docker-compose.homelab.yml](templates/docker-compose.homelab.yml) for copy-paste Docker Compose scaffolding and see the Single-User & Home-Lab Setup Guide (`docs/single-user-and-homelab-guide.md`).

### Option B: Enterprise Mode
- **Database**: Microsoft SQL Server (`mssql`), MySQL (`mysql`), or SQLite (`sqlite`).
- **Authentication Providers**:
  - **Active Directory (Windows Auth / LDAP)**: Domain SID mapping (e.g., `S-1-5-32-544` for local Administrators).
  - **OIDC / Reverse Proxy Forward-Auth**: Authentik, Authelia, PocketID, Keycloak with forward-auth headers (`Remote-User`, `Remote-Groups`).
- **Secret Providers**: HashiCorp Vault KV v2 (`VAULT_ADDR`, `VAULT_TOKEN`) or Windows DPAPI.

---

## Phase 5: Artifact Generation & Secrets Scaffolding

### 5.1 Interactive Admin Key & Master Key Configuration

Before generating deployment manifests, prompt the user for their desired administrative key and master key strategy:

1. **Interactive Admin AppKey Prompt**:
   > *"Enter your desired Admin Key for agent/API access (or press Enter to auto-generate a compact key like `mcp-adm-Xk9L2mPq-7vN3wZ8aB1cE4fG9`):"*
   - Sets `MCG_ADMIN_AUTH_KEY` (or `MCG_ADMIN_KEY`) in `.env` / container environment.
   - Uses concise, high-entropy Base62 keys (~32 characters) following the semantic taxonomy:
     - `mcp-adm-`: System Administrator with full gateway control (`all`, `admin`).
     - `mcp-glb-`: Global tool execution across all backend servers.
     - `mcp-{domain}-` / `mcp-grp-`: Restricted to a specific group or domain (e.g., `mcp-devops-`).
     - `mcp-usr-`: Personal user key tied to a specific username or SID.
     - `mcp-srv-`: Scoped to an individual target backend server.

2. **Master Encryption Key Options**:
   The gateway encrypts database credentials using a 256-bit key. You have four flexible options:
   - **Auto-Generated Persistent Keyfile** *(Default & Recommended)*: Omit `MCG_MASTER_KEY` and the gateway will auto-generate and store `./data/.master.key` (with `chmod 0600`) on first boot.
   - **Vault Master Key Bootstrapping**: If `VAULT_ADDR` is configured, the gateway boots its master key directly from HashiCorp Vault (`secret/data/mcg/master-key`).
   - **File Secret / Docker Secrets**: Set `MCG_MASTER_KEY_FILE=/run/secrets/mcg_master_key`.
   - **Explicit Environment Variable**: Generate a 256-bit Base64 key:

```bash
# Linux / macOS / Bash
openssl rand -base64 32

# Python Fallback
python3 -c "import secrets, base64; print(base64.b64encode(secrets.token_bytes(32)).decode())"

# Windows PowerShell (CSPRNG)
$bytes = New-Object byte[] 32; [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes); [Convert]::ToBase64String($bytes)
```

### 5.2 Docker Compose Artifacts

#### `docker-compose.yml`
```yaml
services:
  mcg:
    image: ghcr.io/spelech/model-context-gateway:latest
    container_name: mcg
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      - DB_PROVIDER=${DB_PROVIDER:-sqlite}
      # Admin Key: compact ~32-char token (or leave blank to auto-generate default)
      - MCG_ADMIN_AUTH_KEY=${MCG_ADMIN_AUTH_KEY:-mcp-adm-Xk9L2mPq-7vN3wZ8aB1cE4fG9}
      # Optional: Auto-generated to ./data/.master.key on first boot if omitted
      # - MCG_MASTER_KEY=${MCG_MASTER_KEY}
      - CORS_ALLOWED_ORIGINS=${CORS_ALLOWED_ORIGINS:-http://localhost:3000,http://localhost:8080}
      - Admin__StandaloneAllowedNetworks__0=${STANDALONE_ALLOWED_NETWORK:-127.0.0.1}
      - Admin__StandaloneAllowedNetworks__1=::1
      # Uncomment for LAN subnet access:
      # - Admin__StandaloneAllowedNetworks__2=192.168.1.0/24
    volumes:
      - ./data:/app/data
      - /var/run/docker.sock:/var/run/docker.sock
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 3
      start_period: 5s
```

#### `.env`
```ini
# Admin API & Agent Access Key (Compact Base62 ~32-char key)
MCG_ADMIN_AUTH_KEY=mcp-adm-Xk9L2mPq-7vN3wZ8aB1cE4fG9

# Core Secrets (Optional: auto-generated to ./data/.master.key if omitted)
MCG_MASTER_KEY=REPLACE_WITH_GENERATED_BASE64_KEY

# Database Configuration
DB_PROVIDER=sqlite

# Network & Admin Access (Loopback or CIDR)
STANDALONE_ALLOWED_NETWORK=127.0.0.1
CORS_ALLOWED_ORIGINS=http://localhost:3000,http://localhost:8080
```

### 5.3 Windows IIS Artifacts

#### `web.config`
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
    </handlers>
    <aspNetCore processPath="dotnet" arguments=".\mcg.dll" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" hostingModel="inprocess">
      <environmentVariables>
        <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        <environmentVariable name="responseBufferLimit" value="0" />
      </environmentVariables>
    </aspNetCore>
    <security>
      <requestFiltering>
        <requestLimits maxAllowedContentLength="52428800" />
      </requestFiltering>
    </security>
  </system.webServer>
</configuration>
```

#### `appsettings.Production.json`
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "ModelContextGateway": "Information"
    }
  },
  "AllowedHosts": "*",
  "DB_PROVIDER": "sqlite",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=data/mcg.db"
  },
  "MCG_ADMIN_AUTH_KEY": "mcp-adm-Xk9L2mPq-7vN3wZ8aB1cE4fG9",
  "Admin": {
    "StandaloneAllowedNetworks": [
      "127.0.0.1",
      "::1"
    ]
  }
}
```

---

## Phase 6: Health Verification & Client Setup

### 6.1 Service Launch & Verification

Start the gateway service and verify health endpoints:

```bash
# Docker Compose Launch
docker compose up -d

# Verify Gateway Health
curl -s -f http://localhost:8080/health || echo "Health check failed"

# Verify SSE Meta-Mode Stream
curl -i -N -H "Accept: text/event-stream" http://localhost:8080/sse
```

Expected Health Check Output:
```json
{"status":"Healthy","database":"Connected","timestamp":"..."}
```

---

### 6.2 Client Configuration Snippets

Provide ready-to-use configuration JSON for the user's AI assistants:

#### Claude Desktop (`claude_desktop_config.json`)
```json
{
  "mcpServers": {
    "mcg": {
      "command": "npx",
      "args": ["-y", "mcp-proxy", "http://localhost:8080/sse"]
    }
  }
}
```

#### Cursor (`.cursor/mcp.json`)
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

#### Cline (`cline_mcp_settings.json` / VS Code)
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

#### Windsurf (`mcp_config.json`)
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

#### Autonomous Agent Gateway Administration (`Admin MCP Server`)
To allow an AI agent to manage, add, edit, or delete backend MCP servers, auth providers, secret stores, and access policies dynamically:
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

> [!TIP]
> **Next Step: Automated Provider Provisioning**
> Once the gateway is deployed and running, use the **`mcg-admin`** skill (`.agents/skills/mcg-admin/SKILL.md`) to autonomously configure Authentik, Keycloak, Microsoft Entra ID, Active Directory, HashiCorp Vault, semantic search embeddings, access policies, and backend servers.

---

## Common Mistakes & Troubleshooting

| Symptom / Error | Root Cause | Solution |
| :--- | :--- | :--- |
| **Container restarts immediately / Key error** | Corrupted or inaccessible master key | Ensure `./data` directory has write permissions (`chmod 777 data` or `chmod 0600 data/.master.key`) or specify a valid 256-bit key via `MCG_MASTER_KEY` / `MCG_MASTER_KEY_FILE`. |
| **`403 Forbidden` on Web UI or Admin endpoints** | Client IP not in standalone allowed list or missing Admin AppKey | Add client IP/CIDR (e.g. `192.168.1.0/24`) to `Admin__StandaloneAllowedNetworks__*` or supply `Authorization: Bearer mcp-adm-...`. |
| **Docker MCP servers fail to spawn** | Gateway container cannot access Docker daemon | Ensure `- /var/run/docker.sock:/var/run/docker.sock` volume is mounted and permissions allow read/write. |
| **SSE streams disconnect or buffer indefinitely in IIS** | IIS response buffering delays text/event-stream chunks | Ensure `<environmentVariable name="responseBufferLimit" value="0" />` is present in `web.config`. |
| **OIDC / Reverse Proxy returns unauthorized** | Missing or stripped forward-auth headers | Verify proxy passes `Remote-User` and `Remote-Groups` headers and upstream IP is in `Oidc:TrustedProxies`. |
| **Database file permission errors on Linux** | SQLite volume `./data` owned by root | Run `mkdir -p data && chmod 777 data` before starting container. |

---

## Quick Reference Commands

```bash
# Generate 256-bit Key
KEY=$(openssl rand -base64 32) && echo "MCG_MASTER_KEY=$KEY"

# Start Gateway
docker compose up -d

# Check Logs
docker compose logs -f mcg

# Probe Meta-Mode Tools List
curl -s http://localhost:8080/health
```
