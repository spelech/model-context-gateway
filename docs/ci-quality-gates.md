# CI Quality Gates & Security Workflows

This document describes the continuous integration (CI), security scanning, automated dependency maintenance, and quality gates implemented for the **Model Context Gateway (MCG)** repository.

---

## 🛡️ Overview

All pull requests targeting `main` and all commits pushed to `main` must pass a series of automated quality gates before integration and production container publishing.

```
+--------------------------------------------------------------------------------+
|                             Pull Request / Push                               |
+--------------------------------------------------------------------------------+
                                       |
       +-------------------------------+-------------------------------+
       |                               |                               |
       v                               v                               v
+--------------+               +---------------+               +---------------+
|   Backend    |               |   Frontend    |               | Docker Check  |
|  .NET 10.0   |               | Node 22 LTS   |               | Docker syntax |
| Build & Test |               | Lint/Build/UI |               | Dry-run build |
+--------------+               +---------------+               +---------------+
       |                               |                               |
       +-------------------------------+-------------------------------+
                                       |
                                       v
                       +-------------------------------+
                       |    Integration Smoke Test     |
                       |  Live SQLite Kestrel Boot     |
                       |  Health Probe & MCP Discovery |
                       +-------------------------------+
                                       |
                                       v
                       +-------------------------------+
                       |       Security Scanning       |
                       |  CodeQL SAST (C# & JS/TS)     |
                       |  PR Dependency Review         |
                       +-------------------------------+
                                       |
                                       v
                       +-------------------------------+
                       | Gated Image Publish (on main) |
                       |    ghcr.io Container Registry  |
                       +-------------------------------+
```

---

## 🚦 Workflows & Pipeline Jobs

### 1. Main CI Workflow (`.github/workflows/ci.yml`)

The primary CI pipeline runs on pull requests and pushes to `main`. It uses concurrency cancellation (`cancel-in-progress: true`) to automatically terminate superseded runs on active branches.

| Job | Description | Validation Command |
| :--- | :--- | :--- |
| **`release-verification`** | Enforces version synchronization across C# project metadata, React store defaults, CHANGELOG, and README, while validating 100% of internal documentation links and anchors. | `python3 scripts/verify_release.py --skip-tests --ci` |
| **`living-catalog-verification`** | Validates that [`docs/software-requirements-and-test-catalog.md`](software-requirements-and-test-catalog.md) and [`docs/requirements-catalog.json`](requirements-catalog.json) have zero drift against test code annotations. | `dotnet run --project scripts/CatalogGenerator -- --verify-only` |
| **`backend`** | Compiles the C# codebase on .NET 10 (`Release`), runs 900+ xUnit integration & unit tests, and captures coverage data. | `CI=true dotnet test ModelContextGateway.slnx --configuration Release --verbosity normal --collect:"XPlat Code Coverage"` |
| **`frontend`** | Sets up Node.js 22 LTS, enforces strict zero-warning ESLint checks, compiles the Vite TypeScript React 19 SPA, and executes Vitest test suites. | `cd frontend && npm ci && npm run lint && npm run build && npm test` |
| **`integration-smoke`** | Boots the compiled Release binary on an ephemeral Kestrel port with an isolated SQLite test database, verifying `GET /health`, authenticated admin API access, AppKey generation/authentication, and MCP SSE protocol tool discovery. Logs are uploaded automatically on failure. | Runs smoke test probe suite against live local instance. |
| **`docker-check`** | Validates the multi-stage `Dockerfile` build integrity via Docker Buildx in dry-run mode (`push: false`) without publishing. | `docker build -t mcg:ci-check .` |

---

### 2. CodeQL Static Analysis (`.github/workflows/codeql.yml`)

GitHub CodeQL SAST scanning runs automatically on pull requests and pushes to `main`, as well as on a weekly schedule (`0 6 * * 1`).

- **Languages Analyzed**:
  - `csharp` (compiled C# ASP.NET Core backend)
  - `javascript-typescript` (React 19 Vite TypeScript frontend SPA)
- **Rulesets**: Standard security queries identifying injection vulnerabilities, insecure deserialization, cryptographic issues, and memory safety risks.

---

### 3. Dependency Review (`.github/workflows/dependency-review.yml`)

Runs on pull requests to detect newly introduced vulnerable dependencies, license policy violations, or known CVEs across both NuGet and npm packages before code is merged.

---

### 4. Automated Dependency Updates (`.github/dependabot.yml`)

Dependabot is configured to check weekly for security patches and version updates across four distinct package ecosystems:

1. **NuGet (`/`)**: .NET package references in `ModelContextGateway.csproj` and `ModelContextGateway.Tests.csproj`.
2. **npm (`/frontend`)**: Node.js dependencies and devDependencies in `frontend/package.json`.
3. **GitHub Actions (`/`)**: Action versions in `.github/workflows/`.
4. **Docker (`/`)**: Base image dependencies in `Dockerfile`.

---

### 5. Gated Container Publishing (`.github/workflows/docker-publish.yml`)

Production container publishing to `ghcr.io/spelech/model-context-gateway` is strictly gated:
- **`main` branch builds**: Triggered via `workflow_run` only after the `CI Quality Gates` workflow completes with status `success`.
- **Release tags**: Publishes semver-tagged images (`v*.*.*`) upon tagged git releases.
- **Manual dispatch**: Maintains `workflow_dispatch` support for emergency operator overrides.

---

## 🔒 Recommended GitHub Branch Protection Rules

To enforce quality gates on the repository, configure branch protection rules for `main`:

1. **Require a pull request before merging**.
2. **Require status checks to pass before merging**:
   - `Release & Version Consistency Gate (Python 3.12)`
   - `Backend Build & Test (.NET 10)`
   - `Frontend Quality & Tests (Node.js 22 LTS)`
   - `Integration Smoke Test`
   - `Docker Build Check`
   - `Analyze (csharp)` (CodeQL)
   - `Analyze (javascript-typescript)` (CodeQL)
   - `Dependency Review`
3. **Require branches to be up to date before merging**.
4. **Require linear history**.
5. **Do not allow bypassing the above settings**.

---

## 💻 Local Pre-Flight & Release Verification

Before opening a pull request or creating release commits, contributors and AI agents can execute the all-in-one verification engine:

```bash
# 🚀 Run complete release verification (versions, markdown links, tests, builds)
./scripts/verify-release.sh

# ⚡ Quick validation (version sync and link integrity only, skipping tests)
./scripts/verify-release.sh --skip-tests

# 🔍 Targeted validations
python3 scripts/verify_release.py --check-versions-only
python3 scripts/verify_release.py --check-links-only
python3 scripts/verify_release.py --check-tests-only
```

Individual sub-system validations can also be run directly:

```bash
# 1. Run backend tests
CI=true dotnet test ModelContextGateway.slnx --configuration Release

# 2. Run frontend lint, build, and test
cd frontend
npm run lint
npm run build
npm test
cd ..

# 3. Validate Docker build syntax
docker build -t mcg:local-check .
```
