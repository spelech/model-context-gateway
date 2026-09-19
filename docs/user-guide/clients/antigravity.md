# Connecting Antigravity CLI & Autonomous Agents

The **Model Context Gateway (MCG)** is optimized for command-line autonomous agents, background workers, and AI coding assistants like Google DeepMind's **Antigravity CLI (`agy`)**.

---

## ⚡ Quick Connect via CLI

You can establish live connections to MCG using environment variables and the `agy mcp connect` command:

```bash
# Export environment variables
export MCG_URL="http://localhost:8080/sse"
export MCG_KEY="mcp-adm-Xk9L2mPq-7vN3wZ8aB1cE4fG9"

# Connect via Antigravity CLI
agy mcp connect --url "$MCG_URL" --header "Authorization: Bearer $MCG_KEY"
```

---

## 🎯 Direct Target Proxy & Admin Server Connection

### Target Server Direct Proxy
If an autonomous agent is tasked with managing a single domain (for example, Docker infrastructure), connect directly to the target server proxy to expose its tools without two-step semantic search:

```bash
agy mcp connect \
  --url "http://localhost:8080/docker" \
  --header "Authorization: Bearer mcp-usr-Xk9L2mPq-7vN3wZ8aB1cE4fG9"
```

### Admin MCP Server Connection
For autonomous DevOps agents that provision, update, or audit the gateway itself:

```bash
agy mcp connect \
  --url "http://localhost:8080/admin/sse" \
  --header "Authorization: Bearer mcp-adm-Xk9L2mPq-7vN3wZ8aB1cE4fG9"
```
This exposes the complete suite of gateway administration tools (`manage_servers`, `manage_appkeys`, `manage_policies`, `manage_providers`, `manage_settings`, `manage_custom_files`).

---

## 📁 Persistent Agent Configuration

To configure MCG persistently for autonomous agent sessions, register it in your agent configuration directory (e.g. `~/.gemini/antigravity-cli/mcp/mcg/settings.json`):

```json
{
  "mcpServers": {
    "mcg": {
      "url": "http://localhost:8080/sse",
      "headers": {
        "Authorization": "Bearer mcp-adm-Xk9L2mPq-7vN3wZ8aB1cE4fG9"
      }
    }
  }
}
```

---

## 🔐 Zero-Config Discovery & Connected Accounts (3LO)

* **RFC 9728 Handshake**: Unauthenticated calls to `/sse` automatically receive standard `WWW-Authenticate: Bearer realm="mcp", resource_metadata=".../.well-known/oauth-protected-resource"` headers, enabling compliant agents to perform automated OAuth discovery without pre-shared API keys.
* **Per-User Egress Tokens**: When an agent invokes tools against third-party SaaS services (GitHub, Slack, Jira), the agent only passes its gateway credentials. MCG inspects the user session, resolves the user's vaulted 3LO OAuth token, strips the agent's ingress key, and injects the user's SaaS bearer token into outbound requests.

---

## 🛠️ Operational Tips for Autonomous Agents

1. **Leverage Meta-Mode in Multi-Server Environments**:
   Autonomous agents perform best when prompt context is not flooded with irrelevant schemas. In Meta-Mode, agents use `search_tools` to find exactly what they need before calling `execute_tool`.
2. **Apply Least-Privilege AppKeys**:
   Assign scoped keys (e.g. `category:infrastructure` or `server:docker`) to agents rather than global `admin` keys whenever possible.
3. **Handle Empty 202 Accepted Gracefully**:
   Stateless downstream notifications or tool calls may return empty HTTP `202 Accepted` bodies. Agent frameworks should handle empty payloads without JSON deserialization crashes.
4. **Use PII-Redacted Logs**:
   Inspect real-time agent tool invocations through the gateway's Test Bench or `GET /api/logs` to monitor agent behavior with automated token redaction.
