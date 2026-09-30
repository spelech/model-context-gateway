# 📚 Technical Reference & Developer Specifications

Welcome to the **Technical Reference & Development** portal for Model Context Gateway (MCG).

This section provides comprehensive specifications, software requirement catalogs, database schemas, and developer quality standards.

---

<div class="landing-grid">

<a href="../software-requirements-and-test-catalog" class="landing-card">
  <h3>📋 Software Requirements (SRS)</h3>
  <p>Complete Software Requirements Specification catalog and traceability mapping to automated test proofs.</p>
</a>

<a href="../test-catalog-guide" class="landing-card">
  <h3>🧪 Test Catalog Taxonomy</h3>
  <p>Detailed taxonomy for unit, integration, and E2E test cases across C# ASP.NET Core and React frontend suites.</p>
</a>

<a href="../database-providers" class="landing-card">
  <h3>🗄️ Database Providers</h3>
  <p>Multi-provider persistence specifications for SQLite (WAL mode), Microsoft SQL Server, and MySQL.</p>
</a>

<a href="../secret-providers" class="landing-card">
  <h3>🔐 Secret Providers & Keys</h3>
  <p>HashiCorp Vault KV v2, Windows DPAPI, AES-256-GCM envelope encryption, and key resolution mechanics.</p>
</a>

<a href="../appkey-scopes" class="landing-card">
  <h3>🔑 AppKey Scopes & RBAC</h3>
  <p>Formal scope grammar definitions (`mcp-adm-`, `mcp-usr-`, `mcp-glb-`) and granular tool permission evaluation rules.</p>
</a>

<a href="../developer-guide" class="landing-card">
  <h3>💻 Developer & Contributor Guide</h3>
  <p>Local build setup, C# solution structure, React 19 web interface, coding conventions, and PR workflow.</p>
</a>

<a href="../ci-quality-gates" class="landing-card">
  <h3>🚦 CI/CD Quality Gates</h3>
  <p>Automated verification pipelines, static analysis rules, formatting controls, and coverage thresholds.</p>
</a>

<a href="../support-matrix" class="landing-card">
  <h3>📊 Support Matrix</h3>
  <p>Supported operating systems, runtime environments, databases, LLM clients, and MCP transports.</p>
</a>

</div>

<style>
.landing-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
  gap: 1.25rem;
  margin-top: 1.5rem;
}

.landing-card {
  display: block;
  padding: 1.25rem;
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(8px);
  text-decoration: none !important;
  transition: all 0.25s ease;
}

.landing-card:hover {
  transform: translateY(-3px);
  border-color: var(--vp-c-brand);
  background: rgba(255, 255, 255, 0.06);
}

.landing-card h3 {
  margin: 0 0 0.5rem 0 !important;
  font-size: 1.1rem;
  color: var(--vp-c-text-1);
}

.landing-card p {
  margin: 0 !important;
  font-size: 0.9rem;
  color: var(--vp-c-text-2);
  line-height: 1.4;
}
</style>

---

## Reference Quick Links

| Category | Document | Description |
| :--- | :--- | :--- |
| **Requirements** | [Software Requirements & Test Catalog](../software-requirements-and-test-catalog.md) | Living SRS matrix mapped to verification tests. |
| **Testing** | [Test Catalog Taxonomy](../test-catalog-guide.md) | Standardized requirement annotation and test taxonomy. |
| **Data & Secrets** | [Database Providers](../database-providers.md) | Supported database engines, schemas, and migrations. |
| **Data & Secrets** | [Secret Providers & Key Management](../secret-providers.md) | Secret resolution pipeline and master key hierarchy. |
| **Security** | [AppKey Scopes & RBAC Grammar](../appkey-scopes.md) | Scope pattern matching and authorization rules. |
| **Development** | [Developer & Contributor Guide](../developer-guide.md) | Local environment setup and repository guidelines. |
| **Quality** | [CI Quality Gates](../ci-quality-gates.md) | Roslyn analyzers, formatting, and build gates. |
