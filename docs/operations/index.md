# 🛠️ Operations & Administration Guide

Welcome to the **Operations & Administration** hub for Model Context Gateway (MCG).

This section covers production maintenance, administrative automation, CLI tools, runbooks, and troubleshooting procedures for system operators and administrators.

---

<div class="landing-grid">

<a href="../admin-guide" class="landing-card">
  <h3>👑 Administrator Guide</h3>
  <p>System configuration, AppKey management, global quotas, security parameters, and RBAC policy administration.</p>
</a>

<a href="../admin-mcp-automation-guide" class="landing-card">
  <h3>🤖 Admin MCP Automation</h3>
  <p>Automate gateway administration directly via AI coding assistants using the dedicated /admin MCP endpoint.</p>
</a>

<a href="../admin-mcp-features" class="landing-card">
  <h3>📖 Admin MCP Tools Reference</h3>
  <p>Complete tool definitions and parameters for manage_servers, manage_appkeys, manage_policies, and audit tools.</p>
</a>

<a href="../runbook" class="landing-card">
  <h3>📑 Operations Runbook</h3>
  <p>Step-by-step procedures for backup & restore, database migrations, secret rotation, and incident management.</p>
</a>

<a href="../mcp-routing-and-admin-issues" class="landing-card">
  <h3>🔍 Troubleshooting & RCA</h3>
  <p>Diagnostic workflows, common error codes, SSE connection debugging, and root-cause analysis (RCA) guides.</p>
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

## Operations Overview

| Focus Area | Description | Primary Document |
| :--- | :--- | :--- |
| **System Administration** | User quotas, key generation, and identity group mappings | [Administrator Guide](../admin-guide.md) |
| **Programmatic Management** | Managing MCG through LLMs and agentic automation | [Admin MCP Automation Guide](../admin-mcp-automation-guide.md) |
| **Tool Specifications** | Operational schemas for `/admin` tool calls | [Admin MCP Tools Reference](../admin-mcp-features.md) |
| **Production Runbook** | Disaster recovery, database maintenance, and updates | [Operations Runbook](../runbook.md) |
| **Diagnostics & RCA** | Troubleshooting logs, HTTP/SSE errors, and resolution steps | [Troubleshooting & RCA](../mcp-routing-and-admin-issues.md) |
