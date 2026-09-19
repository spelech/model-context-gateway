# Connecting Claude Desktop

The **Model Context Gateway (MCG)** integrates with the official [Claude Desktop](https://claude.ai/download) client. Because Claude Desktop runs local MCP processes, it connects to MCG's SSE endpoints using the official `@modelcontextprotocol/client-sse` bridge utility.

---

## 📁 Configuration File Locations

Edit or create the `claude_desktop_config.json` configuration file in your platform's configuration directory:

* **macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
* **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
* **Linux**: `~/.config/Claude/claude_desktop_config.json`

---

## 🚀 Configuration Options

### Option A: Unified Meta-Mode (Recommended)

Connect Claude Desktop to MCG's unified Meta-Mode endpoint (`/sse`). Claude Desktop discovers only `search_tools` and `execute_tool`, avoiding context memory overload:

```json
{
  "mcpServers": {
    "mcg": {
      "command": "npx",
      "args": [
        "-y",
        "@modelcontextprotocol/client-sse",
        "http://localhost:8080/sse"
      ],
      "env": {
        "Authorization": "Bearer mcp-usr-Xk9L2mPq-7vN3wZ8aB1cE4fG9"
      }
    }
  }
}
```

### Option B: Target Server Direct Proxy

To expose all tools from a specific server (e.g. `docker` or `homeassistant`) directly to Claude Desktop without semantic search, target that server's endpoint:

```json
{
  "mcpServers": {
    "docker": {
      "command": "npx",
      "args": [
        "-y",
        "@modelcontextprotocol/client-sse",
        "http://localhost:8080/docker"
      ],
      "env": {
        "Authorization": "Bearer mcp-usr-Xk9L2mPq-7vN3wZ8aB1cE4fG9"
      }
    }
  }
}
```

### Option C: Direct SSE with RFC 9728 Handshake

In environments using MCP clients supporting direct HTTP/SSE transports and RFC 9728 Protected Resource Metadata (such as Claude Code or Cursor), unauthenticated connections to `/sse` or `/{targetServerId}` return:

```http
HTTP/1.1 401 Unauthorized
WWW-Authenticate: Bearer realm="mcp", resource_metadata="http://localhost:8080/.well-known/oauth-protected-resource"
```

The client queries `/.well-known/oauth-protected-resource` to discover corporate IdP endpoints (`authorization_servers`) and scopes (`mcp:access`) for automatic token negotiation.

---

## 🔄 Verification & Testing

1. Completely quit Claude Desktop (ensure all background processes are closed).
2. Start Claude Desktop.
3. In any conversation window, look for the **hammer / tool icon** in the bottom right corner of the prompt box.
4. Click the tool icon to verify that `mcg` tools (`search_tools`, `execute_tool`) are active and listed.
5. Send a prompt to Claude:
   > "Search my tools for Plex library commands and tell me what actions I can run."

---

## 🛠️ Troubleshooting

* **Tool Icon Missing or Grayed Out**:
  * Ensure Node.js and `npx` are installed and available in the system PATH.
  * On macOS, GUI applications may not inherit shell PATH variables. You can specify the absolute path to `npx` (e.g. `/usr/local/bin/npx` or `/opt/homebrew/bin/npx`).
* **Connection Refused**:
  * Verify that MCG is accessible from the host system at `http://localhost:8080`.
  * If running Claude on macOS/Windows and MCG in a remote VM or Docker container, ensure firewall ports (8080) are exposed.
