# 🚀 Getting Started & Deployment Overview

Welcome to the **Getting Started & Deployment** guide for Model Context Gateway (MCG).

Model Context Gateway connects Large Language Models, AI coding assistants, and autonomous agents to downstream tools, microservices, and databases through a unified, secure MCP endpoint.

---

## Core Onboarding & Deployment Guides

<div class="grid cards">

<ul>
<li>

✨ **[Features Overview](../features-guide.md)**

---

Explore core capabilities: Meta-Mode context optimization, MCP Code Mode (`execute_script`), calibrated hybrid search, 4-stage RBAC, and AES-256-GCM encryption.

</li>
<li>

🐳 **[Docker & Containers](../deployment/docker.md)**

---

Deploy MCG using multi-stage Docker containers (`latest` and `latest-full`), Docker Compose, and automatic container discovery.

</li>
<li>

🏠 **[Home-Lab Setup](../deployment/homelab.md)**

---

Fast zero-config setup for homelabs, standalone developer workstations, and local LLMs (Ollama, LM Studio).

</li>
<li>

🪟 **[Windows IIS Deployment](../deployment/windows-iis.md)**

---

Production IIS in-process hosting using AspNetCoreModuleV2 with native Kerberos/NTLM Windows Authentication.

</li>
<li>

⚙️ **[Windows Service & DPAPI](../deployment/windows-service.md)**

---

Run MCG as a dedicated Windows Service (SCM) with registry-backed DPAPI envelope encryption for machine secrets.

</li>
<li>

🗄️ **[Database & Validation](../deployment/database-setup.md)**

---

Configure SQLite (WAL), Microsoft SQL Server, or MySQL, and run post-deployment health checks.

</li>
</ul>

</div>

---

## Quick Navigation Roadmap

| Goal | Recommended Guide |
| :--- | :--- |
| **Understand Product Capabilities** | [Features Overview](../features-guide.md) |
| **Compare Deployment Topologies** | [Deployment Overview](../deployment/index.md) |
| **Deploy with Docker Compose** | [Docker Deployment Guide](../deployment/docker.md) |
| **Deploy on Windows Server / IIS** | [IIS Deployment Guide](../deployment/windows-iis.md) |
| **Run as a Native Windows Service** | [Windows Service (SCM) Guide](../deployment/windows-service.md) |
| **Run Locally on macOS / Linux / Windows** | [Homelab Guide](../deployment/homelab.md) |
| **Configure SQLite, MSSQL, or MySQL** | [Database Setup Guide](../deployment/database-setup.md) |
| **Verify Production Readiness** | [Validation & Runbook](../deployment/validation-and-runbook.md) |
