# 🛠️ Operations & Administration Guide

Welcome to the **Operations & Administration** hub for Model Context Gateway (MCG).

This section covers production maintenance, administrative automation, CLI tools, runbooks, and troubleshooting procedures for system operators and administrators.

---

## Administration & Runbook Directory

<div class="grid cards">

<ul>
<li>

👑 **[Administrator Guide](../admin-guide.md)**

---

System configuration, AppKey management, global quotas, security parameters, and RBAC policy administration.

</li>
<li>

🤖 **[Admin MCP Automation](../admin-mcp-automation-guide.md)**

---

Automate gateway administration directly via AI coding assistants using the dedicated `/admin` MCP endpoint.

</li>
<li>

📖 **[Admin MCP Tools Reference](../admin-mcp-features.md)**

---

Complete tool definitions and parameters for `manage_servers`, `manage_appkeys`, `manage_policies`, `manage_settings`, and audit tools.

</li>
<li>

📑 **[Operations Runbook](../runbook.md)**

---

Step-by-step procedures for backup & restore, database migrations, secret rotation, and incident management.

</li>
<li>

🔍 **[Troubleshooting & RCA](../mcp-routing-and-admin-issues.md)**

---

Diagnostic workflows, common error codes, SSE connection debugging, and root-cause analysis (RCA) guides.

</li>
<li>

🧯 **[Targeted Incident Runbooks](./troubleshooting/subprocess-and-stdio.md)**

---

Focused runbooks for subprocess STDIO failures, OAuth/AppKey token rejections, and database migration locks.

</li>
</ul>

</div>

---

## Operations Overview

| Focus Area | Description | Primary Document |
| :--- | :--- | :--- |
| **System Administration** | User quotas, key generation, and identity group mappings | [Administrator Guide](../admin-guide.md) |
| **Programmatic Management** | Managing MCG through LLMs and agentic automation | [Admin MCP Automation Guide](../admin-mcp-automation-guide.md) |
| **Tool Specifications** | Operational schemas for `/admin` tool calls | [Admin MCP Tools Reference](../admin-mcp-features.md) |
| **Production Runbook** | Disaster recovery, database maintenance, and updates | [Operations Runbook](../runbook.md) |
| **Diagnostics & RCA** | Troubleshooting logs, HTTP/SSE errors, and resolution steps | [Troubleshooting & RCA](../mcp-routing-and-admin-issues.md) |
| **STDIO Subprocesses** | Resolving process exit codes, PATH issues, and security blocks | [Subprocess & STDIO Runbook](./troubleshooting/subprocess-and-stdio.md) |
| **Authentication Failures** | Debugging `401`/`403` errors, OAuth 3LO refresh, and Vault TTLs | [Auth & Token Failures Runbook](./troubleshooting/auth-and-token-failures.md) |
| **Database Maintenance** | Resolving SQLite WAL locks and MSSQL/MySQL schema migrations | [Database Locks & Migrations Runbook](./troubleshooting/database-locks-and-migrations.md) |
