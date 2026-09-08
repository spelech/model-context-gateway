# Code Coverage Report

**Date:** 2026-09-06 (Release v5.11.0)  
**Status:** **1,063 Automated Tests Passing | Core Modules $\ge 85\%$ Line Coverage**

This document details code coverage metrics for Model Context Gateway (MCG) across Linux container and Windows environments.

---

## 1. Coverage Metrics Summary

The test suite contains **1,063 automated tests** across backend and frontend layers:

| Layer | Framework | Passing Tests | Line Coverage | Branch Coverage | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Backend .NET** | xUnit / .NET 10 | 810 tests | 92.4% | 88.1% | Passing |
| **Frontend UI** | Vitest / React 19 | 253 tests | 92.7% | 83.3% | Passing |
| **Living SRS Catalog** | Roslyn AST Generator | 74 Requirements | 100% Verified | 100% Verified | Passing |

### Subsystem Breakdown

* **Core Session & Protocol Handlers**: 92.4% line / 88.1% branch coverage.
* **Routing Engine & Namespacing**: 89.7% line / 85.3% branch coverage.
* **API Controllers**: 94.2% line / 91.0% branch coverage.
* **Security, RBAC & Providers**: 98.5% line / 95.8% branch coverage.

### Database Engine Verification
The test suite executes live against:
* **SQLite (WAL)**: Schema migrations, key encryption, and table seeding.
* **Microsoft SQL Server 2022**: Stored procedures via Dapper (`McpEnterpriseDb`).
* **MySQL 8.0**: Stored procedures via Dapper (`sp_SaveAppKey`, `sp_GetAppKeys`, `sp_SaveSecretProvider`).

---

## 2. Running Tests Locally

To generate and inspect the code coverage report locally:

### Backend .NET Suite:
```bash
dotnet test ModelContextGateway.Tests/ModelContextGateway.Tests.csproj --collect:"XPlat Code Coverage"
```

### Frontend Vitest Suite:
```bash
cd frontend
npm run test:coverage
```

### Windows Host Diagnostics & Validation Suite:
```powershell
.\scripts\windows\Test-WindowsEnvironment.ps1 -JsonReportPath ".\diagnostics-report.json"
```

### Report Generation (HTML):
```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
```

---

## 3. Related Documentation

- Living SRS & Test Verification Catalog: [`docs/software-requirements-and-test-catalog.md`](software-requirements-and-test-catalog.md)
- Test Catalog & Annotation Guide: [`docs/test-catalog-guide.md`](test-catalog-guide.md)
- Detailed Evaluation & Methodology: [`docs/test-coverage-evaluation.md`](test-coverage-evaluation.md)
- Integration Matrix & Requirements: [`docs/testing-matrix.md`](testing-matrix.md)
- CI Quality Gate Pipeline: [`docs/ci-quality-gates.md`](ci-quality-gates.md)
- Windows Deployment & Validation Guide: [`docs/windows-deployment-and-validation-guide.md`](windows-deployment-and-validation-guide.md)
