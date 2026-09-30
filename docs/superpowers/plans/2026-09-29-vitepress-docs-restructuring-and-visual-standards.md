# VitePress Documentation Restructuring & Visual Standards Overhaul Implementation Plan

> **Note:** Execution of this plan is on hold for a future feature track. This document specifies the complete task breakdown ready for future hand-off.

**Goal:** Transform the monolithic VitePress documentation into a clean, modular technical site featuring path-scoped multi-sidebars, decomposed Diátaxis articles under 800 lines, zero ASCII diagrams, responsive styled Mermaid visualizations, consistent typography rhythm, and strict dead-link CI gates.

**Architecture:** Re-structure `docs/` directories into 5 primary domains (`getting-started/`, `user-guide/`, `architecture/`, `operations/`, `reference/`). Configure path-scoped multi-sidebars in `config.mts`. Update `custom.css` for Mermaid container responsiveness and typography spacing. Purge all ASCII diagrams into Mermaid/screenshots. Enable strict dead-link validation in `npm run docs:build`.

**Tech Stack:** VitePress, Vue 3, Vite, Mermaid, Markdown, CSS3, GitHub Actions.

---

### Task 1: Information Architecture & Path-Scoped Multi-Sidebar Configuration

**Files:**
- Modify: `docs/.vitepress/config.mts`
- Create / Move: Directory structures under `docs/` (`docs/getting-started/`, `docs/user-guide/`, `docs/architecture/`, `docs/operations/`, `docs/reference/`)

**Steps:**
1. Configure `themeConfig.nav` with the 5 primary top-level tabs: Getting Started, User Guide, Architecture & Security, Operations, Reference.
2. Convert `themeConfig.sidebar` from a monolithic array into a path-scoped object mapping:
   - `'/getting-started/'`
   - `'/user-guide/'`
   - `'/architecture/'`
   - `'/operations/'`
   - `'/reference/'`
3. Update landing pages (`index.md`) in each subfolder to act as structured domain overviews.
4. Verify local development server navigation (`npm run docs:dev`).

---

### Task 2: Page Decomposition (Diátaxis Framework)

**Files:**
- Split: `docs/features-guide.md` -> concise landing page linking to sub-guides
- Split: `docs/active-directory-and-rbac-guide.md` ->
  - `docs/architecture/security/active-directory-ldap.md`
  - `docs/architecture/security/rbac-and-policies.md`
  - `docs/architecture/security/windows-integrated-auth.md`
- Split: `docs/mcp-routing-and-admin-issues.md` ->
  - `docs/operations/troubleshooting/subprocess-and-stdio.md`
  - `docs/operations/troubleshooting/auth-and-token-failures.md`
  - `docs/operations/troubleshooting/database-locks-and-migrations.md`

**Steps:**
1. Extract procedural and conceptual sections into their respective target files.
2. Verify all extracted files remain under 800 lines with single clear responsibilities.
3. Update internal references and breadcrumb links.

---

### Task 3: Zero ASCII Art Migration & Mermaid Standardization

**Files:**
- Modify: All markdown files containing ASCII box art (`grep -rn "+\-\-\-\-+" docs/`)
- Modify: `docs/.vitepress/theme/custom.css`
- Modify: `docs/.vitepress/theme/mermaid-panzoom.ts`

**Steps:**
1. Identify all ASCII box diagrams and replace them with standard Mermaid syntax (`flowchart LR`, `sequenceDiagram`, `classDiagram`) or authentic screenshots from `docs/assets/`.
2. Add `.vp-doc .mermaid` CSS rules in `custom.css` enforcing max-width, center alignment, border styling, and `JetBrains Mono` typography.
3. Ensure diagrams render cleanly without horizontal scroll clipping on both mobile viewports and large desktops.

---

### Task 4: Typography Rhythm, Admonitions & Layout Spacing

**Files:**
- Modify: `docs/.vitepress/theme/custom.css`

**Steps:**
1. Increase vertical margins for `h2` (`3rem 0 1rem`) and `h3` (`2.25rem 0 0.75rem`).
2. Add spacing and styling to callout blocks (`.custom-block` / admonitions) to ensure breathing room around critical warnings.
3. Add sticky headers and padded cells for large tables.
4. Verify visual hierarchy and reading comfort across light/dark modes.

---

### Task 5: Link Audit, Build Validation & CI Quality Gate

**Files:**
- Modify: `docs/.vitepress/config.mts`
- Modify: `.github/workflows/ci.yml` (or documentation workflow)

**Steps:**
1. Remove `ignoreDeadLinks: 'localhostLinks'` from `config.mts`.
2. Run `npm run docs:build` to reveal all dangling anchors and broken relative links.
3. Fix all broken links and normalize anchor slugs.
4. Add `npm run docs:build` as a mandatory quality gate step in CI.
