# Model Context Gateway Features & Documentation Index

[Home](index.md) > Features Overview

Welcome to the **Model Context Gateway (MCG)** feature directory and documentation hub. Model Context Gateway routes, transforms, and secures communication for the Model Context Protocol (MCP).

This overview organizes MCG's capabilities and technical documentation following the **Diátaxis framework** into four distinct quadrants: **Tutorials**, **How-To Guides**, **Reference**, and **Explanation**.

---

```
                       LEARNING-ORIENTED
                             │
            Tutorials        │       Explanation
      (Getting Started)      │      (Architecture)
                             │
  PROBLEM- ──────────────────┼────────────────── INFORMATION-
  ORIENTED                   │                     ORIENTED
           How-To Guides     │        Reference
            (Task-based)     │      (Specifications)
                             │
                       ACTION-ORIENTED
```

---

## 1. Tutorials (Learning-Oriented)

Step-by-step guides designed to get you up and running with Model Context Gateway quickly.

* [**Single-User & Home-Lab Quickstart**](deployment/homelab.md)
  Deploy MCG for personal automation or homelab environments with zero external dependencies.
* [**Universal Setup Skill (`mcg-setup`)**](admin-guide.md#7-automated-administration)
  Use AI coding assistants (Claude Code, Antigravity, Cursor) to guide you through installing and configuring MCG in any workspace.
* [**Evaluation Guide**](evaluation-guide.md)
  Evaluate MCG features, performance benchmarks, and security boundaries.

---

## 2. How-To Guides (Task & Goal-Oriented)

Practical recipes to help you accomplish specific goals and operational tasks.

### Server & Connection Management
* [**Dynamic Server Management**](user-guide/servers.md)
  Add and configure backend MCP servers via Web UI, JSON seeding (`custom_servers.json`), environment variables, or Docker container labels (`mcp.*`).
* [**Client Setup & IDE Integration**](user-guide/clients/index.md)
  Connect AI clients and IDEs to MCG:
  * [Cursor IDE Integration](user-guide/clients/cursor.md)
  * [Claude Desktop Integration](user-guide/clients/claude-desktop.md)
  * [Cline & VS Code Integration](user-guide/clients/cline-and-vscode.md)
  * [Antigravity CLI Integration](user-guide/clients/antigravity.md)

### Administration & Automation
* [**Admin MCP Server & Agent Automation**](admin-mcp-automation-guide.md)
  Automate gateway configuration using AI agents via the built-in Admin MCP Server (`/admin`, `/mcg-admin`).
* [**Universal Admin MCP Automation Skill (`mcg-admin`)**](admin-mcp-automation-guide.md)
  7-phase automation skill for zero-dashboard programmatic provisioning.
* [**Interactive Developer Test Bench**](user-guide/test-bench/index.md)
  Inspect JSON-RPC traffic, test tools with dynamic form builders, simulate semantic search, and view live logs:
  * [Tool Execution Tester](user-guide/test-bench/tool-tester.md)
  * [Virtual Resources & Prompts](user-guide/test-bench/resources-and-prompts.md)
  * [Semantic Search Simulator](user-guide/test-bench/semantic-search.md)
  * [Raw Console & Live Logs](user-guide/test-bench/console-and-logs.md)

### Deployment & Hosting
* [**Docker & Container Deployment**](deployment/docker.md)
* [**Windows Server IIS In-Process Hosting**](deployment/windows-iis.md)
* [**Windows Service (SCM) & DPAPI**](deployment/windows-service.md)
* [**Multi-Provider Database Setup**](deployment/database-setup.md)

---

## 3. Reference (Information & Specification-Oriented)

Detailed technical specifications, parameter matrices, and API references.

### Gateway Tools & Scopes
* [**Admin MCP Tools Reference**](admin-mcp-features.md)
  Complete reference for the 10 consolidated administrative tools (`manage_servers`, `manage_appkeys`, `manage_policies`, `manage_group_mappings`, `manage_providers`, `manage_settings`, `manage_custom_files`, `manage_system`, `test_tool_call`, `manage_clients`).
* [**AppKey Taxonomy & Scopes**](appkey-scopes.md)
  Base62 AppKey character formats, entropy, fast lookup design, and scope granularity (`all`, `server:<id>`, `category:<name>`, `tool:<name>`).

### MCP Code Mode (JavaScript Sandbox Engine)

Model Context Gateway provides **MCP Code Mode**, an in-gateway orchestration engine powered by an isolated [Jint](https://github.com/sebastienros/jint) JavaScript runtime. Rather than requiring connected AI models to perform multiple serial tool-call round-trips over the network with large JSON-RPC overhead, agents can write and execute JavaScript workflows directly on the gateway via the `execute_script` meta-tool.

#### 1. Invoking `execute_script` in Meta-Mode
When Code Mode is enabled in settings (`CodeMode:Enabled = true`), the gateway exposes `execute_script` alongside `search_tools` and `execute_tool` on Meta-Mode endpoints (`/sse` and `/mcp`).

**Tool Definition:**
- **Name**: `execute_script`
- **Description**: Executes JavaScript within an isolated sandbox with direct access to backend MCP tools.
- **Parameters**:
  - `code` (string, required): JavaScript snippet to execute. Top-level statements and return values are captured as tool output.

#### 2. Dual Dispatcher Syntax
Scripts running inside the sandbox have two ergonomic ways to invoke downstream backend MCP tools:

1. **Direct Gateway Dispatcher (`mcg.callTool`)**:
   ```javascript
   const status = await mcg.callTool("docker/inspect_container", { id: "web" });
   return status;
   ```
   Supports modern slash (`server/tool`) and canonical underscore (`server__tool`) namespacing.

2. **Hierarchical Proxy Object (`tools.<server>.<tool>`)**:
   ```javascript
   const containers = await tools.docker.list_containers({ all: true });
   const running = containers.filter(c => c.state === 'running');
   return { count: running.length, names: running.map(c => c.name) };
   ```
   Both dot-notation (`tools.docker.list_containers(...)`) and bracket-notation (`tools["docker"]["list_containers"](...)`) are dynamically resolved through hierarchical JavaScript proxies.

#### 3. Auto-Unboxing & JSON Parsing
Tools executed within the sandbox automatically unbox MCP JSON-RPC content envelopes:
- If a downstream tool returns a single JSON text object in its content array (`[{ type: "text", text: "{\"key\": \"val\"}" }]`), MCG transparently parses the payload into a native JavaScript object or array.
- This allows immediate property access, array manipulation, and filtering without manual `JSON.parse()` boilerplate. Plain text or non-JSON payloads remain strings.

#### 4. Sandbox Security & Resource Guardrails
The Code Mode engine executes scripts in a fully isolated Jint engine with strict fail-closed resource limits:
- **Strict Isolation**: No access to CLR types, file system, network sockets, reflection, threading, or Node.js/browser globals (`process`, `require`, `window`, `document`).
- **Execution Timeout**: Configurable wall-clock cancellation timeout (default: `5,000 ms`, range: 1,000–60,000 ms).
- **Statement Limit**: Maximum AST statement count to prevent infinite loops (default: `100,000` statements).
- **Memory Ceiling**: Strict heap memory limit allocated to the engine (default: `32 MB`, range: 4–256 MB).
- **Max Tool Calls Quota**: Limits the number of downstream tool invocations allowed per script execution to prevent amplification attacks (default: `25` calls, range: 1–500).

#### 5. Group-Based RBAC & Settings UI
- **Authorization**: Access to `execute_script` can be restricted to specific user groups via `CodeMode:AllowedGroups`. If configured, only users possessing at least one matching group claim (via Windows AD SIDs, OIDC tokens, or reverse proxy SSO headers) are permitted to discover and execute scripts. Unauthorized users receive access-denied errors and do not see `execute_script` in `tools/list`.
- **Settings Management**: Administrators can toggle Code Mode, adjust timeout, statement, memory, and tool-call limits, and select allowed groups via the **Code Mode** tab in the Settings dashboard or via Admin MCP (`manage_settings`).

### Protocols & Standards Alignment
* [**MCP 2026-07-28 Specification Alignment**](architecture/transports-and-subprocesses.md)
  Support for `resultType`, Multi Round-Trip Requests (MRTR), `ttlMs`/`cacheScope` cacheable metadata, and JSON-RPC error taxonomy.
* [**RFC 9728 Protected Resource Metadata & Discovery**](auth-flows/mcp-request-auth-flow.md)
  Zeroconf client discovery via `/.well-known/oauth-protected-resource` and standard 401 `WWW-Authenticate` handshakes.
* [**RFC 7591 Dynamic Client Registration**](auth-flows/dynamic-client-registration.md)
  OAuth 2.1 client registration with mandatory `application_type` validation.

### Platform Infrastructure
* [**Database Providers & ERD**](database-providers.md)
  12-table Entity-Relationship Diagram and multi-provider support matrix (SQLite, MSSQL, MySQL).
* [**Secret Retrievers & Key Management**](secret-providers.md)
  Vault KV v2, Windows DPAPI, Environment, User-Provided (BYOK), and RFC 8693 Token Exchange configuration reference.
* [**Data Model Specification**](data-model.md)
  Detailed C# entity schemas and relational definitions.
* [**Software Requirements & Test Catalog (SRS)**](software-requirements-and-test-catalog.md)
  Automated requirement traceability matrix and test proof mapping.

---

## 4. Explanation & Architecture (Understanding-Oriented)

Deep-dive explanations of core architectural concepts, security pipelines, and subsystem mechanics.

### Core Routing & Processing
* [**Architecture Overview**](architecture/index.md)
  High-level system topology, module boundaries, and request lifecycle.
* [**Routing & Meta-Mode Engine**](architecture/routing-and-meta-mode.md)
  Slash formatting (`{serverId}/{toolName}`), dual-key routing, context window token reduction in Meta-Mode, and Reciprocal Rank Fusion (RRF) hybrid search combining SIMD vector embeddings with lexical search.
* **MCP Code Mode Execution Engine**
  Client-side JavaScript multi-tool workflow orchestration directly within MCG, featuring an isolated Jint sandbox, dual dispatchers (`mcg.callTool`, `tools.<server>.<tool>`), automatic JSON unboxing, statement/memory ceilings, and group-based RBAC.
* [**Transports & Subprocess Execution**](architecture/transports-and-subprocesses.md)
  Stateful SSE, stateless HTTP, and STDIO subprocess execution models (including the batteries-included `latest-full` container image).

### Security & Authentication Pipelines
* [**Authentication Architecture**](authentication-architecture.md)
  Unified identity resolution across Windows Integrated Auth, JWT, and reverse proxies.
* [**Unified Authorization Pipeline**](architecture/authorization-pipeline.md)
  5-level security pipeline enforcing AppKey scopes, administrator SIDs, database access policies, and discovery filtering across tools, prompts, resources, and completions.
* [**Active Directory & LDAPS Domain Setup**](architecture/security/active-directory-ldap.md)
  Domain integration, LDAPS TLS port 636, service credentials, and `tokenGroups` transitive group resolution.
* [**Multi-Level RBAC & Access Control Policies**](architecture/security/rbac-and-policies.md)
  Group mappings, SID evaluation, role definitions, and server/tool level access rules.
* [**Windows Integrated Authentication & IIS**](architecture/security/windows-integrated-auth.md)
  Negotiate/Kerberos handshakes, IIS in-process hosting, and Windows SCM hosting.
* [**Downstream Auth & Identity Delegation**](downstream-auth-and-delegation-guide.md)
  Outbound identity delegation via `X-Forwarded-User`, Kerberos impersonation, OAuth2 OBO token exchange, and BYOK credentials.
* [**Per-User OAuth Delegation & Connected Accounts**](auth-flows/per-user-oauth-flow.md)
  3LO OAuth callback orchestrator, background token refresh, and encrypted user vault storage.
* [**Observability, PII Sanitization & Audit Logging**](runbook.md#observability-health-checks-and-logs)
  `PiiSanitizer` token masking and database audit log stored procedures.
