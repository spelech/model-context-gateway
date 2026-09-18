# Client Setup & Integration Overview

The **Model Context Gateway (MCG)** enables AI coding assistants, IDEs, autonomous agents, and custom software to securely connect to downstream MCP servers through standardized, authenticated endpoints.

---

## 🔌 Connection Endpoints & Routing Modes

MCG provides three primary routing paths for connecting MCP clients:

| Route Path | Mode | Description | Recommended For |
| :--- | :--- | :--- | :--- |
| **`/sse`** | **Unified Meta-Mode** | Exposes only `search_tools` and `execute_tool`. Searches the catalog dynamically on demand to minimize context saturation. | **All AI Assistants & IDEs** (Cursor, Claude Desktop, Cline, Antigravity) |
| **`/{targetServerId}`** | **Target Server Proxy** | Bypasses Meta-Mode, proxying 100% of the tools, resources, and prompts from a specific server (e.g. `/docker`, `/homeassistant`). | Dedicated agents, single-purpose workflows, or clients requiring direct tool schemas. |
| **`/admin/sse`** | **Admin Management Mode** | Exposes the complete suite of gateway administration tools (`manage_servers`, `manage_appkeys`, `manage_policies`). | Autonomous DevOps agents and admin scripts. |

---

## 🎛️ Dynamic Client Configuration Generator

The **Client Setup Guide** card (available on both the **Overview** and **App Keys & Security** tabs) features an interactive configuration generator:

```
+---------------------------------------------------------------------------------------+
| ⚙️ Dynamic Client Configuration Generator                                              |
+---------------------------------------------------------------------------------------+
| Target Route: [ Unified Meta-Mode (/sse) ▾                                          ] |
| Client Tool:  [ Cursor IDE ▾              ]   Host: [ http://localhost:8080         ] |
| [☑ Include Key]  Key: [ mcp-usr-Xk9L2mPq-7vN3wZ8aB1cE4fG9                         ] |
+---------------------------------------------------------------------------------------+
| [ Copy Configuration Snippet ]                                                        |
+---------------------------------------------------------------------------------------+
```

1. **Select Target Route**: Choose **Unified Meta-Mode (`/sse`)** for aggregated discovery, or choose a specific downstream server (e.g. `/docker`).
2. **Select Client Tool**: Choose your AI environment (Cursor, Claude Desktop, Cline / VS Code, Antigravity CLI, or SDKs).
3. **Include Key**: Check the box to auto-fill your active AppKey into the generated JSON or bash snippet.
4. **Copy & Paste**: Click **Copy** and paste directly into your client configuration file.

---

## 🔐 Client Authentication

All client requests must authenticate using an authorized AppKey:
* **HTTP Header (Standard)**:
  ```http
  Authorization: Bearer mcp-usr-Xk9L2mPq-7vN3wZ8aB1cE4fG9
  ```
* **Query Parameter Fallback**: If your client or proxy environment cannot send custom HTTP headers during the initial SSE handshake, MCG accepts:
  ```http
  http://localhost:8080/sse?apiKey=mcp-usr-Xk9L2mPq-7vN3wZ8aB1cE4fG9
  ```

---

## 📚 Dedicated Client Configuration Guides

Follow the step-by-step guide for your preferred AI client:

* [**Cursor IDE Setup**](cursor.md) — Configure `.cursor/mcp.json` for Meta-Mode and direct proxy routing.
* [**Claude Desktop Setup**](claude-desktop.md) — Configure `claude_desktop_config.json` using the `@modelcontextprotocol/client-sse` bridge.
* [**Cline, Roo Code & VS Code Setup**](cline-and-vscode.md) — Configure `cline_mcp_settings.json` and VS Code extensions.
* [**Antigravity CLI & Autonomous Agents**](antigravity.md) — Command-line flags and environment variables for autonomous agent execution.

---

## 💻 Programmatic SDK Integration

For custom software, scripts, or autonomous agent frameworks, use the official MCP SDKs:

### 1. TypeScript SDK (`@modelcontextprotocol/sdk`)

```typescript
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { SSEClientTransport } from "@modelcontextprotocol/sdk/client/sse.js";

const transport = new SSEClientTransport(
  new URL("http://localhost:8080/sse"),
  {
    requestInit: {
      headers: {
        "Authorization": "Bearer mcp-usr-Xk9L2mPq-7vN3wZ8aB1cE4fG9"
      }
    }
  }
);

const client = new Client({ name: "custom-ts-agent", version: "1.0.0" }, { capabilities: {} });
await client.connect(transport);

// In Meta-Mode, search for relevant tools dynamically
const searchResult = await client.callTool({
  name: "search_tools",
  arguments: { query: "restart container" }
});
console.log("Discovered tools:", searchResult);
```

### 2. Python SDK (`mcp`)

```python
import asyncio
from mcp import ClientSession
from mcp.client.sse import sse_client

async def main():
    headers = {"Authorization": "Bearer mcp-usr-Xk9L2mPq-7vN3wZ8aB1cE4fG9"}
    async with sse_client("http://localhost:8080/sse", headers=headers) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            tools = await session.list_tools()
            print("Connected! Available bootstrap tools:", [t.name for t in tools.tools])

asyncio.run(main())
```
