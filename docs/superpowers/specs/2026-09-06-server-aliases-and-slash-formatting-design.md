# Design Specification: Server Namespace Aliasing, Slash Tool Formatting & Collision Guard

- **Date**: 2026-09-06
- **Status**: Approved
- **Repository**: `spelech/model-context-gateway` (`/containers/dev/csharp-mcp-router`)
- **Applies to**: MCG Core Routing, Persistence, Auto-Discovery, Admin MCP, React Frontend UI

---

## 1. Overview & Objectives

AI coding agents and MCP clients natively favor forward slash delimiters (`server/tool`) when invoking tools. Additionally, homelab and enterprise deployments frequently host multiple duplicate MCP server instances (e.g., duplicate Postgres MCP servers such as `postgres-mcp-homebox`, `postgres-mcp-sure`, `postgres-mcp-paperless`, or multiple `contextcortex` notes instances) that export identical tool signatures (e.g. `execute_sql` or `search_notes`).

This feature set:
1. Standardizes all exposed tool names across `tools/list` and `search_tools` on the slash format (`{namespace}/{tool_name}`).
2. Introduces server-level namespace aliasing (`Alias`) so administrators can assign clean, descriptive namespaces (e.g. `homebox_db` instead of `postgres-mcp-homebox`).
3. Supports dual-key routing and multi-delimiter interchangeability (`/`, `:`, `__`) so agents and existing clients can call tools seamlessly by alias or server ID.
4. Provides multi-instance tool collision guards: bare tool calls to duplicate tools return descriptive ambiguity errors listing valid alias options, while namespaced calls execute with strict isolation.
5. Adds full management parity across Docker auto-discovery (`mcp.alias`), REST API (`/api/servers`), `mcg-admin` MCP tool (`manage_servers`), and the React 19 / TypeScript / Zustand frontend.

---

## 2. Architecture & Data Model

### 2.1 Database Schema (`Servers` table)
- **New Column**: `Alias TEXT NULL`
- **Constraint / Index**: `CREATE INDEX IF NOT EXISTS IX_Servers_Alias ON Servers(Alias);`
- **Migration**: Automatic idempotent check on startup in `DatabaseInitializer.cs`:
  - `ALTER TABLE Servers ADD COLUMN Alias TEXT NULL;` if column `Alias` does not exist.

### 2.2 Domain Entity (`McpServer.cs`)
```csharp
namespace ModelContextGateway.Components.Servers
{
    public class McpServer
    {
        public string Id { get; set; } = string.Empty;
        public string? Alias { get; set; } // Clean routing namespace (e.g. "homebox_db")
        public string DisplayName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public bool Hidden { get; set; }
        public string Type { get; set; } = "sse";
        public string SecretProvider { get; set; } = "None";
        public string? SecretItemKey { get; set; }
        public string? SecretMount { get; set; }
        public string? SecretPath { get; set; }
        public string? SecretField { get; set; }
        public string AuthShape { get; set; } = "bearer";
        public string? CustomHeaderName { get; set; }
        public List<string> Categories { get; set; } = new();
        public string? ApiKey { get; set; }
        public string? HeadersJson { get; set; }
        public bool AutoDiscovered { get; set; } = false;
        public bool AllowPassThroughAuth { get; set; } = false;
        public string? DynamicAuthPrompt { get; set; }
    }
}
```

### 2.3 Docker Auto-Discovery (`DockerAutoDiscoveryService.cs`)
- Label inspection:
  - Checks `mcp.alias` or `mcp.namespace`.
  - Validates character set (`^[a-zA-Z0-9_-]+$`).
  - Sets `McpServer.Alias = parsedAlias`.
- Periodic reconciliation:
  - On `UpsertDiscoveredServers`, if `existing.Alias` is already set by an administrator and matches the container or hasn't changed, retain the administrator's configuration.

---

## 3. Tool Exposure & Routing Engine

### 3.1 Exposed Tool Name Format (`tools/list` & `search_tools`)
- Exposed Name:
  ```csharp
  var ns = !string.IsNullOrWhiteSpace(server.Alias) ? server.Alias : server.Id;
  var exposedName = $"{ns}/{rawToolName}";
  ```
- Description Prepending:
  - `[{ns}] {description}`
- Standardizing on slash format (`server/tool`) matches modern MCP client conventions and prompt standards.

### 3.2 Routing Table Registration (`ToolRoutingManager.Cache.cs`)
When caching tools from backend servers:
1. Register primary exposed key: `_toolRoutingTable[$"{ns}/{rawToolName}"] = server.Id;`
2. Register server ID key if alias differs: `_toolRoutingTable[$"{server.Id}/{rawToolName}"] = server.Id;`
3. Register legacy normalized variants:
   - `_toolRoutingTable[$"{ns}__{rawToolName}"] = server.Id;`
   - `_toolRoutingTable[$"{ns}:{rawToolName}"] = server.Id;`
   - `_toolRoutingTable[$"{server.Id}__{rawToolName}"] = server.Id;`
   - `_toolRoutingTable[$"{server.Id}:{rawToolName}"] = server.Id;`

### 3.3 Delimiter Normalization & Execution (`ToolRoutingManager.Execution.cs` & `NormalizeTargetToolName`)
- Incoming tool calls to `execute_tool` or JSON-RPC `tools/call` can supply:
  - `homebox_db/execute_sql` (Primary)
  - `homebox_db:execute_sql` (Colon delimiter)
  - `homebox_db__execute_sql` (Double-underscore)
  - `postgres-mcp-homebox/execute_sql` (Server ID with slash)
  - `postgres-mcp-homebox__execute_sql` (Server ID legacy)
  - `execute_sql` (Bare tool name)
- `NormalizeTargetToolName` extracts namespace and raw tool name across `/`, `:`, and `__`, resolving against both Server Aliases and Server IDs.

### 3.4 Duplicate Tool Collision Guard & Ambiguity Error
- If an agent passes a bare tool name (e.g. `execute_sql`) and that tool exists on more than one server:
  - Bare lookup detects multiple candidate routing keys.
  - Returns an ambiguity rejection error listing all distinct namespace paths:
    ```
    Ambiguous tool name 'execute_sql'. Matching tools found across multiple servers: 'homebox_db/execute_sql', 'sure_db/execute_sql'. Please call execute_tool with the full namespaced name.
    ```
- If the tool exists on exactly one server (e.g. `get_gpu_vram`), bare lookup succeeds automatically.

---

## 4. Administrative Interfaces & UI

### 4.1 REST API (`ServerEndpoints.cs`)
- Validation in `POST /api/servers` and `PUT /api/servers/{id}`:
  - `Alias` must match `^[a-zA-Z0-9_-]*$`.
  - Uniqueness: Rejects any request where `Alias` matches another server's `Id` or another server's `Alias`.
- Persistence:
  - Updated Dapper queries in `ServerEndpoints.cs` to include `Alias`.

### 4.2 Admin MCP Tool (`AdminMcpServer.cs`)
- Tool `manage_servers`:
  - Parameter `alias` added to action `add` and `update`.
  - Returns `alias` in action `list`.

### 4.3 Web UI (React 19 / TypeScript / Zustand in `frontend/`)
- Store & Types (`useServerStore.ts`, `types/server.ts`):
  - Add `alias?: string` to `McpServer` interface.
- Servers Table:
  - Display Server ID with a styled badge for `Alias` (e.g. `<span class="badge-alias">{server.alias}</span>`).
- Add / Edit Server Modal:
  - Input field for **Alias / Namespace (Optional)** with inline help text:
    *"Custom routing namespace (e.g., homebox_db). When set, tools are exposed as homebox_db/tool_name to prevent collisions between duplicate server types."*
  - Client-side validation for character format and uniqueness.

---

## 5. Formal Requirements & Verification Proofs

| Requirement ID | Type | Description | Proof Suite |
| :--- | :--- | :--- | :--- |
| **`MCP-27`** | Positive | Gateway exposes tools using `{namespace}/{tool_name}` format by default in `tools/list` and `search_tools` with `[{namespace}]` description prefix. | Backend xUnit (`ToolRoutingManagerTests.cs`) |
| **`MCP-28`** | Positive | Multi-instance duplicate tool calls route with strict isolation when called via distinct namespaces (e.g., `homebox_db/execute_sql` vs `sure_db/execute_sql`). | Backend xUnit (`ToolRoutingManagerTests.cs`) |
| **`MCP-29`** | Guardrail | Gateway rejects server creation or updates where `Alias` collides with an existing server `Id` or another server's `Alias`, or contains invalid characters. | Backend xUnit (`ServerEndpointsValidationTests.cs`) |
| **`MCP-30`** | Positive | Gateway accepts and routes tool invocations using slash (`/`), colon (`:`), double-underscore (`__`), and fallback server ID interchangeably. | Backend xUnit (`ToolRoutingManagerTests.cs`) |
| **`MCP-31`** | Positive | `DockerAutoDiscoveryService` parses `mcp.alias` from container labels and preserves manually edited DB aliases during reconciliation. | Backend xUnit (`DockerAutoDiscoveryServiceTests.cs`) |
| **`MCP-ADMIN-PARITY-SERVER-ALIAS`** | Positive | `mcg-admin` tool `manage_servers` supports `alias` in `add`, `update`, and `list` actions. | Backend xUnit (`AdminMcpServerTests.cs`) |
| **`UI-SERVERS-ALIAS-MANAGEMENT`** | Positive | React 19 server modal and table render namespace alias, validate input format, and handle collision errors. | Frontend Vitest (`ServerModal.test.tsx`) |

---

## 6. Migration & Rollout Plan

1. **Database**: Backward-compatible non-breaking column addition on startup.
2. **Backend**:
   - Update `McpServer` model and database initialization.
   - Update `DockerAutoDiscoveryService` to ingest `mcp.alias`.
   - Update `ServerEndpoints` and validation helpers.
   - Update `ToolRoutingManager` caching, naming, and execution normalization.
   - Update `AdminMcpServer` parity.
3. **Frontend**:
   - Update TypeScript server types and Zustand store.
   - Update Server table and Add/Edit Server modal with validation.
4. **Testing & Catalog Regeneration**:
   - Implement test proofs for `MCP-27` through `MCP-31`, `MCP-ADMIN-PARITY-SERVER-ALIAS`, and `UI-SERVERS-ALIAS-MANAGEMENT`.
   - Run `CatalogGenerator` to update `docs/requirements-catalog.json` and markdown catalog.
   - Pass all release verifier quality gates.
5. **Release & Deployment**:
   - Open PR on `spelech/model-context-gateway`.
   - Verify GitHub Actions CI quality gates pass.
   - Merge to `main`, push new version tag, await Docker publishing, and deploy to `/containers/mcp`.
