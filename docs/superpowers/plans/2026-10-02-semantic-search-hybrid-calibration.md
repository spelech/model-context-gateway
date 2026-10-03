# Semantic Search Hybrid Calibration & Test Bench Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Calibrate hybrid semantic search with configurable dense weights ($\alpha \in [0.0, 1.0]$), explicit search modes (`hybrid`, `semantic`, `lexical`), score decomposition diagnostics, and a redesigned Semantic Router Simulator on the Test Bench matching ContextCortex.

**Architecture:** Extend `ToolRoutingManager.Search.cs` with a normalized linear combination scoring model ($\text{Score} = \alpha \cdot \text{DenseScore} + (1-\alpha) \cdot \text{SparseScore}$) and return rich `ToolSearchResult` models with decomposed scores. Update `POST /api/test/semantic-search` and meta-mode `search_tools`. Overhaul `SemanticRouterCard.tsx` with a 3-way mode toggle, interactive slider with quick presets, score chips, and a 1-click "Test Tool" delegation button.

**Tech Stack:** C# .NET 10, ASP.NET Core, React 19, TypeScript, Vitest, Playwright, Puppeteer.

## Global Constraints

- Every code change commit or merge to `main` must bump the version number simultaneously in `ModelContextGateway.csproj`, `frontend/src/stores/useUserStore.ts`, `CHANGELOG.md`, and `README.md`.
- All new and modified tests must be annotated with formal requirement metadata (`MCP-40`, `MCP-41`, `MCP-42`, `UI-142`, `UI-143`).
- Requirements catalog must be verified with zero drift: `dotnet run --project scripts/CatalogGenerator -- --verify-only`.
- Real screenshots must be captured using Puppeteer (`scripts/take_screenshots.js`) on `net_cloud` and saved under `docs/assets/` (no placeholders or mockups).
- Atomic commits for each logical task.

---

### Task 1: Core Scoring Engine & Data Models (`MCP-40`, `MCP-41`)

**Files:**
- Create: `Core/Routing/ToolSearchResult.cs`
- Modify: `Core/Routing/ToolRoutingManager.Search.cs:60-220`
- Test: `ModelContextGateway.Tests/ToolRoutingManagerHybridSearchTests.cs`

**Interfaces:**
- Consumes: `IEmbeddingProvider`, `IToolVectorStore`, `ToolMetadata`
- Produces: `ToolSearchResult`, `ToolRoutingManager.SearchToolsDetailedAsync(...)`

- [ ] **Step 1: Write the failing tests**
Create `ToolSearchResult` model and add unit tests in `ModelContextGateway.Tests/ToolRoutingManagerHybridSearchTests.cs`:
  - `HybridSearch_WithBalancedWeight_ComputesLinearCombinationScore`: tests $\alpha = 0.5$ combines dense cosine similarity and normalized lexical score.
  - `HybridSearch_WithSemanticMode_OnlyScoresDenseSimilarity`: tests `searchMode = "semantic"` or $\alpha = 1.0$.
  - `HybridSearch_WithLexicalMode_OnlyScoresLexicalMatch`: tests `searchMode = "lexical"` or $\alpha = 0.0$.
  - `HybridSearch_ReturnsDecomposedScores`: tests `DenseScore`, `SparseScore`, `DenseRank`, `SparseRank` populated correctly in $[0.0, 1.0]$.
  Annotate tests with `[Requirement("MCP-40", "MCP", RequirementType.Positive, "Calibrated hybrid semantic search scoring with configurable dense weight and normalized linear combination.")]` and `[Requirement("MCP-41", "MCP", RequirementType.Positive, "Explicit search modes (hybrid, semantic, lexical) and decomposed score diagnostics in ToolRoutingManager.")]`.

- [ ] **Step 2: Run tests to verify they fail**
```bash
dotnet test ModelContextGateway.slnx --filter "FullyQualifiedName~ToolRoutingManagerHybridSearchTests"
```

- [ ] **Step 3: Implement `ToolSearchResult` and `SearchToolsDetailedAsync`**
  - Define `ToolSearchResult` in `Core/Routing/ToolSearchResult.cs`.
  - In `Core/Routing/ToolRoutingManager.Search.cs`:
    - Implement `SearchToolsDetailedAsync(string query, List<object>? candidateTools, IEmbeddingProvider? embeddingProvider, IToolVectorStore? vectorStore, ILogger? logger, int limit = 15, string searchMode = "hybrid", double? denseWeight = 0.5, CancellationToken cancellationToken = default)`.
    - Handle `searchMode` ("hybrid", "semantic", "lexical") and clamp `denseWeight` to $[0.0, 1.0]$.
    - Compute min-max normalized lexical scores for candidates with matches.
    - Compute dense cosine similarity clamped to $[0.0, 1.0]$.
    - Calculate linear combination: $\text{Score} = \alpha \cdot \text{DenseScore} + (1.0 - \alpha) \cdot \text{SparseScore}$.
    - Return sorted `List<ToolSearchResult>` with populated ranks and scores.
    - Keep `SearchToolsAsync` as a backward-compatible wrapper returning `List<object>`.

- [ ] **Step 4: Run tests to verify they pass**
```bash
dotnet test ModelContextGateway.slnx --filter "FullyQualifiedName~ToolRoutingManagerHybridSearchTests"
```

- [ ] **Step 5: Commit work**
```bash
git add Core/Routing/ToolSearchResult.cs Core/Routing/ToolRoutingManager.Search.cs ModelContextGateway.Tests/ToolRoutingManagerHybridSearchTests.cs
git commit -m "feat(routing): implement calibrated hybrid scoring and explicit search modes (MCP-40, MCP-41)"
```

---

### Task 2: API Endpoint & Meta-Mode MCP Tool Integration (`MCP-41`, `MCP-42`)

**Files:**
- Modify: `Components/Capabilities/CapabilityEndpoints.cs:425-515`
- Modify: `Core/Routing/ToolRoutingManager.cs:50-80`
- Modify: `Core/Routing/ToolRoutingManager.Execution.cs:64-210`
- Test: `ModelContextGateway.Tests/SemanticSearchEndpointTests.cs`

**Interfaces:**
- Consumes: `ToolRoutingManager.SearchToolsDetailedAsync`
- Produces: Enhanced `POST /api/test/semantic-search` endpoint response and meta-mode `search_tools` tool execution

- [ ] **Step 1: Write the failing tests**
Add tests in `ModelContextGateway.Tests/SemanticSearchEndpointTests.cs`:
  - `SemanticSearchEndpoint_AcceptsModeAndDenseWeight_ReturnsDetailedScores`: tests `POST /api/test/semantic-search` with `{ query, mode, denseWeight, limit }` returning `{ query, mode, denseWeight, results: [{ tool, toolName, serverId, score, denseScore, sparseScore }] }`.
  - `MetaMode_SearchTools_AcceptsModeAndDenseWeight`: tests `tools/call` for `search_tools` with arguments `{ query: "...", mode: "semantic", dense_weight: 0.8 }`.
  Annotate tests with `[Requirement("MCP-41", ...)]` and `[Requirement("MCP-42", "MCP", RequirementType.Positive, "Meta-mode search_tools MCP tool accepts optional mode and dense_weight arguments and executes calibrated tool retrieval.")]`.

- [ ] **Step 2: Run tests to verify they fail**
```bash
dotnet test ModelContextGateway.slnx --filter "FullyQualifiedName~SemanticSearchEndpointTests"
```

- [ ] **Step 3: Implement endpoint and MCP tool parameters**
  - In `Components/Capabilities/CapabilityEndpoints.cs`:
    - Update `SearchModel` (or create `SemanticSearchRequest`) to include `Mode`, `DenseWeight`, `Limit`.
    - Route `POST /api/test/semantic-search` through `ToolRoutingManager.SearchToolsDetailedAsync`.
    - Return `Results.Ok(new { query = model.Query, mode = model.Mode, denseWeight = model.DenseWeight, results = scoredResults })`.
  - In `Core/Routing/ToolRoutingManager.cs`:
    - Update `search_tools` declaration `inputSchema` to include optional `mode` (string enum: `["hybrid", "semantic", "lexical"]`) and `dense_weight` (number, range 0.0 to 1.0).
  - In `Core/Routing/ToolRoutingManager.Execution.cs`:
    - Parse `mode` and `dense_weight` from `params.arguments`.
    - Pass to `SearchToolsAsync` / `SearchToolsDetailedAsync`.

- [ ] **Step 4: Run tests to verify they pass**
```bash
dotnet test ModelContextGateway.slnx --filter "FullyQualifiedName~SemanticSearchEndpointTests"
```

- [ ] **Step 5: Commit work**
```bash
git add Components/Capabilities/CapabilityEndpoints.cs Core/Routing/ToolRoutingManager.cs Core/Routing/ToolRoutingManager.Execution.cs ModelContextGateway.Tests/SemanticSearchEndpointTests.cs
git commit -m "feat(api): expose search mode and dense weight in semantic-search API and search_tools MCP tool (MCP-41, MCP-42)"
```

---

### Task 3: Frontend API & State Updates (`UI-142`)

**Files:**
- Modify: `frontend/src/shared/types.ts`
- Modify: `frontend/src/api/testbenchApi.ts:60-70`
- Modify: `frontend/src/components/testbench/useTestBenchState.ts:25-90`
- Test: `frontend/src/test/api/testbenchApi.test.ts`

**Interfaces:**
- Consumes: Backend `/api/test/semantic-search`
- Produces: `ToolSearchResultItem`, `SearchMode`, `semanticSearchApi`, `useTestBenchState` semantic controls

- [ ] **Step 1: Write the failing tests**
Update `frontend/src/test/api/testbenchApi.test.ts` to test calling `semanticSearchApi` with `query`, `mode`, `denseWeight`, and `limit`, receiving structured `results` with scores.
Annotate test with `@requirement UI-142`.

- [ ] **Step 2: Run tests to verify they fail**
```bash
npm --prefix frontend test frontend/src/test/api/testbenchApi.test.ts
```

- [ ] **Step 3: Implement types, API client, and hook state**
  - In `frontend/src/shared/types.ts`:
    - Add `SearchMode = 'hybrid' | 'semantic' | 'lexical'`.
    - Add `ToolSearchResultItem` interface with `tool`, `toolName`, `serverId`, `score`, `denseScore`, `sparseScore`, `denseRank`, `sparseRank`.
    - Add `SemanticSearchResponse` interface.
  - In `frontend/src/api/testbenchApi.ts`:
    - Update `semanticSearchApi(query: string, mode: SearchMode = 'hybrid', denseWeight: number = 0.5, limit: number = 15): Promise<SemanticSearchResponse>`.
  - In `frontend/src/components/testbench/useTestBenchState.ts`:
    - Add state: `searchMode`, `setSearchMode`, `denseWeight`, `setDenseWeight`, `searchLimit`, `setSearchLimit`.
    - Update `handleSemanticSearch` to pass these options to `semanticSearchApi` and store results.

- [ ] **Step 4: Run tests to verify they pass**
```bash
npm --prefix frontend test frontend/src/test/api/testbenchApi.test.ts
```

- [ ] **Step 5: Commit work**
```bash
git add frontend/src/shared/types.ts frontend/src/api/testbenchApi.ts frontend/src/components/testbench/useTestBenchState.ts frontend/src/test/api/testbenchApi.test.ts
git commit -m "feat(ui): add search mode, dense weight, and limit state to test bench (UI-142)"
```

---

### Task 4: Frontend UI: Semantic Router Simulator Redesign (`UI-142`, `UI-143`)

**Files:**
- Modify: `frontend/src/components/testbench/SemanticRouterCard.tsx`
- Modify: `frontend/src/components/testbench/TestBenchView.tsx`
- Modify: `frontend/src/styles/tester.css`
- Test: `frontend/src/test/components/SemanticRouterCard.test.tsx`

**Interfaces:**
- Consumes: `useTestBenchState` semantic state and `onSelectTool` navigation callback
- Produces: Redesigned `SemanticRouterCard` component with mode selector, split slider, presets, score chips, and "Test Tool" action

- [ ] **Step 1: Write the failing tests**
Create `frontend/src/test/components/SemanticRouterCard.test.tsx`:
  - Verifies mode selector toggles between `hybrid`, `semantic`, and `lexical`.
  - Verifies hybrid calibration slider and quick preset chips appear in hybrid mode.
  - Verifies preset buttons update `denseWeight` (`50/50`, `70/30`, `30/70`, `100/0`, `0/100`).
  - Verifies limit dropdown changes `limit`.
  - Verifies score breakdown badges (`Total %`, `Semantic %`, `Keyword %`) render with formatted percentages.
  - Verifies clicking **"Test Tool"** dispatches `onSelectTool(serverId, toolName)`.
  Annotate tests with `@requirement UI-142` and `@requirement UI-143`.

- [ ] **Step 2: Run tests to verify they fail**
```bash
npm --prefix frontend test frontend/src/test/components/SemanticRouterCard.test.tsx
```

- [ ] **Step 3: Implement `SemanticRouterCard.tsx` & styles**
  - Implement 3-way mode toggle:
    `<button className={searchMode === 'hybrid' ? 'active' : ''} onClick={() => onModeChange('hybrid')}>Hybrid Fusion</button>`
  - Implement hybrid split slider & dynamic label:
    `<input type="range" min="0" max="1" step="0.05" value={denseWeight} onChange={...} />`
    `<span>Semantic ${Math.round(denseWeight * 100)}% / Keyword ${Math.round((1 - denseWeight) * 100)}%</span>`
  - Implement preset chips (`Balanced (50/50)`, `Semantic Bias (70/30)`, `Keyword Bias (30/70)`, `Pure Semantic (100/0)`, `Pure Keyword (0/100)`).
  - Implement limit selector dropdown.
  - Implement result hit cards with badges:
    `<span className="badge badge-score-total">Score: ${(item.score * 100).toFixed(1)}%</span>`
    `<span className="badge badge-score-dense">Semantic: ${(item.denseScore * 100).toFixed(1)}%</span>`
    `<span className="badge badge-score-sparse">Keyword: ${(item.sparseScore * 100).toFixed(1)}%</span>`
  - Implement "Test Tool" button calling `onSelectTool(item.serverId, item.toolName)`.
  - In `TestBenchView.tsx`, pass `onSelectTool = (serverId, toolName) => { setSelectedToolServer(serverId); setSelectedToolName(toolName); setActiveTab('tools'); }`.
  - Add styles in `frontend/src/styles/tester.css` using centralized CSS variables.

- [ ] **Step 4: Run tests to verify they pass**
```bash
npm --prefix frontend test frontend/src/test/components/SemanticRouterCard.test.tsx
```

- [ ] **Step 5: Run linter and typecheck**
```bash
npm --prefix frontend run lint
npm --prefix frontend run build
```

- [ ] **Step 6: Commit work**
```bash
git add frontend/src/components/testbench/SemanticRouterCard.tsx frontend/src/components/testbench/TestBenchView.tsx frontend/src/styles/tester.css frontend/src/test/components/SemanticRouterCard.test.tsx
git commit -m "feat(ui): redesign semantic router simulator with mode toggle, slider presets, score badges, and tool testing transition (UI-142, UI-143)"
```

---

### Task 5: Layout Inspector Audit & E2E Tests (`UI-07`, `UI-141`)

**Files:**
- Modify: `frontend/e2e/layout-inspector.spec.ts`
- Modify: `frontend/e2e/testbench.spec.ts`

- [ ] **Step 1: Update Playwright tests**
  - Add test in `frontend/e2e/testbench.spec.ts` testing the Semantic Router simulator:
    - Navigates to `Semantic Router` tab.
    - Selects `Semantic (Vector)` mode.
    - Selects `Hybrid Fusion` mode, clicks `Semantic Bias (70/30)` preset chip.
    - Inputs query "search media" and submits search.
    - Verifies score badges appear.
    - Clicks "Test Tool" on first hit and verifies browser navigates to `Tools` tab with tool preselected.
  - Verify layout inspector in `frontend/e2e/layout-inspector.spec.ts` passes with 0 layout overflows across Desktop, Half-Ultrawide 1440p, and Half-Ultrawide 1080p.

- [ ] **Step 2: Run Playwright tests**
```bash
npx playwright test --config=frontend/playwright.config.ts
```

- [ ] **Step 3: Commit work**
```bash
git add frontend/e2e/layout-inspector.spec.ts frontend/e2e/testbench.spec.ts
git commit -m "test(e2e): add semantic router simulator interaction and multi-viewport layout inspector tests (UI-07, UI-141)"
```

---

### Task 6: Documentation, Real Screenshots, Version Bump & Full Verification

**Files:**
- Modify: `README.md`
- Modify: `ARCHITECTURE.md`
- Modify: `docs/features-guide.md`
- Modify: `ModelContextGateway.csproj`
- Modify: `frontend/src/stores/useUserStore.ts`
- Modify: `CHANGELOG.md`
- Generate: `docs/requirements-catalog.json`, `docs/software-requirements-and-test-catalog.md`
- Capture: `docs/assets/semantic-router-simulator.png` (via Puppeteer script)

- [ ] **Step 1: Update documentation**
  - Update `ARCHITECTURE.md` with the calibrated hybrid scoring formula and search modes.
  - Update `docs/features-guide.md` with instructions on using the Semantic Router simulator, adjusting the slider, and using presets.
- [ ] **Step 2: Version bump to 5.21.0**
  - `ModelContextGateway.csproj`: `<Version>5.21.0</Version>`, `<AssemblyVersion>5.21.0</AssemblyVersion>`, `<FileVersion>5.21.0</FileVersion>`.
  - `frontend/src/stores/useUserStore.ts`: fallback version string `'5.21.0'`.
  - `CHANGELOG.md`: add release entry for `5.21.0` (feat: calibrated hybrid scoring, search modes, score decomposition, and redesigned semantic router simulator).
  - `README.md`: update top-5 release preview table.
- [ ] **Step 3: Regenerate and verify requirements catalog**
```bash
dotnet run --project scripts/CatalogGenerator
dotnet run --project scripts/CatalogGenerator -- --verify-only
```
- [ ] **Step 4: Run full verification test suites**
```bash
dotnet test ModelContextGateway.slnx
npm --prefix frontend test
npm --prefix frontend run lint
npm --prefix frontend run build
```
- [ ] **Step 5: Capture live screenshots**
  - Build and start container on `net_cloud`: `docker compose up -d --build mcg`
  - Run Puppeteer script to capture real screenshot of the redesigned Semantic Router Simulator to `docs/assets/semantic-router-simulator.png`.
- [ ] **Step 6: Commit release bump and documentation**
```bash
git add ModelContextGateway.csproj frontend/src/stores/useUserStore.ts CHANGELOG.md README.md ARCHITECTURE.md docs/
git commit -m "chore(release): bump version to 5.21.0, update docs, and capture live screenshots"
```
