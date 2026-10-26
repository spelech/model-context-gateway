# Model Context Gateway Features & Documentation Index

[Home](index.md) > Features Overview

Welcome to the **Model Context Gateway (MCG)** feature directory and documentation hub. Model Context Gateway routes, orchestrates, and secures communication between AI assistants and backend Model Context Protocol (MCP) servers.

---

## Quickstart & Onboarding Guides

Step-by-step guides to deploy and evaluate Model Context Gateway in your environment:

* [**Getting Started Overview**](getting-started/index.md)
  Choose your deployment path across Docker containers, Windows Server IIS, or single-user home labs.
* [**Single-User & Home-Lab Quickstart**](deployment/homelab.md)
  Deploy MCG for personal automation or homelab environments with zero external dependencies.
* [**Automated Setup & Admin Provisioning (`mcg-admin`)**](admin-mcp-automation-guide.md)
  Use AI coding assistants (Claude Code, Antigravity, Cursor) to install, configure, and manage MCG programmatically.
* [**Evaluation Guide**](evaluation-guide.md)
  Evaluate MCG features, performance benchmarks, and security boundaries.

---

## User & Task Guides

Practical guides for configuring servers, connecting AI clients, and testing capabilities in the Web UI.

### Server & Connection Management
* [**Dynamic Server Management & Health Checks**](user-guide/servers.md)
  Register backend MCP servers via the Web UI, JSON seeding (`custom_servers.json`), or Docker container labels (`mcp.*`), and configure `RootOrigin` or `JsonRpcPing` health probes.
* [**MCP Code Mode (`execute_script`)**](user-guide/code-mode.md)
  Orchestrate multi-step tool workflows in a single LLM turn using sandboxed JavaScript (`mcg.callTool` and `tools.<server>.<tool>`), automatic JSON unboxing, and configurable resource guardrails.
* [**AppKey Management & Atomic Rotation**](user-guide/app-keys.md)
  Generate scoped AppKeys, monitor live `LastUsedAt` activity, rotate credentials in place without recreating keys, and generate ready-to-paste client configs.
* [**System Settings, Search Thresholds & Code Mode**](user-guide/settings.md)
  Configure local ONNX or external embeddings, tune `search_tools` minimum score and drop-off ratios, and manage Code Mode sandbox limits.
* [**Client Setup & IDE Integration**](user-guide/clients/index.md)
  Connect AI clients and IDEs to MCG:
  * [Cursor IDE Integration](user-guide/clients/cursor.md)
  * [Claude Desktop Integration](user-guide/clients/claude-desktop.md)
  * [Cline & VS Code Integration](user-guide/clients/cline-and-vscode.md)
  * [Antigravity CLI Integration](user-guide/clients/antigravity.md)

### Interactive Developer Test Bench
* [**Test Bench Overview**](user-guide/test-bench/index.md)
  Inspect JSON-RPC traffic, test tools with dynamic form builders, simulate calibrated hybrid search, and view live logs:
  * [Tool Execution Tester](user-guide/test-bench/tool-tester.md)
  * [Virtual Resources & Prompts](user-guide/test-bench/resources-and-prompts.md)
  * [Semantic Router Simulator](user-guide/test-bench/semantic-search.md)
  * [Raw Console & Live Logs](user-guide/test-bench/console-and-logs.md)

### Deployment & Hosting
* [**Docker & Container Deployment**](deployment/docker.md)
* [**Windows Server IIS In-Process Hosting**](deployment/windows-iis.md)
* [**Windows Service (SCM) & DPAPI**](deployment/windows-service.md)
* [**Multi-Provider Database Setup**](deployment/database-setup.md)

---

## Technical Reference & Specifications

Detailed technical specifications, parameter matrices, and API references.

### Gateway Tools & Scopes
* [**Admin MCP Tools Reference**](admin-mcp-features.md)
  Complete reference for the 10 consolidated administrative tools (`manage_servers`, `manage_appkeys`, `manage_policies`, `manage_group_mappings`, `manage_providers`, `manage_settings`, `manage_custom_files`, `manage_system`, `test_tool_call`, `manage_clients`).
* [**AppKey Taxonomy & Scopes**](appkey-scopes.md)
  AppKey token structure, SHA-256 hashing, atomic rotation, and scope granularity (`*`, `server:<id>`, `category:<name>`, `tool:<name>`).

### Protocols & Standards Alignment
* [**MCP 2026-07-28 Specification Alignment**](architecture/transports-and-subprocesses.md)
  Support for `resultType`, Multi Round-Trip Requests (MRTR), `ttlMs`/`cacheScope` cacheable metadata, and JSON-RPC error taxonomy.
* [**RFC 9728 Protected Resource Metadata & Discovery**](auth-flows/mcp-request-auth-flow.md)
  Zeroconf client discovery via `/.well-known/oauth-protected-resource` and standard 401 `WWW-Authenticate` handshakes.
* [**RFC 7591 Dynamic Client Registration**](auth-flows/dynamic-client-registration.md)
  OAuth 2.1 client registration with mandatory `application_type` validation and redirect URI management.

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

## Architecture & Deep-Dive Explanations

In-depth explanations of core architectural concepts, security pipelines, and subsystem mechanics.

### Core Routing & Processing
* [**Architecture Overview**](architecture/index.md)
  High-level system topology, module boundaries, and request lifecycle.
* [**Routing, Meta-Mode & Calibrated Hybrid Search**](architecture/routing-and-meta-mode.md)
  Multi-delimiter tool resolution (`__`, `.`, `/`), cold-start on-demand discovery, calibrated hybrid vector + lexical scoring, and two-stage relevance thresholding.
* [**Code Mode Sandbox Engine Architecture**](architecture/code-mode.md)
  In-process Jint JavaScript isolation, `mcg.callTool` and `tools.<server>.<tool>` proxy mechanics, automatic JSON unboxing, statement/memory ceilings, and dual-layer RBAC enforcement.
* [**Transports & Subprocess Execution**](architecture/transports-and-subprocesses.md)
  Stateful SSE, stateless HTTP, and STDIO subprocess execution models (including the batteries-included `latest-full` container image).

### Security & Authentication Pipelines
* [**Authentication Architecture**](authentication-architecture.md)
  Unified identity resolution across Windows Integrated Auth, JWT, and reverse proxies.
* [**Unified Authorization Pipeline**](architecture/authorization-pipeline.md)
  4-stage fail-closed security pipeline enforcing AppKey scopes, administrator SIDs, database access policies, and discovery filtering across tools, prompts, resources, and completions.
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
