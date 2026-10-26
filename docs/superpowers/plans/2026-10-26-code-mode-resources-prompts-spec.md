# Code Mode Resources, Prompts, Downstream Resource Compaction & MCP `2026-07-28` Spec Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Enhance Model Context Gateway (MCG) Code Mode (`execute_script`) and Meta-Mode with live TypeScript `.d.ts` SDK resources, sandbox resource/prompt discovery APIs (`mcg.*`), Meta-Mode downstream resource compaction, built-in Code Mode prompts, and MCP `2026-07-28` protocol enhancements (`instructions`, `annotations`, `outputSchema`, `structuredContent`, and `completion/complete`).

**Design Spec:** `docs/superpowers/specs/2026-10-26-code-mode-resources-prompts-spec-design.md`

**Tech Stack:** C# .NET 10, ASP.NET Core, Jint ES2023 Engine, xUnit, FluentAssertions, Moq.

---

### Task 1: TypeScript Declaration Generator & Meta-Tool Spec Enhancements (`MCP-52`, `MCP-55`)

**Files:**
- Create: `Core/CodeMode/CodeModeTypeGenerator.cs`
- Modify: `Core/Routing/ToolRoutingManager.cs`
- Modify: `Core/Routing/ToolRoutingManager.Execution.cs`
- Test: `ModelContextGateway.Tests/CodeModeTypeGeneratorTests.cs`

- [x] **Step 1:** Implement `CodeModeTypeGenerator` (`GenerateFullSdkDeclarations`, `GenerateServerDeclarations`, `GenerateCompactToolSignatures`, `GenerateSingleToolSignature`, `GenerateCodeModeGuideMarkdown`) converting JSON Schema types, required/optional properties, enums, arrays, nested objects, and JSDoc descriptions into valid TypeScript ambient declarations.
- [x] **Step 2:** Update `ToolRoutingManager.GetMetaModeTools` with MCP `2026-07-28` `annotations` (`readOnlyHint`, `destructiveHint`, `idempotentHint`, `openWorldHint`) and `outputSchema` on `search_tools`, `execute_tool`, and `execute_script`, plus `format` (`"json" | "typescript"`) and `include_typescript` parameters on `search_tools`.
- [x] **Step 3:** Update `ToolRoutingManager.Execution.cs` to emit `structuredContent` on `search_tools` and `execute_script` and return compact TypeScript signatures when `format == "typescript"`.
- [x] **Step 4:** Add unit tests in `ModelContextGateway.Tests/CodeModeTypeGeneratorTests.cs` and verify all pass.

---

### Task 2: Code Mode Sandbox Discovery & Resource/Prompt APIs (`MCP-53`)

**Files:**
- Create: `Core/CodeMode/CodeModeSandboxCallbacks.cs`
- Modify: `Core/CodeMode/ICodeModeExecutionEngine.cs`
- Modify: `Core/CodeMode/CodeModeExecutionEngine.cs`
- Modify: `Core/Routing/ToolRoutingManager.Execution.cs`
- Modify: `Core/Routing/ClientSession.cs`
- Test: `ModelContextGateway.Tests/CodeModeRuntimeInteropTests.cs`

- [x] **Step 1:** Define `CodeModeSandboxCallbacks` holding async delegates for `ListServersAsync`, `ListResourcesAsync`, `SearchResourcesAsync`, `ReadResourceAsync`, `ListPromptsAsync`, and `GetPromptAsync`.
- [x] **Step 2:** Bind `mcg.listServers()`, `mcg.listResources(serverId?)`, `mcg.searchResources(query)`, `mcg.readResource(uri)` (with automatic JSON/text unboxing), `mcg.listPrompts(serverId?)`, and `mcg.getPrompt(name, args?)` inside `CodeModeExecutionEngine.ExecuteAsync`, enforcing `CodeModeMaxToolCalls` across all outbound sandbox operations.
- [x] **Step 3:** Wire session-scoped callbacks in `ClientSession.cs` and `ToolRoutingManager.Execution.cs` so all sandbox operations enforce caller RBAC policies.
- [x] **Step 4:** Add integration tests in `ModelContextGateway.Tests/CodeModeRuntimeInteropTests.cs` verifying sandbox discovery, resource unboxing, and call quota enforcement.

---

### Task 3: Meta-Mode Resource Compaction, Code Mode Resources & Prompts (`MCP-52`, `MCP-54`)

**Files:**
- Modify: `Core/Routing/ResourceCatalogManager.cs`
- Modify: `Core/Routing/ResourceRoutingManager.cs`
- Modify: `Core/Routing/PromptRoutingManager.cs`
- Modify: `Core/Routing/ClientSession/ClientSession.Authorization.cs`
- Test: `ModelContextGateway.Tests/CodeModeResourcesAndPromptsTests.cs`

- [x] **Step 1:** Extend `ResourceCatalogManager` with URI matching in `SearchResourcesAsync` and template matchers `TryMatchCodeModeTypesTemplate` (`router://code-mode/types/{server_name}`) and `TryMatchServerResourcesTemplate` (`router://resources/server/{server_name}`).
- [x] **Step 2:** Update `ResourceRoutingManager.GetResourcesListResultAsync` so Meta-Mode sessions return compacted gateway resources (`router://code-mode/guide`, `router://code-mode/sdk.d.ts`, `router://resources/catalog`, `router://prompts/catalog`, and per-server `router://resources/server/{id}` + `router://code-mode/types/{id}`) instead of flooding `resources/list` with raw downstream resources.
- [x] **Step 3:** Update `ResourceRoutingManager.ReadResourceAsync` to serve `router://code-mode/guide`, `router://code-mode/sdk.d.ts`, `router://code-mode/types/{server_name}`, `router://resources/catalog`, `router://prompts/catalog`, and `router://resources/server/{server_name}` while preserving direct `resources/read` access to downstream `mcp://` URIs.
- [x] **Step 4:** Add `router__code_mode_guide` and `router__write_code_mode_script` prompts in `PromptRoutingManager.cs` and enforce server-level RBAC in `ClientSession.Authorization.cs`.

---

### Task 4: MCP `2026-07-28` Initialize Instructions & Autocomplete (`completion/complete`) (`MCP-55`)

**Files:**
- Modify: `Core/GatewayMetadata.cs`
- Modify: `Components/Capabilities/ProxyEndpoints.cs`
- Modify: `Core/Routing/ClientSession.cs`
- Modify: `Core/Routing/ClientSession/ClientSession.JsonRpcRewriter.cs`
- Test: `ModelContextGateway.Tests/CodeModeResourcesAndPromptsTests.cs`

- [x] **Step 1:** Add `GatewayMetadata.BuildGatewayInstructions(bool enableCodeMode)` and include `instructions` and `completions = new { }` in `InitializeResult` (`ProxyEndpoints.cs`).
- [x] **Step 2:** Update `ClientSession.CompleteAsync` to resolve completions for `router__*` prompt arguments, `router://code-mode/types/{server_name}` and `router://resources/server/{server_name}` resource templates, and URL-encoded `mcp://{alias}/{encoded_uri}` downstream resource templates.
- [x] **Step 3:** Update `ClientSession.JsonRpcRewriter.RewriteObject` so `params.ref.name` and `params.ref.uri` are rewritten for downstream `completion/complete` forwarding.
- [x] **Step 4:** Add end-to-end tests in `ModelContextGateway.Tests/CodeModeResourcesAndPromptsTests.cs`.

---

### Task 5: Documentation, Version Bump (`5.24.0`), and SRS Catalog Sync

**Files:**
- Modify: `docs/architecture/code-mode.md`
- Modify: `docs/user-guide/code-mode.md`
- Modify: `ModelContextGateway.csproj`
- Modify: `frontend/src/shared/stores/useUserStore.ts`
- Modify: `CHANGELOG.md`
- Modify: `README.md`
- Modify: `docs/software-requirements-and-test-catalog.md`
- Modify: `docs/requirements-catalog.json`

- [x] **Step 1:** Document Code Mode resources, prompts, sandbox discovery APIs, Meta-Mode resource compaction, and MCP `2026-07-28` enhancements in `docs/architecture/code-mode.md` and `docs/user-guide/code-mode.md`.
- [x] **Step 2:** Bump version from `5.23.0` to `5.24.0` across `ModelContextGateway.csproj`, `frontend/src/shared/stores/useUserStore.ts`, `CHANGELOG.md`, and `README.md`.
- [x] **Step 3:** Regenerate and verify the SRS catalog via `dotnet run --project scripts/CatalogGenerator -- --verify-only`.
