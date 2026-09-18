# Windows Deployment, Enterprise Hosting and Validation Guide

> [!TIP]
> **This guide has been modularized.**
> The documentation for deploying, configuring, operating, and validating Model Context Gateway (MCG) on Windows Server and Windows 10/11 has moved to our canonical **[Deployment & Hosting Architecture Documentation](deployment/index.md)**.

## Canonical Documentation Links

- **[Windows IIS In-Process Deployment](deployment/windows-iis.md)**: Production IIS hosting with ANCM, `Deploy-IIS.ps1`, `web.config` SSE streaming zero-buffering rules, Application Pool tuning, and HTTPS bindings.
- **[Windows Service (SCM) & DPAPI](deployment/windows-service.md)**: Windows Service lifecycle, `Setup-WindowsService.ps1`, auto-restart crash recovery, DPAPI registry secrets (`HKLM:\SOFTWARE\McpRouter\Secrets`), and standalone Kestrel console mode.
- **[Validation Runbook & Troubleshooting](deployment/validation-and-runbook.md)**: 4-scenario end-to-end validation test runbook (Active Directory / WIA, DPAPI Registry Secrets, STDIO Subprocesses, Environment Diagnostics) and troubleshooting matrix.
- **[Deployment & Hosting Architecture Overview](deployment/index.md)**: Executive topology overview, zero-config startup defaults, master key resolution hierarchy, and live endpoints.
