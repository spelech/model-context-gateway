# Semantic Search Hybrid Calibration & Scoring Alignment Design Spec

## 1. Context & Motivation

Model Context Gateway (MCG) routes natural language queries and tool invocations to backend MCP servers. In meta-mode (`/sse`), the gateway exposes `search_tools` and `execute_tool` to AI agents to prevent context bloat. In the admin dashboard, the **Test Bench** provides a **Semantic Router Simulator** (`SemanticRouterCard.tsx`) to evaluate tool retrieval against simulated natural language user intents.

Recently, **ContextCortex** (PR #36) addressed hybrid search calibration, dense/sparse weighting, and search inspector UX. Comparing ContextCortex with MCG revealed key opportunities for alignment:
1. **Fixed Fusion Without Tuning**: MCG currently uses Reciprocal Rank Fusion (RRF, $k=60$) with an unweighted 50/50 reciprocal rank sum in `ToolRoutingManager.Search.cs`, or legacy unnormalized cosine+boosting in `SemanticSearchService.cs`. Users cannot adjust the balance between dense vector similarity and lexical keyword matching.
2. **Missing Search Modes**: Search is restricted to hybrid fallback; users and AI agents cannot explicitly request pure semantic (dense vector only) or pure lexical (keyword only) retrieval.
3. **Score Discarding & Opacity**: Search returns a raw list of tool objects, discarding the computed match scores. In the UI, the Semantic Router Simulator only shows `Rank #1`, `Rank #2`, with zero visibility into semantic similarity or keyword scores.
4. **Test Bench Workflow Gap**: When the simulator finds a tool, there is no direct action to transition to the Tool Tester with that tool pre-selected.

This specification aligns MCG's semantic search and simulator with ContextCortex's calibrated hybrid model, explicit search modes, decomposed score diagnostics, and enhanced test bench UX.

---

## 2. Goals & Non-Goals

### Goals
- **Configurable Hybrid Calibration**: Support dynamic dense weight $\alpha \in [0.0, 1.0]$ (default $0.5$ balanced) across backend search, HTTP APIs, MCP tools, and Web UI.
- **Explicit Search Modes**: Support `hybrid`, `semantic` (pure dense vector similarity), and `lexical` (pure keyword matching).
- **Normalized Linear Combination Scoring**: Normalize dense cosine similarity and lexical match scores into $[0.0, 1.0]$, computing:
  $$\text{score} = \alpha \cdot \text{dense\_score} + (1.0 - \alpha) \cdot \text{sparse\_score}$$
- **Score Transparency & Diagnostics**: Expose decomposed score components (`score`, `dense_score`, `sparse_score`, `dense_rank`, `sparse_rank`) in search result models, HTTP endpoints, and the UI.
- **Enhanced Test Bench UI (`SemanticRouterCard.tsx`)**:
  - 3-way search mode toggle (`Hybrid`, `Semantic`, `Lexical`).
  - Interactive hybrid split slider with dynamic label (`Semantic X% / Keyword Y%`) and presets (`Balanced 50/50`, `Semantic Bias 70/30`, `Keyword Bias 30/70`, `Pure Semantic 100/0`, `Pure Keyword 0/100`).
  - Result limit selector (`5`, `10`, `15`, `25`).
  - Score breakdown badges on result cards (`Total %`, `Semantic %`, `Keyword %`).
  - 1-click **"Test Tool"** action button that transitions to the Tools tab and preselects the tool in the Tool Tester form.
- **Meta-Mode MCP Tool Enhancement**: Update `search_tools` to accept optional `mode` and `dense_weight` parameters while remaining 100% backward compatible.
- **Real Screenshots & Documentation**: Capture live screenshots of the updated simulator with real tools and update documentation (`README.md`, `ARCHITECTURE.md`, `docs/features-guide.md`).

### Non-Goals
- Modifying underlying embedding models or ONNX runtimes.
- Breaking backward compatibility with existing `search_tools(query: "...")` calls.
- Modifying custom tool registry or non-search routing logic.

---

## 3. Architecture & Detailed Design

### 3.1 Backend Models & Data Structures
In `Core/VectorSearch/ToolSearchResult.cs` (or `Models/Routing/ToolSearchResult.cs`):
```csharp
namespace ModelContextGateway.Core.Routing
{
    public class ToolSearchResult
    {
        public required object Tool { get; init; }
        public required string ToolName { get; init; }
        public string? ServerId { get; init; }
        public double Score { get; init; }         // Fused score: 0.0 to 1.0
        public double? DenseScore { get; init; }   // Vector similarity: 0.0 to 1.0
        public double? SparseScore { get; init; }  // Normalized lexical score: 0.0 to 1.0
        public int? DenseRank { get; init; }
        public int? SparseRank { get; init; }
    }
}
```

### 3.2 Hybrid Scoring & Weight Calibration
In `Core/Routing/ToolRoutingManager.Search.cs`:
1. **Search Modes**:
   - `hybrid`: Computes both lexical and dense scores, then fuses using weight $\alpha = \max(0.0, \min(1.0, \text{denseWeight} ?? 0.5))$.
   - `semantic`: Computes dense vector similarity only ($\alpha = 1.0$).
   - `lexical`: Computes keyword lexical match only ($\alpha = 0.0$).
2. **Score Normalization & Fusion**:
   - **Dense Score**: Cosine similarity between query embedding and tool embedding, clamped to $[0.0, 1.0]$.
   - **Sparse Score**: Lexical score calculated via `CalculateLexicalScore`. Scores are min-max normalized across all non-zero candidates in the batch to $[0.0, 1.0]$. If only one candidate matches, normalized score is $1.0$.
   - **Combined Score**:
     $$\text{FinalScore} = \alpha \cdot \text{DenseScore} + (1.0 - \alpha) \cdot \text{SparseScore}$$
3. **Methods**:
   - `SearchToolsDetailedAsync(...)`: Returns `List<ToolSearchResult>` with complete diagnostics.
   - `SearchToolsAsync(...)`: Backwards-compatible wrapper returning `List<object>`.

### 3.3 HTTP API (`POST /api/test/semantic-search`)
In `Components/Capabilities/CapabilityEndpoints.cs`:
- Request schema:
  ```json
  {
    "query": "string (required)",
    "mode": "hybrid | semantic | lexical (optional, default hybrid)",
    "denseWeight": 0.5,
    "limit": 15
  }
  ```
- Response schema:
  ```json
  {
    "query": "search movie",
    "mode": "hybrid",
    "denseWeight": 0.5,
    "results": [
      {
        "tool": { "name": "plex__search_media", "description": "..." },
        "toolName": "plex__search_media",
        "serverId": "plex",
        "score": 0.852,
        "denseScore": 0.910,
        "sparseScore": 0.794,
        "denseRank": 1,
        "sparseRank": 2
      }
    ]
  }
  ```

### 3.4 MCP Tool `search_tools` in Meta-Mode
In `Core/Routing/ToolRoutingManager.cs` & `Core/Routing/ToolRoutingManager.Execution.cs`:
- Tool schema definition updated in `tools/list`:
  - `query` (string, required): Search query or intent description.
  - `mode` (string, optional, enum: `["hybrid", "semantic", "lexical"]`): Search mode.
  - `dense_weight` (number, optional, range: `0.0` - `1.0`): Weight between vector (1.0) and lexical (0.0).
- Tool execution:
  - Extracts optional `mode` and `dense_weight` from `params.arguments`.
  - Executes calibrated search and returns matching tools.

### 3.5 Frontend UI: `SemanticRouterCard.tsx` & Test Bench
1. **Mode Controls**:
   - Segmented buttons for `Hybrid Fusion`, `Semantic (Vector)`, and `Lexical (Keyword)`.
2. **Hybrid Calibration Slider**:
   - Displayed conditionally when `mode === 'hybrid'`.
   - Range input `0.0` to `1.0` (step `0.05`).
   - Live label: `Semantic X% / Keyword Y%`.
   - Preset chips:
     - `Balanced 50/50`
     - `Semantic Bias 70/30`
     - `Keyword Bias 30/70`
     - `Pure Semantic 100/0`
     - `Pure Keyword 0/100`
3. **Limit Filter**:
   - Dropdown with `5`, `10`, `15`, `25`.
4. **Enhanced Result Cards**:
   - Header with tool name and server badge.
   - Score badges:
     - `Total Score: XX.X%` (accent color)
     - `Semantic: XX.X%` (cyan/blue badge)
     - `Keyword: XX.X%` (purple badge)
   - Formatted tool description with clean markdown rendering.
   - **"Test Tool"** button: clicking dispatches `onSelectTool(serverId, toolName)`, switching the Test Bench to the `Tools` tab and preselecting the server & tool in `ToolTesterCard`.

---

## 4. Documentation & Visual Assets Plan

1. **Features Guide (`docs/features-guide.md`)**:
   - Document the calibrated hybrid scoring model, search modes, and slider usage.
2. **Architecture Guide (`ARCHITECTURE.md`)**:
   - Detail the normalized linear combination formula and score decomposition.
3. **Real Screenshots (`docs/assets/`)**:
   - Capture real screenshots of the Semantic Router Simulator showing the mode selector, hybrid slider with presets, and score badges on live tools.

---

## 5. Testing & Verification Plan

### Backend Tests (`ModelContextGateway.Tests`)
- `ToolRoutingManagerHybridSearchTests.cs`:
  - Test pure semantic mode ($\alpha = 1.0$).
  - Test pure lexical mode ($\alpha = 0.0$).
  - Test hybrid calibration with varying `denseWeight` ($0.2, 0.5, 0.8$).
  - Test score decomposition: verify `DenseScore`, `SparseScore`, `Score` are in $[0.0, 1.0]$.
  - Test `POST /api/test/semantic-search` endpoint contract.
  - Test `search_tools` MCP execution with custom `mode` and `dense_weight`.
  - All tests annotated with formal requirement attributes (`ROUT-XX`, `MCP-XX`).

### Frontend Tests (`frontend/src/test`)
- `SemanticRouterCard.test.tsx`:
  - Verify mode toggle changes mode state.
  - Verify hybrid slider and presets adjust `denseWeight`.
  - Verify score badges render formatted percentages.
  - Verify "Test Tool" button triggers callback with server and tool.
- E2E layout audit (`frontend/e2e/layout-inspector.spec.ts`):
  - Audit Semantic Router tab across desktop, half-ultrawide 1440p, and half-ultrawide 1080p viewports.

### Verification Gate
- Run `CatalogGenerator` and verify zero requirements drift (`--verify-only`).
