# Live MCG Demo Errors Resolution & Playwright Layout Inspector Integration Design

## 1. Problem Statement
During live MCG container demos, several defects and failures were identified:
1. **Backend Routing Failures**: In `ClientSession.NotificationBroadcaster.cs`, stateless broadcast notifications use `Task.WhenAll` across all backend transports. If an upstream backend rejects a notification with HTTP `404 Not Found` or `405 Method Not Allowed`, `HttpTransport.SendNotificationAsync` throws an exception, failing `Task.WhenAll`, producing `Error routing stateless message` logs in `ProxyEndpoints.cs`, and returning HTTP 500 to clients.
2. **Modal Cutoff & Scroll Failure**: In `components.css`, `.modal-backdrop` centers `.modal-card` in a fixed-height viewport. `.modal-card` lacks `max-height` and overflow handling, causing long forms like `ServerModal` (Display Name, Alias, Type, Categories, URL, Secret Provider/Key, Auth Shapes, Custom Header, API Key, toggles) to be vertically cut off at both top and bottom with zero scrollability.
3. **Responsive UI Anomalies**: Server cards, inspect modals, tables, and settings forms exhibit overflow or layout shifts on mobile (e.g. Samsung Galaxy S25+) and tablet viewports.
4. **Limited Layout Inspection**: The existing `frontend/e2e/layout-inspector.spec.ts` only audits the root `/` page on Desktop and S25+, without auditing modals, tabs, or layout stability.

## 2. Architecture & Design

### 2.1 Backend Fault-Tolerant Notification Pipeline
- **Isolated Upstream Notification Execution**:
  - In `ClientSession.NotificationBroadcaster.cs`, execute each backend's `SendNotificationAsync` in an isolated task wrapper:
    ```csharp
    var tasks = _backendConnections.Values.Select(async conn => {
        try {
            await conn.SendNotificationAsync(method, body);
        } catch (Exception ex) {
            _logger.LogWarning("Upstream server '{ServerId}' failed to receive notification '{Method}': {Message}", conn.ServerId, method, ex.Message);
        }
    });
    await Task.WhenAll(tasks);
    ```
- **HttpTransport Notification Resilience**:
  - In `HttpTransport.SendNotificationAsync`: one-way notifications encountering 404 or 405 should be logged as warnings rather than throwing unhandled fatal exceptions.
- **Stateless Endpoint Safety**:
  - Ensure proxy endpoints return HTTP 202 Accepted on notification delivery without bubbling 500 errors.

### 2.2 Modal & Responsive UI Layout Architecture
- **Backdrop & Modal Positioning**:
  - Update `.modal-backdrop`:
    - `overflow-y: auto;`
    - `padding: var(--space-4);`
    - Keep flex centering with `.modal-card { margin: auto; }` so shorter cards center naturally, and cards taller than the viewport start cleanly without clipping.
- **Scrollable Modal Card Structure**:
  - Update `.modal-card`:
    - `max-height: calc(100vh - 40px);` (and `max-height: calc(100dvh - 32px);` for mobile).
    - `display: flex; flex-direction: column;`
    - Make the form or content body scrollable: `overflow-y: auto; overscroll-behavior: contain;`.
    - Ensure `.modal-header` and `.modal-footer` remain sticky/visible.
- **Responsive Adjustments**:
  - Update `responsive.css` to ensure full width, proper padding, and zero horizontal scrolling on mobile viewports (< 768px).

### 2.3 Comprehensive Playwright Layout Inspector Suite
- Expand `frontend/e2e/layout-inspector.spec.ts` using `playwright-layout-inspector`:
  - **Viewports**: Desktop 1080p (`1920x1080`) and Mobile (`Samsung Galaxy S25+` `412x915`).
  - **Views Audited**:
    - Overview / Servers Dashboard (`/`)
    - App Keys & Security tab (`SecurityView`)
    - Test Bench tab (`TestBenchView`)
    - Settings tab (`SettingsView`)
    - My MCP Servers tab (`MyMcpServers`)
  - **Modals Audited**:
    - Add/Edit MCP Server modal (`ServerModal`) - verify all fields visible/scrollable, zero overflow.
    - Capabilities Inspect modal (`ServerInspectModal`) - verify Tools/Resources/Prompts tabs and search.
    - App Key generation modal (`AppKeyModal`).
  - **Layout Stability (`trackShifts`)**:
    - Measure shift scores during tab navigation to guarantee no layout shifts.
  - **Assertions**:
    - `overflowIssues.length === 0`
    - `uxScore.totalScore >= 85` (Desktop) and `>= 80` (Mobile).
  - **Traceability**:
    - Annotate tests with `@requirement UI-07` per mandatory test requirement rule.

## 3. Verification & Quality Gates
1. Unit/Integration Tests: `dotnet test ModelContextGateway.slnx`
2. Frontend Component Tests: `npm test` in `frontend/`
3. E2E Layout Inspector Tests: `npm run test:e2e -- e2e/layout-inspector.spec.ts`
4. Requirement Catalog Generation: `dotnet run --project scripts/CatalogGenerator -- --verify-only`
5. Version bump per MANDATORY VERSIONING RULE (v5.17.0 -> v5.17.1).
