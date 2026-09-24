# Test Bench Server & Tool Dropdown & Interactive Hints Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix Test Bench server/tool grouping by backend MCP servers across all naming delimiters (`/`, `__`, `:`) and enhance usability with interactive tool hints, enum dropdowns, and pre-fill example arguments.

**Architecture:** 
1. Centralized frontend utility (`parseNamespacedName`) parses tool/prompt names across `/`, `__`, and `:` delimiters, enabling dynamic server extraction, clean tool name display, and accurate filtering.
2. `ToolTesterCard` and `PromptTesterCard` display dedicated tool info callout banners (descriptions, server badges, parameter counts), render guided `<select>` dropdowns for enum parameters, and provide a "Pre-fill Example" action.
3. Backend `CapabilityEndpoints.cs` (`POST /api/test/call` and `POST /api/test/prompts/get`) extracts `serverId` across all delimiters as a fallback and strips namespace prefixes before forwarding to upstream servers.

**Tech Stack:** ASP.NET Core, C# 13, xUnit, React 18, TypeScript, Vitest, Playwright Layout Inspector.

## Global Constraints

- **MANDATORY TEST REQUIREMENT ANNOTATIONS RULE**:
  - C# xUnit tests must use `[Requirement("MCP-35", "MCP", RequirementType.Positive, "Description")]`.
  - Frontend Vitest tests must use JSDoc `@requirement UI-132`.
  - Requirement IDs must NEVER use `REQ-` prefixes.
  - Zero catalog drift: `dotnet run --project scripts/CatalogGenerator -- --verify-only` must pass.
- **NO RELEASE TAG / PUBLISH YET**:
  - User explicitly specified: *"once all ci passes we can merge, no release yet, I have another set of fixes in mind"*.
  - Do NOT create git release tags, do NOT trigger container build/publish workflows.
- **NO STRING-REPLACE FOR JSON**:
  - Use `JsonNode` / `JsonDocument` or structured parameters for JSON payload manipulation.

---

### Task 1: Backend Server Resolution & Prefix Stripping in Capability Endpoints (`MCP-35`)

**Files:**
- Modify: `Components/Capabilities/CapabilityEndpoints.cs:278-305, 748-763`
- Test: `ModelContextGateway.Tests/CapabilityEndpointsTests.cs` (or `ModelContextGateway.Tests/PipelineIntegrationTests.cs`)

**Interfaces:**
- Consumes: `POST /api/test/call` and `POST /api/test/prompts/get`
- Produces: Correctly resolved `serverId` and stripped `targetToolName` / `targetPromptName` forwarded to backend connections.

- [ ] **Step 1: Write the failing backend tests**

In `ModelContextGateway.Tests/CapabilityEndpointsTests.cs` (or create if not present):
```csharp
[Fact]
[Requirement("MCP-35", "MCP", RequirementType.Positive, "Test call endpoint resolves server and strips tool prefix across slash, dunder, and colon delimiters")]
public async Task TestCall_ResolvesServerAndStripsPrefix_AcrossDelimiters()
{
    // Verify slash format "mcp-arr-hd/arr_status" resolves server "mcp-arr-hd" and strips to "arr_status"
    // Verify dunder format "docker__ps" resolves server "docker" and strips to "ps"
    // Verify colon format "media:search" resolves server "media" and strips to "search"
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~CapabilityEndpointsTests"`
Expected: FAIL or compilation if helper not yet updated.

- [ ] **Step 3: Update `CapabilityEndpoints.cs`**

In `Components/Capabilities/CapabilityEndpoints.cs`:
Update `POST /api/test/call` (lines 278–305):
```csharp
if (string.IsNullOrEmpty(serverId) || serverId == "custom")
{
    var slashIdx = toolName.IndexOf('/');
    var dunderIdx = toolName.IndexOf("__");
    var colonIdx = toolName.IndexOf(':');

    if (slashIdx > 0)
    {
        serverId = toolName.Substring(0, slashIdx);
    }
    else if (dunderIdx > 0)
    {
        serverId = toolName.Substring(0, dunderIdx);
    }
    else if (colonIdx > 0)
    {
        serverId = toolName.Substring(0, colonIdx);
    }
}
```
And strip server prefix:
```csharp
var targetToolName = toolName;
if (!string.IsNullOrEmpty(serverId))
{
    if (targetToolName.StartsWith(serverId + "/", StringComparison.OrdinalIgnoreCase))
    {
        targetToolName = targetToolName.Substring(serverId.Length + 1);
    }
    else if (targetToolName.StartsWith(serverId + "__", StringComparison.OrdinalIgnoreCase))
    {
        targetToolName = targetToolName.Substring(serverId.Length + 2);
    }
    else if (targetToolName.StartsWith(serverId + ":", StringComparison.OrdinalIgnoreCase))
    {
        targetToolName = targetToolName.Substring(serverId.Length + 1);
    }
}
```
Apply the identical logic to `POST /api/test/prompts/get` (lines 748–763) for `serverId` and `targetPromptName`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~CapabilityEndpointsTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Components/Capabilities/CapabilityEndpoints.cs ModelContextGateway.Tests/CapabilityEndpointsTests.cs
git commit -m "fix(capabilities): resolve server and strip prefixes across all delimiters in test endpoints [MCP-35]"
```

---

### Task 2: Shared MCP Naming Utility and Unit Tests (`UI-132`)

**Files:**
- Create: `frontend/src/shared/utils/mcpNaming.ts`
- Create: `frontend/src/test/utils/mcpNaming.test.ts`

**Interfaces:**
- Produces: `parseNamespacedName(name: string, defaultServer?: string): ParsedMcpName`

- [ ] **Step 1: Write the failing unit tests**

In `frontend/src/test/utils/mcpNaming.test.ts`:
```typescript
import { describe, it, expect } from 'vitest';
import { parseNamespacedName } from '../../shared/utils/mcpNaming';

describe('mcpNaming utility', () => {
  /**
   * @requirement UI-132
   * @category UI
   * @type PositiveFeature
   * @description Correctly parses tool names with slash delimiter
   */
  it('parses tool names with slash delimiter', () => {
    const result = parseNamespacedName('mcp-arr-hd/arr_status');
    expect(result.serverId).toBe('mcp-arr-hd');
    expect(result.cleanName).toBe('arr_status');
    expect(result.isCustom).toBe(false);
  });

  /**
   * @requirement UI-132
   * @category UI
   * @type PositiveFeature
   * @description Correctly parses tool names with dunder delimiter
   */
  it('parses tool names with dunder delimiter', () => {
    const result = parseNamespacedName('docker__list_containers');
    expect(result.serverId).toBe('docker');
    expect(result.cleanName).toBe('list_containers');
    expect(result.isCustom).toBe(false);
  });

  /**
   * @requirement UI-132
   * @category UI
   * @type PositiveFeature
   * @description Correctly parses tool names with colon delimiter
   */
  it('parses tool names with colon delimiter', () => {
    const result = parseNamespacedName('media:search');
    expect(result.serverId).toBe('media');
    expect(result.cleanName).toBe('search');
    expect(result.isCustom).toBe(false);
  });

  /**
   * @requirement UI-132
   * @category UI
   * @type PositiveFeature
   * @description Handles un-namespaced custom tools
   */
  it('identifies un-namespaced custom tools', () => {
    const result = parseNamespacedName('native_search');
    expect(result.serverId).toBe('custom');
    expect(result.cleanName).toBe('native_search');
    expect(result.isCustom).toBe(true);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test src/test/utils/mcpNaming.test.ts` in `frontend/`
Expected: FAIL (module not found).

- [ ] **Step 3: Implement `frontend/src/shared/utils/mcpNaming.ts`**

```typescript
export interface ParsedMcpName {
  serverId: string;
  cleanName: string;
  isCustom: boolean;
}

export function parseNamespacedName(name: string, defaultServer = 'custom'): ParsedMcpName {
  if (!name) return { serverId: defaultServer, cleanName: '', isCustom: true };

  const slashIndex = name.indexOf('/');
  if (slashIndex > 0) {
    return {
      serverId: name.substring(0, slashIndex),
      cleanName: name.substring(slashIndex + 1),
      isCustom: false,
    };
  }

  const dunderIndex = name.indexOf('__');
  if (dunderIndex > 0) {
    return {
      serverId: name.substring(0, dunderIndex),
      cleanName: name.substring(dunderIndex + 2),
      isCustom: false,
    };
  }

  const colonIndex = name.indexOf(':');
  if (colonIndex > 0) {
    return {
      serverId: name.substring(0, colonIndex),
      cleanName: name.substring(colonIndex + 1),
      isCustom: false,
    };
  }

  return { serverId: defaultServer, cleanName: name, isCustom: true };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test src/test/utils/mcpNaming.test.ts` in `frontend/`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add frontend/src/shared/utils/mcpNaming.ts frontend/src/test/utils/mcpNaming.test.ts
git commit -m "feat(testbench): add shared parseNamespacedName utility [UI-132]"
```

---

### Task 3: Test Bench Tool & Prompt Tester Cards with Server Grouping & Interactive Hints (`UI-132`)

**Files:**
- Modify: `frontend/src/components/testbench/ToolTesterCard.tsx`
- Modify: `frontend/src/components/testbench/PromptTesterCard.tsx`
- Modify: `frontend/src/styles/components.css`
- Modify: `frontend/src/test/components/TestBenchView.test.tsx`

**Interfaces:**
- Consumes: `parseNamespacedName` from `frontend/src/shared/utils/mcpNaming`
- Produces: Enhanced Test Bench with backend server grouping, clean tool naming, selected tool hint banner, enum dropdowns, and pre-fill example arguments.

- [ ] **Step 1: Write the failing component tests**

In `frontend/src/test/components/TestBenchView.test.tsx`:
Add tests for:
- Server dropdown displaying distinct backend servers (`MCP-ARR-HD`, `DOCKER`) from slash-namespaced tools without grouping under `custom`.
- Selected tool displaying the Tool Hint banner with description and parameter badges.
- Enum parameters rendering as a `<select>` dropdown.
- "Pre-fill Example" button populating argument inputs.
Annotate with `@requirement UI-132`.

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test src/test/components/TestBenchView.test.tsx` in `frontend/`
Expected: FAIL

- [ ] **Step 3: Implement `ToolTesterCard.tsx`, `PromptTesterCard.tsx`, and CSS**

1. In `ToolTesterCard.tsx`:
   - Use `parseNamespacedName` to compute servers in `getToolServers()`.
   - Only include `'custom'` if `tools.some(t => parseNamespacedName(t.name).isCustom)`.
   - In `getFilteredTools()`, filter by `parseNamespacedName(t.name).serverId === selectedServer`.
   - In Tool `<select>`, display `parseNamespacedName(t.name).cleanName`.
   - Render `ToolHintBanner` when `currentTool` is selected:
     - Shows tool name, server badge, full `currentTool.description`, and parameter summary.
   - In `renderDynamicFields`:
     - If `prop.enum && Array.isArray(prop.enum)`, render `<select>` dropdown.
     - Show type badges (`string`, `integer`, `boolean`, `array`, `object`).
     - Show default/example placeholder hints.
   - Add "Pre-fill Example" button that generates starter argument values from `tool.inputSchema`.
2. In `PromptTesterCard.tsx`:
   - Apply same server parsing and hint callout banner for prompts.
3. In `frontend/src/styles/components.css`:
   - Add styling for `.tool-hint-banner`, `.tool-hint-header`, `.type-badge`, and `.prefill-btn`.

- [ ] **Step 4: Run component tests to verify they pass**

Run: `npm test src/test/components/TestBenchView.test.tsx` in `frontend/`
Expected: PASS

- [ ] **Step 5: Run full frontend test suite**

Run: `npm test` in `frontend/`
Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/components/testbench/ToolTesterCard.tsx frontend/src/components/testbench/PromptTesterCard.tsx frontend/src/styles/components.css frontend/src/test/components/TestBenchView.test.tsx
git commit -m "feat(testbench): add backend server grouping, tool hint banners, enum dropdowns and example pre-fill [UI-132]"
```

---

### Task 4: Quality Gates, SRS Requirements Catalog Verification, and PR Creation

**Files:**
- Modify: `docs/software-requirements-and-test-catalog.md`
- Modify: `docs/requirements-catalog.json`

- [ ] **Step 1: Regenerate and verify SRS Requirements Catalog**

Run:
```bash
dotnet run --project scripts/CatalogGenerator
dotnet run --project scripts/CatalogGenerator -- --verify-only
```
Expected: Zero drift.

- [ ] **Step 2: Run all backend tests**

Run:
```bash
dotnet test ModelContextGateway.slnx
```
Expected: 864+ tests pass.

- [ ] **Step 3: Run Playwright Layout Inspector suite**

Run:
```bash
cd frontend && npm run test:e2e -- e2e/layout-inspector.spec.ts
```
Expected: All 11 tests pass with zero layout overflow.

- [ ] **Step 4: Commit catalog updates**

```bash
git add docs/software-requirements-and-test-catalog.md docs/requirements-catalog.json
git commit -m "docs(catalog): update SRS requirements catalog for MCP-35 and UI-132"
```

- [ ] **Step 5: Push branch and create PR**

```bash
git push origin fix/testbench-server-tool-dropdown
gh pr create --title "fix: test bench backend server grouping, execution multi-delimiter stripping, and interactive tool hints [MCP-35, UI-132]" --body "..."
```

*(Reminder: Do NOT tag or cut a release per user instruction)*
