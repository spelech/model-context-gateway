# Contributing to Model Context Gateway (MCG)

Thank you for your interest in contributing to **Model Context Gateway (MCG)**.

Read this document before you submit changes. It helps you prepare code, write tests, and pass reviews quickly.

---

## 📋 Table of Contents

1. [Code of Conduct](#code-of-conduct)
2. [Development Environment Setup](#development-environment-setup)
3. [Branching & Git Workflow](#branching--git-workflow)
4. [Commit Conventions](#commit-conventions)
5. [Mandatory Versioning Rule](#mandatory-versioning-rule)
6. [CI Quality Gates & Verification](#ci-quality-gates--verification)
7. [Test Requirement Annotations Rule](#test-requirement-annotations-rule)
8. [Screenshots & Documentation Standards](#screenshots--documentation-standards)

---

## 🤝 Code of Conduct

We provide a welcoming, inclusive, and respectful environment for everyone. Treat all contributors with respect. Give constructive feedback during code reviews and discussions.

---

## 💻 Development Environment Setup

Read the [**Developer Guide**](docs/developer-guide.md) for detailed setup instructions.

### Core Prerequisites
* **.NET 10.0 SDK** (`dotnet --version`)
* **Node.js 22 LTS & npm** (`node -v`, `npm -v`)
* **Git** (`git --version`)

```bash
# Clone the repository
git clone https://github.com/spelech/model-context-gateway.git
cd model-context-gateway

# Restore .NET dependencies
dotnet restore ModelContextGateway.slnx

# Install frontend dependencies
cd frontend && npm install && cd ..
```

---

## 🌿 Branching & Git Workflow

1. Create a descriptive branch from `main`:
   * Feature: `feat/issue-<number>-<short-description>`
   * Bug Fix: `fix/issue-<number>-<short-description>`
   * Documentation: `docs/issue-<number>-<short-description>`
   * Refactoring: `refactor/issue-<number>-<short-description>`

2. Keep branches focused and isolated. Do not combine unrelated changes into one pull request.

---

## 📝 Commit Conventions

We follow the [Conventional Commits](https://www.conventionalcommits.org/) standard. Format every commit message like this:

```
<type>(<scope>): <short summary>

[optional body explaining motivation and architectural rationale]

[optional issue reference, e.g. Closes #58]
```

### Allowed Types
* `feat`: A new feature or capability.
* `fix`: A bug fix.
* `docs`: Documentation updates only.
* `refactor`: Code changes that do not fix bugs or add features.
* `test`: New tests or test fixes.
* `perf`: Performance improvements.
* `chore`: Build tooling, dependency updates, or repository maintenance.

### Atomic Commits Rule
* Group your changes into clean, atomic commits.
* Keep code modifications and documentation updates in separate or cleanly grouped commits.

---

## 🏷️ Mandatory Versioning Rule

> [!IMPORTANT]
> **EVERY COMMIT OR MERGE TO `main` MUST BUMP THE VERSION NUMBER.**
> Exception: Documentation-only changes do not require a version bump.

* **Patch Bumps (e.g. `4.12.2` -> `4.12.3`)**: Use for bug fixes, performance optimizations, log refactoring, or minor UI tweaks.
* **Minor Bumps (e.g. `4.12.0` -> `4.13.0`)**: Use for new features, API endpoints, schema changes, or architectural additions.
* **Major Bumps (e.g. `4.0.0` -> `5.0.0`)**: Use for breaking protocol or architectural redesigns.

### Files That MUST Be Updated Simultaneously:
1. [`ModelContextGateway.csproj`](ModelContextGateway.csproj) (`<Version>`, `<AssemblyVersion>`, `<FileVersion>`).
2. [`frontend/src/stores/useUserStore.ts`](frontend/src/stores/useUserStore.ts) (React fallback version string).
3. [`CHANGELOG.md`](CHANGELOG.md) (Add a release entry to the Release Changelog table).
4. [`README.md`](README.md) (Update the top-5 release preview table).

---

## 🚦 CI Quality Gates & Verification

Verify that all quality gates pass on your computer before you open a pull request:

### 1. Backend Tests & Coverage
```bash
CI=true dotnet test ModelContextGateway.slnx --configuration Release --verbosity normal --collect:"XPlat Code Coverage"
```
All tests must pass with 0 errors.

### 2. C# Formatting & Roslyn Analyzers
```bash
dotnet format ModelContextGateway.slnx --verify-no-changes
```

### 3. Frontend Lint, Build, & Tests
```bash
cd frontend
npm run lint
npm run build
npm test
cd ..
```
Resolve all ESLint warnings and TypeScript errors before you commit.

### 4. Living Requirements Catalog Verification
```bash
dotnet run --project scripts/CatalogGenerator -- --verify-only
```
This check ensures that [`docs/software-requirements-and-test-catalog.md`](docs/software-requirements-and-test-catalog.md) matches all test annotations without drift.

---

## 🧪 Test Requirement Annotations Rule

Annotate all new or modified tests in C# (`ModelContextGateway.Tests`), Vitest (`frontend/src/test`), and Playwright (`frontend/e2e`):
- **C#**: `[Requirement("AUTH-01", "AUTH", RequirementType.Positive, "Description")]`
- **TypeScript**: Add a JSDoc `@requirement AUTH-01` block.
- **Naming Rule**: Never use the `REQ-` prefix in requirement IDs. Use standard category codes (`AUTH-01`, `DB-01`, `GUARD-01`, `MCP-01`, `SEC-01`, `TRANS-01`, `UI-01`).

Read the [**Software Requirements & Test Catalog Guide**](docs/test-catalog-guide.md) for full taxonomy conventions and generator instructions.
For details on CI pipeline jobs, CodeQL scans, and smoke tests, read [**CI Quality Gates & Security Workflows**](docs/ci-quality-gates.md).

---

## 📸 Screenshots & Documentation Standards

* **Real Screenshots Standard**: **Do not use AI-generated images or placeholder mockups** in documentation. Capture all images in `docs/assets/` from the live application with the automated screenshot tool (`scripts/capture_guide_screenshots.mjs` or `scripts/take_screenshots.js`).
* **Documentation Currency**: Add or update documentation under `docs/` whenever you change features or APIs.
