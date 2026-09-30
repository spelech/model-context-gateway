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
* [**Universal Setup Skill (`mcg-setup`)**](admin-guide.md#universal-setup-skill-mcg-setup)
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
* [**Universal Admin MCP Automation Skill (`mcg-admin`)**](admin-mcp-automation-guide.md#universal-admin-mcp-automation-skill)
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
* [**Observability, PII Sanitization & Audit Logging**](runbook.md#audit-logging-and-observability)
  `PiiSanitizer` token masking and database audit log stored procedures.
