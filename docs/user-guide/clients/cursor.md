# Connecting Cursor IDE

The **Model Context Gateway (MCG)** integrates natively with [Cursor IDE](https://cursor.com/) using standard Model Context Protocol (MCP) configuration files.

---

## 📁 Configuration File Locations

Cursor reads MCP configurations from either a project-specific directory or global user settings:

* **Project-Specific (Recommended)**: `.cursor/mcp.json` in the root of your workspace or git repository.
* **Global User Settings**:
  * **macOS**: `~/.cursor/mcp.json`
  * **Linux**: `~/.cursor/mcp.json`
  * **Windows**: `%USERPROFILE%\.cursor\mcp.json`

---

## 🚀 Configuration Options

### Option A: Unified Meta-Mode (Recommended)

Connecting to `/sse` activates **Meta-Mode**. Cursor receives only `search_tools` and `execute_tool`, allowing your AI model to search hundreds of tools without context window saturation:

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

If you want Cursor to access a specific backend server directly (exposing all its tools without two-step semantic search), target the server ID route:

```json
{
  "mcpServers": {
    "docker": {
      "url": "http://localhost:8080/docker",
      "headers": {
        "Authorization": "Bearer mcp-usr-Xk9L2mPq-7vN3wZ8aB1cE4fG9"
      }
    },
    "homeassistant": {
      "url": "http://localhost:8080/homeassistant",
      "headers": {
        "Authorization": "Bearer mcp-usr-Xk9L2mPq-7vN3wZ8aB1cE4fG9"
      }
    }
  }
}
### Option C: Automatic OAuth Discovery (RFC 9728)

If your organization secures MCG using an Identity Provider (Authentik, Keycloak, Microsoft Entra ID), modern Cursor versions support automatic OAuth Protected Resource Metadata (RFC 9728) discovery:

```json
{
  "mcpServers": {
    "mcg": {
      "url": "http://localhost:8080/sse"
    }
  }
}
```

When connecting without a hardcoded AppKey, Cursor receives a `401 Unauthorized` challenge with `WWW-Authenticate: Bearer realm="mcp", resource_metadata=".../.well-known/oauth-protected-resource"`. Cursor automatically resolves the corporate IdP metadata and initiates standard browser authentication.

---

## 🔄 Verification & Reloading

1. Open **Cursor Settings** (`Cmd + ,` or `Ctrl + ,`).
2. Navigate to **Features** -> **MCP Servers**.
3. Verify that `mcg` displays a **green status indicator**.
4. In the Chat panel (`Cmd + L` or `Ctrl + L`), ask the AI:
   > "Search available tools for inspecting running Docker containers."
5. Cursor invokes `search_tools` on MCG, receives matching tool schemas, and presents execution options.

---

## 🛠️ Troubleshooting

* **Yellow / Red Dot in Settings**: Ensure MCG is running (`curl -s http://localhost:8080/health` should return `{"status":"healthy"}`).
* **Authorization Failed (`401` / `403`)**: Verify your AppKey starts with `mcp-usr-` or `mcp-adm-` and has not expired or been revoked.
* **Trailing Slashes**: Ensure the URL matches `http://localhost:8080/sse` exactly without a trailing slash.
