# 📚 Technical Reference & Developer Specifications

Welcome to the **Technical Reference & Development** portal for Model Context Gateway (MCG).

This section provides comprehensive specifications, software requirement catalogs, database schemas, and developer quality standards.

---

## Reference & Specification Directory

<div class="grid cards">

<ul>
<li>

📋 **[Software Requirements (SRS)](../software-requirements-and-test-catalog.md)**

---

Complete Software Requirements Specification catalog and traceability mapping across 1,209+ automated test proofs.

</li>
<li>

🧪 **[Test Catalog Taxonomy](../test-catalog-guide.md)**

---

Detailed taxonomy for unit, integration, and E2E test cases across C# ASP.NET Core and React frontend suites.

</li>
<li>

🗄️ **[Database Providers](../database-providers.md)**

---

Multi-provider persistence specifications for SQLite (WAL mode), Microsoft SQL Server, and MySQL.

</li>
<li>

🔐 **[Secret Providers & Keys](../secret-providers.md)**

---

HashiCorp Vault KV v2, Windows DPAPI, AES-256-GCM envelope encryption, and key resolution mechanics.

</li>
<li>

🔑 **[AppKey Scopes & RBAC](../appkey-scopes.md)**

---

Formal scope grammar definitions (`*`, `server:*`, `category:*`, `tool:*`), atomic rotation, and permission evaluation rules.

</li>
<li>

💻 **[Developer & Contributor Guide](../developer-guide.md)**

---

Local build setup, C# solution structure, React 19 web interface, coding conventions, and PR workflow.

</li>
<li>

🚦 **[CI/CD Quality Gates](../ci-quality-gates.md)**

---

Automated verification pipelines, static analysis rules, formatting controls, and coverage thresholds.

</li>
<li>

📊 **[Support Matrix](../support-matrix.md)**

---

Supported operating systems, runtime environments, databases, LLM clients, and MCP transports.

</li>
</ul>

</div>

---

## Reference Quick Links

| Category | Document | Description |
| :--- | :--- | :--- |
| **Requirements** | [Software Requirements & Test Catalog](../software-requirements-and-test-catalog.md) | Living SRS matrix mapped to verification tests. |
| **Testing** | [Test Catalog Taxonomy](../test-catalog-guide.md) | Standardized requirement annotation and test taxonomy. |
| **Testing** | [Testing Matrix](../testing-matrix.md) | Pairwise transport × secret provider × database test matrix. |
| **Coverage** | [Test Coverage Evaluation](../test-coverage-evaluation.md) | Subsystem confidence scores and UI component coverage breakdown. |
| **Coverage** | [Code Coverage Report](../coverage-report.md) | Line and branch coverage metrics and local execution commands. |
| **Data & Secrets** | [Database Providers](../database-providers.md) | Supported database engines, schemas, and migrations. |
| **Data & Secrets** | [Secret Providers & Key Management](../secret-providers.md) | Secret resolution pipeline and master key hierarchy. |
| **Security** | [AppKey Scopes & RBAC Grammar](../appkey-scopes.md) | Scope pattern matching and authorization rules. |
| **Development** | [Developer & Contributor Guide](../developer-guide.md) | Local environment setup and repository guidelines. |
| **Quality** | [CI Quality Gates](../ci-quality-gates.md) | Roslyn analyzers, formatting, and build gates. |
| **Evaluation** | [Evaluation Guide](../evaluation-guide.md) | Architectural evaluation and benchmark walkthrough. |
