---
layout: home

hero:
  name: Model Context Gateway
  text: Enterprise MCP Router & Control Plane
  tagline: High-performance C# gateway connecting AI assistants and IDEs to your tools with zero-trust security, sandboxed Code Mode orchestration, and sub-millisecond semantic search.
  image:
    src: /logo.svg
    alt: Model Context Gateway Logo
  actions:
    - theme: brand
      text: Get Started
      link: /getting-started/
    - theme: alt
      text: MCP Code Mode
      link: /user-guide/code-mode
    - theme: alt
      text: User Guide
      link: /user-guide/
    - theme: alt
      text: GitHub
      link: https://github.com/spelech/model-context-gateway

features:
  - icon: 💻
    title: MCP Code Mode (execute_script)
    details: Orchestrate multi-step tool workflows in a single LLM turn inside an isolated Jint JavaScript sandbox with automatic JSON unboxing and strict resource quotas.
  - icon: 🎯
    title: Meta-Mode Context Optimization
    details: Exposes only search_tools, execute_tool, and execute_script by default. Eliminates context window bloat across hundreds of downstream tools.
  - icon: ⚡
    title: Calibrated Hybrid Semantic Search
    details: .NET 10 hardware SIMD TensorPrimitives and normalized lexical scoring with configurable relevance floors and relative score drop-off filtering.
  - icon: 🛡️
    title: Enterprise Identity & Zero Trust
    details: Active Directory Kerberos, OIDC header SSO, scoped AppKeys with atomic rotation, and per-user 3LO OAuth delegation for SaaS tools.
  - icon: 🔐
    title: Envelope Encryption & Vault
    details: Hardware AES-256-GCM database envelope encryption and HashiCorp Vault KV v2 secret engine with automated background token refresh.
  - icon: 🤖
    title: Autonomous Admin Control Plane
    details: Docker container auto-discovery and 10 standard Admin MCP tools enabling AI agents to manage servers, RBAC policies, and live diagnostics.
---

<p align="center" class="badge-row" style="margin-top: 2rem;">
  <img src="https://img.shields.io/badge/version-v5.23.0-orange?style=for-the-badge" alt="Version" />
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&amp;logo=dotnet&amp;logoColor=white" alt=".NET 10.0" />
  <img src="https://img.shields.io/badge/MCP%20Spec-2026--07--28-0052CC?style=for-the-badge" alt="MCP Spec" />
  <img src="https://img.shields.io/badge/tests-1%2C209%20passing-2ea44f?style=for-the-badge" alt="Tests" />
  <img src="https://img.shields.io/badge/docker-ready-2496ED?style=for-the-badge&amp;logo=docker&amp;logoColor=white" alt="Docker Ready" />
  <img src="https://img.shields.io/badge/frontend-Vite%20React%2019-61DAFB?style=for-the-badge&amp;logo=react&amp;logoColor=black" alt="React 19" />
  <img src="https://img.shields.io/badge/license-Apache--2.0-blue?style=for-the-badge" alt="License" />
</p>

---

## What is Model Context Gateway?

The **Model Context Protocol (MCP)** connects AI assistants to external tools and data sources.

Connecting an AI client directly to dozens of individual MCP servers creates operational bottlenecks:
* **Context Bloat**: Loading hundreds of tool schemas consumes 40,000–80,000 tokens before your conversation begins.
* **Multi-Step Round-Trip Tax**: Chaining five tool calls requires five sequential LLM turns and floods the prompt with raw intermediate JSON.
* **Security Exposure**: Plaintext API keys and tokens sit scattered across local IDE configuration files.
* **Multi-User Isolation Gaps**: Direct connections lack per-user RBAC policies and individual 3LO OAuth token isolation.

**Model Context Gateway (MCG) solves these challenges:**
* **Single Gateway Endpoint (`/sse`)**: Connect your IDE or AI agent to one URL. MCG routes requests across all registered backend servers.
* **Meta-Mode & Code Mode (`search_tools`, `execute_tool`, `execute_script`)**: Discover relevant tools on demand with calibrated hybrid search, or execute multi-tool JavaScript workflows in a single turn inside an isolated sandbox.
* **Zero-Trust Security & Per-User OAuth**: Protect credentials with AES-256-GCM envelope encryption or HashiCorp Vault, enforce 4-stage RBAC policies, and delegate per-user 3LO OAuth tokens automatically.
* **Universal Transport Support**: Route across Docker containers, remote HTTP/SSE services, and local STDIO subprocesses (Node.js, Python, uv, bun) with configurable background health probing.

![Model Context Gateway Dashboard](assets/dashboard.jpg)

---

## Core Architecture

```mermaid
flowchart LR
    subgraph Clients["AI Clients & IDEs"]
        Claude["Claude Desktop"]
        Cursor["Cursor / VS Code"]
        Agents["Autonomous Agents"]
    end

    subgraph Gateway["Model Context Gateway (MCG)"]
        Auth["OAuth / OIDC SSO / AppKey"]
        MetaMode["Meta-Mode Router<br/>(search_tools / execute_tool)"]
        CodeMode["Code Mode Sandbox<br/>(execute_script / Jint)"]
        Vector["Calibrated Hybrid Search<br/>(SIMD Vector + Lexical)"]
        AdminMCP["Admin MCP Server<br/>(/admin, /mcg-admin)"]
        Secrets["Secret Providers<br/>(Vault / DPAPI / AES)"]
    end

    subgraph Backends["Downstream MCP Servers"]
        DockerSrv["Docker Containers<br/>(Auto-Discovery)"]
        SSESrv["Remote SSE / HTTP Servers"]
        StdioSrv["Subprocess STDIO<br/>(Node / Python / uv / bun)"]
    end

    Clients -->|Bearer / SSE| Auth
    Auth --> MetaMode
    Auth --> CodeMode
    Auth --> AdminMCP
    MetaMode <--> Vector
    CodeMode --> MetaMode
    MetaMode --> Secrets
    Secrets --> DockerSrv
    Secrets --> SSESrv
    Secrets --> StdioSrv
```

---

## Quickstart: Zero-Config Startup

Run the gateway container with zero initial configuration. On first boot, the gateway automatically generates a 256-bit AES Master Key in `./data/.master.key` and initializes a SQLite database:

::: code-group

```bash [Docker CLI]
docker run -d \
  --name mcg \
  --restart unless-stopped \
  -p 8080:8080 \
  -v $(pwd)/data:/app/data \
  -v /var/run/docker.sock:/var/run/docker.sock \
  ghcr.io/spelech/model-context-gateway:latest
```

```yaml [Docker Compose]
services:
  mcg:
    image: ghcr.io/spelech/model-context-gateway:latest
    container_name: mcg
    restart: unless-stopped
    ports:
      - "8080:8080"
    volumes:
      - ./data:/app/data
      - /var/run/docker.sock:/var/run/docker.sock
    environment:
      - DB_PROVIDER=sqlite
      - MCG_ADMIN_KEY=mcp-adm-prod-bootstrap-token-99
```

:::

### Live Gateway Endpoints

* **Web UI Dashboard**: `http://localhost:8080/`
* **Health Check**: `http://localhost:8080/health` → `{"status":"healthy","service":"ModelContextGateway","version":"5.23.0"}`
* **Meta-Mode & Code Mode Gateway**: `http://localhost:8080/sse`
* **Admin MCP Server**: `http://localhost:8080/admin/sse` (or `POST /admin` / `GET /mcg-admin/sse`)
* **Direct Backend Proxy**: `http://localhost:8080/{targetServerId}`

---

## Documentation Directory

### Getting Started & Deployment

<div class="grid cards">

<ul>
<li>

🚀 **[Getting Started & Quickstart](getting-started/index.md)**

---

5-minute setup paths for homelab users, enterprise platform engineers, and Windows IIS administrators.

</li>
<li>

🐳 **[Container Deployment Guide](deployment/docker.md)**

---

Production Docker, Docker Compose, `latest` vs `latest-full` images, and multi-database configurations.

</li>
<li>

🏠 **[Single-User & Home-Lab Setup](deployment/homelab.md)**

---

Fast setup for personal automation, home labs, SQLite database, and local AI clients.

</li>
<li>

🪟 **[Windows & IIS Deployment](deployment/windows-iis.md)**

---

Native Windows Server IIS in-process hosting (ANCM v2), Windows Service (SCM), and DPAPI encryption.

</li>
</ul>

</div>

### User Guide & MCP Code Mode

<div class="grid cards">

<ul>
<li>

📖 **[Official User Guide](user-guide/index.md)**

---

Interactive dashboard walkthrough, server registration, health check modes, RBAC management, and client setup.

</li>
<li>

💻 **[MCP Code Mode (`execute_script`)](user-guide/code-mode.md)**

---

Write sandboxed JavaScript workflows that chain `mcg.callTool` and `tools.<server>.<tool>` in a single turn.

</li>
<li>

🧪 **[Interactive Test Bench](user-guide/test-bench/index.md)**

---

Execute tools with schema forms, simulate calibrated hybrid search, and inspect live JSON-RPC streams.

</li>
<li>

⚙️ **[System Settings & Embeddings](user-guide/settings.md)**

---

Configure local ONNX or OpenAI embeddings, search drop-off thresholds, identity providers, and Code Mode limits.

</li>
</ul>

</div>

### Enterprise Security & Authentication

<div class="grid cards">

<ul>
<li>

🛡️ **[Authentication Architecture](authentication-architecture.md)**

---

Active Directory SIDs, OIDC reverse proxy SSO, standalone trust, and 4-stage RBAC evaluation.

</li>
<li>

🔑 **[AppKey Scopes & Authorization](appkey-scopes.md)**

---

Scope grammar (`*`, `server:*`, `category:*`, `tool:*`), atomic key rotation, usage tracking, and SHA-256 hashing.

</li>
<li>

🔐 **[Per-User OAuth & Connected Accounts](auth-flows/per-user-oauth-flow.md)**

---

Individual user authentication (Google Home, GitHub, Slack), 3LO protocol, encrypted vaulting, and auto-refresh.

</li>
<li>

🔒 **[Secret Providers & Key Management](secret-providers.md)**

---

HashiCorp Vault KV v2 JIT renewal, Windows DPAPI, Master Key lifecycle, and zero-downtime hot reloading.

</li>
<li>

🏢 **[Active Directory & RBAC Guide](active-directory-and-rbac-guide.md)**

---

Inbound Windows Kerberos, LDAPS `tokenGroups` recursive resolution, and multi-level group policies.

</li>
<li>

🧑‍🍳 **[MCP Server Auth Cookbook](mcp-server-auth-cookbook.md)**

---

Scenario-driven integration recipes for Bearer auth, Custom Headers, Vault, BYOK, and Pass-Through.

</li>
</ul>

</div>

### Architecture, Operations & Reference

<div class="grid cards">

<ul>
<li>

🗺️ **[Comprehensive Architecture](architecture/index.md)**

---

System topology, component boundaries, routing & Meta-Mode engine, and Code Mode sandbox internals.

</li>
<li>

🤖 **[Admin MCP Automation Guide](admin-mcp-automation-guide.md)**

---

Autonomous agent administration via the `mcg-admin` skill and the 10 built-in administrative MCP tools.

</li>
<li>

🗄️ **[Database Providers & Data Model](database-providers.md)**

---

Canonical 12-table ERD, dialect specifications for SQLite, MSSQL, and MySQL, and migration guide.

</li>
<li>

🔄 **[Downstream Transports Guide](transports.md)**

---

SSE, HTTP/streamable, subprocess STDIO security policies, and configurable `RootOrigin` / `JsonRpcPing` health checks.

</li>
<li>

📋 **[SRS & Test Catalog](software-requirements-and-test-catalog.md)**

---

Living Software Requirements Specification, requirement taxonomy (`AUTH`, `MCP`, `SEC`, `GUARD`), and test proofs.

</li>
<li>

⚠️ **[Troubleshooting & Runbooks](operations/index.md)**

---

Operational runbooks, health diagnostics, and root cause analysis for STDIO, OAuth tokens, and database locks.

</li>
</ul>

</div>

---

## Connecting AI Clients

### 1. Claude Desktop (`claude_desktop_config.json`)

::: code-group

```json [Meta-Mode Gateway (/sse)]
{
  "mcpServers": {
    "mcg": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/client-sse", "http://localhost:8080/sse"]
    }
  }
}
```

```json [Admin Control Plane (/admin)]
{
  "mcpServers": {
    "mcg-admin": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/client-sse", "http://localhost:8080/admin"]
    }
  }
}
```

:::

### 2. Cursor / Windsurf / Cline (`mcp.json` / `cline_mcp_settings.json`)

```json
{
  "mcpServers": {
    "mcg": {
      "url": "http://localhost:8080/sse",
      "headers": {
        "Authorization": "Bearer mcp-usr-my-developer-token-123"
      }
    }
  }
}
```

---

## Verification & Quality Assurance

All features, security guardrails, and authentication flows are validated across automated test suites:

* **xUnit Backend Suite**: 917+ integration & unit tests ([`ModelContextGateway.Tests`](developer-guide.md#backend-test-suite))
* **Vitest Frontend Suite**: 269+ component and state store tests ([`frontend/src/test`](developer-guide.md#frontend-vitest-suite))
* **Playwright E2E Suite**: 23+ end-to-end browser automation specs ([`frontend/e2e`](developer-guide.md#end-to-end-testing-playwright))
* **Living Requirements Matrix**: Zero-drift catalog generation via `dotnet run --project scripts/CatalogGenerator -- --verify-only` ([SRS Catalog](software-requirements-and-test-catalog.md))
