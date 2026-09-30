# Model Context Gateway (MCG)

![Version](https://img.shields.io/badge/version-v5.20.0-orange?style=for-the-badge)
[![Documentation](https://img.shields.io/badge/docs-GitHub%20Pages-blue?style=for-the-badge&logo=githubpages&logoColor=white)](https://spelech.github.io/model-context-gateway/)
![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![MCP Spec](https://img.shields.io/badge/MCP%20Spec-2026--07--28-0052CC?style=for-the-badge)
![Tests](https://img.shields.io/badge/tests-1%2C209%20passing-2ea44f?style=for-the-badge)
![Docker Ready](https://img.shields.io/badge/docker-ready-2496ED?style=for-the-badge&logo=docker&logoColor=white)
![React 19](https://img.shields.io/badge/frontend-Vite%20React%2019-61DAFB?style=for-the-badge&logo=react&logoColor=black)
![License](https://img.shields.io/badge/license-Apache--2.0-blue?style=for-the-badge)

An enterprise C# ASP.NET Core gateway, OAuth 2.0 provider, and routing proxy for the **Model Context Protocol (MCP)**. 

**Model Context Gateway (MCG)** connects your AI assistants (Claude Desktop, Cursor, Cline, Windsurf, Antigravity) to all your tools and data sources through a single secure connection.

📖 **Documentation Portal:** [https://spelech.github.io/model-context-gateway/](https://spelech.github.io/model-context-gateway/)

![Model Context Gateway Dashboard](docs/assets/dashboard.jpg)

---

## What is Model Context Gateway?

The **Model Context Protocol (MCP)** lets AI assistants use external tools and data sources.

When you connect an AI assistant directly to many individual tools, you face common problems:
* **Memory Waste**: Loading hundreds of tool schemas fills the AI context memory before your conversation begins.
* **Higher Costs and Latency**: Large prompts increase inference costs and response times.
* **Security Risks**: API keys and database passwords sit in plain text across local config files.
* **Configuration Overhead**: You must configure each tool separately in every AI application.

**Model Context Gateway (MCG) solves these problems:**

* **One Connection Endpoint (`/sse`)**: Connect your AI assistant to a single gateway URL. MCG routes requests to the correct tool.
* **Context Optimization (Meta-Mode)**: By default, the gateway exposes only two tools: `search_tools` and `execute_tool`. The AI searches for tools when needed and executes them on demand. This saves context memory and reduces token costs.
* **Central Security**: MCG keeps credentials secure on the server with AES-256 encryption. The gateway checks user permissions before tools run.
* **Universal Tool Support**: Route requests across Docker containers, remote HTTP/SSE services, and local scripts (Node.js, Python) without reconfiguring clients.

---

## Key Capabilities

* **Admin MCP Control Plane (`/admin`, `/mcg-admin`)**: Control the gateway programmatically through standard MCP tools (`manage_servers`, `manage_appkeys`, `manage_clients`, `manage_policies`, `manage_group_mappings`, `manage_providers`, `manage_settings`, `manage_custom_files`, `manage_system`, `test_tool_call`). See [Admin Guide](docs/admin-guide.md).
* **Autonomous Setup & Administration**: Built-in agent skills (`mcg-setup` and `mcg-admin`) let AI agents configure servers, secret stores, and access policies automatically. See [User Guide](docs/user-guide/index.md).
* **Meta-Mode Context Saving**: Hides tool schemas during startup to prevent context memory exhaustion and model hallucinations.
* **Modern Slash Tool Routing**: Use modern slash format (`{namespace}/{tool_name}`) with backwards-compatible format (`{serverId}__{toolName}`) and collision checks.
* **Dynamic Docker Discovery**: Automatically discovers containers labeled `mcp.enabled=true` through `/var/run/docker.sock`. See [Features Guide](docs/features-guide.md).
* **Identity and Single Sign-On**: Authenticate users through **Active Directory** (Windows SIDs) or **OIDC / Reverse Proxy Headers** (Authentik, Keycloak, Authelia). See [Authentication Architecture](docs/authentication-architecture.md).
* **Enterprise Secret Storage**: Resolve credentials at runtime from **HashiCorp Vault (KV v2)**, **Windows Registry (DPAPI)**, **Environment Variables**, **RFC 8693 Token Exchange**, or **Per-User Secret Stores (Database / Vault)**. See [Secret Providers Guide](docs/secret-providers.md).
* **Multi-Database Support**: Run on **SQLite (WAL)**, **Microsoft SQL Server**, or **MySQL**. See [Database Providers Guide](docs/database-providers.md) and [Data Model & ERD](docs/data-model.md).
* **PII Sanitization & Audit Logs**: Redact tokens and passwords automatically while writing complete audit logs.
* **Pre-Configured Docker Image**: The `ghcr.io/spelech/model-context-gateway:latest-full` image includes Node.js, Python 3, `uv`, and `bun` pre-installed for local scripts. See [Transports Guide](docs/transports.md).
* **Web UI Dashboard**: Modern dark-mode web interface with real-time metrics, server health cards, logs, and an interactive test bench.

---

## Quickstart: Run in 2 Minutes

Run **Model Context Gateway** with Docker. The gateway starts with safe default settings:

```bash
docker run -d \
  --name mcg \
  -p 8080:8080 \
  -v $(pwd)/data:/app/data \
  -v /var/run/docker.sock:/var/run/docker.sock \
  ghcr.io/spelech/model-context-gateway:latest
```

### What Happens on First Start
* **Master Key**: Generates a 256-bit AES key at `./data/.master.key` with restricted permissions (`chmod 0600`).
* **Database**: Creates and migrates the SQLite database at `./data/mcg.db`.
* **Local Trust**: Grants admin access to local connections (`127.0.0.1`, `::1`) on the Web Dashboard (`http://localhost:8080`).
* **Admin Key**: Generates a compact admin key (`mcp-adm-...`) saved to `./data/.admin.key` for remote AI agents.
* **Docker Discovery**: Automatically connects to any containers labeled `mcp.enabled=true`.

For homelab instructions, see the [Single-User & Home-Lab Setup Guide](docs/deployment/homelab.md).

---

## Client Integration

### 1. General Tool Access (Meta-Mode Gateway)
Connect your AI assistant to `/sse`:
1. **Search Tools**: The AI calls `search_tools` with a plain text query (for example: `"restart container"`).
2. **Execute Tool**: The AI runs the returned tool (for example: `docker/restart_container`) with `execute_tool(name, arguments)`.

### 2. Connect Your AI Application

#### Claude Desktop (`claude_desktop_config.json`)
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

#### Cursor (`~/.cursor/mcp.json`) / Windsurf / Cline (`cline_mcp_settings.json`)
```json
{
  "mcpServers": {
    "mcg": {
      "url": "http://localhost:8080/sse",
      "headers": {
        "Authorization": "Bearer mcp-adm-Xk9L2mPq-7vN3wZ8aB1cE4fG9"
      }
    }
  }
}
```

### 3. Universal Agent Setup Skill
Install the setup skill in your workspace to let your AI assistant configure the gateway automatically:

```bash
mkdir -p .agents/skills/mcg-setup && curl -fsSL https://raw.githubusercontent.com/spelech/model-context-gateway/main/skills/mcg-setup/SKILL.md -o .agents/skills/mcg-setup/SKILL.md
```

Then tell your agent: *"Set up Model Context Gateway for my environment"*.

---

## Authentication Modes

For complete credential flow diagrams, see the [Authentication Support Matrix](docs/auth-flows/auth-support-matrix.md).

### 1. Standalone Mode (Zero-Config / Personal Network)
* **Active**: Runs automatically when no external identity provider (Active Directory or OIDC) is configured.
* **Local Loopback (`127.0.0.1`, `::1`)**: Local connections receive administrator access automatically.
* **Local Subnets**: Configure `ADMIN__STANDALONE_ALLOWED_NETWORKS__0="192.168.1.0/24"` to trust your home or office network.
* **Remote Clients**: Requests from outside trusted networks require an AppKey (`mcp-adm-...` or `mcp-usr-...`).

### 2. Enterprise Mode (Active Directory & Single Sign-On)
* **Active Directory**: Users matching `Admin:GroupSid` (default: `S-1-5-32-544` / Administrators) receive admin rights.
* **OIDC & Reverse Proxy**: Proxies passing `Remote-User` and `Remote-Groups` headers grant access according to group rules.
* **Group Mappings**: Map external group names to internal roles in the Web Dashboard or database.
* **Admin AppKeys**: AI agents with `admin`, `all`, or `*` scopes receive full administrative access.

---

## Documentation Directory

| Guide | Description |
| :--- | :--- |
| [**Single-User & Home-Lab Setup Guide**](docs/deployment/homelab.md) | Fast setup for personal use, home labs, and local AI clients. |
| [**Official User Guide**](docs/user-guide/index.md) | Web dashboard, server management, AppKeys, and test bench. |
| [**Administrator Guide**](docs/admin-guide.md) | Server management, 10 Admin MCP tools, RBAC policies, and providers. |
| [**Admin MCP Automation Guide**](docs/admin-mcp-automation-guide.md) | AI agent automation with `mcg-admin` and configuration playbooks. |
| [**Architecture Specification**](docs/architecture/index.md) | System components, request flow diagrams, and encryption pipelines. |
| [**Container Deployment Guide**](docs/deployment/docker.md) | Production Docker, Docker Compose, and environment settings. |
| [**Operations Runbook**](docs/runbook.md) | Health checks, database backups, key rotation, and disaster recovery. |
| [**Windows & IIS Deployment Guide**](docs/deployment/windows-iis.md) | Windows Server IIS hosting, Windows services, and DPAPI keys. |
| [**MCP Server Auth Cookbook**](docs/mcp-server-auth-cookbook.md) | Setup recipes for Bearer auth, custom headers, Vault, and BYOK. |
| [**Canonical Data Model & Database ERD**](docs/data-model.md) | Complete 12-table entity-relationship diagram and schema details. |
| [**Database Providers Guide**](docs/database-providers.md) | SQLite, Microsoft SQL Server, and MySQL database setup. |
| [**AppKey Scopes & Authorization Guide**](docs/appkey-scopes.md) | Scope rules (`*`, `category:*`, `server:*`, `tool:*`) and role checks. |
| [**Secret Providers Guide**](docs/secret-providers.md) | HashiCorp Vault, Windows DPAPI, and AES master key lifecycle. |
| [**Downstream Transports Guide**](docs/transports.md) | SSE, HTTP, and STDIO local subprocess security and isolation. |
| [**Product Evaluation Guide**](docs/evaluation-guide.md) | Context window reduction, token cost savings, and comparisons. |
| [**Troubleshooting & RCA Guide**](docs/mcp-routing-and-admin-issues.md) | Solutions for session timeouts, cache sync, and backend errors. |
| [**Enterprise AD, Vault & Auth Architecture**](docs/enterprise-ad-vault-scenarios-and-gap-analysis.md) | Enterprise AD, Vault topology, and downstream auth matrix architecture. |
| [**Active Directory & RBAC Guide**](docs/active-directory-and-rbac-guide.md) | Inbound AD Kerberos/LDAPS, tokenGroups recursive resolution, and multi-level RBAC. |
| [**OIDC & SSO Reverse Proxy Guide**](docs/oidc-and-sso-guide.md) | External JWT validation, header SSO, and downstream token exchange. |
| [**Downstream Auth & Delegation Guide**](docs/downstream-auth-and-delegation-guide.md) | Six credential delegation patterns, RLS identity forwarding, and mixing guardrails. |

---

## Release Changelog

For complete release history and version logs, see [**CHANGELOG.md**](CHANGELOG.md).

| Version | Release Date | Summary of Key Changes |
| :--- | :--- | :--- |
| **`v5.20.0`** | 2026-09-30 | feat(security,keys,ui): AppKey and OAuthClient LastUsedAt Tracking, Safe Key Rotation Workflow, and Multi-Client Config Generator. Implemented asynchronous non-blocking activity tracking for AppKey authentications and OAuth client token requests with 60-second debounce caching (`AUTH-120`, `AUTH-121`); introduced safe key rotation API endpoints (`POST /api/appkeys/{id}/rotate`, `POST /api/clients/{id}/rotate-secret`) supporting a 24-hour transition grace period where both previous and new secrets remain valid simultaneously (`AUTH-122`, `AUTH-123`); built interactive `AppKeyRotateModal` featuring confirmation safeguards, countdown warnings, and immediate secret copy protections (`UI-135`, `UI-136`); implemented `AppKeyConfigGenerator` generating ready-to-use client connection snippets for Claude Desktop, Cursor, Cline, Windsurf, and Antigravity (`UI-137`); updated AppKeys and Registered Clients tables with human-readable relative Last Used timestamps and direct Rotate Key actions; and achieved 100% test coverage with zero requirements catalog drift. |
| **`v5.19.1`** | 2026-09-29 | docs(slack): Purge Deprecated Wrapper References, Renumber Documentation Sections, and Refresh Wiki. Streamlined documentation for Slack Native Direct MCP (`https://mcp.slack.com/mcp`) and the Hybrid Egress Delegation Model; eliminated legacy self-hosted Node.js wrapper references across repository and wiki; updated release assets; and pinned container image tags. |
| **`v5.19.0`** | 2026-09-29 | feat(slack,oauth,ui): Slack Direct Native MCP Server Integration, 3LO OAuth Egress UI, and Hybrid Token Delegation Model. Added native integration with Slack's cloud MCP server (`https://mcp.slack.com/mcp`) over streamable HTTP; implemented Hybrid Egress fallback enabling autonomous background daemons (e.g. OpenClaw) to authenticate via static User OAuth tokens (`xoxp-...`) while allowing interactive users to authorize individually via 3-legged OAuth (3LO); relocated injected trace telemetry into `params._meta` to satisfy TypeScript SDK Zod `strict()` schema validation; added 3LO user delegation toggle and configuration fields to `ServerModal` UI; exposed OAuth properties in Admin MCP `manage_servers` tooling; added Slack OAuth callback handling for `user_scope` and non-expiring credentials; and resolved ESLint `prefer-const` rules. |
| **`v5.18.0`** | 2026-09-27 | feat(core,ui,e2e): Comprehensive E2E Real Wiring Overhaul, Cold-Start Dynamic Delimiter Routing, Full data-testid UI Instrumentation, and Deterministic E2E Test Suite. Added on-demand delimiter-tolerant (`/`, `:`, `__`) routing across `PromptRoutingManager` and `ResourceRoutingManager` allowing connected agents to retrieve prompts and read un-virtualized `mcp://` resources without prior list handshakes; safely returned 404 for missing/disabled servers in `CapabilityEndpoints` testbench APIs; resolved runtime crashes in `AccessControlTab` with defensive array handling; instrumented all frontend components with standard `data-testid` attributes; created in-memory stateful `mockApi.ts` fixture eliminating 502 proxy errors during isolated E2E testing; eliminated all conditional skips and brittle locators in Playwright test suites (64/64 tests passing 100% across all 20 spec files); upgraded `mock-mcp-server` in `docker-compose.test.yml` to full Model Context Protocol parity; and achieved 1,209 total passing automated tests with zero requirements catalog drift (`MCP-36`, `MCP-37`, `MCP-38`, `MCP-39`, `UI-133`, `UI-134`, `TRANS-01`, `TRANS-02`, `SEC-01`, `AUTH-05`). |
| **`v5.17.6`** | 2026-09-27 | test(containers): High-Fidelity Test Container Mock Server Expansion for Prompts, Resources, and Tools. Upgraded `mock-mcp-server` in `docker-compose.test.yml` to full Model Context Protocol parity, advertising complete prompt capabilities (`prompts/list`, `prompts/get`), resource reading (`resources/list`, `resources/read`), and deterministic tool execution payloads (`tools/call`) alongside existing tools (`health`, `echo`, `semantic_search`) for end-to-end integration and container testing. |

---

## Code Coverage & Quality Gates

Our core modules maintain high code coverage and automated CI quality gates on pull requests and pushes to `main`. For the complete breakdown and documentation, see:
- [Software Requirements Specification & Test Verification Catalog](docs/software-requirements-and-test-catalog.md)
- [Test Catalog Developer & Annotation Guide](docs/test-catalog-guide.md)
- [CI Quality Gates & Security Scanning Guide](docs/ci-quality-gates.md)
- [Detailed Code Coverage Report](docs/coverage-report.md)

| Module | Line Coverage | Branch Coverage | Status |
| :--- | :--- | :--- | :--- |
| **Core Session** | 92.4% | 88.1% | Passing |
| **Routing Engine** | 89.7% | 85.3% | Passing |
| **Controllers** | 94.2% | 91.0% | Passing |
| **Security & Providers** | 98.5% | 95.8% | Passing |
| **CI Quality Gates** | 100% | 100% | Passing |

---

## Contributor & Developer Guide

For complete developer onboarding, environment setup, testing protocols, and release verification, see [**Developer Guide**](docs/developer-guide.md).

### Quick Quality & Release Verification
Run the unified verification engine locally before creating pull requests:
```bash
./scripts/verify-release.sh
```

### C# Backend (Roslyn & .NET Analyzers)
- **EditorConfig**: Supported globally across C#, TSX, JSON, and YAML. Indentation is 4 spaces for C# and 2 spaces for web files.
- **Analysis Policy**: Rules are configured via `Directory.Build.props` at the workspace root, applying implicit usings, nullable context, deterministic builds, and latest-recommended Roslyn analyzers.
- **Verification Command**:
  ```bash
  dotnet format ModelContextGateway.slnx --verify-no-changes
  ```

### TypeScript / React Frontend (ESLint Flat Config)
- **ESLint v10**: Managed via flat configuration (`frontend/eslint.config.js`) supporting React 19, TypeScript-ESLint, and React Hooks/Refresh checks.
- **Verification Command**:
  ```bash
  cd frontend
  npm run lint
  ```
