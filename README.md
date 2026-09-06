# Model Context Gateway (MCG)

![Version](https://img.shields.io/badge/version-v5.9.1-orange?style=for-the-badge)
[![Documentation](https://img.shields.io/badge/docs-GitHub%20Pages-blue?style=for-the-badge&logo=githubpages&logoColor=white)](https://spelech.github.io/model-context-gateway/)
![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![MCP Spec](https://img.shields.io/badge/MCP%20Spec-2026--07--28-0052CC?style=for-the-badge)
![Tests](https://img.shields.io/badge/tests-959%20passing-2ea44f?style=for-the-badge)
![Docker Ready](https://img.shields.io/badge/docker-ready-2496ED?style=for-the-badge&logo=docker&logoColor=white)
![React 19](https://img.shields.io/badge/frontend-Vite%20React%2019-61DAFB?style=for-the-badge&logo=react&logoColor=black)
![License](https://img.shields.io/badge/license-Apache--2.0-blue?style=for-the-badge)

An enterprise C# ASP.NET Core gateway, OAuth 2.0 provider, and semantic proxy for the **Model Context Protocol (MCP)**. 

**Model Context Gateway (MCG)** aggregates backend MCP servers (Docker, Plex, Home Assistant, Actual Budget, Excel) and proxies them to clients via a single unified connection.

📖 **Documentation Portal:** [https://spelech.github.io/model-context-gateway/](https://spelech.github.io/model-context-gateway/)

![Model Context Gateway Dashboard](docs/assets/dashboard.jpg)

---

## 🌟 Key Features

* **Admin MCP Server & Control Plane (`/admin`, `/mcg-admin`)**: In-process virtual MCP server providing 10 consolidated entity management tools (`manage_servers`, `manage_appkeys`, `manage_clients`, `manage_policies`, `manage_group_mappings`, `manage_providers`, `manage_settings`, `manage_custom_files`, `manage_system`, `test_tool_call`) allowing autonomous AI agents (Claude Desktop, Cursor, Cline, Windsurf) to manage gateway configuration directly via MCP protocol with hybrid standalone network auth and audit logging.
* **Universal Admin Automation Skill (`mcg-admin`)**: Specialized [AgentSkills.io](https://agentskills.io) skill enabling AI agents to programmatically provision Auth providers (Authentik, Keycloak, Entra ID, Active Directory LDAPS), Secret stores (Vault KV v2, AES-256-GCM Master Key, DPAPI), RBAC policies, group mappings, embeddings, backend servers, and client keys from a blank slate with zero UI clicking (see [docs/admin-mcp-automation-guide.md](docs/admin-mcp-automation-guide.md)).
* **Universal Setup Skill (`mcg-setup`)**: Self-contained [AgentSkills.io](https://agentskills.io)-compliant skill enabling any AI assistant to bootstrap and configure the gateway across Docker Compose and Windows IIS with zero source code cloning.
* **MCP 2026-07-28 Spec Support**: Spec-compliant header annotation; routing is body/path based (`Mcp-Method` & `Mcp-Name`) via `McpSpecMiddleware` with backwards-compatible JSON body fallback.
* **Dynamic Docker Auto-Discovery**: Mounts `/var/run/docker.sock` to automatically discover and register backend MCP containers labeled with `mcp.enabled=true`, `mcp.id`, `mcp.port`, and `mcp.categories` (see [docs/features-guide.md](docs/features-guide.md#method-d-dynamic-docker-label-auto-discovery-mcp-labels)).
* **Pluggable Identity Providers**: Dual authentication support for **Active Directory** (Kerberos/NTLM Windows SIDs) and **OIDC / Reverse Proxy Headers** (`Remote-User`, `Remote-Groups` headers from Authentik, Authelia, PocketID, Keycloak, etc.).
* **Pluggable Secret Retrievers**: Fetch downstream server API keys and tokens dynamically from **HashiCorp Vault (KV v2)**, **Windows Registry (DPAPI)**, or **Environment Variables** per server (`SecretProvider` column).
* **Windows Enterprise Hosting & Automation**: First-class support for **IIS In-Process (`AspNetCoreModuleV2`)** with unbuffered SSE streaming (`responseBufferLimit="0"`), **Managed Windows Services** with SCM crash auto-recovery, Windows DPAPI registry secrets, and automated PowerShell deployment toolkits. See [docs/windows-deployment-and-validation-guide.md](docs/windows-deployment-and-validation-guide.md).
* **Multi-Database & Stored Procedure Engine**: Complete stored procedure suites for **MS SQL Server** (`Microsoft.Data.SqlClient`), **MySQL** (`MySqlConnector`), and **SQLite** (`Microsoft.Data.Sqlite`) using Dapper. See [docs/database-providers.md](docs/database-providers.md).
* **Observability & PII Audit Logging**: Automatic payload redaction of Bearer tokens, API keys, and passwords (`PiiSanitizer`) paired with stored procedure audit logging (`sp_InsertAuditLog`).
* **Consolidated Tools Gateway:** Merges 300+ tools from dozens of isolated backend servers into a single endpoint.
* **Meta-Mode Dynamic Tool Filtering:** 
  * Defaults to Meta-Mode on the main `/sse` connection path to prevent context window bloat and tool confusion.
  * Instantly returns only two bootstrap tools: `search_tools` and `execute_tool`.
  * Asynchronously warms backend caches in the background using a thread-safe, single-execution initialization lock.
  * Performs semantic scoring and ranking of backend tools on-demand when `search_tools` is called.
* **Dual-Provider Semantic Search**:
  * **Local ONNX (In-Process)**: CPU-friendly vector embeddings using a local `all-MiniLM-L6-v2` model and `Microsoft.ML.Tokenizers` (no external APIs). Automatically downloads model/vocab files into persistent volumes.
  * **API Provider**: OpenAI-compatible embedding calls (LiteLLM, Open WebUI, OpenAI, etc.).
  * **Secure DB Storage**: Embedding configurations and API keys are stored securely inside the SQLCipher-encrypted SQLite database.
* **Developer Test Bench & Dashboard**: 
  * **Interactive UI**: Form builder renders interactive input controls directly from tools' JSON schema specs.
  * **Logs Console**: Styled real-time terminal rendering thread-safe in-memory gateway logs.
  * **Search Simulator**: Real-time evaluation panel for intent ranking.
  * **Provider Management Controls**: Interactive UI cards in Settings to toggle and configure Auth and Secret providers.
* **Target-Specific Proxying:** Exposes separate endpoints (`/{targetServerId}`) to route directly to specific backends (e.g., `/plex`, `/docker`).
* **OAuth 2.0 Security & CORS Config:** Integrates a lightweight OAuth 2.0 authorization server for secure API access. Leverages strict, configurable CORS protection with `CORS_ALLOWED_ORIGINS` to prevent cross-origin request hijacking / forgery vulnerabilities.
* **Enterprise Identity Delegation**:
  * **X-Forwarded-User Propagation (Trusted Gateway Pattern)**: Automatically injects the inbound authenticated user's identity into downstream HTTP/SSE backend requests for seamless Row-Level Security (RLS) enforcement.
  * **Kerberos / NTLM Impersonation**: For native Windows IIS deployments, the gateway utilizes `S4U2Proxy` to assume the inbound caller's Active Directory identity when communicating with downstream enterprise endpoints.
  * **OAuth2 / OIDC On-Behalf-Of**: Acts as a Confidential Client to dynamically mint/exchange tokens with identity providers (Azure AD, Okta, Authentik) on behalf of the user.
  * **Dynamic Auth Pass-Through**: Issues `dynamic_auth` prompts directly to the client (IDE/LLM) when downstream services require interactive challenges.
* **Batteries-Included Docker**: `ghcr.io/spelech/model-context-gateway:latest-full` tag provides pre-installed Node.js, Python 3, `uv`, and `bun` environments for natively executing `stdio` sub-process servers without sidecar networking complexity.

* **Built-in Web Dashboard:** A responsive, dark-mode, glassmorphic UI to monitor connected clients, stats, and backend health status.

---

## ⚡ Quickstart: Zero-Config Blank-Slate Deployment

You can spin up **Model Context Gateway** with **zero required environment variables**. On first launch, the gateway automatically generates a 256-bit master key saved to `./data/.master.key` and initializes safe defaults:

```bash
docker run -d \
  --name mcg \
  -p 8080:8080 \
  -v $(pwd)/data:/app/data \
  -v /var/run/docker.sock:/var/run/docker.sock \
  ghcr.io/spelech/model-context-gateway:latest
```

*(Alternatively, mount a Docker/Kubernetes file secret with `-e MCG_MASTER_KEY_FILE=/run/secrets/my_key` or pass `-e MCG_MASTER_KEY="<key>"`).*

### Out-of-the-Box Safe Defaults
* **Auto-Generated Master Key**: Automatically created and stored in `./data/.master.key` (with `chmod 0600`) so credentials remain encrypted at rest with zero plaintext env vars.
* **Compact Base62 AppKeys**: Semantic, high-entropy ~32-character tokens (`mcp-adm-`, `mcp-glb-`, `mcp-{domain}-`, `mcp-usr-`, `mcp-srv-`).
* **SQLite Database**: Automatically created and migrated at `./data/mcg.db`.
* **Standalone Security**: Local loopback (`127.0.0.1`, `::1`) is trusted as `Administrator` for the Web Dashboard (`http://localhost:8080`).
* **Declarative Admin Key**: Seed custom keys via `MCG_ADMIN_AUTH_KEY` / `MCG_ADMIN_KEY` or connect with the auto-generated `mcp-adm-` admin key for remote AI agents and DevOps scripts to automate configuration via the Admin MCP Server (`/admin/sse` or `POST /admin`).
* **Instant Automation**: Use the **`mcg-admin`** skill (`.agents/skills/mcg-admin/SKILL.md`) to autonomously configure Authentik, Keycloak, Entra ID, Active Directory, Vault, embeddings, and backend servers. See [**docs/deployment-guide.md**](docs/deployment-guide.md#minimal-blank-slate-startup-zero-config-or-file-secrets).

---

## 🎯 Evaluation & Product Overview Guide

For details on context window management, STDIO secret security, authorization, and reverse proxy comparisons, see:
* [**Evaluation & Product Overview Guide**](docs/evaluation-guide.md)

---

## 🏛️ Comprehensive Architecture & Specification Guide

For architectural specifications, Mermaid sequence diagrams, component models, ERDs, authorization flows, transport lifecycles, and AES-256-GCM encryption pipelines, see:
* [**Comprehensive Enterprise Architecture Guide**](docs/architecture.md)
* [**Executive Architecture Overview**](ARCHITECTURE.md)

---

## 📖 Official User Guide & Manual

For UI guides, server registration, secret provider configuration, RBAC, client setup, and test bench operations, see:
* [**Official User Guide Suite**](docs/user-guide/README.md)
  * [**📖 MCP Server Auth & Integration Cookbook**](docs/mcp-server-auth-cookbook.md) (*"If your server requires X ➔ Setup is Y"*)
  * [01. Dashboard & Navigation Interface](docs/user-guide/01-dashboard-and-navigation.md)
  * [02. Server Management & Secret Providers](docs/user-guide/02-server-management-and-secrets.md)
  * [03. RBAC, Security & Approvals](docs/user-guide/03-rbac-and-security.md)
  * [04. Client Setup & App Key Management](docs/user-guide/04-client-setup-and-app-keys.md)
  * [05. Interactive Test Bench](docs/user-guide/05-interactive-test-bench.md)
  * [06. System Settings & Vector Embeddings](docs/user-guide/06-settings-and-embeddings.md)

---

## 💻 Developer & Operations Guides

For setup, testing, production deployment, database management, observability, and disaster recovery:
* [**Developer Guide & Local Setup**](docs/developer-guide.md)
* [**Windows Deployment & Validation Guide**](docs/windows-deployment-and-validation-guide.md)
* [**Software Requirements Specification (SRS) & Test Catalog**](docs/software-requirements-and-test-catalog.md)
* [**Test Catalog & Annotation Developer Guide**](docs/test-catalog-guide.md)
* [**Operations & Production Runbook**](docs/runbook.md)
* [**Contributing Guide**](CONTRIBUTING.md)

---

## 🚀 Transport Capability & Configuration Guide

For an in-depth breakdown of downstream transports (`sse`, `http`/`streamable`, `stdio`, target proxying `/{targetServerId}`), subprocess STDIO security policies, environment variable secret injection, process tree lifecycle management, SSE concurrency/ID isolation, configuration examples, and troubleshooting procedures, see [**docs/transports.md**](docs/transports.md).

---

## 🔑 AppKey Scopes & Authorization Guide

For complete scope syntax grammar (`*`, `server:*`, `category:*`, `tool:*`, `prompt:*`, `resource:*`), multi-stage pipeline evaluation rules, the capability authorization matrix, cryptographic token hashing, and least-privilege persona recipes, see the canonical [**AppKey Scopes & Authorization Guide**](docs/appkey-scopes.md).

---

## 🔐 Enterprise Secret Providers & Key Management Guide

For detailed documentation on supported secret providers (HashiCorp Vault KV v2 with JIT renewal, Windows Registry DPAPI, Environment Variables), AES-256-GCM encryption at rest, dynamic runtime reloading, audit safety, and Docker Compose setup snippets, see [**docs/secret-providers.md**](docs/secret-providers.md).

---

## 🗄️ Database Provider Support, Data Model & ERD

For complete dialect specifications across **SQLite**, **Microsoft SQL Server**, and **MySQL**, the complete 12-table [**Canonical Data Model & Database ERD**](docs/data-model.md), stored procedure suites (`sp_*`), AES-256-GCM envelope encryption, and Docker Compose deployment recipes, see:
* [**Canonical Data Model & Database ERD**](docs/data-model.md)
* [**Database Provider Support & Deployment Matrix**](docs/database-providers.md)

---

## 📡 Features & Usage Guide

For deep technical walkthroughs, setup configuration examples, connection guidelines, secret retrievers, and usage instructions for the Web UI/Test Bench, see [docs/features-guide.md](docs/features-guide.md).

---

## 🤖 Client Agent Integration Guidelines

### 1. General Tool Access (Meta-Mode Gateway)
When using agentic coding assistants connected to the main `/sse` gateway:
1. **Bootstrap Search (Meta-Mode)**: By default, the gateway hides all underlying tools to prevent context bloat. The agent must first query `search_tools` with a natural language query describing the desired action (e.g., `"restart actual budget container"`).
2. **Namespaced Execution**: After `search_tools` returns matching namespaced tools (e.g. `docker__restart_container`), the agent must invoke it via `execute_tool(name, arguments)`.
3. **Semantic Knowledge Retrieval (`notes-rag`)**: AI agents **MUST** query the `notes-rag` service first (using the `search_notes` tool) for system architecture or setup questions before attempting to grep the filesystem.

### 2. Autonomous Gateway Administration (Admin MCP Server)
Autonomous agents (Claude Desktop, Cursor, Cline, Windsurf, Antigravity) can directly manage gateway configuration by connecting to `/admin` or `/mcg-admin`:

#### Claude Desktop (`claude_desktop_config.json`)
```json
{
  "mcpServers": {
    "mcg-admin": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/client-sse", "http://localhost:8026/admin"]
    }
  }
}
```

#### Cursor (`~/.cursor/mcp.json`) / Windsurf / Cline (`cline_mcp_settings.json`)
```json
{
  "mcpServers": {
    "mcg-admin": {
      "url": "http://localhost:8026/admin",
      "headers": {
        "Authorization": "Bearer mcp-adm-Xk9L2mPq-7vN3wZ8aB1cE4fG9"
      }
    }
  }
}
```

### 3. Universal Agent Setup Skill (Zero-Clone Bootstrapping)
Equip any AI assistant (Antigravity, Claude Code, Cursor, Cline, Windsurf, Copilot CLI) to install, configure, and bootstrap the gateway for Docker Compose or Windows Server IIS without cloning or compiling source code:

```bash
mkdir -p .agents/skills/mcg-setup && curl -fsSL https://raw.githubusercontent.com/spelech/model-context-gateway/main/skills/mcg-setup/SKILL.md -o .agents/skills/mcg-setup/SKILL.md
```

Once installed, simply prompt your agent: *"Set up Model Context Gateway for my environment"* or *"Deploy MCG on Docker/IIS"*. The skill automatically:
- Probes host environment capabilities (OS, Docker daemon socket, HashiCorp Vault, Active Directory domain).
- Guides deployment target selection (**Docker Compose** or **Windows IIS**).
- Clarifies trade-offs between **Environment Variables** (`.env`) vs. **Web UI & Database** (dynamic hot-reloading).
- Configures network topology (**Standalone / Home-Lab** with SQLite vs. **Enterprise** with AD/OIDC + MSSQL/MySQL/Vault).
- Generates cryptographically secure 256-bit `MCG_MASTER_KEY` values and production configuration files (`docker-compose.yml`, `web.config`, `.env`, `appsettings.Production.json`).
- Verifies gateway health (`/health`, `/sse`) and outputs client configuration snippets.

---

## 🛡️ Authentication Modes & Zero-Configuration Standalone Access

> **Note:** For a detailed breakdown of end-to-end credential passing, Kerberos limitations, and Pass-Through routing constraints, see the [Authentication End-to-End Support Matrix](docs/auth-flows/auth-support-matrix.md).

The gateway features a hybrid administrative authorization engine supporting both isolated bare-metal developers and massive enterprise Active Directory forests.:

### 1. Standalone Mode (Zero-Config / Personal / Private Network)
* **When Active**: Whenever no external identity provider (Active Directory LDAP or OIDC Reverse Proxy) is configured.
* **Local Loopback (`127.0.0.1`, `::1`)**: By default, connections originating from localhost/loopback are granted local administrative privileges automatically without requiring an SSO provider or password.
* **Private LAN / Docker Subnets (Central Gateway)**: Configure `Admin:StandaloneAllowedNetworks` in `appsettings.json` or environment variables (e.g. `ADMIN__STANDALONE_ALLOWED_NETWORKS__0="10.0.0.0/8"` or `"0.0.0.0/0"` for open private LANs) to grant admin access to your local network.
* **External Clients**: Requests originating from outside the allowed subnets require an Admin AppKey (such as `MCG_ADMIN_KEY`, a compact `mcp-adm-` key, or custom generated keys).

### 2. Enterprise IDP Mode (Active Directory & OIDC Reverse Proxy)
* **Active Directory (Windows Authentication / LDAP)**: Users whose SID matches `Admin:GroupSid` (default: `S-1-5-32-544` / Local Administrators) or domain admin groups are granted full gateway administration.
* **OIDC & Reverse Proxy SSO**: Reverse proxies (Authentik, Authelia, PocketID, Keycloak, Traefik, Caddy, Nginx) transmitting `Remote-User` and `Remote-Groups` matching `Admin:GroupName` or `Admin:Groups` (e.g. `full_admin`, `Administrator`) are authorized.
* **Dynamic Group Mappings**: Map external IdP group names to internal roles via the `GroupMappings` database table or Web Dashboard.
* **Admin AppKeys**: Autonomous AI agents presenting an AppKey with `admin`, `all`, or `*` scope are granted the `Administrator` role across all endpoints.

---

## 📜 Release Changelog

For complete release history and version logs, see [**CHANGELOG.md**](CHANGELOG.md).

| Version | Release Date | Summary of Key Changes |
| :--- | :--- | :--- |
| **`v5.9.1`** | 2026-09-06 | fix(meta): Resilient Meta-Mode Tool Execution, Request ID Injection, and Tool Identifier Normalization. Resolved fatal missing JSON-RPC request identifier in `execute_tool` which caused downstream Streamable HTTP and SSE backends to treat tool executions as notifications and return empty bodies; added automatic request ID injection safeguards in `HttpTransport` and `SseTransport`; implemented tool delimiter normalization converting `server/tool` and `server:tool` into canonical `server__tool` format across execution, authorization, and RBAC evaluation; added bare tool name auto-resolution for unambiguous tools; and added test coverage for Meta-Mode resilience (`MCP-26`). |
| **`v5.9.0`** | 2026-09-06 | feat(routing): Dynamic Downstream Protocol Version Negotiation and Stateless Handshake Support. Implemented dynamic protocol version negotiation in `ClientSession.BackendInitializer.cs` when downstream MCP servers reject newer spec versions (e.g. `2026-07-28`) with error `-32022`; automatically parses `error.data.supported`, sorts chronologically descending, and retries with the highest supported version (enabling seamless connection to `ModelContextProtocol.AspNetCore` v1.x / `2025-11-25` servers); added support for modern v2.0 stateless backends returning `-32601` on initialize; dynamically echoes client protocol versions in `AdminMcpServer` and proxy endpoints; and added full integration test coverage (`MCP-22`). |
| **`v5.8.0`** | 2026-09-05 | feat(routing): Downstream MCP Server Resilience, End-to-End Test Harness, and Admin Control Plane Hardening. Built high-fidelity `MockDownstreamMcpServer` test fixture and remediated all placeholder/weak tests (`AUTH-14`); decoupled `ClientSession` background backend connections and identity resolution from disposed `HttpContext` / `IFeatureCollection` lifecycles on stateless HTTP requests; synchronized `_toolRoutingTable` during cold-start cache fallbacks and added resilient prefix-based routing (`{serverId}__{toolName}`); enforced Zod `.strict()` wire compliance by omitting null properties on `JsonRpcResponse`; injected `CompositeSecretRetriever` into `AdminMcpServer.test_tool_call`; propagated `reconnect_all` to active sessions; and added `X-Forwarded-Host` reverse proxy support. |
| **`v5.7.1`** | 2026-09-02 | feat(audit): Granular Meta-Mode Target Tool Attribution in Invocations Audit Log. Enhanced `AuditInvocationAsync` to parse `execute_tool` payloads and log the underlying target tool name and server code name (e.g. `ha__ha_search` / `ha`), allowing administrators to view precise high-usage tools and user attribution in the audit logs. |
| **`v5.7.0`** | 2026-09-02 | feat(transports): FastMCP / Streamable HTTP SSE Streaming Resolution, Meta-Mode Cold-Start Cache Recovery, and Downstream Protocol Test Suite. Fixed SSE multi-line and notification truncation in `HttpTransport.SendRequestAsync` allowing full accumulation of multi-event streams from FastMCP backends (Home Assistant MCP) and skipping intermediate notifications before the JSON-RPC response; added global server cache fallback and on-demand cache population in `search_tools` resolving cold-start empty tool listings; relaxed semantic search keyword filtering to preserve 2-character domain tokens (`ha`, `on`, `tv`, `ac`); and added comprehensive test coverage for downstream streaming protocols and tool routing (`TRANS-04`, `TRANS-05`, `TRANS-06`, `MCP-25`). |
| **`v5.6.9`** | 2026-09-02 | fix(oauth): Enhanced OAuth 2.0 / OpenID Connect authorization code and token exchange scope, resource, and claim destination propagation for DCR clients (Google Gemini / Spark), and aligned reverse proxy routing rules for interactive consent screens. |
| **`v5.6.8`** | 2026-09-02 | 🧪 Testing: Added error handling unit test coverage for `TokenExchangeSecretRetriever` verifying malformed and missing responses. |
| **`v5.6.5`** | 2026-09-02 | 🧪 Testing: Implemented `ILdapConnection` and `ILdapConnectionFactory` abstractions in `LdapActiveDirectoryService` and added fail-closed negative unit tests for connection failures. |
| **`v5.5.4`** | 2026-09-01 | chore(refactor): Comprehensive Code Health, Performance Optimizations & Security Hardening across Core and Infrastructure. Refactors nested control flows with clean guard clauses and early returns across `SseTransport`, `StdioTransport`, `VaultSecretRetriever`, `DbKeyHelper`, `JsonRpcStateManager`, `AppKeyAuthenticationHandler`, `TrustedProxyHelper`, and `AuditLogger`; parameterizes server listing and admin SQL queries to ensure safe execution; replaces hardcoded `Console.WriteLine` outputs with dependency-injected/optional `ILogger` calls in `DbKeyHelper` and `SymmetricEncryptionHelper`; optimizes string interpolation in `PromptRoutingManager` via `StringBuilder`; replaces slow `Math.Pow` with direct multiplication in ONNX vector magnitude normalization (`OnnxEmbeddingService`); eliminates unnecessary `.ToList()` allocations during `JsonObject` iteration (`ProviderConfigSecurityHelper`); fixes N+1 queries in `DatabaseSeederService` via cross-compatible `IN` clauses; and caches `JsonSerializerOptions` in `McpIntegrationTests` for improved test suite throughput. |
| **`v5.5.3`** | 2026-08-31 | perf(docker): Fixed N+1 query issue in Docker Auto Discovery by batching container disabling updates in a single IN clause, improving scalability when many stopped containers are detected. |
| **`v5.5.0`** | 2026-08-31 | feat(mcp): MCP 2026-07-28 Spec Middleware Encapsulation & Transport Normalization. Replaces dual-spec naming with unified `McpSpecMiddleware`, centralizing MCP 2026-07-28 specification inspection (`Mcp-Method`, `Mcp-Name`, `Mcp-Session-Id`, `MCP-Protocol-Version`) and backwards-compatible JSON-RPC body parsing across all MCP endpoints (`/sse`, `/mcp`, `/admin`, `/admin/sse`, `/mcg-admin`, `/mcg-admin/sse`, `/{targetServerId}`, `/message`, `/admin/message`); enables direct Streamable HTTP POST handling and notification recognition (`202 Accepted`) on `/admin` and target proxy endpoints; restores seamless compatibility for both stateless HTTP direct clients (Google Antigravity) and stateful 2-way SSE clients (OpenCode); and updates SRS test catalog with annotated proofs (`MCP-21`). |
---

## 🧪 Code Coverage & Quality Gates

Our core modules maintain high code coverage and automated CI quality gates on pull requests and pushes to `main`. For the complete breakdown and documentation, see:
- [Software Requirements Specification & Test Verification Catalog](docs/software-requirements-and-test-catalog.md)
- [Test Catalog Developer & Annotation Guide](docs/test-catalog-guide.md)
- [CI Quality Gates & Security Scanning Guide](docs/ci-quality-gates.md)
- [Detailed Code Coverage Report](docs/coverage-report.md)

| Module | Line Coverage | Branch Coverage | Status |
| :--- | :--- | :--- | :--- |
| **Core Session** | 92.4% | 88.1% | 🟢 Passing |
| **Routing Engine** | 89.7% | 85.3% | 🟢 Passing |
| **Controllers** | 94.2% | 91.0% | 🟢 Passing |
| **Security & Providers** | 98.5% | 95.8% | 🟢 Passing |
| **CI Quality Gates** | 100% | 100% | 🟢 Passing |

---

## 🛠️ Contributor & Developer Guide

For complete developer onboarding, environment setup, testing protocols, and release verification, see [**docs/developer-guide.md**](docs/developer-guide.md).

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
