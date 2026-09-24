# Comprehensive Layout Inspector Audit Across All Dashboard Views & Modals Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expand the Playwright Layout Inspector test matrix to audit every view, sub-navigation tab, and modal across Desktop 1080p, Samsung Galaxy S25+ mobile, and Samsung Galaxy Tab S10 Lite tablet, fixing any layout overflow or clipping issues surfaced by `playwright-layout-inspector`.

**Architecture:**
1. Populate complete API route mocks in `frontend/e2e/layout-inspector.spec.ts` (testbench tools/prompts/resources, logs, appkeys personal/system/quotas, clients, policies, mappings, custom files, my-mcp-servers).
2. Group audits into focused describe blocks or test suites:
   - **Suite 1 (Views)**: Overview, Test Bench (Tools form/JSON, Prompts, Resources), App Keys & Security (Personal, System, Quotas), Settings (all 5 tabs), My MCP Servers across Desktop, Mobile, and Tablet.
   - **Suite 2 (Modals)**: ServerModal, ServerInspectModal, AppKeyModal, ClientModal, CustomFileModal, PolicyModal, MappingModal across Desktop, Mobile, and Tablet.
   - **Suite 3 (Stability & Navigation)**: Tab-switching stability score with `trackShifts`.
3. Fix any responsive CSS issues surfaced by the inspector (e.g. form controls, tables, modals on small viewports) to ensure `overflowIssues.length === 0` and UX score >= 85 (desktop/tablet) / >= 80 (mobile).

**Tech Stack:** Playwright Test, `playwright-layout-inspector` (`LayoutInspector`, `getDevicePreset`, `trackShifts`), React 18, CSS.

## Global Constraints

- **MANDATORY TEST REQUIREMENT ANNOTATIONS RULE**:
  - Annotate all Playwright tests with JSDoc `@requirement UI-07` (`@category UI`, `@type PositiveFeature`).
  - Requirement IDs must NEVER use `REQ-` prefixes.
  - Zero catalog drift: `dotnet run --project scripts/CatalogGenerator -- --verify-only` must pass.
- **NO RELEASE TAG / PUBLISH YET**:
  - User explicitly specified: *"once all ci passes we can merge, no release yet, I have another set of fixes in mind"*.
  - Do NOT create git release tags, do NOT trigger container build/publish workflows.
- **ZERO HORIZONTAL OVERFLOW**:
  - `result.overflowIssues.length === 0` across all viewports.
  - UX score >= 85 on desktop & tablet, >= 80 on mobile.

---

### Task 1: Comprehensive Mock Routes & Test Bench Layout Audits (`UI-07`)

**Files:**
- Modify: `frontend/e2e/layout-inspector.spec.ts`
- Modify: `frontend/src/styles/components.css` / `responsive.css` (if any layout fixes needed)

**Steps:**
1. Update `test.beforeEach` in `frontend/e2e/layout-inspector.spec.ts` with mocks for:
   - `/api/test/tools`, `/api/test/prompts`, `/api/test/resources`, `/api/logs`
   - `/api/appkeys` (personal, system, quotas), `/api/clients`
   - `/api/my-mcp/servers`, `/api/policies`, `/api/group-mappings`, `/api/custom-files`
2. Add Test Bench layout audit tests:
   - Desktop 1080p: Navigate to Test Bench, select server `mock-docker`, select tool `mock-docker/docker_ps`, inspect `.tool-hint-banner`, audit with `LayoutInspector` (0 overflow, score >= 85). Switch to Raw JSON tab, audit.
   - Mobile S25+: Navigate to Test Bench, audit (0 overflow, score >= 80).
   - Tablet Tab S10 Lite: Navigate to Test Bench, audit (0 overflow, score >= 85).
   - Prompts & Resources tabs: Switch to Prompts tab, select prompt, audit; switch to Resources tab, audit.
3. Run tests: `npm run test:e2e -- e2e/layout-inspector.spec.ts -g "Test Bench"`.
4. Fix any CSS issues found.
5. Commit:
   `git commit -m "test(layout): add comprehensive Test Bench layout audits across viewports [UI-07]"`

---

### Task 2: Security, Settings Sub-Tabs, and My MCP Servers Layout Audits (`UI-07`)

**Files:**
- Modify: `frontend/e2e/layout-inspector.spec.ts`
- Modify: `frontend/src/styles/components.css` / `responsive.css` (if any layout fixes needed)

**Steps:**
1. Add App Keys & Security layout audit tests:
   - Mobile S25+ & Tablet Tab S10 Lite: Personal Keys tab, System Keys tab, Quotas tab.
   - Assert `overflowIssues.length === 0`, UX score >= 80 mobile / >= 85 tablet.
2. Add Settings sub-tabs layout audit tests:
   - Mobile S25+ & Tablet Tab S10 Lite: GeneralTab (Vector Engine), IdentityAuthTab, SecretProvidersTab, CustomFilesTab, AccessControlTab.
   - Assert `overflowIssues.length === 0`.
3. Add My MCP Servers layout audit tests:
   - Desktop 1080p, Mobile S25+, Tablet Tab S10 Lite.
   - Assert `overflowIssues.length === 0`.
4. Run tests: `npm run test:e2e -- e2e/layout-inspector.spec.ts`.
5. Fix any table or responsive container issues surfaced.
6. Commit:
   `git commit -m "test(layout): add Security, Settings sub-tabs, and My MCP Servers audits across viewports [UI-07]"`

---

### Task 3: Modals Across All Viewports Layout Audits (`UI-07`)

**Files:**
- Modify: `frontend/e2e/layout-inspector.spec.ts`
- Modify: `frontend/src/styles/components.css` / `responsive.css` (if any modal fixes needed)

**Steps:**
1. Add audits for all modals:
   - `AppKeyModal`: Click "New App Key", audit Desktop 1080p and Mobile S25+, verify scrollability and 0 overflow.
   - `ClientModal`: Click "New OAuth Client", audit Desktop and Mobile, verify 0 overflow.
   - `CustomFileModal`: Open Custom Files tab, click "New File", audit Desktop and Mobile.
   - `PolicyModal`: Open Access Control tab, click "Add Policy", audit Desktop and Mobile.
   - `MappingModal`: Open Access Control tab, click "Add Mapping", audit Desktop and Mobile.
   - `ServerInspectModal`: Audit on Mobile S25+ and Tablet Tab S10 Lite (expanding beyond desktop).
2. Run tests: `npm run test:e2e -- e2e/layout-inspector.spec.ts -g "modal"`.
3. Fix any modal clipping or backdrop issues surfaced.
4. Commit:
   `git commit -m "test(layout): add modal layout audits for AppKey, Client, Policy, Mapping, CustomFile modals [UI-07]"`

---

### Task 4: Full Quality Gates, Catalog Verification, and PR

**Files:**
- Modify: `docs/software-requirements-and-test-catalog.md`
- Modify: `docs/requirements-catalog.json`

**Steps:**
1. Run full Playwright layout inspector suite:
   `cd frontend && npm run test:e2e -- e2e/layout-inspector.spec.ts`
2. Run frontend unit tests:
   `cd frontend && npm test`
3. Run backend tests:
   `dotnet test ModelContextGateway.slnx`
4. Regenerate and verify SRS Requirements Catalog:
   `dotnet run --project scripts/CatalogGenerator`
   `dotnet run --project scripts/CatalogGenerator -- --verify-only`
5. Commit catalog updates if changed.
6. Push branch:
   `git push origin test/comprehensive-layout-inspector-all-views-modals`
7. Create PR via `gh pr create`.
   *(No release tags / publishing per user constraint)*
