# Live MCG Demo Errors & Playwright Layout Inspector Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve live MCG demo errors (stateless broadcast failures on 404/405), fix modal cut-off/scrolling and responsive UI overflow, implement a comprehensive Playwright Layout Inspector suite, and prepare a PR.

**Architecture:** Fault-tolerant broadcast in `ClientSession.NotificationBroadcaster` with per-backend exception isolation; resilient one-way notification sends in `HttpTransport`; responsive flex/max-height CSS architecture for `.modal-backdrop` and `.modal-card`; expanded Playwright Layout Inspector test suite auditing Desktop/Mobile viewports, tabs, and modals using `playwright-layout-inspector`.

**Tech Stack:** C# ASP.NET Core 10, React 19, TypeScript, Playwright, `playwright-layout-inspector`, Vitest.

## Global Constraints
- MANDATORY VERSIONING RULE: Bump version number simultaneously across `ModelContextGateway.csproj`, `frontend/src/stores/useUserStore.ts`, `frontend/package.json`, `CHANGELOG.md`, and `README.md`.
- MANDATORY TEST REQUIREMENT ANNOTATIONS RULE: Annotate all C# tests with `[Requirement("ID", "CATEGORY", ...)]` and Playwright/TS tests with JSDoc `@requirement`. Requirement IDs must NEVER use `REQ-` prefixes.
- Zero horizontal overflow (`overflowIssues.length === 0`) and high UX score (`>= 85` desktop, `>= 80` mobile).

---

### Task 1: Backend Resilience for Notification Broadcasting and Upstream 404/405 Handling

**Files:**
- Modify: `Core/Routing/ClientSession/ClientSession.NotificationBroadcaster.cs:45-55`
- Modify: `Infrastructure/Transports/HttpTransport.cs:424-428`
- Test: `ModelContextGateway.Tests/TransportResilienceTests.cs`

**Interfaces:**
- Consumes: `ClientSession.BroadcastNotificationAsync(string method, string body)`, `HttpTransport.SendNotificationAsync(string method, string bodyJson)`
- Produces: Resilient broadcast where backend failures log warnings rather than throwing and crashing proxy endpoints.

- [ ] **Step 1: Write failing unit test in `TransportResilienceTests.cs`**

```csharp
[Fact]
[Requirement("TRANS-02", "TRANS", RequirementType.Positive, "HttpTransport SendNotificationAsync gracefully handles 404 and 405 without throwing exceptions")]
public async Task HttpTransport_SendNotificationAsync_DoesNotThrow_On404Or405()
{
    var server = new McpServer
    {
        Id = "http-test-405",
        Url = "http://localhost:9999/messages",
        SecretProvider = "None"
    };

    var handler = new MockHttpMessageHandler((req, token) =>
    {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.MethodNotAllowed));
    });

    using var httpClient = new HttpClient(handler);
    using var transport = new HttpTransport(server, httpClient, NullLogger.Instance);

    var act = () => transport.SendNotificationAsync("notifications/initialized", "{}");
    await act.Should().NotThrowAsync();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~HttpTransport_SendNotificationAsync_DoesNotThrow_On404Or405"`
Expected: FAIL with `HttpRequestException` (405 Method Not Allowed)

- [ ] **Step 3: Implement minimal code in `HttpTransport.cs` and `ClientSession.NotificationBroadcaster.cs`**

In `Infrastructure/Transports/HttpTransport.cs`:
```csharp
public async Task SendNotificationAsync(string method, string bodyJson)
{
    try
    {
        await SendRequestAsync(method, bodyJson);
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.MethodNotAllowed)
    {
        _logger.LogWarning("Upstream backend '{ServerId}' rejected notification '{Method}' with HTTP {StatusCode}", _server.Id, method, ex.StatusCode);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to send notification '{Method}' to upstream backend '{ServerId}': {Message}", method, _server.Id, ex.Message);
    }
}
```

In `Core/Routing/ClientSession/ClientSession.NotificationBroadcaster.cs`:
```csharp
public async Task BroadcastNotificationAsync(string method, string body)
{
    var tasks = _backendConnections.Values.Select(async conn =>
    {
        try
        {
            await conn.SendNotificationAsync(method, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Upstream server '{ServerId}' failed to receive notification '{Method}': {Message}", conn.ServerId, method, ex.Message);
        }
    });
    await Task.WhenAll(tasks);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~HttpTransport_SendNotificationAsync_DoesNotThrow_On404Or405"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Core/Routing/ClientSession/ClientSession.NotificationBroadcaster.cs Infrastructure/Transports/HttpTransport.cs ModelContextGateway.Tests/TransportResilienceTests.cs
git commit -m "fix(transports): handle upstream 404 and 405 gracefully during notification broadcast [TRANS-02]"
```

---

### Task 2: Modal Card & Backdrop Responsive Layout Improvements

**Files:**
- Modify: `frontend/src/styles/components.css:220-250`
- Modify: `frontend/src/styles/responsive.css:235-250`
- Modify: `frontend/src/components/servers/ServerModal.tsx`
- Test: `frontend/src/test/components/ServerModal.test.tsx`

**Interfaces:**
- Consumes: CSS modal architecture (`.modal-backdrop`, `.modal-card`)
- Produces: Vertically scrollable modals that fit within viewport boundaries with sticky headers/footers.

- [ ] **Step 1: Check existing `ServerModal.test.tsx` and ensure modal render passes**

Run: `npm test -- src/test/components/ServerModal.test.tsx`

- [ ] **Step 2: Update CSS in `components.css` and `responsive.css`**

In `frontend/src/styles/components.css`:
Update `.modal-backdrop`:
```css
.modal-backdrop {
    position: fixed;
    top: 0;
    left: 0;
    width: 100vw;
    height: 100vh;
    background: rgb(0 0 0 / 0.8);
    backdrop-filter: blur(8px);
    -webkit-backdrop-filter: blur(8px);
    z-index: var(--z-modal-backdrop);
    display: flex;
    justify-content: center;
    align-items: center;
    padding: var(--space-4);
    box-sizing: border-box;
    overflow-y: auto;
}
```
Update `.modal-card`:
```css
.modal-card {
    width: 100%;
    max-width: 500px;
    max-height: calc(100vh - 40px);
    max-height: calc(100dvh - 40px);
    padding: 30px;
    z-index: var(--z-modal);
    animation: modalSlideUp 0.3s ease-out;
    display: flex;
    flex-direction: column;
    margin: auto;
    overflow-y: auto;
    box-sizing: border-box;
}
```
In `frontend/src/styles/responsive.css`:
```css
.modal-card {
    padding: var(--space-4) 15px;
    max-height: calc(100vh - 20px);
    max-height: calc(100dvh - 20px);
    width: 95%;
}
```

- [ ] **Step 3: Run component tests to verify no regressions**

Run: `npm test` in `frontend/`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add frontend/src/styles/components.css frontend/src/styles/responsive.css
git commit -m "fix(ui): ensure modals scroll and do not clip on viewport boundaries [UI-07]"
```

---

### Task 3: Comprehensive Playwright Layout Inspector Test Matrix

**Files:**
- Modify: `frontend/e2e/layout-inspector.spec.ts`
- Uses: `playwright-layout-inspector` (`LayoutInspector`, `getDevicePreset`)

**Interfaces:**
- Consumes: Playwright page fixtures and `playwright-layout-inspector`
- Produces: Rigorous E2E test proofs validating desktop & mobile viewports, tabs (Overview, Security, Settings, TestBench, MyMcpServers), and modals (ServerModal, ServerInspectModal, AppKeyModal) with zero overflow.

- [ ] **Step 1: Expand `frontend/e2e/layout-inspector.spec.ts`**

Add tests for:
1. Desktop 1080p audit of `/` (Overview)
2. Samsung Galaxy S25+ mobile audit of `/`
3. Add Server Modal layout audit on Desktop & Mobile (open modal, inspect, verify `result.overflowIssues.length === 0`, verify scrollability)
4. Capabilities Inspect Modal audit (open inspect modal, switch tabs to Tools/Resources/Prompts, audit layout)
5. Security / App Keys tab audit
6. Settings tab audit
7. Layout shift audit (`trackShifts`) during view navigation

- [ ] **Step 2: Run Playwright Layout Inspector test suite**

Run: `npm run test:e2e -- e2e/layout-inspector.spec.ts`

- [ ] **Step 3: Fix any UI anomalies surfaced by the inspector**

If the inspector detects overflow, touch target, or layout issues, adjust the respective CSS rules in `components.css`, `dashboard.css`, or `responsive.css`.

- [ ] **Step 4: Re-run tests to confirm all pass with 0 overflow and high UX scores**

Run: `npm run test:e2e -- e2e/layout-inspector.spec.ts`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add frontend/e2e/layout-inspector.spec.ts frontend/src/styles/
git commit -m "test(layout): expand Playwright layout inspector suite across tabs and modals [UI-07]"
```

---

### Task 4: Version Bump, Catalog Verification, and PR Creation

**Files:**
- Modify: `ModelContextGateway.csproj`
- Modify: `frontend/src/stores/useUserStore.ts`
- Modify: `frontend/package.json`
- Modify: `CHANGELOG.md`
- Modify: `README.md`
- Update: `docs/software-requirements-and-test-catalog.md`, `docs/requirements-catalog.json`

- [ ] **Step 1: Bump version to 5.17.1**
Update:
- `ModelContextGateway.csproj` (`<Version>5.17.1</Version>`, `<AssemblyVersion>5.17.1</AssemblyVersion>`, `<FileVersion>5.17.1</FileVersion>`)
- `frontend/src/stores/useUserStore.ts` (fallback to `5.17.1`)
- `frontend/package.json` (`"version": "5.17.1"`)
- `CHANGELOG.md` (add v5.17.1 entry)
- `README.md` (update preview table)

- [ ] **Step 2: Regenerate and verify requirements catalog**

Run:
```bash
dotnet run --project scripts/CatalogGenerator
dotnet run --project scripts/CatalogGenerator -- --verify-only
```
Expected: Verification succeeds with zero drift.

- [ ] **Step 3: Run complete project quality gates**

Run:
```bash
dotnet test ModelContextGateway.slnx
cd frontend && npm test && npm run test:e2e -- e2e/layout-inspector.spec.ts
```
Expected: All suites PASS.

- [ ] **Step 4: Commit, push branch, and create PR**

```bash
git add ModelContextGateway.csproj frontend/src/stores/useUserStore.ts frontend/package.json CHANGELOG.md README.md docs/software-requirements-and-test-catalog.md docs/requirements-catalog.json
git commit -m "chore(release): bump version to v5.17.1"
git push origin fix/live-demo-errors-layout-inspector
gh pr create --title "fix: live demo errors, modal scrolling, and comprehensive layout inspector audit" --body "..."
```
