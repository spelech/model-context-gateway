# Model Context Gateway (MCG)

![Version](https://img.shields.io/badge/version-v5.10.0-orange?style=for-the-badge)
[![Documentation](https://img.shields.io/badge/docs-GitHub%20Pages-blue?style=for-the-badge&logo=githubpages&logoColor=white)](https://spelech.github.io/model-context-gateway/)
![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![MCP Spec](https://img.shields.io/badge/MCP%20Spec-2026--07--28-0052CC?style=for-the-badge)
![Tests](https://img.shields.io/badge/tests-1%2C063%20passing-2ea44f?style=for-the-badge)
![Docker Ready](https://img.shields.io/badge/docker-ready-2496ED?style=for-the-badge&logo=docker&logoColor=white)
![React 19](https://img.shields.io/badge/frontend-Vite%20React%2019-61DAFB?style=for-the-badge&logo=react&logoColor=black)
![License](https://img.shields.io/badge/license-Apache--2.0-blue?style=for-the-badge)

An enterprise C# ASP.NET Core gateway, OAuth 2.0 provider, and semantic proxy for the **Model Context Protocol (MCP)**. 

**Model Context Gateway (MCG)** consolidates downstream MCP servers (Docker, Home Assistant, SQL databases, Plex, Actual Budget, Excel, and custom APIs) and proxies them to AI clients (Claude Desktop, Cursor, Cline, Windsurf, Antigravity) through a single unified connection.

📖 **Documentation Portal:** [https://spelech.github.io/model-context-gateway/](https://spelech.github.io/model-context-gateway/)

![Model Context Gateway Dashboard](docs/assets/dashboard.jpg)

---

## Why Model Context Gateway? (The Airport Hub Metaphor)

Connecting AI clients directly to dozens of isolated microservices creates severe operational friction: credentials leak across workstations, connection shapes diverge, and dumping hundreds of tool schemas saturates the LLM's context window.

**Model Context Gateway functions as an international airport hub for your AI tools:**

* **Single Terminal Gate (`/sse`)**: Clients connect once to a single, governed endpoint.
* **Security Checkpoint**: The gateway authenticates users (Active Directory Windows SIDs, OIDC reverse proxy headers, or scoped AppKeys), enforces least-privilege RBAC, and automatically scrubs sensitive PII before requests touch backend infrastructure.
* **The Concierge (Meta-Mode)**: Instead of handing an LLM an overwhelming 500-page directory of every tool on every server, Meta-Mode provides an on-demand concierge: `search_tools` and `execute_tool`. The model asks in plain English (*"Restart the Plex service"*), and the gateway ranks, exposes, and executes the exact tool dynamically.
* **Flight Dispatch**: The gateway seamlessly routes traffic across Docker containers, remote HTTP/SSE endpoints, and local STDIO subprocesses without client-side reconfiguration.

---

## Key Features

* **Admin MCP Control Plane (`/admin`, `/mcg-admin`)**: In-process virtual MCP server providing 10 consolidated entity management tools (`manage_servers`, `manage_appkeys`, `manage_clients`, `manage_policies`, `manage_group_mappings`, `manage_providers`, `manage_settings`, `manage_custom_files`, `manage_system`, `test_tool_call`), allowing autonomous AI agents to administer the gateway programmatically. See [Admin MCP Guide](docs/admin-mcp-automation-guide.md).
* **Zero-Code Agent Automation**: Autonomous setup and admin skills (`mcg-setup` and `mcg-admin`) enable AI agents to provision identity providers, secret stores, RBAC policies, and backend servers with zero manual UI clicking. See [User Guide](docs/user-guide/README.md).
* **Meta-Mode Context Optimization**: Hides hundreds of backend tools during bootstrap; exposes only `search_tools` and `execute_tool` by default to prevent LLM context exhaustion and hallucinations.
* **Dual-Key Routing & Namespace Aliasing**: Exposes tools with clean modern slash formatting (`{namespace}/{tool_name}`) with backwards-compatible normalization (`{serverId}__{toolName}`) and collision guards.
* **Dynamic Docker Discovery**: Automatically registers containers labeled with `mcp.enabled=true`, `mcp.id`, `mcp.port`, and `mcp.alias` directly via `/var/run/docker.sock`. See [Features Guide](docs/features-guide.md).
* **Pluggable Identity & Single Sign-On**: Native support for **Active Directory** (Kerberos / NTLM Windows SIDs) and **OIDC / Reverse Proxy Headers** (`Remote-User`, `Remote-Groups` from Authentik, Authelia, Keycloak, etc.). See [Authentication Architecture](docs/authentication-architecture.md).
* **Enterprise Secret Providers**: Fetches downstream credentials dynamically from **HashiCorp Vault (KV v2)**, **Windows Registry (DPAPI)**, or **Environment Variables**, with AES-256-GCM envelope encryption at rest. See [Secret Providers Guide](docs/secret-providers.md).
* **Multi-Database Persistence**: Complete stored procedure and Dapper suites across **SQLite (WAL)**, **Microsoft SQL Server**, and **MySQL**. See [Database Providers Guide](docs/database-providers.md) and [Data Model & ERD](docs/data-model.md).
* **PII Sanitization & Audit Trails**: Real-time redaction of Bearer tokens, API keys, and passwords (`PiiSanitizer`) paired with stored procedure audit logging (`sp_InsertAuditLog`).
* **Batteries-Included Docker Tag**: `ghcr.io/spelech/model-context-gateway:latest-full` includes Node.js, Python 3, `uv`, and `bun` pre-installed for executing `stdio` sub-process tools without sidecar networking complexity. See [Transports Guide](docs/transports.md).
* **Built-in Web Dashboard**: Responsive, dark-mode, glassmorphic UI with real-time stats, server health cards, logs console, interactive test bench, and settings management.

---

## Quickstart: Zero-Config Deployment

Spin up **Model Context Gateway** with **zero required environment variables**. On first launch, the gateway automatically generates a 256-bit AES master key saved to `./data/.master.key` and initializes safe defaults:

```bash
docker run -d \
  --name mcg \
  -p 8080:8080 \
  -v $(pwd)/data:/app/data \
  -v /var/run/docker.sock:/var/run/docker.sock \
  ghcr.io/spelech/model-context-gateway:latest
```

### Safe Out-of-the-Box Defaults
* **Auto-Generated Master Key**: Created in `./data/.master.key` (with `chmod 0600`) so credentials remain encrypted at rest with zero plaintext environment variables.
* **Compact Base62 AppKeys**: Semantic, high-entropy tokens (`mcp-adm-`, `mcp-glb-`, `mcp-usr-`).
* **SQLite Database**: Automatically created and migrated at `./data/mcg.db`.
* **Standalone Security**: Local loopback (`127.0.0.1`, `::1`) is trusted as `Administrator` for the Web Dashboard (`http://localhost:8080`).
* **Declarative Admin Key**: Seed custom keys via `MCG_ADMIN_AUTH_KEY` or connect with the auto-generated admin key for remote AI agents and DevOps automation. See [Deployment Guide](docs/deployment-guide.md).
* **Single-User & Home-Lab Walkthrough**: For dedicated homelab instructions, see [Single-User & Home-Lab Setup Guide](docs/single-user-and-homelab-guide.md).

---

## Client Agent Integration

### 1. General Tool Access (Meta-Mode Gateway)
When using agentic coding assistants connected to `/sse`:
1. **Search (The Concierge)**: The agent calls `search_tools` with a natural language query describing the desired action (e.g. `"restart actual budget container"`).
2. **Execute**: The agent invokes the returned namespaced tool (e.g. `docker/restart_container` or `docker__restart_container`) via `execute_tool(name, arguments)`.

### 2. Autonomous Gateway Administration (Admin MCP Server)
Autonomous agents (Claude Desktop, Cursor, Cline, Windsurf, Antigravity) can directly manage gateway configuration by connecting to `/admin` or `/mcg-admin`:

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

### 3. Universal Agent Setup Skill (Zero-Clone Bootstrapping)
Equip any AI assistant (Antigravity, Claude Code, Cursor, Cline, Windsurf, Copilot CLI) to install, configure, and bootstrap the gateway without cloning or compiling source code:

```bash
mkdir -p .agents/skills/mcg-setup && curl -fsSL https://raw.githubusercontent.com/spelech/model-context-gateway/main/skills/mcg-setup/SKILL.md -o .agents/skills/mcg-setup/SKILL.md
```

Once installed, prompt your agent: *"Set up Model Context Gateway for my environment"*.

---

## Authentication Modes & Standalone Access

For a complete breakdown of end-to-end credential passing and token flows, see the [Authentication Support Matrix](docs/auth-flows/auth-support-matrix.md).

### 1. Standalone Mode (Zero-Config / Personal Network)
* **When Active**: Whenever no external identity provider (Active Directory LDAP or OIDC Reverse Proxy) is configured.
* **Local Loopback (`127.0.0.1`, `::1`)**: Connections originating from localhost are granted administrative privileges automatically.
* **Private LAN / Docker Subnets**: Configure `Admin:StandaloneAllowedNetworks` in `appsettings.json` or environment variables (e.g. `ADMIN__STANDALONE_ALLOWED_NETWORKS__0="10.0.0.0/8"`) to grant admin access across your local network.
* **External Clients**: Requests originating from outside allowed subnets require an Admin AppKey (`mcp-adm-...`).

### 2. Enterprise IDP Mode (Active Directory & OIDC Reverse Proxy)
* **Active Directory (Windows Authentication / LDAP)**: Users whose SID matches `Admin:GroupSid` (default: `S-1-5-32-544` / Local Administrators) or domain admin groups receive administrative privileges.
* **OIDC & Reverse Proxy SSO**: Reverse proxies transmitting `Remote-User` and `Remote-Groups` matching `Admin:GroupName` or `Admin:Groups` (e.g. `full_admin`, `Administrator`) are authorized.
* **Dynamic Group Mappings**: Map external IdP group names to internal roles via the `GroupMappings` database table or Web Dashboard.
* **Admin AppKeys**: Autonomous AI agents presenting an AppKey with `admin`, `all`, or `*` scope are granted the `Administrator` role across all endpoints.

---

## Subsystem Documentation Library

| Guide | Focus Area |
| :--- | :--- |
| [**Architecture & System Specification**](docs/architecture.md) | Architectural Tenets, Sequence Flows, Component Models, and Encryption Pipelines |
| [**Single-User & Home-Lab Setup Guide**](docs/single-user-and-homelab-guide.md) | 60-Second Setup, AppKey Generation, Standalone Trust, and Local Agent Integration |
| [**Enterprise Deployment & Operations Runbook**](docs/runbook.md) | Production Topology, Backups, Health Monitoring, and Disaster Recovery |
| [**Windows & IIS Deployment Guide**](docs/windows-deployment-and-validation-guide.md) | Windows Server IIS In-Process Hosting, Windows Services, DPAPI, and PowerShell Automation |
| [**Official User Guide Suite**](docs/user-guide/README.md) | Interactive Dashboard, Server Registration, RBAC, Client Setup, and Test Bench |
| [**MCP Server Auth & Integration Cookbook**](docs/mcp-server-auth-cookbook.md) | Scenario-Driven Setup Recipes for Bearer, Custom Headers, Vault, BYOK, and Pass-Through |
| [**Canonical Data Model & Database ERD**](docs/data-model.md) | Complete 12-Table Entity-Relationship Diagram, Constraints, and Schema Specifications |
| [**Database Provider Support & Dialects**](docs/database-providers.md) | SQLite WAL, Microsoft SQL Server Stored Procedures, and MySQL Parameter Conventions |
| [**AppKey Scopes & Authorization Guide**](docs/appkey-scopes.md) | Scope Syntax Grammar, Multi-Stage Pipeline Evaluation, and Least-Privilege Personas |
| [**Enterprise Secret Providers & Key Management**](docs/secret-providers.md) | HashiCorp Vault KV v2 JIT Renewal, Windows DPAPI, and Master Key Rotation |
| [**Downstream Transports & Subprocess STDIO**](docs/transports.md) | Transport Comparison, Process Security Policies, JSON-RPC Concurrency, and Isolation |
| [**Admin MCP Server & Automation Guide**](docs/admin-mcp-automation-guide.md) | Autonomous Agent Administration via `mcg-admin` Skill and Control Plane Tools |
| [**Product Evaluation & Gateway Comparison**](docs/evaluation-guide.md) | Context Window Reduction, Token Savings, Security Isolation, and Proxy Comparisons |
| [**Troubleshooting & RCA Postmortem**](docs/mcp-routing-and-admin-issues.md) | Root Cause Analysis and Remediation Guide for Session Lifecycles and Routing Caches |
| [**Executive Management Briefing**](docs/management-brief.md) | Leadership Summary, Enterprise Value Propositions, and NotebookLM Audio Overview Prompt |

---

## Release Changelog

For complete release history and version logs, see [**CHANGELOG.md**](CHANGELOG.md).

| Version | Release Date | Summary of Key Changes |
| :--- | :--- | :--- |
| **`v5.10.0`** | 2026-09-06 | feat(routing): Server Namespace Aliasing, Slash Tool Formatting, and Multi-Server Collision Guard. Added optional `Alias` namespace override for backend MCP servers with database migration (`EnsureAliasColumn`) and Docker container label discovery (`mcp.alias`, `mcp.namespace`); updated default tool exposure to modern slash format `{namespace}/{tool_name}` with dual-key routing (`{serverId}__{toolName}`); added multi-delimiter normalization (`/`, `:`, `__`) across tool execution and RBAC authorization; implemented collision guard rejecting ambiguous bare tool executions across distinct servers with descriptive candidate hints (`MCP-28`); added alias character validation and collision guard (`MCP-29`); enhanced `AdminMcpServer` `manage_servers` to support server aliases (`MCP-ADMIN-PARITY-SERVER-ALIAS`); and added alias configuration and badges in Web UI (`UI-SERVERS-ALIAS-MANAGEMENT`). |
| **`v5.9.1`** | 2026-09-06 | fix(meta): Resilient Meta-Mode Tool Execution, Request ID Injection, and Tool Identifier Normalization. Resolved fatal missing JSON-RPC request identifier in `execute_tool` which caused downstream Streamable HTTP and SSE backends to treat tool executions as notifications and return empty bodies; added automatic request ID injection safeguards in `HttpTransport` and `SseTransport`; implemented tool delimiter normalization converting `server/tool` and `server:tool` into canonical `server__tool` format across execution, authorization, and RBAC evaluation; added bare tool name auto-resolution for unambiguous tools; and added test coverage for Meta-Mode resilience (`MCP-26`). |
| **`v5.9.0`** | 2026-09-06 | feat(routing): Dynamic Downstream Protocol Version Negotiation and Stateless Handshake Support. Implemented dynamic protocol version negotiation in `ClientSession.BackendInitializer.cs` when downstream MCP servers reject newer spec versions (e.g. `2026-07-28`) with error `-32022`; automatically parses `error.data.supported`, sorts chronologically descending, and retries with the highest supported version (enabling seamless connection to `ModelContextProtocol.AspNetCore` v1.x / `2025-11-25` servers); added support for modern v2.0 stateless backends returning `-32601` on initialize; dynamically echoes client protocol versions in `AdminMcpServer` and proxy endpoints; and added full integration test coverage (`MCP-22`). |
| **`v5.8.0`** | 2026-09-05 | feat(routing): Downstream MCP Server Resilience, End-to-End Test Harness, and Admin Control Plane Hardening. Built high-fidelity `MockDownstreamMcpServer` test fixture and remediated all placeholder/weak tests (`AUTH-14`); decoupled `ClientSession` background backend connections and identity resolution from disposed `HttpContext` / `IFeatureCollection` lifecycles on stateless HTTP requests; synchronized `_toolRoutingTable` during cold-start cache fallbacks and added resilient prefix-based routing (`{serverId}__{toolName}`); enforced Zod `.strict()` wire compliance by omitting null properties on `JsonRpcResponse`; injected `CompositeSecretRetriever` into `AdminMcpServer.test_tool_call`; propagated `reconnect_all` to active sessions; and added `X-Forwarded-Host` reverse proxy support. |
| **`v5.7.1`** | 2026-09-02 | feat(audit): Granular Meta-Mode Target Tool Attribution in Invocations Audit Log. Enhanced `AuditInvocationAsync` to parse `execute_tool` payloads and log the underlying target tool name and server code name (e.g. `ha__ha_search` / `ha`), allowing administrators to view precise high-usage tools and user attribution in the audit logs. |
| **`v5.7.0`** | 2026-09-02 | feat(transports): FastMCP / Streamable HTTP SSE Streaming Resolution, Meta-Mode Cold-Start Cache Recovery, and Downstream Protocol Test Suite. Fixed SSE multi-line and notification truncation in `HttpTransport.SendRequestAsync` allowing full accumulation of multi-event streams from FastMCP backends (Home Assistant MCP) and skipping intermediate notifications before the JSON-RPC response; added global server cache fallback and on-demand cache population in `search_tools` resolving cold-start empty tool listings; relaxed semantic search keyword filtering to preserve 2-character domain tokens (`ha`, `on`, `tv`, `ac`); and added comprehensive test coverage for downstream streaming protocols and tool routing (`TRANS-04`, `TRANS-05`, `TRANS-06`, `MCP-25`). |
| **`v5.6.9`** | 2026-09-02 | fix(oauth): Enhanced OAuth 2.0 / OpenID Connect authorization code and token exchange scope, resource, and claim destination propagation for DCR clients (Google Gemini / Spark), and aligned reverse proxy routing rules for interactive consent screens. |
| **`v5.6.8`** | 2026-09-02 | Testing: Added error handling unit test coverage for `TokenExchangeSecretRetriever` verifying malformed and missing responses. |
| **`v5.6.5`** | 2026-09-02 | Testing: Implemented `ILdapConnection` and `ILdapConnectionFactory` abstractions in `LdapActiveDirectoryService` and added fail-closed negative unit tests for connection failures. |
| **`v5.5.4`** | 2026-09-01 | chore(refactor): Comprehensive Code Health, Performance Optimizations & Security Hardening across Core and Infrastructure. Refactors nested control flows with clean guard clauses and early returns across `SseTransport`, `StdioTransport`, `VaultSecretRetriever`, `DbKeyHelper`, `JsonRpcStateManager`, `AppKeyAuthenticationHandler`, `TrustedProxyHelper`, and `AuditLogger`; parameterizes server listing and admin SQL queries to ensure safe execution; replaces hardcoded `Console.WriteLine` outputs with dependency-injected/optional `ILogger` calls in `DbKeyHelper` and `SymmetricEncryptionHelper`; optimizes string interpolation in `PromptRoutingManager` via `StringBuilder`; replaces slow `Math.Pow` with direct multiplication in ONNX vector magnitude normalization (`OnnxEmbeddingService`); eliminates unnecessary `.ToList()` allocations during `JsonObject` iteration (`ProviderConfigSecurityHelper`); fixes N+1 queries in `DatabaseSeederService` via cross-compatible `IN` clauses; and caches `JsonSerializerOptions` in `McpIntegrationTests` for improved test suite throughput. |
| **`v5.5.3`** | 2026-08-31 | perf(docker): Fixed N+1 query issue in Docker Auto Discovery by batching container disabling updates in a single IN clause, improving scalability when many stopped containers are detected. |
| **`v5.5.0`** | 2026-08-31 | feat(mcp): MCP 2026-07-28 Spec Middleware Encapsulation & Transport Normalization. Replaces dual-spec naming with unified `McpSpecMiddleware`, centralizing MCP 2026-07-28 specification inspection (`Mcp-Method`, `Mcp-Name`, `Mcp-Session-Id`, `MCP-Protocol-Version`) and backwards-compatible JSON-RPC body parsing across all MCP endpoints (`/sse`, `/mcp`, `/admin`, `/admin/sse`, `/mcg-admin`, `/mcg-admin/sse`, `/{targetServerId}`, `/message`, `/admin/message`); enables direct Streamable HTTP POST handling and notification recognition (`202 Accepted`) on `/admin` and target proxy endpoints; restores seamless compatibility for both stateless HTTP direct clients (Google Antigravity) and stateful 2-way SSE clients (OpenCode); and updates SRS test catalog with annotated proofs (`MCP-21`). |

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
