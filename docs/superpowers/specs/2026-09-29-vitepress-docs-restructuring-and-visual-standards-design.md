# Design Specification: VitePress Documentation Restructuring & Visual Standards Overhaul

**Date:** 2026-09-29  
**Status:** Approved for Planning (Execution on Hold)  
**Scope:** Architecture, Information Architecture (IA), Visual Standards, Page Decomposition, and Quality Gates for Model Context Gateway (MCG) VitePress documentation.

---

## 1. Problem Statement & Motivation

1. **Information Density & Overwhelming Navigation**: The current VitePress documentation maintains a single monolithic sidebar (`docs/.vitepress/config.mts`) containing over 60 navigation links across unrelated domains (Deployments, User Guides, Architecture, Operations, SRS). Navigating to any topic subjects the reader to a dense vertical wall of links.
2. **Monolithic "Kitchen-Sink" Guides**: Several key articles (e.g., `features-guide.md`, `active-directory-and-rbac-guide.md`, `mcp-routing-and-admin-issues.md`) exceed 1,000–1,500 lines, conflating high-level overviews, tutorials, step-by-step how-to procedures, and raw reference tables into single unmaintainable files.
3. **Legacy ASCII Box Diagrams**: Multiple pages still rely on ASCII art diagrams (`+-----+ ---> +-----+`). On mobile screens and high-DPI displays, these break layout boundaries, wrap irregularly, and cannot be searched or styled cleanly.
4. **Poorly Sized Mermaid Diagrams**: Many Mermaid diagrams render either infinitely wide or with microscopic, illegible text labels due to unconstrained viewport scaling.
5. **Section Rhythm & Vertical Spacing**: Heading margins, code blocks, tables, and admonition callouts (`::: info`, `::: tip`, `::: warning`) lack consistent spacing rhythm, causing cramped layouts.
6. **Link Rot & Broken Anchors**: Stale relative links and anchor slugs exist across moved pages, masked by `ignoreDeadLinks: 'localhostLinks'`.

---

## 2. Goals & Non-Goals

### Goals
- Implement **path-scoped multi-sidebars** in VitePress so each domain (`/getting-started/`, `/user-guide/`, `/architecture/`, `/operations/`, `/reference/`) presents only 4–8 focused, contextually relevant navigation links.
- Decompose monolithic pages exceeding 800 lines into modular, single-responsibility articles following the **Diátaxis documentation framework** (Tutorials, How-To Guides, Reference, Explanation).
- Enforce a **Zero ASCII Art Policy**: Replace all ASCII flowcharts and boxes with responsive Mermaid diagrams or authentic retina screenshots captured via the automated Puppeteer tool (`scripts/take_screenshots.js`).
- Standardize Mermaid diagram containers in `docs/.vitepress/theme/custom.css` with responsive max-width rules, centered layout, and accessible typography using `JetBrains Mono`.
- Establish a consistent vertical rhythm (`h2`, `h3`, admonitions, code blocks, tables).
- Turn on strict dead-link checking in `docs/.vitepress/config.mts` and add `npm run docs:build` as an automated CI quality gate.

### Non-Goals
- Altering the backend C# ASP.NET Core gateway code or frontend React dashboard application code.
- Removing technical content or specifications from the documentation (all technical material is preserved and organized).

---

## 3. Information Architecture (IA) & Multi-Sidebar Design

### 3.1 Top-Level Navigation
VitePress top navigation bar links to 5 core domains:
1. **Getting Started** (`/getting-started/`): Overview, quickstarts, and deployment topologies.
2. **User Guide** (`/user-guide/`): Dashboard walkthroughs, IDE client configurations, interactive Test Bench, and settings.
3. **Architecture & Security** (`/architecture/`): Component boundaries, transports, authorization pipeline, enterprise auth, secret management, and data model.
4. **Operations** (`/operations/`): Administrator guide, Admin MCP automation, operational runbooks, and incident RCA playbooks.
5. **Reference** (`/reference/`): Software Requirements Specification (SRS), Test Catalog, Support Matrix, Database Providers, and Admin MCP API reference.

### 3.2 Path-Scoped Sidebars (`config.mts`)
Configure VitePress `themeConfig.sidebar` as an object keyed by URL paths:
- `'/getting-started/'`: Overview, Docker setup, Windows IIS, Windows Service, Homelab setup, Support Matrix.
- `'/user-guide/'`: Dashboard navigation, Servers & Secrets, RBAC & Policies, AppKey management, Client guides (Claude Desktop, Cursor, Cline/VS Code, Antigravity), Test Bench simulator.
- `'/architecture/'`: System overview, Transports & Subprocesses, Routing & Meta-Mode, Authorization Pipeline, Active Directory & LDAP, Per-User OAuth & Delegation, Database & Encryption, Secret Providers.
- `'/operations/'`: Admin Guide, Admin MCP Tools Reference, Admin Automation, Operations Runbook, Troubleshooting Playbooks.
- `'/reference/'`: Software Requirements (SRS), Test Catalog, Database Providers, Support Matrix, Code Coverage.

---

## 4. Page Decomposition & Structure

Following the Diátaxis framework, oversized documents (>800 lines) are decomposed into focused articles:

1. **`features-guide.md` (1,400+ lines)**:
   - Converted into a concise landing page featuring interactive glassmorphic cards linking to:
     - `/user-guide/dashboard.md`
     - `/user-guide/servers.md`
     - `/user-guide/app-keys.md`
     - `/architecture/routing-and-meta-mode.md`
2. **`active-directory-and-rbac-guide.md` (1,100+ lines)**:
   - Split into:
     - `/architecture/security/active-directory-ldap.md` (Domain controller setup, LDAPS, service credentials)
     - `/architecture/security/rbac-and-policies.md` (Group mappings, SID evaluation, role definitions)
     - `/architecture/security/windows-integrated-auth.md` (Negotiate/Kerberos, Windows Service hosting)
3. **`mcp-routing-and-admin-issues.md` (1,300+ lines)**:
   - Split into `/operations/troubleshooting/`:
     - `subprocess-and-stdio.md` (Transport timeouts, SIGTERM, buffer saturation)
     - `auth-and-token-failures.md` (OIDC audience mismatch, AppKey expiry, expired secrets)
     - `database-locks-and-migrations.md` (SQLite write locks, DDL migration recovery)

---

## 5. Visual Standards & CSS Typography Rhythm

### 5.1 Zero ASCII Art Policy
All ASCII box drawings are converted to:
- Mermaid flowcharts (`flowchart LR`, `flowchart TD`)
- Mermaid sequence diagrams (`sequenceDiagram`)
- High-fidelity screenshots under `docs/assets/` captured via `scripts/take_screenshots.js`.

### 5.2 Mermaid Container & Typography Styling (`custom.css`)
```css
/* docs/.vitepress/theme/custom.css */
.vp-doc .mermaid {
  display: flex;
  justify-content: center;
  align-items: center;
  margin: 2.5rem auto;
  max-width: 100%;
  overflow-x: auto;
  background: rgba(255, 255, 255, 0.02);
  border: 1px solid var(--vp-c-divider);
  border-radius: 8px;
  padding: 1.5rem;
}

.vp-doc .mermaid svg {
  max-width: 100%;
  height: auto;
  font-family: 'JetBrains Mono', monospace !important;
}
```

### 5.3 Typography Spacing Rhythm
- `h2`: Margin top `3rem`, margin bottom `1rem`, clear border-bottom divider.
- `h3`: Margin top `2.25rem`, margin bottom `0.75rem`.
- Admonitions (`.custom-block`): Margin `1.75rem 0`, border-radius `8px`, glassmorphic backdrop.
- Tables: Sticky header row, alternating subtle background rows, padding `10px 16px`.

---

## 6. Link Validation & Quality Gates

1. **Remove Lenient Dead Links**: Remove `ignoreDeadLinks: 'localhostLinks'` from `config.mts`.
2. **Build Validation**: Ensure `npm run docs:build` passes cleanly with zero dead link errors and zero markdown syntax warnings.
3. **CI Integration**: Add documentation verification step to `.github/workflows/ci.yml` or relevant PR workflow.
