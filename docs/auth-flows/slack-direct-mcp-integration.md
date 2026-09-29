# Slack Direct MCP Server & MCG Hybrid Integration Guide

## 1. Overview & Architecture

Slack natively provides an official, direct Model Context Protocol (MCP) server at:
```
https://mcp.slack.com/mcp
```

This guide details how to integrate Slack's direct MCP server with **Model Context Gateway (MCG)** using the **Hybrid Egress Model**, which supports both:
1. **Autonomous Background Agents (e.g., OpenClaw, CI/CD bots)**: Authenticate 24/7 without interactive browser prompts via a dedicated installer/service account User token (`xoxp-...`).
2. **Interactive Human Users (e.g., Developers, Admins)**: Authenticate individually via standard 3-Legged OAuth (3LO) redirect, granting fine-grained user delegation and personal audit trails.

### Core Capabilities
Connecting to `https://mcp.slack.com/mcp` provides direct access to Slack tools:
* **Search**: Global search across messages, channels, files, users, and emojis.
* **Canvases**: Read, create, and update rich formatted canvases with markdown export.
* **Lists**: Query, create, and modify structured records and databases.
* **Messaging & Collaboration**: Read/write public channels, private groups, threads, DMs, and reactions.
* **Files**: Two-step signed upload URLs and file completion.

---

## 2. Strict Protocol & Schema Compliance

### 2.1 The `_meta` Parameter Placement (Zod Strict Validation)
The official TypeScript `@modelcontextprotocol/sdk` validates incoming JSON-RPC 2.0 requests with `JSONRPCRequestSchema.strict()`. Root-level properties not defined in the JSON-RPC 2.0 spec (`jsonrpc`, `id`, `method`, `params`) will fail validation with `400 Bad Request: Unrecognized key(s) in object: '_meta'`.

**Requirement**: In MCG, OpenTelemetry and W3C trace context metadata (`traceparent`, `tracestate`) must **never** be injected at the JSON-RPC root. They are injected inside `params._meta` (`BaseRequestParamsSchema` permits `_meta` for tracing).

### 2.2 Token Type Requirement: User Token (`xoxp-...`) Is Mandatory
Slack's MCP endpoint strictly verifies that the Bearer token belongs to an authorized user:
* Calling with a Bot Token (`xoxb-...`) returns:
  ```json
  {"jsonrpc":"2.0","id":null,"error":{"code":-32001,"message":"invalid_token_type"}}
  ```
* Calling without a token returns:
  ```json
  {"jsonrpc":"2.0","id":null,"error":{"code":-32001,"message":"missing_token"}}
  ```
* Calling with an authorized User Token (`xoxp-...`) succeeds.

---

## 3. Slack App Manifest Configuration

In the Slack Developer Portal ([api.slack.com/apps](https://api.slack.com/apps)), apply the following App Manifest JSON:

```json
{
  "display_information": {
    "name": "ModelContextGateway",
    "description": "Enterprise Model Context Gateway Slack Integration",
    "background_color": "#002045"
  },
  "features": {
    "bot_user": {
      "display_name": "MCG Assistant",
      "always_online": true
    }
  },
  "oauth_config": {
    "scopes": {
      "user": [
        "canvases:read",
        "canvases:write",
        "channels:history",
        "channels:read",
        "channels:write",
        "chat:write",
        "emoji:read",
        "files:read",
        "files:write",
        "groups:history",
        "groups:read",
        "groups:write",
        "im:history",
        "im:read",
        "im:write",
        "lists:read",
        "lists:write",
        "mpim:history",
        "mpim:read",
        "mpim:write",
        "reactions:read",
        "reactions:write",
        "search:read.files",
        "search:read.im",
        "search:read.mpim",
        "search:read.private",
        "search:read.public",
        "search:read.users",
        "users:read",
        "users:read.email"
      ],
      "user_optional": [
        "canvases:read",
        "canvases:write",
        "chat:write",
        "files:read",
        "files:write",
        "groups:history",
        "im:history",
        "lists:read",
        "lists:write",
        "mpim:history",
        "search:read.files",
        "search:read.im",
        "search:read.mpim",
        "search:read.private"
      ],
      "bot": [
        "app_mentions:read",
        "channels:history",
        "channels:read",
        "chat:write",
        "groups:history",
        "groups:read",
        "im:history",
        "im:read",
        "im:write",
        "mpim:history",
        "mpim:read"
      ]
    },
    "pkce_enabled": false
  },
  "settings": {
    "interactivity": {
      "is_enabled": true
    },
    "org_deploy_enabled": false,
    "socket_mode_enabled": true,
    "token_rotation_enabled": false,
    "app_level_token_rotation_enabled": false,
    "is_mcp_enabled": true
  }
}
```

> [!IMPORTANT]
> Under `settings`, `"is_mcp_enabled": true` is required to activate Slack's native MCP server for your app.
> Under **OAuth & Permissions** -> **Redirect URLs**, register your gateway callback URL:
> `https://<your-mcg-host>/api/oauth/egress/callback`

---

## 4. The Hybrid Integration Model in MCG

MCG supports a hybrid authentication resolution pipeline in `HttpTransport`:

```mermaid
flowchart TD
    Req[Incoming Client Request] --> AuthCheck{Is User Identified?}
    AuthCheck -- "Yes (Remote-User header)" --> UserStoreCheck{User Has 3LO Token in DB?}
    UserStoreCheck -- "Yes" --> InjectUser[Inject User's xoxp Token]
    UserStoreCheck -- "No" --> FallbackCheck{Server ApiKey Configured?}
    AuthCheck -- "No (Headless / Agent)" --> FallbackCheck
    FallbackCheck -- "Yes" --> InjectService[Inject Fallback Service xoxp Token]
    FallbackCheck -- "No" --> Challenge[Return 401 Unauthorized / Prompt 3LO]
    InjectUser --> Dispatch[POST https://mcp.slack.com/mcp]
    InjectService --> Dispatch
```

### 4.1 Server Configuration Parameters

| Field | Value | Description |
| :--- | :--- | :--- |
| `Id` | `slack` | Unique identifier for routing |
| `Url` | `https://mcp.slack.com/mcp` | Direct Slack MCP endpoint |
| `Type` | `http` | Streamable HTTP transport |
| `AuthShape` | `bearer` | Standard HTTP `Authorization: Bearer <token>` |
| `ApiKey` | `xoxp-...` | Static service account User token (autonomous agent fallback) |
| `EnableOAuth3Lo` | `true` | Activates per-user 3LO OAuth flow |
| `OAuthClientId` | `<Slack App Client ID>` | From App Credentials |
| `OAuthClientSecret` | `<Slack App Client Secret>` | From App Credentials (stored encrypted) |
| `OAuthAuthorizationUrl` | `https://slack.com/oauth/v2_user/authorize` | Slack user authorization endpoint |
| `OAuthTokenUrl` | `https://slack.com/api/oauth.v2.user.access` | Slack user token exchange endpoint |
| `OAuthScopes` | `<comma-separated 28 scopes>` | Requested user scopes |
| `OAuthRedirectUri` | `https://<mcg-host>/api/oauth/egress/callback` | Callback URL |

---

## 5. How Users & Agents Interact

### 5.1 Interactive Humans (3LO Flow)
1. User logs into MCG (or visits `https://<mcg-host>/api/oauth/egress/authorize/slack`).
2. MCG redirects to Slack with `user_scope` and cryptographically random `state`.
3. User approves in Slack browser prompt.
4. Slack redirects to `/api/oauth/egress/callback`.
5. MCG exchanges `code` for the user's specific `xoxp` token and encrypts it in `UserServerCredentials`.
6. Future tool calls from this user are attributed directly to them in Slack audit logs.

### 5.2 Autonomous Agents (Headless Flow)
1. Agent (e.g. OpenClaw) connects to MCG via `/sse` or target route using its AppKey.
2. Request has no `Remote-User` header.
3. MCG automatically uses the static `ApiKey` (`xoxp-...`) configured on server `slack`.
4. Agent executes tools 24/7 without needing interactive browser logins.

---

## 6. Verification & Testing

### 6.1 Verify Tools via Admin MCP
```json
{
  "tool": "test_tool_call",
  "arguments": {
    "serverId": "slack",
    "toolName": "slack_list_user_channels",
    "arguments": {
      "types": "public_channel"
    }
  }
}
```

### 6.2 Verify Direct Curl
```bash
curl -s -X POST https://mcp.slack.com/mcp \
  -H "Authorization: Bearer xoxp-YOUR-TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
```
