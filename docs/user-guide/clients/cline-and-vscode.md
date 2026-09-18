# Connecting Cline, Roo Code & VS Code

The **Model Context Gateway (MCG)** integrates seamlessly with autonomous coding extensions in Visual Studio Code, including [Cline](https://github.com/cline/cline), [Roo Code](https://github.com/RooVetGit/Roo-Code), and [Continue](https://continue.dev/).

---

## 📁 Configuration File Locations

Cline and Roo Code store MCP server definitions in their extension settings directory:

* **macOS**: `~/Library/Application Support/Code/User/globalStorage/saoudrizwan.claude-dev/settings/cline_mcp_settings.json`
* **Windows**: `%APPDATA%\Code\User\globalStorage\saoudrizwan.claude-dev\settings\cline_mcp_settings.json`
* **Linux**: `~/.config/Code/User/globalStorage/saoudrizwan.claude-dev/settings/cline_mcp_settings.json`

Alternatively, you can open the settings file directly within VS Code:
1. Open the Cline or Roo Code extension tab in the VS Code Activity Bar.
2. Click the **MCP Servers** icon (the plug or network icon).
3. Click **Configure MCP Servers** or **Edit Settings File**.

---

## 🚀 Configuration Options

### Option A: Unified Meta-Mode (Recommended)

Connect to `/sse` to use dynamic two-step tool searching:

```json
{
  "mcpServers": {
    "mcg": {
      "url": "http://localhost:8080/sse",
      "headers": {
        "Authorization": "Bearer mcp-usr-Xk9L2mPq-7vN3wZ8aB1cE4fG9"
      }
    }
  }
}
```

### Option B: Target Server Direct Proxy

To expose all tools from a specific backend server directly to Cline (e.g. `docker`):

```json
{
  "mcpServers": {
    "docker": {
      "url": "http://localhost:8080/docker",
      "headers": {
        "Authorization": "Bearer mcp-usr-Xk9L2mPq-7vN3wZ8aB1cE4fG9"
      }
    }
  }
}
```

---

## 🔄 Verification & Usage

1. In the Cline / Roo Code panel, open the **MCP Servers** view.
2. Verify that `mcg` shows a **Connected** badge with a green light.
3. In a new chat prompt, instruct the agent:
   > "Use your MCP tools to check the status of running containers on the Docker server."
4. Cline calls `search_tools` on MCG, retrieves the relevant tool definition (`docker__ps`), and executes it via `execute_tool`.

---

## 🛠️ Troubleshooting

* **Connection Errors in Output Panel**:
  * Open the VS Code **Output** panel (`View` -> `Output`).
  * In the dropdown, select **Cline** or **Roo Code**.
  * Check for HTTP connection refused or authorization failures.
* **Header Authorization Issues**:
  * Verify that the header key is spelled `"Authorization"` with `"Bearer <key>"` as the value.
  * Ensure the AppKey has not expired or exceeded quota limits.
