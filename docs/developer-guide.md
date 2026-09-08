# Developer & Contributor Guide

This document provides instructions for developers. It explains setup steps, architecture rules, coding standards, testing procedures, version rules, and release checks for the **Model Context Gateway (MCG) & Semantic Proxy**.

---

## 📑 Table of Contents

- [Prerequisites & Development Environment](#prerequisites-development-environment)
- [Repository Structure & Architecture Conventions](#repository-structure-architecture-conventions)
- [Local Development Workflow](#local-development-workflow)
  - [Backend (.NET 10 C#)](#backend-net-10-c)
  - [Frontend (React 19 / Vite / TypeScript)](#frontend-react-19-vite-typescript)
- [Automated Testing & Code Coverage](#automated-testing-code-coverage)
  - [Backend Test Suite](#backend-test-suite)
  - [Frontend Vitest Suite](#frontend-vitest-suite)
  - [End-to-End Testing (Playwright)](#end-to-end-testing-playwright)
- [Formatting, Linting & Static Analysis](#formatting-linting-static-analysis)
- [Version Synchronization & Release Verification](#version-synchronization-release-verification)
  - [Mandatory Version Synchronization Contract](#mandatory-version-synchronization-contract)
  - [Release Verification Script (`verify-release.sh`)](#release-verification-script-verify-releasesh)
  - [CLI Flags & Options Reference](#cli-flags-options-reference)
  - [Automated Version Bumping & Atomic Commits](#automated-version-bumping-atomic-commits)
- [Continuous Integration & Quality Gates](#continuous-integration-quality-gates)

---

## 🛠️ Prerequisites & Development Environment

Install these tools before you start development:

| Tool / Runtime | Minimum Version | Purpose |
| :--- | :--- | :--- |
| **.NET SDK** | `10.0.x` | Compiles the C# backend, Minimal APIs, Dapper repositories, and xUnit tests. |
| **Node.js** | `22.x LTS` | Runs the Vite development server, ESLint v10, Vitest, and the React 19 UI build. |
| **npm** | `10.x+` | Manages packages for the frontend Single Page Application (SPA). |
| **Python** | `3.10+` | Runs release verification and automated version bump scripts. |
| **Docker** | `24.x+` | Builds multi-stage container images and runs integration test environments. |

---

## 🏛️ Repository Structure & Architecture Conventions

The repository organizes code into these domains:

```
├── Components/                 # Decomposed domain modules & Minimal API mappers
│   ├── AppKeys/                # High-entropy AppKey models, crypto, and endpoints
│   ├── Authorization/          # RBAC access policies, group mappings, and controllers
│   ├── Capabilities/           # Proxy endpoints, SSE/HTTP mappers, and custom tools
│   ├── Clients/                # Registered client profiles and setup guide generators
│   ├── Providers/              # Identity and secret provider configurations & controllers
│   └── Servers/                # Upstream server registry, health checks, and discovery
├── Core/                       # Core routing engine & MCP protocol handlers
│   ├── Protocol/               # JSON-RPC 2.0 message contracts and spec models
│   └── Routing/                # ClientSession, DynamicEmbeddingService, SemanticSearch
├── Infrastructure/             # Persistence, secrets, identity, and logging adapters
│   ├── Identity/               # Active Directory (LDAP), OIDC, and AppKey providers
│   ├── Logging/                # PII sanitization, structured audit logging, ring buffers
│   ├── Persistence/            # DbConnectionFactory & Repositories (see [Database ERD](database-providers.md#unified-database-entity-relationship-diagram-erd))
│   ├── Secrets/                # Vault KV v2, DPAPI, Environment, and AES-256-GCM crypto
│   └── Transports/             # SseTransport, HttpTransport, and StdioTransport
├── frontend/                   # React 19 + Vite + TypeScript glassmorphic SPA
│   ├── src/api/                # Typed API client layer
│   ├── src/components/         # Domain-decomposed UI views, modals, and tabs
│   ├── src/shared/stores/      # Zustand state management stores
│   └── src/test/               # Vitest component, store, and unit test suites
├── ModelContextGateway.Tests/   # 600+ xUnit integration, security, and contract tests
├── scripts/                    # Release verification, version bumping, and DB DDL scripts
└── docs/                       # Architectural specifications and user guides
```

---

## 💻 Local Development Workflow

### Backend (.NET 10 C#)

1. **Restore dependencies & build**:
   ```bash
   dotnet restore ModelContextGateway.slnx
   dotnet build ModelContextGateway.slnx --configuration Debug
   ```

2. **Run the gateway locally**:
   ```bash
   # Runs on http://localhost:8080 with ephemeral SQLite database
   dotnet run --project ModelContextGateway.csproj
   ```

3. **Configure environment overrides**:
   ```bash
   # Custom database path and admin bypass SID
   MCG_DATABASE_PATH="./data/dev.db" \
   MCG_MASTER_KEY="dev-master-key-32-chars-long!" \
   dotnet run --project ModelContextGateway.csproj
   ```

### Frontend (React 19 / Vite / TypeScript)

1. **Install dependencies**:
   ```bash
   cd frontend
   npm ci
   ```

2. **Start Vite development server**:
   ```bash
   npm run dev
   ```
   The development proxy forwards `/api`, `/sse`, and `/mcp` traffic directly to `http://localhost:8080`.

3. **Build production bundle**:
   ```bash
   npm run build
   ```

---

## 🧪 Automated Testing & Code Coverage

### Backend Test Suite

The C# test suite contains more than 600 unit, integration, and security contract tests:

```bash
# Run all backend tests
CI=true dotnet test ModelContextGateway.slnx --configuration Release

# Collect code coverage
CI=true dotnet test ModelContextGateway.slnx --configuration Release --collect:"XPlat Code Coverage"
```

### Frontend Vitest Suite

The frontend test suite validates Zustand stores, typed API handlers, and React components:

```bash
cd frontend

# Run test suite once
npm test

# Run tests with coverage
npm run test:coverage
```

### End-to-End Testing (Playwright)

Run user workflows across multi-user security matrices:

```bash
cd frontend
npx playwright test
```

### Living Software Requirements Specification (SRS) & Test Catalog

Tests in C# and TypeScript include annotations for requirements and safety guardrails. Use these commands to generate or verify the catalog:

```bash
# Generate human-readable Markdown and machine JSON matrix
dotnet run --project scripts/CatalogGenerator

# Verify zero-drift in CI quality gates
dotnet run --project scripts/CatalogGenerator -- --verify-only
```

* **Living SRS Document:** [`docs/software-requirements-and-test-catalog.md`](software-requirements-and-test-catalog.md)
* **Test Catalog & Annotation Guide:** [`docs/test-catalog-guide.md`](test-catalog-guide.md)

---

## 🎨 Formatting, Linting & Static Analysis

1. **C# Backend Formatting & Roslyn Analyzers**:
   ```bash
   dotnet format ModelContextGateway.slnx --verify-no-changes
   ```
   Configuration rules reside in `.editorconfig` and `Directory.Build.props`.

2. **Frontend ESLint (Zero-Warning Policy)**:
   ```bash
   cd frontend
   npm run lint
   ```
   This command uses the ESLint v10 configuration in `frontend/eslint.config.js`. You must resolve all warnings.

---

## 🚀 Version Synchronization & Release Verification

### Mandatory Version Synchronization Contract

Every release, pull request, and commit to `main` must update the version number in **four required locations**:

1. **`ModelContextGateway.csproj`**:
   - `<Version>X.Y.Z</Version>`
   - `<AssemblyVersion>X.Y.Z.0</AssemblyVersion>`
   - `<FileVersion>X.Y.Z.0</FileVersion>`
2. **`frontend/src/shared/stores/useUserStore.ts`**:
   - `version: 'X.Y.Z', // fallback default`
3. **`CHANGELOG.md`**:
   - Top entry row in Release Changelog table matching `| **`vX.Y.Z`** | YYYY-MM-DD | ... |`
4. **`README.md`**:
   - Shield badge: `![Version](https://img.shields.io/badge/version-vX.Y.Z-orange?style=for-the-badge)`
   - Top entry row in the top-5 release preview table

### Release Verification Script (`verify-release.sh`)

The release verification engine is located at `scripts/verify_release.py`. Use the bash wrapper `scripts/verify-release.sh` to run the suite:

```bash
# 🛡️ Run full verification suite (versions, links, tests, builds)
./scripts/verify-release.sh
```

```
==================================================================
  🛡️  Model Context Gateway - Release & Quality Verification Engine  🛡️  
==================================================================
  Repository Root: /containers/dev/csharp-mcp-router

🏷️  1. Version Synchronization & Consistency
------------------------------------------------------------------
  [PASS] Canonical Version in ModelContextGateway.csproj
  [PASS] Csproj <AssemblyVersion> Alignment
  [PASS] Csproj <FileVersion> Alignment
  [PASS] React Store Fallback Version (frontend/src/shared/stores/useUserStore.ts)
  [PASS] CHANGELOG.md Top Entry Alignment
  [PASS] README.md Version Badge Alignment
  [PASS] README.md Release Preview Top Entry

🔗  2. Markdown Link & Anchor Integrity
------------------------------------------------------------------
  [PASS] Scanned Markdown Files (45 files discovered)
  [PASS] Relative Links & Anchor Validity (151 links verified)

🧪  3. Backend .NET Build & Test Verification
------------------------------------------------------------------
  [PASS] .NET Backend Test Suite (500+ tests)

⚛️  4. Frontend Quality, Lint, Build & Vitest Verification
------------------------------------------------------------------
  [PASS] Frontend ESLint Quality Check (0 warnings)
  [PASS] Frontend Vite Production Build (SPA)
  [PASS] Frontend Vitest Component & Store Suite

==================================================================
  📊  Release Verification Summary Report  📊  
==================================================================
  Total Checks:    13
  Passed Checks:   13
  Failed Checks:   0
------------------------------------------------------------------
  🎉 ALL RELEASE & QUALITY GATES PASSED CLEANLY! 🎉
==================================================================
```

### CLI Flags & Options Reference

Use these flags to control verification steps:

| Flag | Purpose | Example |
| :--- | :--- | :--- |
| **`--skip-tests`** | Skips backend and frontend tests. Verifies versions and links in less than 2 seconds. | `./scripts/verify-release.sh --skip-tests` |
| **`--skip-links`** | Skips markdown link and anchor validation. | `./scripts/verify-release.sh --skip-links` |
| **`--skip-versions`** | Skips version synchronization checks. | `./scripts/verify-release.sh --skip-versions` |
| **`--check-versions-only`** | Runs only version synchronization checks. | `python3 scripts/verify_release.py --check-versions-only` |
| **`--check-links-only`** | Runs only markdown relative link and anchor checks. | `python3 scripts/verify_release.py --check-links-only` |
| **`--check-tests-only`** | Runs only backend and frontend test and build suites. | `python3 scripts/verify_release.py --check-tests-only` |
| **`--ci`** | Uses simple output formatted for automated CI jobs. | `python3 scripts/verify_release.py --ci` |
| **`-v`, `--verbose`** | Enables verbose logging with detailed check descriptions. | `./scripts/verify-release.sh -v` |

### Automated Version Bumping & Atomic Commits

Use `./commit.sh` to bump the version and commit files atomically:

```bash
./commit.sh "feat(auth): add fine-grained category scopes"
```

The script runs these steps:
1. Validates the .NET project build.
2. Runs `scripts/bump_version.py` to increment the version number. Minor versions increment for `feat:` or breaking changes. Patch versions increment for `fix:` or `docs:`.
3. Synchronizes version references in `.csproj`, `useUserStore.ts`, `CHANGELOG.md`, and `README.md`.
4. Creates a clean, atomic git commit.

---

## 🔒 Continuous Integration & Quality Gates

Pull requests to `main` must pass the quality gates defined in `.github/workflows/ci.yml`:

1. **`release-verification`**: Validates version synchronization and checks that markdown links and anchors are valid.
2. **`backend`**: Compiles the release build and runs all xUnit tests with coverage collection.
3. **`frontend`**: Enforces zero ESLint warnings, builds the Vite production SPA, and runs Vitest suites.
4. **`integration-smoke`**: Starts the compiled release binary on a local port with an ephemeral SQLite database. Tests health endpoints, AppKey generation, and live MCP discovery.
5. **`docker-check`**: Validates multi-stage Docker build integrity.
6. **`CodeQL` & `Dependency Review`**: Runs static security analysis and scans dependencies for vulnerabilities.

For more details on CI workflows, branch protection rules, and coverage metrics, read [**CI Quality Gates & Security Workflows**](ci-quality-gates.md) and the [**Code Coverage Report**](coverage-report.md).
