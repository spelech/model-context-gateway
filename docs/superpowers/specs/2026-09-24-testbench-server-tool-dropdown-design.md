# Test Bench Server & Tool Dropdown & Interactive Hints Design Specification

## Overview
This specification details the architecture, component changes, and API refinements required to resolve the Test Bench server/tool grouping limitations and add interactive tool guidance in Model Context Gateway (MCG).

### Problems Solved
1. **Server Dropdown Grouping Defect**: The Test Bench server selector hardcoded a single delimiter split on `__`. Because MCG names exposed tools using standard slash notation (`${namespace}/${toolName}`, e.g. `mcp-arr-hd/arr_status`), all backend tools collapsed into `"custom"`, showing only `"Native C# Registry (custom)"` in the dropdown.
2. **Backend Execution Defect**: `POST /api/test/call` and `POST /api/test/prompts/get` only extracted `serverId` from `toolName` when it contained `__`. If a tool or prompt name contained `/` or `:`, the backend failed to resolve the server (returning HTTP 404). Furthermore, stripping the prefix before forwarding to the upstream backend server only checked `toolName.StartsWith(serverId + "__")`, sending namespaced names (like `mcp-arr-hd/arr_status`) to upstream servers that only accept raw tool names (`arr_status`), causing upstream tool errors.
3. **Lack of Tool Context & Guidance**: The Test Bench did not surface tool descriptions, parameter summaries, type badges, or schema enums. Users had to guess what tools did and manually formulate argument payloads without guidance.

---

## Architectural Design

### 1. Frontend: Shared MCP Naming Utility
Create a centralized utility in `frontend/src/shared/utils/mcpNaming.ts` to standardize name parsing across tools and prompts:

```typescript
export interface ParsedMcpName {
  serverId: string;
  cleanName: string;
  isCustom: boolean;
}

/**
 * Parses a tool or prompt name into its serverId and raw clean name.
 * Supports '/', '__', and ':' delimiters.
 * If no delimiter is found, returns isCustom = true and serverId = defaultServer.
 */
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

### 2. Frontend: ToolTesterCard Enhancements
- **Server Discovery (`getToolServers`)**:
  - Uses `parseNamespacedName(t.name)`.
  - Collects all distinct server IDs and sorts them alphabetically.
  - Adds `'custom'` ONLY if at least one tool is genuinely un-namespaced (`isCustom === true`).
  - Formats server options as:
    - Custom: `Native C# Registry (custom)`
    - Backend: `${srv.toUpperCase()} Server` (or `${srv}`)
- **Tool Filtering (`getFilteredTools`)**:
  - Filters tools by checking whether `parseNamespacedName(t.name).serverId === selectedServer`.
  - In the Tool dropdown `<select>`, displays `cleanName` (e.g., `arr_status`), with the full namespaced name as the option value.
- **Selected Tool Hint Card (`ToolHintBanner`)**:
  - Positioned directly below the Server & Tool selectors when a tool is selected.
  - Displays:
    - **Header**: Tool clean name, Server tag badge (`[SERVER-ID]`), and required parameter count badge.
    - **Description**: Full tool description from `currentTool.description`.
    - **Parameter Summary**: Lists required vs optional parameters with their types.
- **Smart Form Field Controls (`renderDynamicFields`)**:
  - **Enum Dropdown**: If `prop.enum` (array of allowed values) is defined, renders a `<select>` dropdown with an empty placeholder option plus all allowed enum values.
  - **Type & Requirement Badges**: Displays type badge (`string`, `integer`, `boolean`, `array`, `object`) and red required marker.
  - **Descriptions & Placeholders**: Shows property description and uses `prop.default` or `prop.examples` as input placeholder.
- **"Pre-fill Example" Quick Action**:
  - A button next to "Interactive Form / Raw JSON" tabs.
  - When clicked, creates a sample argument dictionary based on `inputSchema`:
    - Enum properties: first enum value.
    - Booleans: `false` or default.
    - Integers/Numbers: `0` or default/minimum.
    - Strings: property default, first example, or `"<key_value>"`.
    - Arrays: `[]` or parsed example.
    - Objects: `{}` or parsed example.
  - Populates both `toolArguments` and `rawToolJson`.

### 3. Frontend: PromptTesterCard Matching Improvements
- Uses `parseNamespacedName(p.name, 'router')`.
- Displays distinct server IDs (including `router` for built-in prompts).
- Displays clean prompt names in the dropdown.
- Displays prompt description and parameter hint card when selected.

### 4. Backend: Robust Execution in `CapabilityEndpoints.cs`
In both `POST /api/test/call` and `POST /api/test/prompts/get`:

#### A. Server Resolution Fallback:
If `string.IsNullOrEmpty(serverId) || serverId == "custom"`:
- Extract server prefix using `/`, `__`, or `:`:
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

#### B. Tool / Prompt Name Stripping:
Ensure that `targetToolName` passed to the upstream backend server strips any server prefix regardless of delimiter:
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

Apply the identical logic to `POST /api/test/prompts/get` for `targetPromptName`.

---

## Testing & Quality Gates

1. **Unit Tests (Frontend)**:
   - `frontend/src/test/utils/mcpNaming.test.ts`: Verify `parseNamespacedName` correctly handles `/`, `__`, `:`, and un-namespaced tools/prompts.
   - `frontend/src/test/components/TestBenchView.test.tsx`:
     - Test that server dropdown populates with individual servers from slash-namespaced tools.
     - Test that selecting a server filters tools to that server with clean display names.
     - Test that tool hint card renders with description and parameter badges.
     - Test that clicking "Pre-fill Example" populates argument inputs.
     - Test enum fields render as `<select>` dropdowns.
   - Annotate all frontend tests with `@requirement UI-132`.
2. **Integration Tests (Backend C#)**:
   - `ModelContextGateway.Tests/CapabilityEndpointsTests.cs` (or `PipelineIntegrationTests.cs`):
     - Test `POST /api/test/call` with slash-namespaced `toolName: "mcp-arr-hd/arr_status"` and `serverId: "custom"` resolves server and strips prefix.
     - Test `POST /api/test/call` with explicit `serverId: "mcp-arr-hd"` and `toolName: "mcp-arr-hd/arr_status"` strips prefix.
     - Test `POST /api/test/prompts/get` with slash and dunder names.
     - Annotate all C# tests with `[Requirement("MCP-35", "MCP", RequirementType.Positive, "...")]`.
3. **E2E / Regression Verification**:
   - `frontend/e2e/testbench.spec.ts` (or `layout-inspector.spec.ts`): Verify Test Bench displays server list and tool hint card without layout overflow.
   - Zero catalog drift: `dotnet run --project scripts/CatalogGenerator -- --verify-only`.
