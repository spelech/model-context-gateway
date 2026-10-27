# Code Mode Resources, Prompts, Downstream Resource Compaction & MCP `2026-07-28` Spec Enhancements — Design Specification

- **Date:** 2026-10-26
- **Target Version:** `5.24.0`
- **Branch:** `feat/code-mode-resources-prompts-spec`
- **Status:** Implemented & Verified

---

## 1. Problem Statement & Motivation

Model Context Gateway (MCG) introduced `execute_script` (Code Mode) in v5.23.0, allowing AI agents to orchestrate multi-tool workflows in an isolated Jint ES2023 JavaScript sandbox (`await mcg.callTool(...)` and `await servers.<alias>.<tool>(...)`). However, four practical usability and protocol gaps remained:

1. **Lack of TypeScript Type Declarations & Usage Guide for Code Mode:**
   - Agents writing `execute_script` code had to guess parameter shapes or parse verbose JSON Schemas from `search_tools`.
   - Cloudflare's Code Mode pattern demonstrates that LLMs write significantly more accurate scripts when given compact TypeScript `.d.ts` declarations (`interface Server_<id>_Tools`, `declare const mcg`, `declare const servers`).
2. **No Downstream Resource or Prompt Access Inside `execute_script`:**
   - Scripts could call tools via `mcg.callTool()`, but could not list, search, or read MCP resources (`resources/read`) or prompts (`prompts/get`), nor inspect connected servers (`mcg.listServers()`).
3. **Downstream Resource Flood in Meta-Mode (`resources/list`):**
   - While Meta-Mode compacted `tools/list` down to 3 meta-tools (`search_tools`, `execute_tool`, `execute_script`), `resources/list` still returned the uncompacted union of every downstream server's resources.
   - In homelab/enterprise setups with many servers, this floods `resources/list` and obscures gateway-level discovery resources.
4. **MCP `2026-07-28` Specification Enhancements & `completion/complete` Gaps:**
   - `initialize` did not return top-level `instructions` or advertise `capabilities.completions = {}`.
   - Meta-tools lacked MCP `2026-07-28` `annotations` (`readOnlyHint`, `destructiveHint`, `idempotentHint`, `openWorldHint`), `outputSchema`, and `structuredContent`.
   - `completion/complete` did not autocomplete built-in `router__*` prompt arguments, `router://` resource template parameters (`{server_name}`), or URL-encoded `mcp://{alias}/{encoded_uri}` downstream resource templates.

---

## 2. Architecture & Design

### 2.1 Code Mode TypeScript SDK Generator (`CodeModeTypeGenerator`)

`Core/CodeMode/CodeModeTypeGenerator.cs` converts cached MCP JSON Schemas (`inputSchema`) and server metadata into strongly-typed TypeScript `.d.ts` declarations:

- **`GenerateFullSdkDeclarations(servers, toolsByServer, includeRbacFiltered)`**:
  - Emits ambient declarations for `console`, `mcg` (`callTool`, `parallel`, `listServers`, `listResources`, `searchResources`, `readResource`, `listPrompts`, `getPrompt`), and per-server `interface Server_<canonicalId>_Tools` with JSDoc comments and typed parameter interfaces.
  - Binds both canonical server IDs and configured aliases under `declare const servers: { readonly <id>: Server_<id>_Tools; readonly <alias>: Server_<id>_Tools; }`.
- **`GenerateServerDeclarations(server, tools)`**:
  - Emits focused `.d.ts` declarations for a single downstream server (`router://code-mode/types/{server_name}`).
- **`GenerateCompactToolSignatures(tools)` & `GenerateSingleToolSignature(tool)`**:
  - Supports `search_tools(query, format: "typescript")` and `search_tools(query, include_typescript: true)` so agents can retrieve compact TypeScript signatures directly from semantic search.
- **`GenerateCodeModeGuideMarkdown(serverIds)`**:
  - Generates the `router://code-mode/guide` Markdown reference covering sandbox globals, resource/prompt access, parallel execution, and error handling.

### 2.2 Sandbox Discovery & Resource/Prompt APIs (`CodeModeSandboxCallbacks`)

`Core/CodeMode/CodeModeSandboxCallbacks.cs` and `CodeModeExecutionEngine.cs` extend the `mcg` global object inside `execute_script`:

| Sandbox API | Signature | Behavior & Security |
| :--- | :--- | :--- |
| `mcg.listServers()` | `() => Promise<Array<{ id, alias, name, category }>>` | Lists enabled servers authorized for the caller session. |
| `mcg.listResources(serverId?)` | `(serverId?: string) => Promise<Array<ResourceSummary>>` | Lists authorized downstream resources (optionally filtered by server ID or alias). Counts toward `CodeModeMaxToolCalls`. |
| `mcg.searchResources(query)` | `(query: string) => Promise<Array<ResourceSummary>>` | Keyword/URI search across authorized downstream resources. Counts toward `CodeModeMaxToolCalls`. |
| `mcg.readResource(uri)` | `(uri: string) => Promise<any>` | Reads any `router://` or `mcp://` resource with RBAC enforcement. Automatically unboxes `contents[0].text` (and parses JSON if valid) for ergonomic scripting. Counts toward `CodeModeMaxToolCalls`. |
| `mcg.listPrompts(serverId?)` | `(serverId?: string) => Promise<Array<PromptSummary>>` | Lists authorized prompts (optionally filtered by server ID or alias). Counts toward `CodeModeMaxToolCalls`. |
| `mcg.getPrompt(name, args?)` | `(name: string, args?: Record<string, string>) => Promise<any>` | Retrieves and renders a prompt (`router__*` or `{server}__{prompt}`) with RBAC enforcement. Counts toward `CodeModeMaxToolCalls`. |

### 2.3 Meta-Mode Resource Compaction (Option B)

When a client connects in **Meta-Mode** (`IsMetaMode == true`), `ResourceRoutingManager.GetResourcesListResultAsync` compacts `resources/list` so downstream server resources do not flood the client context:

1. **Core Gateway Discovery & Code Mode Resources:**
   - `router://code-mode/guide` (`text/markdown`) — Complete Code Mode reference & patterns.
   - `router://code-mode/sdk.d.ts` (`application/typescript`) — Full TypeScript ambient declarations for all authorized servers/tools.
   - `router://catalog/categories` (`application/json`) — Server category taxonomy.
   - `router://resources/catalog` (`application/json`) — Compact index of all authorized downstream resources and templates across servers.
   - `router://prompts/catalog` (`application/json`) — Compact index of all authorized downstream prompts.
2. **Per-Server Summary & Type Resources (RBAC-Filtered):**
   - `router://resources/server/{serverId}` (`application/json`) — Lists all resources and resource templates for `{serverId}`.
   - `router://code-mode/types/{serverId}` (`application/typescript`) — TypeScript `.d.ts` declarations for `{serverId}`.
3. **Resource Templates (`resources/templates/list`):**
   - Advertises `router://code-mode/types/{server_name}` and `router://resources/server/{server_name}` alongside RBAC-filtered downstream `mcp://{prefix}/{uri}` templates.
4. **Full Readability (`resources/read`):**
   - Any downstream `mcp://{server}/{uri}` resource remains directly readable via `resources/read` or `mcg.readResource(uri)` in Meta-Mode even though it is compacted out of the top-level `resources/list` response.

### 2.4 Built-In Code Mode Prompts (`prompts/list` & `prompts/get`)

`PromptRoutingManager` exposes two new built-in prompts when Code Mode is enabled:

- **`router__code_mode_guide`**:
  - Optional argument: `server_filter` (autocompleted via `completion/complete`).
  - Returns a structured system/user message pair containing the Code Mode guide and live TypeScript declarations for the requested server(s).
- **`router__write_code_mode_script`**:
  - Arguments: `task` (required), `servers` (optional comma-separated server IDs/aliases).
  - Synthesizes a ready-to-run prompt instructing the LLM to write an `execute_script` snippet using the embedded `.d.ts` definitions for the target servers.

### 2.5 MCP `2026-07-28` Specification Enhancements

1. **`initialize` Instructions & Capabilities (`GatewayMetadata` & `ProxyEndpoints`):**
   - `initialize` and `server/discover` include top-level `instructions` (generated by `GatewayMetadata.BuildGatewayInstructions(enableCodeMode)`) explaining Meta-Mode workflow (`search_tools` -> `execute_tool` / `execute_script`, `router://code-mode/sdk.d.ts`, `router://resources/catalog`).
   - Advertises `capabilities.completions = {}` alongside `tools`, `resources`, `prompts`, and `logging`.
2. **Tool Annotations, `outputSchema`, and `structuredContent` (`ToolRoutingManager`):**
   - `search_tools`: `readOnlyHint = true`, `destructiveHint = false`, `idempotentHint = true`, `openWorldHint = false`, plus structured JSON `outputSchema` and `structuredContent = new { tools, totalCount, format }`.
   - `execute_tool`: `readOnlyHint = false`, `destructiveHint = true`, `idempotentHint = false`, `openWorldHint = true`.
   - `execute_script`: `readOnlyHint = false`, `destructiveHint = true`, `idempotentHint = false`, `openWorldHint = true`, plus `outputSchema` and `structuredContent` matching `CodeModeExecutionResult`.
3. **Autocomplete (`completion/complete`) Fixes (`ClientSession` & `ClientSession.JsonRpcRewriter`):**
   - Handles local `ref/prompt` completions for `router__*` prompts (`server_filter`, `servers`, `category`, `server_name`) by matching authorized server IDs, aliases, and categories.
   - Handles local `ref/resource` completions for `router://code-mode/types/{server_name}` and `router://resources/server/{server_name}`.
   - Unescapes URL-encoded downstream `mcp://{prefix}/{encoded_uri}` resource references, resolves server aliases to canonical IDs, and rewrites `params.ref.uri` (`ClientSession.JsonRpcRewriter.cs`) before forwarding `completion/complete` to downstream MCP servers.
4. **Downstream Server `instructions` Capture & 4-Surface Propagation (`MCP-56`):**
   - `ClientSession.BackendInitializer.cs` captures `result.instructions` from each downstream server's `initialize` response and caches it in `SessionManager` (`_serverInstructionsCache`).
   - **Surface 1 (Direct Proxy `initialize`):** When a client connects in direct proxy mode (`!IsMetaMode`), `ResolveSessionInstructionsAsync` returns the downstream server's `instructions` directly in `InitializeResult`.
   - **Surface 2 (Per-Server Resources & Sandbox `mcg.listServers()`):** `router://resources/server/{serverId}`, `router://active-servers`, Meta-Mode `resources/list` descriptions, and `await mcg.listServers()` inside `execute_script` include the server's `instructions`.
   - **Surface 3 (Code Mode TypeScript `.d.ts` Declarations & Prompts):** `CodeModeTypeGenerator` embeds `Server Instructions: ...` in the JSDoc block above `interface Server_<id>_Tools` in `router://code-mode/sdk.d.ts`, `router://code-mode/types/{serverId}`, and built-in Code Mode prompts.
   - **Surface 4 (Meta-Mode `initialize` Rollup):** `GatewayMetadata.BuildGatewayInstructions(enableCodeMode, downstreamInstructions)` appends a `Downstream Server Instructions:` section rolling up cached instructions across active servers.

---

## 3. Requirements Traceability

| Requirement ID | Category | Description | Verification Test Suite |
| :--- | :--- | :--- | :--- |
| `MCP-52` | `MCP` | CodeModeTypeGenerator produces valid TypeScript `.d.ts` declarations and compact signatures from MCP tool JSON schemas | `CodeModeTypeGeneratorTests`, `CodeModeResourcesAndPromptsTests` |
| `MCP-53` | `MCP` | Code Mode sandbox exposes `mcg.listServers`, `mcg.listResources`, `mcg.searchResources`, `mcg.readResource`, `mcg.listPrompts`, and `mcg.getPrompt` with RBAC and quota enforcement | `CodeModeRuntimeInteropTests`, `CodeModeResourcesAndPromptsTests` |
| `MCP-54` | `MCP` | Meta-Mode compacts `resources/list` into gateway discovery/catalog resources while preserving full `resources/read` access and RBAC filtering | `CodeModeResourcesAndPromptsTests` |
| `MCP-55` | `MCP` | Gateway advertises MCP `2026-07-28` `instructions`, `completions` capability, meta-tool `annotations`/`outputSchema`/`structuredContent`, and resolves `completion/complete` for `router__*` and `mcp://` refs | `CodeModeResourcesAndPromptsTests` |
| `MCP-56` | `MCP` | Downstream server `initialize` instructions are cached and surfaced across direct proxy `initialize`, per-server resources/`mcg.listServers()`, TypeScript `.d.ts` JSDoc headers, and Meta-Mode `initialize` rollup | `CodeModeResourcesAndPromptsTests` |
