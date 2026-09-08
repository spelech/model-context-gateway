# Model Context Gateway (MCG) User Guide

Welcome to the **Model Context Gateway (MCG)** User Guide. 

This guide teaches you how to use the web dashboard, register backend tools, configure security, connect AI assistants, and test tool calls.

---

## What is Model Context Gateway?

The **Model Context Protocol (MCP)** lets AI assistants use external tools and read data. 

For example, an MCP tool can:
* Query a database.
* Control smart home devices.
* Read a local file or repository.
* Run a terminal command.

When you use many tools, connecting your AI directly to each one causes problems:
* **Memory waste**: Loading hundreds of tool schemas fills the AI context memory.
* **Higher cost**: Large prompts increase token usage and response times.
* **Security risks**: Storing API keys in local desktop configuration files can leak credentials.

**Model Context Gateway (MCG)** solves these problems by acting as a single, central hub between your AI assistants and all your tools.

---

## Core Concepts Explained Simply

Before you start, review these common terms:

### 1. MCP Server
An MCP server is an application that provides tools, resources, or prompt templates. Servers can run in Docker containers, as local scripts (Python, Node.js), or on remote networks.

### 2. Meta-Mode
Meta-Mode is a key feature of Model Context Gateway. 
* Standard MCP gateways send all tool definitions to the AI at startup. If you have 100 tools, the AI receives thousands of lines of JSON schema before you send your first message.
* In **Meta-Mode**, the gateway shows only two tools: `search_tools` and `execute_tool`.
* When you ask the AI to perform a task, the AI calls `search_tools` with your intent (for example: *"check database health"*).
* The gateway searches the tool catalog and returns only matching tools. The AI then calls `execute_tool` to run the specific tool.
* This process saves context memory, prevents hallucinations, and cuts API costs.

### 3. AppKey
An AppKey is a secure authentication token for client applications. AppKeys start with semantic prefixes:
* `mcp-adm-`: Administrator keys with full gateway permissions.
* `mcp-usr-`: User keys with personal permissions.
* `mcp-glb-`: Global keys for shared applications.

### 4. Transports
A transport is the communication channel between the gateway and a backend server:
* **SSE (Server-Sent Events)**: Long-lived streaming connection over HTTP.
* **HTTP / Streamable**: Request-response connection over HTTP.
* **STDIO**: Runs a local subprocess (Python script, Node.js script, or binary executable) on the host server.

### 5. Role-Based Access Control (RBAC)
Security rules that control which users, groups, or AI agents can see and run specific tools.

---

## User Guide Chapters

This guide contains six detailed chapters:

| Chapter | Topic | What You Will Learn |
| :--- | :--- | :--- |
| [**01. Dashboard & Navigation**](user-guide/01-dashboard-and-navigation.md) | Interface & Metrics | Navigate dashboard tabs, read system metrics, search and sort servers. |
| [**02. Server Management & Secrets**](user-guide/02-server-management-and-secrets.md) | Tool Servers & Credentials | Add new MCP servers, inspect tools, configure Vault, DPAPI, or Environment secrets. |
| [**03. RBAC, Security & Policies**](user-guide/03-rbac-and-security.md) | Access Control | Create access policies, set user quotas, map Active Directory or OIDC groups. |
| [**04. Client Setup & AppKeys**](user-guide/04-client-setup-and-app-keys.md) | AI Assistant Setup | Generate AppKeys, set scopes, and connect Claude Desktop, Cursor, Cline, or Antigravity. |
| [**05. Interactive Test Bench**](user-guide/05-interactive-test-bench.md) | Testing & Debugging | Test tool execution directly, read virtual resources, inspect live logs. |
| [**06. Settings & Embeddings**](user-guide/06-settings-and-embeddings.md) | System Configuration | Configure local ONNX or OpenAI vector search, manage auth providers and custom prompts. |

---

## Common Workflows

### Workflow 1: Connect Your First AI Assistant

1. Open the Web Dashboard at `http://localhost:8080`.
2. Click the **App Keys & Security** tab.
3. Click **+ Generate App Key**.
4. Enter a name (for example: `Cursor IDE`) and select scope `all`.
5. Copy the generated key (`mcp-usr-...` or `mcp-adm-...`).
6. Open your AI client settings file (for example: `~/.cursor/mcp.json`).
7. Paste the configuration snippet from [Chapter 04: Client Setup](user-guide/04-client-setup-and-app-keys.md).
8. Start using your tools in your AI assistant!

### Workflow 2: Add a Backend MCP Server

1. In the Web Dashboard, click the **Overview** tab.
2. Click **+ Add Server** in the top right toolbar.
3. Enter the server details:
   * **Display Name**: Human-readable name (for example: `Home Assistant`).
   * **URL**: Server address (for example: `http://ha-mcp:8086/mcp`).
   * **Transport Type**: Select `sse`, `http`, or `stdio`.
   * **Alias**: Short prefix for tool names (for example: `ha`).
4. Click **Save Server**.
5. The gateway connects to the server and discovers its tools automatically.

### Workflow 3: Test a Tool in the Test Bench

Before exposing a new tool to your AI, test it in the dashboard:

1. Click the **Test Bench** tab.
2. Select your server from the dropdown menu.
3. Select the tool you want to test.
4. Enter test arguments in the JSON form editor.
5. Click **Call Tool** to execute the request.
6. Review the output returned by the tool.

### Workflow 4: Manage Your Profile and Quotas

Users can check active credentials and quotas:
1. In the top right corner, click your username badge.
2. Review your quota limits:
   * **Global Maximum Keys**: Maximum active keys across the gateway.
   * **User Maximum Keys**: Maximum active keys allowed for your account.
   * **Your Active Keys**: Keys you currently have active.
3. If an AppKey is lost or compromised, find the key in the list and click **Revoke** immediately.

---

## Related Guides

* [**Single-User & Home-Lab Setup Guide**](single-user-and-homelab-guide.md) — Fast setup for local AI environments.
* [**Administrator Guide**](admin-guide.md) — Complete administration procedures and MCP tool reference.
* [**AppKey Scopes & Authorization Guide**](appkey-scopes.md) — Scope grammar and evaluation rules.
* [**Troubleshooting & RCA Guide**](mcp-routing-and-admin-issues.md) — Common error messages and solutions.
