# Enterprise Production Deployment and Database Migration Guide

> [!TIP]
> **This guide has been modularized.**
> The documentation for deploying Model Context Gateway (MCG) across container, enterprise, and multi-database topologies has moved to our canonical **[Deployment & Hosting Architecture Documentation](deployment/index.md)**.

## Canonical Documentation Links

- **[Deployment & Hosting Architecture Overview](deployment/index.md)**: Executive topology, zero-config startup defaults, master key resolution hierarchy, and live endpoints.
- **[Docker & Container Production Deployment](deployment/docker.md)**: Official GHCR images, Docker Compose recipes, persistent volume management (`/app/data`), and environment variables.
- **[Multi-Provider Database Setup & Migrations](deployment/database-setup.md)**: SQLite, Microsoft SQL Server, MySQL, initialization scripts (`01_tables.sql`, `02_procedures.sql`), and versioned migrations.
- **[Single-User & Home-Lab Setup](deployment/homelab.md)**: Standalone Mode, trusted subnet bypass, and local LLM integrations (Ollama, LM Studio).
- **[Windows IIS In-Process Deployment](deployment/windows-iis.md)**: Production IIS hosting with ANCM, `Deploy-IIS.ps1`, SSE zero-buffering rules, and AppPool tuning.
- **[Windows Service (SCM) & DPAPI](deployment/windows-service.md)**: Windows Service lifecycle, `Setup-WindowsService.ps1`, SCM auto-recovery, and registry DPAPI secrets.
- **[Validation Runbook & Troubleshooting](deployment/validation-and-runbook.md)**: 4-scenario validation test runbook, observability, and root-cause analysis matrix.
