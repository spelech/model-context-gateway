# Model Context Gateway (MCG)

<div align="center">

![Version](https://img.shields.io/badge/version-v5.10.0-orange?style=for-the-badge)
![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![MCP Spec](https://img.shields.io/badge/MCP%20Spec-2026--07--28-0052CC?style=for-the-badge)
![Tests](https://img.shields.io/badge/tests-1%2C063%20passing-2ea44f?style=for-the-badge)
![Docker Ready](https://img.shields.io/badge/docker-ready-2496ED?style=for-the-badge&logo=docker&logoColor=white)
![React 19](https://img.shields.io/badge/frontend-Vite%20React%2019-61DAFB?style=for-the-badge&logo=react&logoColor=black)
![License](https://img.shields.io/badge/license-Apache--2.0-blue?style=for-the-badge)

</div>

---

**Model Context Gateway (MCG)** is a high-performance C# ASP.NET Core gateway, OAuth 2.0 provider, and semantic proxy for the [Model Context Protocol (MCP)](https://modelcontextprotocol.io).

It aggregates hundreds of tools from isolated backend servers (Docker, Home Assistant, SQL databases, Plex, Actual Budget, Excel, custom APIs, and STDIO subprocesses) and proxies them to AI clients (Claude Desktop, Cursor, Cline, Windsurf, Antigravity) through a single unified connection.

### The Problem & Solution

Connecting AI clients directly to dozens of disparate backend tools creates significant operational overhead: credentials leak across developer environments, protocol configurations diverge, and loading hundreds of tool schemas saturates the model's context window with irrelevant tokens.

**Model Context Gateway provides a unified, governed front-door for all your AI tools:**

* **Single Connection Endpoint (`/sse`)**: AI assistants connect once to a dependable gateway rather than juggling dozens of point-to-point connections.
* **Context Optimization (Meta-Mode)**: Instead of injecting a massive catalog of tool schemas into the model's prompt, Meta-Mode exposes lightweight discovery tools (`search_tools` and `execute_tool`). The LLM finds and executes the exact tool it needs on demand.
* **Centralized Governance & Security**: Enforces role-based access control (RBAC), authenticates Active Directory SIDs or OIDC headers, derives scoped AppKeys, and redacts PII before requests touch backend infrastructure.
* **Universal Transport Routing**: Seamlessly routes requests across Docker containers, remote HTTP/SSE services, and local STDIO subprocesses without client-side reconfiguration.

![Model Context Gateway Dashboard](assets/dashboard.jpg)

---

## Core Architecture Highlights

```mermaid
flowchart LR
    subgraph Clients["AI Clients & IDEs"]
        Claude["Claude Desktop"]
        Cursor["Cursor / Windsurf"]
        Agents["Autonomous Agents"]
    end

    subgraph Gateway["Model Context Gateway (MCG)"]
        Auth["OAuth / Reverse Proxy Auth / AppKey"]
        MetaMode["Meta-Mode Router\n(search_tools / execute_tool)"]
        Vector["Semantic Vector Search\n(Local ONNX / OpenAI)"]
        AdminMCP["Admin MCP Server\n(/admin, /mcg-admin)"]
        Secrets["Secret Providers\n(Vault / DPAPI / AES)"]
    end

    subgraph Backends["Downstream MCP Servers"]
        DockerSrv["Docker Containers\n(Auto-Discovery)"]
        SSESrv["Remote SSE / HTTP Servers"]
        StdioSrv["Subprocess STDIO\n(Node / Python / uv / bun)"]
    end

    Clients -->|Bearer / SSE| Auth
    Auth --> MetaMode
    Auth --> AdminMCP
    MetaMode <--> Vector
    MetaMode --> Secrets
    Secrets --> DockerSrv
    Secrets --> SSESrv
    Secrets --> StdioSrv
```

* **Meta-Mode Dynamic Tool Filtering**: Exposes only `search_tools` and `execute_tool` on `/sse` by default, eliminating LLM context window bloat while dynamically ranking and routing across hundreds of backend tools on demand.
* **Multi-Tenant Auth & Zero-Config Standalone**: Native Active Directory LDAP / Kerberos, OIDC reverse proxy headers (`Remote-User`, `Remote-Groups` from Authentik, Authelia, Keycloak), scoped AppKeys (`mcp-adm-`, `mcp-usr-`, `mcp-srv-`), and trusted local loopback for personal home-labs.
* **In-Process Admin MCP Control Plane (`/admin`, `/mcg-admin`)**: Autonomous AI agents can manage providers, servers, RBAC policies, group mappings, vector search, and personal AppKeys via standard MCP tool calls without manual UI clicking.
* **Dual-Provider Semantic Vector Search**: Local in-process CPU vector embeddings (`all-MiniLM-L6-v2` via `Microsoft.ML.Tokenizers`) or remote OpenAI-compatible API providers stored securely in SQLCipher/AES encrypted databases.
* **Enterprise Secrets & Key Lifecycle**: Just-in-time secret retrieval from HashiCorp Vault (KV v2), Windows Registry (DPAPI), or Environment Variables with AES-256-GCM envelope encryption and dynamic master key rotation.
* **Docker Label Auto-Discovery**: Mounts `/var/run/docker.sock` to dynamically discover and register containers with `mcp.enabled=true` labels with zero manual registration.
* **Multi-Database Support**: First-class stored procedure suites across SQLite, Microsoft SQL Server, and MySQL via Dapper.

---

## Documentation Portal Navigation

<div class="grid cards" markdown>

-   :material-rocket-launch: __[Deployment & Getting Started](deployment-guide.md)__

    ---

    Blank-slate Docker, Docker Compose, Windows IIS setup, environment variables, master encryption keys, and zero-config deployment.

-   :material-home: __[Single-User & Home-Lab Setup](single-user-and-homelab-guide.md)__

    ---

    60-second setup, loopback trust, SQLite database, Docker container auto-discovery, and local AI client integration.

-   :material-shield-account: __[Authentication & Identity Architecture](authentication-architecture.md)__

    ---

    Active Directory SIDs, OIDC reverse proxy SSO, standalone loopback trust, AppKey scopes, and token exchange flows.

-   :material-cog-transfer: __[Enterprise Administration Guide](admin-guide.md)__

    ---

    Operational administration, provider configuration, group mappings, master key rotation, and system management.

-   :material-robot: __[Admin MCP Automation Guide](admin-mcp-automation-guide.md)__

    ---

    Autonomous agent administration via the `mcg-admin` skill, control plane tools, and programmatic provisioning.

-   :material-book-open-page-variant: __[Official User Guide Suite](user-guide/README.md)__

    ---

    Interactive dashboard walkthrough, server registration, RBAC management, client configuration, and test bench usage.

-   :material-chef-hat: __[MCP Server Auth Cookbook](mcp-server-auth-cookbook.md)__

    ---

    Scenario-driven integration recipes for Bearer auth, Custom Headers, Vault, BYOK, Pass-Through, and Identity-Forwarding.

-   :material-sitemap: __[Comprehensive Architecture](architecture.md)__

    ---

    Complete enterprise architecture specification, sequence diagrams, component models, and AES-256-GCM encryption pipelines.

-   :material-database: __[Database Providers & Data Model](database-providers.md)__

    ---

    Canonical 12-table ERD, dialect specifications for SQLite, MSSQL, and MySQL, stored procedures, and migration guide.

-   :material-key-wireless: __[AppKey Scopes & Authorization](appkey-scopes.md)__

    ---

    Scope grammar (`*`, `server:*`, `category:*`, `tool:*`), evaluation pipeline, least-privilege personas, and token hashing.

-   :material-safe: __[Secret Providers & Key Management](secret-providers.md)__

    ---

    HashiCorp Vault KV v2 JIT renewal, Windows DPAPI, Master Key lifecycle, and secure credential storage.

-   :material-swap-horizontal: __[Downstream Transports Guide](transports.md)__

    ---

    SSE, HTTP/streamable, subprocess STDIO security policies, environment secret injection, and process tree isolation.

-   :material-clipboard-check: __[SRS & Test Catalog](software-requirements-and-test-catalog.md)__

    ---

    Living Software Requirements Specification, requirement taxonomy (`AUTH`, `MCP`, `SEC`, `GUARD`), and test verification matrix.

-   :material-alert-circle-outline: __[Troubleshooting & RCA Guide](mcp-routing-and-admin-issues.md)__

    ---

    In-depth root cause analysis and resolution guide covering session lifecycles, cache synchronization, and downstreams.

-   :material-file-document-outline: __[Management Overview & Briefing](management-brief.md)__

    ---

    Executive-level summary of architecture, selling points, enterprise governance, and NotebookLM briefing prompt.

</div>

---

## Quickstart: Zero-Config Startup

Run the gateway container with zero required configuration. On first boot, the gateway automatically generates a 256-bit AES Master Key in `./data/.master.key` and initializes a secure SQLite database:

=== "Docker CLI"

    ```bash
    docker run -d \
      --name mcg \
      --restart unless-stopped \
      -p 8080:8080 \
      -v $(pwd)/data:/app/data \
      -v /var/run/docker.sock:/var/run/docker.sock \
      ghcr.io/spelech/model-context-gateway:latest
    ```

=== "Docker Compose"

    ```yaml
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

### Live Endpoints

* **Web UI Dashboard**: [`http://localhost:8080/`](http://localhost:8080/)
* **Health Check**: [`http://localhost:8080/health`](http://localhost:8080/health) &rarr; `{"status":"healthy","service":"ModelContextGateway","version":"5.10.0"}`
* **Meta-Mode Gateway**: `http://localhost:8080/sse`
* **Admin MCP Server**: `http://localhost:8080/admin/sse` (or `POST /admin` / `GET /mcg-admin/sse`)
* **Direct Backend Proxy**: `http://localhost:8080/{targetServerId}`

---

## Connecting AI Clients

### 1. Claude Desktop (`claude_desktop_config.json`)

=== "Meta-Mode Gateway (/sse)"

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

=== "Admin Control Plane (/admin)"

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

* **xUnit Backend Suite**: 810 integration & unit tests ([`ModelContextGateway.Tests`](developer-guide.md#backend-test-suite))
* **Vitest Frontend Suite**: 253 component and state store tests ([`frontend/src/test`](developer-guide.md#frontend-vitest-suite))
* **Playwright E2E Suite**: End-to-end browser automation ([`frontend/e2e`](developer-guide.md#end-to-end-testing-playwright))
* **Living Requirements Matrix**: Zero-drift catalog generation via `dotnet run --project scripts/CatalogGenerator -- --verify-only` ([SRS Catalog](software-requirements-and-test-catalog.md))
