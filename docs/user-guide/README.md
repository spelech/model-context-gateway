# Model Context Gateway (MCG) User Guide

Welcome to the **Model Context Gateway (MCG)** User Guide. 

MCG connects your AI assistants to all your tools and data sources through a single secure connection.

For the primary user guide index and common workflows, see the [**User Guide Index**](index.md).

---

## User Guide Directory Structure

* [**User Guide Overview & Core Concepts**](index.md)
* [**01. Dashboard & Navigation**](dashboard.md)
* [**02. Server Management & Secrets**](servers.md)
* [**03. RBAC, Security & Policies**](rbac-and-policies.md)
* [**04. AppKey Management & Scopes**](app-keys.md)
* [**05. Client Setup & Integration**](clients/index.md)
  * [Cursor IDE](clients/cursor.md)
  * [Claude Desktop](clients/claude-desktop.md)
  * [Cline & VS Code](clients/cline-and-vscode.md)
  * [Antigravity CLI & Agents](clients/antigravity.md)
* [**06. Interactive Test Bench**](test-bench/index.md)
  * [Tool Execution Tester](test-bench/tool-tester.md)
  * [Virtual Resources & Prompts](test-bench/resources-and-prompts.md)
  * [Semantic Router Simulator](test-bench/semantic-search.md)
  * [Raw Console & Live Logs](test-bench/console-and-logs.md)
* [**07. System Settings & Embeddings**](settings.md)

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
