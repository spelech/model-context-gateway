# Server Namespace Aliasing, Slash Tool Formatting & Collision Guard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Standardize exposed tool names on forward-slash format (`{namespace}/{tool_name}`), introduce server-level namespace aliasing (`Alias`), support multi-delimiter interchangeable routing (`/`, `:`, `__`), prevent bare-name collisions across duplicate servers, and provide management parity across Docker discovery, REST APIs, `mcg-admin`, and the React 19 frontend.

**Architecture:** Add nullable `Alias` column to `Servers` database table. Update `McpServer` entity, validation helpers, and Docker label parser. In `ToolRoutingManager`, expose `{namespace}/{tool_name}` where namespace defaults to `Alias ?? Id`. Register dual keys in the routing table (both alias and server ID across `/`, `:`, and `__`). In `execute_tool`, normalize delimiters and resolve targets; if bare tool calls encounter duplicate tool names across multiple servers, return an explicit ambiguity error listing valid namespaced choices. Update Admin MCP server and React frontend for alias management.

**Tech Stack:** .NET 10 Minimal APIs, C#, Dapper, SQLite/PostgreSQL/SQL Server, React 19, TypeScript 5.7, Zustand 5, Vite 8, Vitest, xUnit.

## Global Constraints

- Expose tools across `tools/list` and `search_tools` using `{namespace}/{tool_name}` format by default, with `[{namespace}]` prepended to descriptions.
- Support `/`, `:`, and `__` interchangeably upon invocation in `execute_tool` and `tools/call`.
- Enforce alias format: alphanumeric plus underscores and hyphens (`^[a-zA-Z0-9_-]*$`).
- Enforce uniqueness: an alias cannot collide with another server's `Id` or another server's `Alias`.
- Protect against bare tool call collisions across duplicate backend servers.
- Preserve backward compatibility for existing callers using `serverId` or `__`.
- All tests must pass: `dotnet test` (741+ tests) and `pnpm --dir frontend test`.
- All requirements must be cataloged: `MCP-27`, `MCP-28`, `MCP-29`, `MCP-30`, `MCP-31`, `MCP-ADMIN-PARITY-SERVER-ALIAS`, `UI-SERVERS-ALIAS-MANAGEMENT`.

---

### Task 1: Database Migration & McpServer Domain Model

**Files:**
- Modify: `Components/Servers/McpServer.cs`
- Modify: `Infrastructure/Persistence/DatabaseInitializer.cs`
- Test: `ModelContextGateway.Tests/McpServerTests.cs`

**Interfaces:**
- Produces: `McpServer.Alias` property (`string?`), database column `Alias` in `Servers` table.

- [ ] **Step 1: Write the failing unit test for McpServer.Alias persistence**

Add test in `ModelContextGateway.Tests/McpServerTests.cs`:
```csharp
[Fact]
public void McpServer_Supports_Alias_Property()
{
    var server = new McpServer
    {
        Id = "postgres-mcp-homebox",
        Alias = "homebox_db",
        DisplayName = "Homebox DB",
        Url = "http://postgres-mcp-homebox:8000/sse"
    };

    Assert.Equal("homebox_db", server.Alias);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ModelContextGateway.Tests/ModelContextGateway.Tests.csproj --filter "FullyQualifiedName~McpServer_Supports_Alias_Property"`
Expected: Compilation failure or test failure (property `Alias` not found).

- [ ] **Step 3: Implement McpServer.Alias and DatabaseInitializer migration**

In `Components/Servers/McpServer.cs`:
```csharp
public string? Alias { get; set; }
```

In `Infrastructure/Persistence/DatabaseInitializer.cs`, ensure the column check/add runs for SQLite/Postgres/SQL Server:
```csharp
try
{
    conn.Execute("ALTER TABLE Servers ADD COLUMN Alias TEXT NULL;");
}
catch
{
    // Column already exists
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ModelContextGateway.Tests/ModelContextGateway.Tests.csproj --filter "FullyQualifiedName~McpServer_Supports_Alias_Property"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Components/Servers/McpServer.cs Infrastructure/Persistence/DatabaseInitializer.cs ModelContextGateway.Tests/McpServerTests.cs
git commit -m "feat(servers): add Alias property to McpServer and database migration"
```

---

### Task 2: Server Endpoints Validation & Collision Prevention (`MCP-29`)

**Files:**
- Modify: `Components/Servers/ServerValidationHelper.cs`
- Modify: `Components/Servers/ServerEndpoints.cs`
- Test: `ModelContextGateway.Tests/ServerEndpointsValidationTests.cs`

**Interfaces:**
- Consumes: `McpServer.Alias`
- Produces: Validation rejecting invalid alias characters and preventing alias collisions against other servers' IDs or aliases.

- [ ] **Step 1: Write the failing tests for ServerValidationHelper alias validation (`MCP-29`)**

In `ModelContextGateway.Tests/ServerEndpointsValidationTests.cs`:
```csharp
[Fact]
public void ValidateServer_Rejects_Invalid_Alias_Characters()
{
    // MCP-29: Rejects invalid characters in Alias
    var server = new McpServer
    {
        Id = "test-server",
        Alias = "invalid alias!@#",
        Url = "http://localhost:8000/sse"
    };

    var error = ServerValidationHelper.ValidateAlias(server.Alias, server.Id, new List<McpServer>());
    Assert.NotNull(error);
    Assert.Contains("letters, numbers, underscores, and hyphens", error);
}

[Fact]
public void ValidateServer_Rejects_Alias_Colliding_With_Existing_ServerId()
{
    // MCP-29: Rejects Alias colliding with another server's Id
    var existing = new List<McpServer>
    {
        new McpServer { Id = "docker", DisplayName = "Docker" }
    };

    var error = ServerValidationHelper.ValidateAlias("docker", "other-server", existing);
    Assert.NotNull(error);
    Assert.Contains("collides with an existing server ID", error);
}

[Fact]
public void ValidateServer_Rejects_Alias_Colliding_With_Existing_Server_Alias()
{
    // MCP-29: Rejects Alias colliding with another server's Alias
    var existing = new List<McpServer>
    {
        new McpServer { Id = "db1", Alias = "shared_db" }
    };

    var error = ServerValidationHelper.ValidateAlias("shared_db", "db2", existing);
    Assert.NotNull(error);
    Assert.Contains("already in use by another server", error);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ModelContextGateway.Tests/ModelContextGateway.Tests.csproj --filter "FullyQualifiedName~ValidateServer_Rejects"`
Expected: FAIL (ValidateAlias does not exist).

- [ ] **Step 3: Implement ValidateAlias and update ServerEndpoints.cs**

In `Components/Servers/ServerValidationHelper.cs`:
```csharp
public static string? ValidateAlias(string? alias, string serverId, IEnumerable<McpServer> existingServers)
{
    if (string.IsNullOrWhiteSpace(alias))
    {
        return null;
    }

    var trimmed = alias.Trim();
    if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, "^[a-zA-Z0-9_-]+$"))
    {
        return "Server Alias may only contain letters, numbers, underscores, and hyphens.";
    }

    if (existingServers.Any(s => !string.Equals(s.Id, serverId, StringComparison.OrdinalIgnoreCase) &&
                                 string.Equals(s.Id, trimmed, StringComparison.OrdinalIgnoreCase)))
    {
        return $"Server Alias '{trimmed}' collides with an existing server ID.";
    }

    if (existingServers.Any(s => !string.Equals(s.Id, serverId, StringComparison.OrdinalIgnoreCase) &&
                                 string.Equals(s.Alias, trimmed, StringComparison.OrdinalIgnoreCase)))
    {
        return $"Server Alias '{trimmed}' is already in use by another server.";
    }

    return null;
}
```

In `Components/Servers/ServerEndpoints.cs`:
Update SQL SELECT, INSERT, and UPDATE queries to include `Alias`. Call `ValidateAlias` on POST and PUT.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test ModelContextGateway.Tests/ModelContextGateway.Tests.csproj --filter "FullyQualifiedName~ValidateServer_Rejects"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Components/Servers/ServerValidationHelper.cs Components/Servers/ServerEndpoints.cs ModelContextGateway.Tests/ServerEndpointsValidationTests.cs
git commit -m "feat(servers): implement alias validation and collision guardrail (MCP-29)"
```

---

### Task 3: Docker Auto-Discovery `mcp.alias` Ingestion (`MCP-31`)

**Files:**
- Modify: `Components/Servers/DockerAutoDiscoveryService.cs`
- Test: `ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs`

**Interfaces:**
- Consumes: Container label `mcp.alias` or `mcp.namespace`.
- Produces: Populated `McpServer.Alias` during Docker auto-discovery and preservation during database upsert.

- [ ] **Step 1: Write the failing tests for Docker auto-discovery alias parsing (`MCP-31`)**

In `ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs`:
```csharp
[Fact]
public void ParseDiscoveredServers_Parses_McpAlias_Label()
{
    // MCP-31: Parses mcp.alias from Docker container labels
    var json = @"
    [
      {
        ""Names"": [""/postgres-mcp-homebox""],
        ""Labels"": {
          ""mcp.enabled"": ""true"",
          ""mcp.id"": ""postgres-mcp-homebox"",
          ""mcp.port"": ""8000"",
          ""mcp.alias"": ""homebox_db""
        }
      }
    ]";

    using var doc = System.Text.Json.JsonDocument.Parse(json);
    var servers = DockerAutoDiscoveryService.ParseDiscoveredServers(doc.RootElement, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, new[] { "10.0.0.0/8", "127.0.0.0/8" });

    Assert.Single(servers);
    Assert.Equal("homebox_db", servers[0].Alias);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ModelContextGateway.Tests/ModelContextGateway.Tests.csproj --filter "FullyQualifiedName~ParseDiscoveredServers_Parses_McpAlias_Label"`
Expected: FAIL.

- [ ] **Step 3: Implement mcp.alias ingestion in DockerAutoDiscoveryService.cs**

In `Components/Servers/DockerAutoDiscoveryService.cs`:
- Check for `mcp.alias` or `mcp.namespace` in `labelsProp`.
- Trim and validate characters. Assign to `discoveredServers.Add(new McpServer { ... Alias = parsedAlias })`.
- In `UpsertDiscoveredServers`, include `Alias` in the INSERT query and update logic:
  ```csharp
  if (!string.IsNullOrEmpty(discovered.Alias) && string.IsNullOrEmpty(existing.Alias))
  {
      existing.Alias = discovered.Alias;
      updated = true;
  }
  ```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ModelContextGateway.Tests/ModelContextGateway.Tests.csproj --filter "FullyQualifiedName~ParseDiscoveredServers_Parses_McpAlias_Label"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Components/Servers/DockerAutoDiscoveryService.cs ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs
git commit -m "feat(discovery): support mcp.alias docker label and upsert reconciliation (MCP-31)"
```

---

### Task 4: Tool Exposure Formatting & Caching with Slash & Dual-Key Registration (`MCP-27`)

**Files:**
- Modify: `Core/Routing/ToolRoutingManager.Cache.cs`
- Modify: `Core/Routing/ToolRoutingManager.cs`
- Test: `ModelContextGateway.Tests/ToolRoutingManagerTests.cs`

**Interfaces:**
- Consumes: `McpServer.Alias` and `McpServer.Id`
- Produces: Primary exposed tool name `{namespace}/{tool_name}` with description `[{namespace}] {desc}`. Multi-key registration in `_toolRoutingTable`.

- [ ] **Step 1: Write the failing tests for slash tool exposure and dual-key registration (`MCP-27`)**

In `ModelContextGateway.Tests/ToolRoutingManagerTests.cs`:
```csharp
[Fact]
public async Task CacheTools_Exposes_Slash_Formatted_Name_With_Server_Alias()
{
    // MCP-27: Primary exposed name uses namespace/tool format where namespace is Alias ?? Id
    var manager = new ToolRoutingManager();
    var server = new McpServer
    {
        Id = "postgres-mcp-homebox",
        Alias = "homebox_db",
        DisplayName = "Homebox Database"
    };

    // Construct mock tools payload
    var toolsJson = @"[{""name"": ""execute_sql"", ""description"": ""Run SQL query"", ""inputSchema"": {}}]";
    using var doc = System.Text.Json.JsonDocument.Parse(toolsJson);

    // Call internal tool registration helper or PopulateCache
    var exposed = manager.BuildExposedToolDefinition(server, doc.RootElement[0]);
    Assert.Equal("homebox_db/execute_sql", exposed["name"]);
    Assert.Equal("[homebox_db] Run SQL query", exposed["description"]);

    // Verify dual-key routing entries
    Assert.Equal("postgres-mcp-homebox", manager.ToolRoutingTable["homebox_db/execute_sql"]);
    Assert.Equal("postgres-mcp-homebox", manager.ToolRoutingTable["postgres-mcp-homebox/execute_sql"]);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ModelContextGateway.Tests/ModelContextGateway.Tests.csproj --filter "FullyQualifiedName~CacheTools_Exposes_Slash_Formatted_Name_With_Server_Alias"`
Expected: FAIL.

- [ ] **Step 3: Implement slash tool formatting and dual-key registration**

In `Core/Routing/ToolRoutingManager.Cache.cs`:
```csharp
var ns = !string.IsNullOrWhiteSpace(srv?.Alias) ? srv.Alias : item.ServerId;
var exposedName = $"{ns}/{rawToolName}";

_toolRoutingTable[exposedName] = item.ServerId;
_toolRoutingTable[$"{item.ServerId}/{rawToolName}"] = item.ServerId;

// Register legacy delimiters for resilient O(1) resolution
_toolRoutingTable[$"{ns}__{rawToolName}"] = item.ServerId;
_toolRoutingTable[$"{ns}:{rawToolName}"] = item.ServerId;
_toolRoutingTable[$"{item.ServerId}__{rawToolName}"] = item.ServerId;
_toolRoutingTable[$"{item.ServerId}:{rawToolName}"] = item.ServerId;
```
Ensure tool dictionary description uses `[{ns}]`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ModelContextGateway.Tests/ModelContextGateway.Tests.csproj --filter "FullyQualifiedName~CacheTools_Exposes_Slash_Formatted_Name_With_Server_Alias"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Core/Routing/ToolRoutingManager.Cache.cs Core/Routing/ToolRoutingManager.cs ModelContextGateway.Tests/ToolRoutingManagerTests.cs
git commit -m "feat(routing): expose slash tool format with server namespace aliases (MCP-27)"
```

---

### Task 5: Routing Execution, Delimiter Normalization & Ambiguity Detection (`MCP-28`, `MCP-30`)

**Files:**
- Modify: `Core/Routing/ToolRoutingManager.cs`
- Modify: `Core/Routing/ToolRoutingManager.Execution.cs`
- Modify: `Core/Routing/ClientSession/ClientSession.Authorization.cs`
- Test: `ModelContextGateway.Tests/ToolRoutingManagerTests.cs`

**Interfaces:**
- Consumes: Target tool name across `/`, `:`, `__`, or bare name.
- Produces: Normalized target name, resolution of alias to serverId, duplicate tool ambiguity rejection error, strict multi-instance isolation.

- [ ] **Step 1: Write failing tests for duplicate collision ambiguity and multi-delimiter routing (`MCP-28`, `MCP-30`)**

In `ModelContextGateway.Tests/ToolRoutingManagerTests.cs`:
```csharp
[Fact]
public void NormalizeTargetToolName_Resolves_Multiple_Delimiters_And_Aliases()
{
    // MCP-30: Resolves alias/tool, alias:tool, alias__tool, serverId/tool, serverId__tool
    var manager = new ToolRoutingManager();
    var servers = new List<McpServer>
    {
        new McpServer { Id = "postgres-mcp-homebox", Alias = "homebox_db" }
    };
    manager.ToolRoutingTable["homebox_db/execute_sql"] = "postgres-mcp-homebox";
    manager.ToolRoutingTable["postgres-mcp-homebox/execute_sql"] = "postgres-mcp-homebox";
    manager.ToolRoutingTable["homebox_db__execute_sql"] = "postgres-mcp-homebox";

    var (norm1, err1) = manager.NormalizeTargetToolName("homebox_db/execute_sql", servers);
    Assert.Null(err1);
    Assert.Equal("homebox_db__execute_sql", norm1);

    var (norm2, err2) = manager.NormalizeTargetToolName("homebox_db:execute_sql", servers);
    Assert.Null(err2);
    Assert.Equal("homebox_db__execute_sql", norm2);
}

[Fact]
public void NormalizeTargetToolName_Returns_Ambiguity_Error_Listing_Aliases_For_Duplicates()
{
    // MCP-28: Detects duplicate tool names across distinct servers and returns candidates formatted with namespaces
    var manager = new ToolRoutingManager();
    var servers = new List<McpServer>
    {
        new McpServer { Id = "postgres-mcp-homebox", Alias = "homebox_db" },
        new McpServer { Id = "postgres-mcp-sure", Alias = "sure_db" }
    };
    manager.ToolRoutingTable["homebox_db/execute_sql"] = "postgres-mcp-homebox";
    manager.ToolRoutingTable["sure_db/execute_sql"] = "postgres-mcp-sure";

    var (norm, err) = manager.NormalizeTargetToolName("execute_sql", servers);
    Assert.NotNull(err);
    Assert.Contains("Ambiguous tool name 'execute_sql'", err);
    Assert.Contains("'homebox_db/execute_sql'", err);
    Assert.Contains("'sure_db/execute_sql'", err);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ModelContextGateway.Tests/ModelContextGateway.Tests.csproj --filter "FullyQualifiedName~NormalizeTargetToolName_Returns_Ambiguity_Error_Listing_Aliases_For_Duplicates"`
Expected: FAIL.

- [ ] **Step 3: Implement enhanced ambiguity detection and alias resolution**

In `Core/Routing/ToolRoutingManager.cs`:
- Update `NormalizeTargetToolName`:
  - Split on `/`, `:`, or `__`. Extract namespace prefix.
  - Check if prefix matches a known `server.Alias` or `server.Id`. Resolve to underlying `server.Id`.
  - On bare tool search, collect all candidate keys ending with `/{trimmed}`, `__{trimmed}`, or `:{trimmed}`.
  - Filter down to canonical unique namespaced forms (`{ns}/{trimmed}`).
  - If count > 1, return the structured ambiguity error listing the candidate options.

In `Core/Routing/ClientSession/ClientSession.Authorization.cs`:
- Extract namespace across `/`, `:`, and `__`, resolving alias to server ID before performing permission/authorization checks.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test ModelContextGateway.Tests/ModelContextGateway.Tests.csproj --filter "FullyQualifiedName~NormalizeTargetToolName_"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Core/Routing/ToolRoutingManager.cs Core/Routing/ToolRoutingManager.Execution.cs Core/Routing/ClientSession/ClientSession.Authorization.cs ModelContextGateway.Tests/ToolRoutingManagerTests.cs
git commit -m "feat(routing): handle duplicate tool ambiguity and multi-delimiter alias resolution (MCP-28, MCP-30)"
```

---

### Task 6: Admin MCP Server Parity (`MCP-ADMIN-PARITY-SERVER-ALIAS`)

**Files:**
- Modify: `Core/Routing/AdminMcpServer.cs`
- Test: `ModelContextGateway.Tests/AdminMcpServerTests.cs`

**Interfaces:**
- Consumes: `manage_servers` parameters
- Produces: `alias` support for `add`, `update`, and `list` in `AdminMcpServer`.

- [ ] **Step 1: Write the failing tests for AdminMcpServer alias management**

In `ModelContextGateway.Tests/AdminMcpServerTests.cs`:
```csharp
[Fact]
public async Task AdminMcpServer_ManageServers_Supports_Alias()
{
    // MCP-ADMIN-PARITY-SERVER-ALIAS
    var admin = CreateTestAdminMcpServer();
    var addResult = await admin.ExecuteManageServersAsync(new Dictionary<string, object>
    {
        ["action"] = "add",
        ["id"] = "test-mcp-db",
        ["alias"] = "test_db",
        ["displayName"] = "Test DB",
        ["url"] = "http://localhost:9000/sse"
    });

    Assert.True(addResult.Success);

    var listResult = await admin.ExecuteManageServersAsync(new Dictionary<string, object>
    {
        ["action"] = "list"
    });
    var json = JsonSerializer.Serialize(listResult.Data);
    Assert.Contains("\"alias\":\"test_db\"", json);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ModelContextGateway.Tests/ModelContextGateway.Tests.csproj --filter "FullyQualifiedName~AdminMcpServer_ManageServers_Supports_Alias"`
Expected: FAIL.

- [ ] **Step 3: Implement alias in AdminMcpServer.cs**

In `Core/Routing/AdminMcpServer.cs`:
- Add `alias` to `manage_servers` schema parameters.
- In `add` and `update` action handlers, read `alias` and assign to `McpServer.Alias`.
- In `list` action handler, ensure `alias` is included in the returned server objects.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ModelContextGateway.Tests/ModelContextGateway.Tests.csproj --filter "FullyQualifiedName~AdminMcpServer_ManageServers_Supports_Alias"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Core/Routing/AdminMcpServer.cs ModelContextGateway.Tests/AdminMcpServerTests.cs
git commit -m "feat(admin): support server alias in mcg-admin manage_servers (MCP-ADMIN-PARITY-SERVER-ALIAS)"
```

---

### Task 7: React 19 Frontend UI Updates & Vitest Proofs (`UI-SERVERS-ALIAS-MANAGEMENT`)

**Files:**
- Modify: `frontend/src/types/server.ts`
- Modify: `frontend/src/features/servers/ServerTable.tsx` (or equivalent server view)
- Modify: `frontend/src/features/servers/ServerModal.tsx` (or equivalent modal)
- Test: `frontend/src/features/servers/ServerModal.test.tsx`

**Interfaces:**
- Consumes: Server `alias?: string` from `/api/servers`.
- Produces: UI table badge rendering, modal form input, client-side format and collision validation.

- [ ] **Step 1: Write the failing Vitest test for server alias input and badge rendering**

In `frontend/src/features/servers/ServerModal.test.tsx`:
```typescript
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ServerModal } from './ServerModal';

describe('ServerModal Alias Field (UI-SERVERS-ALIAS-MANAGEMENT)', () => {
  it('renders Alias input and validates characters', async () => {
    render(<ServerModal isOpen={true} onClose={vi.fn()} onSave={vi.fn()} />);

    const aliasInput = screen.getByLabelText(/alias/i);
    expect(aliasInput).toBeInTheDocument();

    fireEvent.change(aliasInput, { target: { value: 'invalid alias!@#' } });
    expect(screen.getByText(/letters, numbers, underscores, and hyphens/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm --dir frontend test`
Expected: FAIL (alias input not found).

- [ ] **Step 3: Implement Alias field in React store and modal**

- Update `types/server.ts` to include `alias?: string`.
- Update `ServerModal.tsx` with an input field for "Alias / Namespace (Optional)" and validation regex (`^[a-zA-Z0-9_-]*$`).
- Update `ServerTable.tsx` to display `{server.alias}` badge beside the Server ID.

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm --dir frontend test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/types/server.ts frontend/src/features/servers/
git commit -m "feat(ui): add server alias input, validation, and table badge in React frontend (UI-SERVERS-ALIAS-MANAGEMENT)"
```

---

### Task 8: Requirements Catalog Regeneration & Quality Gates

**Files:**
- Modify: `docs/requirements-catalog.json`
- Modify: `docs/software-requirements-and-test-catalog.md`
- Modify: `ModelContextGateway.csproj` (bump version to `5.10.0` for new minor feature)

- [ ] **Step 1: Run CatalogGenerator**

Run: `dotnet run --project scripts/CatalogGenerator`
Verify that `MCP-27`, `MCP-28`, `MCP-29`, `MCP-30`, `MCP-31`, `MCP-ADMIN-PARITY-SERVER-ALIAS`, and `UI-SERVERS-ALIAS-MANAGEMENT` are indexed with their corresponding test proofs.

- [ ] **Step 2: Run full backend and frontend test suites**

Run: `dotnet test ModelContextGateway.Tests/ModelContextGateway.Tests.csproj`
Run: `pnpm --dir frontend test`
Run: `pnpm --dir frontend build`
Verify all tests pass and formatting is clean.

- [ ] **Step 3: Commit**

```bash
git add docs/requirements-catalog.json docs/software-requirements-and-test-catalog.md ModelContextGateway.csproj
git commit -m "docs(catalog): register MCP-27 through MCP-31 requirements and bump version to 5.10.0"
```

---

### Task 9: PR, CI Quality Gates & Docker Production Deployment

- [ ] **Step 1: Push branch and open GitHub PR**
- [ ] **Step 2: Monitor and verify all 9 CI Quality Gates pass**
- [ ] **Step 3: Merge PR to main and push tag v5.10.0**
- [ ] **Step 4: Wait for Docker build workflow to publish ghcr.io image**
- [ ] **Step 5: Pull and recreate mcg in /containers/mcp and verify live**
