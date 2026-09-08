# Model Context Gateway (MCG) User Guide

Welcome to the **Model Context Gateway (MCG)** User Guide. 

MCG connects your AI assistants to all your tools and data sources through a single secure connection.

For a beginner-friendly overview and common workflows, see the [**User Guide Overview**](../user-guide.md).

---

## User Guide Chapters

1. [**01. Dashboard & Navigation Interface**](01-dashboard-and-navigation.md)
   - Dashboard layout, top navigation tabs, health metric cards, real-time search, sorting, and category grouping.
2. [**02. Server Management & Secret Providers**](02-server-management-and-secrets.md)
   - Registering backend MCP servers across transports (`SSE`, `HTTP`, `STDIO`), inspecting tool schemas, and configuring secrets:
     - Plaintext / Static Keys
     - Host Environment Variables (`ENV:KEY`)
     - HashiCorp Vault (KV v2 engine, AppRole, JIT renewal)
     - Windows Registry (DPAPI decryption)
     - OAuth 2.0 / OIDC Token Exchange (RFC 8693)
3. [**03. RBAC, Security & Policies**](03-rbac-and-security.md)
   - 4-Stage Authorization Pipeline (`Explicit Deny` > `Explicit Allow` > `AppKey Scope` > `Default Policy`), Identity Providers (OIDC headers, Active Directory SIDs, AppKeys, standalone IP allowlists), user quotas, and group mappings.
4. [**04. Client Setup & AppKey Management**](04-client-setup-and-app-keys.md)
   - Generating hashed AppKeys, scope syntax (`*`, `category:*`, `server:*`, granular capabilities), and integration snippets for Cursor, Claude Desktop, Antigravity CLI, and VS Code Cline.
5. [**05. Interactive Test Bench**](05-interactive-test-bench.md)
   - Testing tool execution directly, inspecting virtual resources (`mcp://...`), evaluating prompt templates, testing semantic vector search, and monitoring live gateway logs.
6. [**06. System Settings & Vector Embeddings**](06-settings-and-embeddings.md)
   - Configuring vector search engines (Local ONNX CPU vs remote OpenAI/Ollama APIs), managing identity providers, and uploading custom prompt files.

---

## Core System Concepts

- **Meta-Mode (`/sse`)**: Exposes only 2 tools (`search_tools` and `execute_tool`) by default to save AI context memory and prevent hallucinations.
- **Slash Tool Routing**: Exposes tools with clean slash formatting (`{namespace}/{tool_name}`) with backwards-compatible format (`{serverId}__{toolName}`).
- **Secure Secret Storage**: Subprocesses receive credentials via environment variables rather than command-line arguments.
- **AES-256 Encryption**: All tokens, API keys, and provider secrets are encrypted at rest with AES-256-GCM.
- **Multi-Database Support**: Runs on SQLite (WAL), Microsoft SQL Server, and MySQL.

---

## 🧭 Related Technical Documentation

* 📖 [**MCP Server Auth & Integration Cookbook**](../mcp-server-auth-cookbook.md)
* 🎯 [**Evaluation & Product Overview Guide**](../evaluation-guide.md)
* 🏛️ [**Comprehensive Enterprise Architecture Guide**](../architecture.md)
* 🔐 [**Enterprise Secret Providers Guide**](../secret-providers.md)
* 🔑 [**AppKey Scopes & Authorization Guide**](../appkey-scopes.md)
* 🚀 [**Transport Capability & Configuration Guide**](../transports.md)
* 🗄️ [**Database Provider Support & Deployment Matrix**](../database-providers.md)
* 💻 [**Developer & Contributing Guide**](../developer-guide.md)
* 🛠️ [**Operations & Production Runbook**](../runbook.md)
* 🤖 [**Admin MCP Server & Automation Guide**](../admin-mcp-automation-guide.md)

