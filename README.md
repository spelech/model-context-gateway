# Model Context Gateway (MCG)

![Version](https://img.shields.io/badge/version-v5.12.0-orange?style=for-the-badge)
[![Documentation](https://img.shields.io/badge/docs-GitHub%20Pages-blue?style=for-the-badge&logo=githubpages&logoColor=white)](https://spelech.github.io/model-context-gateway/)
![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![MCP Spec](https://img.shields.io/badge/MCP%20Spec-2026--07--28-0052CC?style=for-the-badge)
![Tests](https://img.shields.io/badge/tests-1%2C074%20passing-2ea44f?style=for-the-badge)
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
* **Autonomous Setup & Administration**: Built-in agent skills (`mcg-setup` and `mcg-admin`) let AI agents configure servers, secret stores, and access policies automatically. See [User Guide](docs/user-guide.md).
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

For homelab instructions, see the [Single-User & Home-Lab Setup Guide](docs/single-user-and-homelab-guide.md).

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
| [**Single-User & Home-Lab Setup Guide**](docs/single-user-and-homelab-guide.md) | Fast setup for personal use, home labs, and local AI clients. |
| [**Official User Guide**](docs/user-guide.md) | Web dashboard, server management, AppKeys, and test bench. |
| [**Administrator Guide**](docs/admin-guide.md) | Server management, 10 Admin MCP tools, RBAC policies, and providers. |
| [**Admin MCP Automation Guide**](docs/admin-mcp-automation-guide.md) | AI agent automation with `mcg-admin` and configuration playbooks. |
| [**Architecture Specification**](docs/architecture.md) | System components, request flow diagrams, and encryption pipelines. |
| [**Container Deployment Guide**](docs/deployment-guide.md) | Production Docker, Docker Compose, and environment settings. |
| [**Operations Runbook**](docs/runbook.md) | Health checks, database backups, key rotation, and disaster recovery. |
| [**Windows & IIS Deployment Guide**](docs/windows-deployment-and-validation-guide.md) | Windows Server IIS hosting, Windows services, and DPAPI keys. |
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

---

## Release Changelog

For complete release history and version logs, see [**CHANGELOG.md**](CHANGELOG.md).

| Version | Release Date | Summary of Key Changes |
| :--- | :--- | :--- |
| **`v5.12.0`** | 2026-09-09 | feat(enterprise): HashiCorp Vault User Secret Store CRUD, Path Templating, DI Provider Selection, and In-House IdP External JWT Validation. Enhanced `VaultUserSecretStore` with full CRUD support (`SaveSecretAsync`, `DeleteSecretAsync`, `GetServerIdsAsync`) and configurable path templates (`{company}/mcgateway/{user}/{app}`); decoupled `IUserSecretStore` DI registration allowing seamless configuration switching between Database and HashiCorp Vault (`Secrets:UserStore:Provider`); implemented `ExternalJwtAuthenticationHandler` for validating external In-House IdP JWT Bearer tokens against `.well-known/openid-configuration` JWKS endpoints; added containerized enterprise verification test harness with Active Directory OpenLDAP, HashiCorp Vault, Mock IdP, and multi-auth downstream servers; and expanded test proofs with zero catalog drift (`AUTH-130` through `AUTH-135`, `SEC-30`, `SEC-31`). |
| **`v5.11.1`** | 2026-09-08 | chore(deps): Upgrade TypeScript and ESLint Tooling across Frontend Suite. Upgraded TypeScript to `~6.0.3`, ESLint to `^10.10.0`, `typescript-eslint` to `^8.70.0`, and `eslint-plugin-react-refresh` to `^0.5.6`; added `@types/node` and `vite-env.d.ts` client types; and verified clean linting, builds, 253 Vitest unit tests, Playwright layout tests, and 810 backend tests. |
| **`v5.11.0`** | 2026-09-06 | docs(release): Comprehensive Documentation Sweep, GitHub Pages Admonitions, and Direct Architecture Overview. Integrated `mkdocs-callouts` natively translating GitHub alert callouts (`> [!NOTE]`, `> [!TIP]`, etc.) into MkDocs Material admonitions; overhauled `CatalogGenerator` to normalize paths and emit canonical repository blob links, scrubbing 1,333 brittle local file paths; purged dead worktree links across architecture and transport guides; streamlined `README.md`, `ARCHITECTURE.md`, and `docs/index.md` with plain-English system overviews; de-cluttered excessive header emojis across all core guides; updated test metrics to 1,063 automated tests; standardized public client snippets to port 8080; and verified all 358 relative links and anchors with zero errors. |
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
