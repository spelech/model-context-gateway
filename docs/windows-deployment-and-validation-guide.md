# Windows Deployment, Enterprise Hosting & Validation Guide

![Windows Server](https://img.shields.io/badge/Windows%20Server-2022%20%7C%202025-0078D6?style=for-the-badge&logo=windows&logoColor=white)
![IIS In-Process](https://img.shields.io/badge/IIS-In--Process%20ANCM-0052CC?style=for-the-badge&logo=windows-terminal&logoColor=white)
![Windows Service](https://img.shields.io/badge/Service-SCM%20Auto--Recovery-2ea44f?style=for-the-badge&logo=powershell&logoColor=white)
![DPAPI Protected](https://img.shields.io/badge/Secrets-Registry%20DPAPI-orange?style=for-the-badge&logo=shield&logoColor=white)
![Living Catalog](https://img.shields.io/badge/Quality%20Gate-Verified%20Zero--Drift-green?style=for-the-badge)

Comprehensive, production-grade guide for deploying, configuring, validating, and operating the **Model Context Gateway (MCG)** natively on Microsoft Windows Server and Windows 10/11 environments.

---

## 📑 Table of Contents

1. [Executive Architecture & Windows Subsystems](#1-executive-architecture-windows-subsystems)
   - [Architectural Topology & Gateway Model](#architectural-topology-gateway-model)
   - [Native Windows Subsystems](#native-windows-subsystems)
   - [Hosting Options Comparison Matrix](#hosting-options-comparison-matrix)
2. [Prerequisites & Host Preparation](#2-prerequisites-host-preparation)
   - [Operating System & Hardware](#operating-system-hardware)
   - [Runtimes & SDKs](#runtimes-sdks)
   - [IIS Roles & Features Installation](#iis-roles-features-installation)
   - [PowerShell & Security Privileges](#powershell-security-privileges)
3. [Option 1: Production IIS In-Process Deployment](#3-option-1-production-iis-in-process-deployment)
   - [Overview & In-Process Benefits](#overview-in-process-benefits)
   - [Automated Deployment with Deploy-IIS.ps1](#automated-deployment-with-deploy-iisps1)
   - [web.config Architectural Deep Dive](#webconfig-architectural-deep-dive)
   - [SSE Streaming & Zero-Buffering Architecture](#sse-streaming-zero-buffering-architecture)
   - [Application Pool Tuning & Lifecycle](#application-pool-tuning-lifecycle)
   - [Manual IIS Setup Reference](#manual-iis-setup-reference)
4. [Option 2: Managed Windows Service (SCM)](#4-option-2-managed-windows-service-scm)
   - [Overview & Service Architecture](#overview-service-architecture)
   - [Automated Lifecycle with Setup-WindowsService.ps1](#automated-lifecycle-with-setup-windowsserviceps1)
   - [Auto-Recovery & SCM Crash Action Configuration](#auto-recovery-scm-crash-action-configuration)
   - [Service Account & Security Permissions](#service-account-security-permissions)
5. [Option 3: Standalone Kestrel Console](#5-option-3-standalone-kestrel-console)
   - [Developer & Interactive Execution](#developer-interactive-execution)
   - [Command-Line Overrides & Ports](#command-line-overrides-ports)
6. [End-to-End Validation Runbook (4 Key Scenarios)](#6-end-to-end-validation-runbook-4-key-scenarios)
   - [Scenario 1: Active Directory & Windows Integrated Auth (Kerberos / NTLM / SIDs)](#scenario-1-active-directory-windows-integrated-auth-kerberos-ntlm-sids)
   - [Scenario 2: Windows Registry Secrets & DPAPI Encryption](#scenario-2-windows-registry-secrets-dpapi-encryption)
   - [Scenario 3: STDIO Transport Subprocess Execution on Windows](#scenario-3-stdio-transport-subprocess-execution-on-windows)
   - [Scenario 4: Automated Environment Diagnostics & Quality Gates](#scenario-4-automated-environment-diagnostics-quality-gates)
7. [Production Operations, Security Hardening & Observability](#7-production-operations-security-hardening-observability)
   - [SSL/TLS Certificates & HTTPS Bindings](#ssltls-certificates-https-bindings)
   - [Health Probes & Monitoring](#health-probes-monitoring)
   - [Prometheus Metrics Scraping](#prometheus-metrics-scraping)
   - [Logging Architecture (IIS, Stdout, Windows Event Log)](#logging-architecture-iis-stdout-windows-event-log)
   - [Database Backup & Recovery on Windows](#database-backup-recovery-on-windows)
8. [Comprehensive Troubleshooting Guide](#8-comprehensive-troubleshooting-guide)

---

## 🏛️ 1. Executive Architecture & Windows Subsystems

### Architectural Topology & Gateway Model

The **Model Context Gateway (MCG)** acts as an enterprise gateway and semantic proxy for the Model Context Protocol (MCP). On Windows Server, it natively bridges Windows infrastructure (Active Directory, DPAPI, IIS, Windows Services) with heterogeneous downstream MCP servers.

```mermaid
flowchart TD
    subgraph Clients["LLM Clients & Developer Tools"]
        IDE["Visual Studio / VS Code / Cursor"]
        LLM["AI Agents / Claude Desktop / Antigravity"]
        WebBrowser["Web Dashboard UI / Browser"]
    end

    subgraph WindowsHost["Windows Server Host Environment"]
        subgraph IIS["IIS Web Server (In-Process ANCM)"]
            HttpSys["HTTP.sys Driver / Port 80, 443, 8080"]
            ANCModule["AspNetCoreModuleV2 (InProcess)"]
            W3WP["w3wp.exe (Worker Process)"]
        end

        subgraph CoreEngine["Model Context Gateway ASP.NET Core Engine (.NET 10)"]
            AuthPipeline["ActiveDirectoryIdentityProvider\n(IWindowsIdentityAccessor)"]
            SecretEngine["WindowsRegistrySecretRetriever\n(IDpapiProtector)"]
            RoutingEngine["ToolRoutingManager\n(Meta-Mode & Semantic Search)"]
            TransportTier["Downstream Transports Tier"]
        end

        subgraph WindowsSubsystems["Native Windows Security & Storage Subsystems"]
            AD["Active Directory\n(Kerberos / NTLM / SIDs / S-1-5-32-544)"]
            Registry["Windows Registry\n(HKLM:\\SOFTWARE\\McpRouter\\Secrets)"]
            DPAPI["DPAPI ProtectedData\n(DataProtectionScope.LocalMachine)"]
            SCM["Service Control Manager\n(Auto-Restart Recovery)"]
        end
    end

    subgraph Backends["Downstream MCP Servers"]
        StdioProc["Subprocess STDIO\n(.exe, .cmd, node.exe, python.exe)"]
        HttpServer["Remote HTTP / SSE MCP Servers"]
        DockerContainers["Windows / WSL2 Docker Containers"]
    end

    Clients -->|HTTP / SSE Streaming| HttpSys
    HttpSys --> ANCModule
    ANCModule --> W3WP
    W3WP --> CoreEngine

    AuthPipeline <-->|Extract User & Group SIDs| AD
    SecretEngine <-->|Read Encrypted REG_BINARY| Registry
    SecretEngine <-->|Decrypt Machine Keys| DPAPI

    TransportTier -->|ProcessStartInfo (Injected Env Vars)| StdioProc
    TransportTier -->|Streamable HTTP / SSE| HttpServer
    TransportTier -->|TCP Named Pipes / Sockets| DockerContainers
```

### Native Windows Subsystems

1. **Active Directory & Integrated Windows Authentication (`WindowsIdentity`)**:
   - Implemented via [`IWindowsIdentityAccessor.cs`](https://github.com/spelech/model-context-gateway/blob/main/Infrastructure/Identity/IWindowsIdentityAccessor.cs) and [`ActiveDirectoryIdentityProvider.cs`](https://github.com/spelech/model-context-gateway/blob/main/Infrastructure/Identity/ActiveDirectoryIdentityProvider.cs).
   - Extracts caller identity, Primary SID, and full Group SID security token lists directly from `WindowsIdentity.Groups` when running under IIS or Kestrel Negotiate authentication.
   - Transparently handles well-known security identifiers such as `S-1-5-32-544` (Builtin Administrators) and domain security groups for role-based authorization without requiring external LDAP binds.

2. **Windows Registry & DPAPI Cryptography (`WindowsRegistrySecretRetriever`)**:
   - Implemented via [`WindowsRegistrySecretRetriever.cs`](https://github.com/spelech/model-context-gateway/blob/main/Infrastructure/Secrets/WindowsRegistrySecretRetriever.cs).
   - Reads secrets from `HKLM:\SOFTWARE\McpRouter\Secrets`.
   - Supports plaintext `REG_SZ` strings and cryptographically secure `REG_BINARY` blobs protected with the Windows Data Protection API (`System.Security.Cryptography.ProtectedData.Protect` / `Unprotect`) under `DataProtectionScope.LocalMachine`.
   - Allows machine-level secret provisioning that is completely decoupled from configuration files or source code repositories.

3. **Subprocess STDIO Transport Isolation (`StdioTransport`)**:
   - Implemented via [`StdioTransport.cs`](https://github.com/spelech/model-context-gateway/blob/main/Infrastructure/Transports/StdioTransport.cs).
   - Spawns Windows processes (`.exe`, `.cmd`, `.bat`, `node.exe`, `python.exe`, `uvx.exe`, `npx.cmd`) with standard input/output redirection.
   - Enforces **Zero CLI Secret Leakage**: sensitive API tokens and keys resolved from DPAPI, Vault, or environment variables are injected exclusively into `ProcessStartInfo.Environment` rather than command-line arguments.

---

### Hosting Options Comparison Matrix

| Feature / Attribute | Option 1: IIS In-Process (Recommended) | Option 2: Managed Windows Service | Option 3: Standalone Kestrel Console |
| :--- | :--- | :--- | :--- |
| **Primary Use Case** | Enterprise production servers, shared web infrastructure | Dedicated servers, headless background hosting | Local testing, debugging, CI/CD runners |
| **Process Hosting Model** | In-Process inside `w3wp.exe` via `AspNetCoreModuleV2` | Standalone `.exe` managed by Windows SCM | Direct `dotnet run` / interactive executable |
| **Throughput & Performance** | **Maximum** (Direct CLR execution in IIS worker process) | High (Direct Kestrel socket pipeline) | High (Development pipeline) |
| **Port Sharing & Bindings** | Full HTTP.sys port sharing (80, 443, multiple hostnames) | Dedicated port binding (e.g. `http://0.0.0.0:8080`) | Dedicated port binding (e.g. `http://localhost:5000`) |
| **SSL/TLS Termination** | Native IIS Certificate Store, SNI, win-acme Let's Encrypt | Kestrel PFX binding or upstream reverse proxy | Kestrel dev certificate or HTTP only |
| **SSE Streaming Optimization** | Supported with `responseBufferLimit="0"` | Native (Zero buffering in Kestrel) | Native |
| **Crash Auto-Recovery** | IIS Application Pool auto-restart & health monitoring | SCM failure actions (`sc.exe failure actions= restart`) | Manual restart or console loop |
| **Integrated Windows Auth** | Native IIS Negotiate / Kerberos / NTLM module | Kestrel Negotiate or Header-based Auth | Negotiate or Anonymous |
| **Automation Script** | [`Deploy-IIS.ps1`](https://github.com/spelech/model-context-gateway/blob/main/scripts/windows/Deploy-IIS.ps1) | [`Setup-WindowsService.ps1`](https://github.com/spelech/model-context-gateway/blob/main/scripts/windows/Setup-WindowsService.ps1) | Direct CLI / Terminal |

---

## 🛠️ 2. Prerequisites & Host Preparation

### Operating System & Hardware

- **Operating System**: Windows Server 2025, Windows Server 2022, Windows Server 2019, or Windows 10/11 (64-bit x64 / arm64).
- **CPU & Memory**: Minimum 2 Cores, 4 GB RAM (8 GB+ recommended if utilizing local ONNX vector embeddings).
- **Disk**: Minimum 2 GB free disk space for published binaries, SQLite database, and ONNX models.

### Runtimes & SDKs

1. **.NET 10 SDK / ASP.NET Core 10 Windows Hosting Bundle**:
   - Download and install the [.NET 10 Windows Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0). The hosting bundle installs the .NET Runtime, ASP.NET Core Runtime, and the **ASP.NET Core Module v2 (ANCM)** for IIS.
   - Verify installation via PowerShell:
     ```powershell
     dotnet --info
     ```

2. **Node.js & npm (Required for Dashboard UI Build)**:
   - Node.js LTS (v20.x or v22.x) from [nodejs.org](https://nodejs.org/).
   - Verify installation:
     ```powershell
     node -v
     npm -v
     ```

### IIS Roles & Features Installation

To install IIS and all required modules on Windows Server, run an elevated Administrator PowerShell prompt:

```powershell
# Install IIS, Management Tools, WebSockets, and Windows Authentication
Install-WindowsFeature -Name Web-Server, `
                            Web-WebServer, `
                            Web-Common-Http, `
                            Web-Static-Content, `
                            Web-Default-Doc, `
                            Web-Http-Errors, `
                            Web-Http-Redirect, `
                            Web-Filtering, `
                            Web-Security, `
                            Web-Windows-Auth, `
                            Web-App-Dev, `
                            Web-Net-Ext45, `
                            Web-WebSockets, `
                            Web-Mgmt-Tools, `
                            Web-Mgmt-Console, `
                            Web-Scripting-Tools -IncludeManagementTools
```

*For Windows 10/11 Workstations, enable features via `dism`:*
```powershell
Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole, IIS-WebServer, IIS-CommonHttpFeatures, IIS-StaticContent, IIS-DefaultDocument, IIS-DirectoryBrowsing, IIS-HttpErrors, IIS-ApplicationDevelopment, IIS-WebSockets, IIS-Security, IIS-WindowsAuthentication, IIS-RequestFiltering, IIS-WebServerManagementTools, IIS-ManagementConsole -All
```

### PowerShell & Security Privileges

All deployment scripts must be run from an **Elevated Administrator PowerShell Prompt**. Ensure execution policy allows script execution:

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope Process -Force
```

---

## 🚀 3. Option 1: Production IIS In-Process Deployment

### Overview & In-Process Benefits

In-Process hosting (`hostingModel="inprocess"`) loads the ASP.NET Core application directly inside the IIS worker process (`w3wp.exe`). This delivers:
1. **Zero Loopback Latency**: Requests are processed directly in memory without out-of-process HTTP forwarding to a separate Kestrel process.
2. **Native Windows Authentication**: Kerberos and NTLM tokens are transferred directly to `HttpContext.User` as `WindowsIdentity` objects.
3. **Robust Lifecycle Management**: Application recycling, idle shutdown, and CPU throttling are handled by the IIS kernel.

---

### Automated Deployment with Deploy-IIS.ps1

The repository includes a comprehensive deployment automation script: [`scripts/windows/Deploy-IIS.ps1`](https://github.com/spelech/model-context-gateway/blob/main/scripts/windows/Deploy-IIS.ps1).

#### Parameter Reference

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `-SiteName` | string | `"ModelContextGateway"` | Name of the IIS Website. |
| `-AppPoolName` | string | `"ModelContextGatewayAppPool"` | Name of the dedicated IIS Application Pool. |
| `-Port` | int | `8080` | HTTP port for the website binding. |
| `-HostName` | string | `""` | Optional hostname binding (e.g. `mcp.corp.local`). |
| `-PhysicalPath` | string | `"C:\inetpub\mcg"` | Target physical deployment directory. |
| `-Configuration` | string | `"Release"` | Build configuration (`Release` or `Debug`). |
| `-RepoRoot` | string | Auto-resolved | Path to the repository root directory. |
| `-SkipFrontend` | switch | `false` | Skips compiling the Vite React frontend. |
| `-SkipBuild` | switch | `false` | Skips compilation (assumes binaries are already published). |
| `-SelfContained`| switch | `false` | Publishes self-contained .NET binary including runtime. |
| `-RuntimeIdentifier` | string | `"win-x64"` | Target runtime architecture (`win-x64`, `win-arm64`). |
| `-EnableWindowsAuth` | switch | `false` | Explicitly enables IIS Windows Authentication on the site. |

#### Deployment Commands

```powershell
# Standard Production Deployment (Port 8080 with Windows Authentication):
.\scripts\windows\Deploy-IIS.ps1 -SiteName "ModelContextGateway" -Port 8080 -EnableWindowsAuth

# Host Header Binding Deployment (e.g. mcp.company.internal):
.\scripts\windows\Deploy-IIS.ps1 -SiteName "ModelContextGateway" -Port 80 -HostName "mcp.company.internal" -EnableWindowsAuth

# Self-Contained Deployment to Custom Directory:
.\scripts\windows\Deploy-IIS.ps1 -PhysicalPath "D:\Apps\ModelContextGateway" -Port 8443 -SelfContained
```

---

### web.config Architectural Deep Dive

The IIS deployment uses an optimized `web.config` file derived from [`scripts/windows/web.config.example`](https://github.com/spelech/model-context-gateway/blob/main/scripts/windows/web.config.example):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>

      <!--
        CRITICAL ARCHITECTURAL DIRECTIVES:
        1. hostingModel="inprocess": High performance in-process worker pipeline.
        2. responseBufferLimit="0": CRITICAL for MCP Server-Sent Events (SSE). 
           Completely disables IIS response buffering so streaming chunks flush immediately.
      -->
      <aspNetCore processPath="dotnet"
                  arguments=".\mcg.dll"
                  stdoutLogEnabled="false"
                  stdoutLogFile=".\logs\stdout"
                  hostingModel="inprocess"
                  responseBufferLimit="0">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
          <environmentVariable name="DB_PROVIDER" value="sqlite" />
          <!-- Optional: Uncomment for SQL Server Windows Authentication:
          <environmentVariable name="ConnectionStrings__McpDatabase" value="Server=sql.domain.local;Database=McpGatewayDb;Integrated Security=True;TrustServerCertificate=True;" />
          -->
        </environmentVariables>
      </aspNetCore>

      <!-- Integrated Windows Authentication & Anonymous Public Access -->
      <security>
        <authentication>
          <windowsAuthentication enabled="true" />
          <anonymousAuthentication enabled="true" />
        </authentication>
        <requestFiltering>
          <!-- 100 MB max request body limit for large tool parameters & embeddings -->
          <requestLimits maxAllowedContentLength="104857600" />
        </requestFiltering>
      </security>

      <httpProtocol>
        <customHeaders>
          <remove name="X-Powered-By" />
          <add name="X-Content-Type-Options" value="nosniff" />
          <add name="X-Frame-Options" value="SAMEORIGIN" />
        </customHeaders>
      </httpProtocol>

      <!--
        URL Compression Configuration:
        Dynamic compression MUST be disabled for SSE streaming endpoints
        to prevent gzip filters from buffering real-time chunk streams.
      -->
      <urlCompression doStaticCompression="true" doDynamicCompression="false" />

    </system.webServer>
  </location>
</configuration>
```

---

### SSE Streaming & Zero-Buffering Architecture

The Model Context Protocol heavily relies on Server-Sent Events (`/sse` and `/message`) for real-time JSON-RPC messaging and streaming LLM token updates.

> [!CAUTION]
> **Why `responseBufferLimit="0"` is Mandatory:**
> By default, IIS buffers outbound HTTP responses in chunks up to 4 KB before flushing to the client. This breaks MCP SSE connections, causing client requests (`search_tools`, `execute_tool`) to hang indefinitely waiting for the buffer to fill. Setting `responseBufferLimit="0"` in `<aspNetCore>` completely turns off ANCM buffering.

> [!IMPORTANT]
> **Disabling Dynamic Compression:**
> IIS dynamic compression (`<urlCompression doDynamicCompression="false" />`) compresses streaming HTTP responses on the fly. Because compression algorithms require looking ahead across byte chunks, dynamic compression buffers SSE events. Static compression remains enabled for frontend assets (`.js`, `.css`), while dynamic compression is disabled.

---

### Application Pool Tuning & Lifecycle

To keep upstream connections persistently alive, configure the following IIS Application Pool properties (automatically applied by `Deploy-IIS.ps1`):

1. **.NET CLR Version**: Set to `No Managed Code` (`""`). ANCM loads the .NET Core runtime directly.
2. **Start Mode**: Set to `AlwaysRunning`. Prevents IIS from placing the worker process to sleep.
3. **Idle Time-out**: Set to `0` (Disabled). Ensures the gateway remains active during quiet periods without dropping downstream MCP transport pipes.
4. **Permissions**: The AppPool identity (`IIS AppPool\McgAppPool`) must have read/write access to the deployment folder and the `logs` subdirectory:
   ```powershell
   icacls "C:\inetpub\mcg" /grant "IIS AppPool\McgAppPool:(OI)(CI)M" /T /Q
   ```

---

### Manual IIS Setup Reference

For air-gapped systems or environments where automated scripts cannot be run directly:

1. Build and publish the application:
   ```powershell
   cd frontend; npm run build; cd ..
   dotnet publish ModelContextGateway.csproj -c Release -o C:\inetpub\mcg
   ```
2. Copy `scripts\windows\web.config.example` to `C:\inetpub\mcg\web.config`.
3. Open **IIS Manager (`inetmgr`)**:
   - Create Application Pool: `McgAppPool` -> .NET CLR Version: `No Managed Code`.
   - In AppPool **Advanced Settings**: set `Start Mode` = `AlwaysRunning`, `Idle Time-out (minutes)` = `0`.
   - Add Website: Site Name = `ModelContextGateway`, Physical Path = `C:\inetpub\mcg`, Port = `8080`.
   - Navigate to **Authentication**: Enable `Windows Authentication` and `Anonymous Authentication`.
4. Grant filesystem permissions to `IIS AppPool\McgAppPool`.

---

## ⚙️ 4. Option 2: Managed Windows Service (SCM)

### Overview & Service Architecture

When deploying as a dedicated background daemon without IIS, the router can run directly as a **Windows Service** managed by the Windows Service Control Manager (SCM).

- Uses ASP.NET Core Kestrel directly on a dedicated TCP port.
- Configured with automatic crash recovery triggers.
- Can run under `NT AUTHORITY\LocalSystem`, `NT AUTHORITY\NetworkService`, or a domain Group Managed Service Account (gMSA).

---

### Automated Lifecycle with Setup-WindowsService.ps1

The repository provides the lifecycle management script: [`scripts/windows/Setup-WindowsService.ps1`](https://github.com/spelech/model-context-gateway/blob/main/scripts/windows/Setup-WindowsService.ps1).

#### Supported Actions

```powershell
# 1. Install & Start Service on Port 8080:
.\scripts\windows\Setup-WindowsService.ps1 -Action Install -Port 8080

# 2. Check Service Status & Health:
.\scripts\windows\Setup-WindowsService.ps1 -Action Status

# 3. Restart Service:
.\scripts\windows\Setup-WindowsService.ps1 -Action Restart

# 4. Stop Service:
.\scripts\windows\Setup-WindowsService.ps1 -Action Stop

# 5. Start Service:
.\scripts\windows\Setup-WindowsService.ps1 -Action Start

# 6. Uninstall & Remove Service:
.\scripts\windows\Setup-WindowsService.ps1 -Action Uninstall
```

#### Parameter Reference

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `-Action` | string | *(Mandatory)* | `Install`, `Uninstall`, `Start`, `Stop`, `Restart`, `Status`. |
| `-ServiceName` | string | `"ModelContextGateway"` | Unique service name in SCM. |
| `-DisplayName` | string | `"Model Context Gateway (MCG) Service"` | User-friendly display name. |
| `-InstallDir` | string | `"C:\Program Files\McpRouter"` | Target directory for published binaries. |
| `-Port` | int | `8080` | Port for ASP.NET Core Kestrel HTTP listener. |
| `-Urls` | string | `"http://0.0.0.0:8080"` | Complete URL bindings. |
| `-ServiceAccount` | string | `"NT AUTHORITY\LocalSystem"` | Service user account (e.g. `DOMAIN\gMSA_McpRouter$`). |
| `-Configuration` | string | `"Release"` | Build configuration (`Release` or `Debug`). |
| `-SelfContained` | switch | `false` | Publishes self-contained .NET binary. |

---

### Auto-Recovery & SCM Crash Action Configuration

`Setup-WindowsService.ps1` automatically configures Windows Service Control Manager recovery actions using `sc.exe`:

```powershell
# Automatically restart the service after 60 seconds on 1st, 2nd, and subsequent crashes:
sc.exe failure McpRouter reset= 86400 actions= restart/60000/restart/60000/restart/60000
sc.exe failureflag McpRouter 1
```

---

### Service Account & Security Permissions

If running under a restricted domain service account or gMSA (`DOMAIN\svc_mcp`):
1. Grant the service account read/write access to the install folder (`C:\Program Files\McpRouter`).
2. Grant read access to the Windows Registry subkey `HKLM:\SOFTWARE\McpRouter\Secrets`.
3. Reserve the URL port binding via `netsh`:
   ```cmd
   netsh http add urlacl url=http://+:8080/ user="DOMAIN\svc_mcp"
   ```

---

## 💻 5. Option 3: Standalone Kestrel Console

For local development, testing, or ad-hoc validation on Windows:

### Developer & Interactive Execution

```powershell
# Run from repository root in Development mode:
dotnet run --project ModelContextGateway.csproj --urls "http://localhost:5000"
```

### Command-Line Overrides & Ports

```powershell
# Run published binary in Production mode:
$env:ASPNETCORE_ENVIRONMENT="Production"
$env:DB_PROVIDER="sqlite"
$env:MCG_MASTER_KEY="your_32_char_secure_hex_master_key_here"
.\bin\Release\net10.0\win-x64\publish\mcg.exe --urls "http://0.0.0.0:8080"
```

---

## 🧪 6. End-to-End Validation Runbook (4 Key Scenarios)

This section provides executable runbooks for verifying all native Windows capabilities across 4 core scenarios.

```mermaid
graph TD
    subgraph Scenario1["Scenario 1: Active Directory & Windows Auth"]
        S1A["HTTP Request with Negotiate Header"] --> S1B["IWindowsIdentityAccessor"]
        S1B --> S1C["Extract User SID & Group SIDs"]
        S1C --> S1D["Validate S-1-5-32-544 (Admin Role)"]
    end

    subgraph Scenario2["Scenario 2: Registry Secrets & DPAPI"]
        S2A["Set-RegistrySecrets.ps1 -Encrypt"] --> S2B["HKLM:\\SOFTWARE\\McpRouter\\Secrets"]
        S2B --> S2C["WindowsRegistrySecretRetriever"]
        S2C --> S2D["DPAPI LocalMachine Decryption"]
    end

    subgraph Scenario3["Scenario 3: STDIO Process Execution"]
        S3A["Register STDIO Server"] --> S3B["ProcessStartInfo Redirection"]
        S3B --> S3C["Zero CLI Leakage (Injected Env)"]
        S3C --> S3D["JSON-RPC Tool Execution"]
    end

    subgraph Scenario4["Scenario 4: Automated Quality Gates"]
        S4A["Test-WindowsEnvironment.ps1"] --> S4B["dotnet test McpRouter.slnx"]
        S4B --> S4C["CatalogGenerator --verify-only"]
        S4C --> S4D["Zero-Drift & 100% Pass"]
    end
```

---

### Scenario 1: Active Directory & Windows Integrated Auth (Kerberos / NTLM / SIDs)

#### Objective
Validate that Windows caller identities, user security identifiers (SIDs), and group security identifiers (e.g. `S-1-5-32-544` for Administrators) are extracted by `IWindowsIdentityAccessor` and mapped to RBAC permissions by `ActiveDirectoryIdentityProvider`.

#### Step-by-Step Validation

1. **Verify Windows Identity & SIDs via PowerShell**:
   ```powershell
   # Inspect current token identity and security SIDs:
   $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
   Write-Host "Current User: $($identity.Name)" -ForegroundColor Cyan
   Write-Host "User SID    : $($identity.User.Value)" -ForegroundColor Cyan
   Write-Host "Group SIDs  :" -ForegroundColor Cyan
   $identity.Groups | ForEach-Object { Write-Host " - $($_.Value)" -ForegroundColor DarkGray }
   ```

2. **Test IIS Windows Integrated Authentication**:
   ```powershell
   # Test with current Windows logon credentials:
   $response = Invoke-RestMethod -Uri "http://localhost:8080/api/auth/me" -UseDefaultCredentials
   $response | ConvertTo-Json -Depth 4
   ```

   *Expected Response:*
   ```json
   {
     "username": "DOMAIN\\s_pelech",
     "provider": "ActiveDirectory",
     "roles": ["admin"],
     "sids": [
       "S-1-5-21-1234567890-1234567890-1234567890-1001",
       "S-1-5-32-544",
       "S-1-5-32-545"
     ]
   }
   ```

3. **Verify Builtin Administrator Authorization (`S-1-5-32-544`)**:
   - The router automatically identifies members of the local `Administrators` group (`S-1-5-32-544`) and grants administrative access without requiring manual group mapping in the database.

---

### Scenario 2: Windows Registry Secrets & DPAPI Encryption

#### Objective
Validate that API keys and credentials can be securely stored in the Windows Registry (`HKLM:\SOFTWARE\McpRouter\Secrets`) with DPAPI encryption (`DataProtectionScope.LocalMachine`) and dynamically retrieved at runtime by `WindowsRegistrySecretRetriever`.

#### Step-by-Step Validation

1. **Store DPAPI-Encrypted Secret using `Set-RegistrySecrets.ps1`**:
   ```powershell
   # Write an encrypted PAT token:
   .\scripts\windows\Set-RegistrySecrets.ps1 -SecretName "DockerApiKey" -SecretValue "dckr_pat_secret_token_12345" -Encrypt
   ```

2. **Store a Plaintext String Secret**:
   ```powershell
   # Write a plaintext token:
   .\scripts\windows\Set-RegistrySecrets.ps1 -SecretName "PlexToken" -SecretValue "plex_plain_token_xyz"
   ```

3. **List Configured Secrets**:
   ```powershell
   .\scripts\windows\Set-RegistrySecrets.ps1 -List
   ```

   *Expected Output:*
   ```text
   Secrets configured in HKLM:\SOFTWARE\McpRouter\Secrets (2 items):
   ------------------------------------------------------------
   SecretName    Type
   ----------    ----
   DockerApiKey  Binary (DPAPI Encrypted)
   PlexToken     String (Plaintext)
   ```

4. **Verify Decryption**:
   ```powershell
   .\scripts\windows\Set-RegistrySecrets.ps1 -SecretName "DockerApiKey" -Get
   ```

   *Expected Output:*
   ```text
   Secret Name : DockerApiKey
   Value Type  : Binary (DPAPI Encrypted)
   Plaintext   : dckr_pat_secret_token_12345
   ```

5. **Test in Model Context Gateway Runtime**:
   - Register a backend server with `SecretProvider = "WindowsRegistry"`, `SecretPath = "SOFTWARE\McpRouter\Secrets"`, and `SecretItemKey = "DockerApiKey"`.
   - Invoke a tool on that server via the Test Bench. The gateway dynamically reads and decrypts the registry binary value and injects it into the transport request.

---

### Scenario 3: STDIO Transport Subprocess Execution on Windows

#### Objective
Validate that the router can spawn local Windows subprocesses (`.exe`, `.bat`, `node.exe`, `python.exe`), safely inject credentials via environment variables without CLI leakage, and manage process tree lifecycles.

#### Step-by-Step Validation

1. **Verify Executable Path Resolution in PowerShell**:
   ```powershell
   # Test node and python resolution:
   Get-Command node, python, npx, uvx -ErrorAction SilentlyContinue | Select-Object Name, Source
   ```

2. **Register a Local STDIO Backend Server**:
   Create a sample STDIO server in the router database or test configuration:
   - **Name**: `LocalFilesystemMcp`
   - **Transport**: `stdio`
   - **Command / URL**: `npx -y @modelcontextprotocol/server-filesystem C:\data`
   - **Secret Provider**: `WindowsRegistry`
   - **Secret Path**: `SOFTWARE\McpRouter\Secrets`
   - **Secret Key**: `FilesystemSecretKey`

3. **Validate Zero CLI Secret Leakage**:
   - When the router executes `StdioTransport`, observe running processes in PowerShell:
     ```powershell
     Get-CimInstance Win32_Process -Filter "Name like '%node%'" | Select-Object ProcessId, CommandLine
     ```
   - Notice that the secret key does **not** appear anywhere in `CommandLine`. It is securely passed inside `ProcessStartInfo.Environment["API_KEY"]`.

4. **Execute Tools via Test Bench**:
   - Open the web dashboard: `http://localhost:8080/#/testbench`.
   - Select the `LocalFilesystemMcp` server tools (e.g. `list_directory`).
   - Execute the tool and verify JSON-RPC output streaming.

---

### Scenario 4: Automated Environment Diagnostics & Quality Gates

#### Objective
Execute the unified Windows diagnostic runner, backend test suites, and living requirements catalog zero-drift verification.

#### Step-by-Step Validation

1. **Run Automated Diagnostic Runner (`Test-WindowsEnvironment.ps1`)**:
   ```powershell
   # Execute full diagnostic suite and export JSON report:
   .\scripts\windows\Test-WindowsEnvironment.ps1 -JsonReportPath ".\diagnostics-report.json"
   ```

   *Expected Output:*
   ```text
   ================================================================================
     1. Host Environment & Toolchain Prerequisites
   ================================================================================
     [ PASS ] Operating System : Microsoft Windows NT 10.0.26100.0 (X64)
     [ PASS ] PowerShell Version : PowerShell 7.4.2
     [ PASS ] Admin Privileges : Elevated (Administrator)
     [ PASS ] .NET 10 SDK : Installed (.NET SDK 10.0.100)
     [ PASS ] ASP.NET Core 10 Runtime : Microsoft.AspNetCore.App 10.x runtime present
     [ PASS ] Node.js & npm : Node v22.12.0 / npm 10.9.0

   ================================================================================
     2. Windows Registry Secrets Subsystem (HKLM:\SOFTWARE\McpRouter\Secrets)
   ================================================================================
     [ PASS ] HKLM Secrets Subkey Write Access : Successfully opened HKLM:\SOFTWARE\McpRouter\Secrets with write access
     [ PASS ] Plaintext Value Read/Write : Stored and verified plaintext REG_SZ value

   ================================================================================
     3. DPAPI Cryptography (LocalMachine Scope) & Secret Retriever
   ================================================================================
     [ PASS ] DPAPI Machine Protect : Successfully protected payload (36 bytes -> 248 cipher bytes)
     [ PASS ] DPAPI Machine Unprotect : Successfully decrypted payload matching original string
     [ PASS ] Secret Retriever End-to-End : REG_BINARY DPAPI value verified compatible with WindowsRegistrySecretRetriever

   ================================================================================
     4. Windows Identity Subsystem & S-1-5-32-544 SID Mapping
   ================================================================================
     [ PASS ] Current Windows Identity : User: DOMAIN\s_pelech | SID: S-1-5-21-... | AuthType: Kerberos
     [ PASS ] Windows Groups Extracted : Extracted 18 security group SIDs for current identity
     [ PASS ] Builtin Admin SID (S-1-5-32-544) : Current token contains Builtin Administrators SID 'S-1-5-32-544'
     [ PASS ] IWindowsIdentityAccessor Contract : Validated User SID and Group SID list extraction logic matching WindowsIdentityAccessor

   ================================================================================
     Diagnostic Summary & Quality Gate Status: PASSED
   ================================================================================
     Total Validations : 14
     Passed            : 14
     Failed            : 0
     Warnings          : 0
     Skipped           : 0
   ```

2. **Execute C# Solution Tests**:
   ```powershell
   dotnet test McpRouter.slnx --logger "console;verbosity=normal"
   ```

3. **Execute Requirements Catalog Zero-Drift Verification**:
   ```powershell
   dotnet run --project scripts/CatalogGenerator -- --verify-only
   ```

---

## 🔒 7. Production Operations, Security Hardening & Observability

### SSL/TLS Certificates & HTTPS Bindings

For enterprise production deployments on IIS, configure HTTPS bindings:
1. **Corporate PKI Certificate**: Import your enterprise certificate into `Certificates (Local Computer) -> Personal`.
2. **Automated ACME (Let's Encrypt)**: Use `win-acme` (wacs.exe) to automatically provision and renew certificates:
   ```cmd
   wacs.exe --target iissite --siteid 1 --host mcp.domain.local
   ```
3. In IIS Manager, add HTTPS binding on port 443 with SNI enabled.

### Health Probes & Monitoring

The router exposes a structured health endpoint at `/health` for uptime monitors (e.g. PRTG, Uptime Kuma, Nagios):

```powershell
# Probe health endpoint:
Invoke-RestMethod -Uri "http://localhost:8080/health"
```

*Response Contract:*
```json
{
  "status": "Healthy",
  "version": "v4.17.0",
  "database": {
    "provider": "SQLite",
    "connected": true
  },
  "servers": {
    "total": 14,
    "healthy": 14
  },
  "sessions": {
    "active": 2
  }
}
```

### Prometheus Metrics Scraping

Configure Prometheus to scrape `GET /metrics`:
- `mcp_router_active_sessions_total`: Active SSE client connections.
- `mcp_router_tool_executions_total`: Tool call throughput by server ID and response status code.
- `mcp_router_tool_execution_duration_seconds`: Execution latency histogram.

### Logging Architecture (IIS, Stdout, Windows Event Log)

1. **IIS W3C Logs**: Located at `C:\inetpub\logs\LogFiles\W3SVC*`.
2. **ASP.NET Core Stdout Logs**: Enable in `web.config` (`stdoutLogEnabled="true"`) to log unhandled crashes to `C:\inetpub\mcg\logs\stdout\*.log`.
3. **Windows Event Log**: Service startup events and catastrophic unhandled exceptions are logged under **Event Viewer -> Windows Logs -> Application** (Source: `IIS AspNetCore Module V2` or `ModelContextGateway`).

### Database Backup & Recovery on Windows

1. **SQLite Provider**:
   ```powershell
   # Safe online backup without locking:
   sqlite3 "C:\inetpub\mcg\mcg.db" ".backup 'C:\backups\mcg-backup.db'"
   ```
2. **Microsoft SQL Server Provider**:
   ```sql
   BACKUP DATABASE [McpGatewayDb]
   TO DISK = N'C:\backups\McpGatewayDb_Full.bak'
   WITH FORMAT, INIT, COMPRESSION, STATS = 10;
   ```

---

## 🛠️ 8. Comprehensive Troubleshooting Guide

### 1. SSE Streaming Hangs or Responses Are Buffered

**Symptom**: LLM clients connect to `/sse`, but tool execution responses or semantic search results do not stream in real time; responses arrive in one large batch after a delay or time out.

**Root Causes & Solutions**:
1. **IIS Response Buffering Enabled**:
   - Check `web.config`. Ensure `<aspNetCore ... responseBufferLimit="0">` is present.
2. **IIS Dynamic Compression Enabled**:
   - Check `web.config`. Ensure `<urlCompression doDynamicCompression="false" />` is set.
3. **Upstream Reverse Proxy Buffering**:
   - If an external reverse proxy (NGINX, Caddy, Cloudflare) sits in front of IIS, disable proxy buffering (`proxy_buffering off;` in NGINX, or bypass buffering in Cloudflare).

---

### 2. DPAPI Decryption Failure (`CryptographicException`)

**Symptom**: `WindowsRegistrySecretRetriever` logs `CryptographicException: The system cannot find the file specified` or `Keyset does not exist`.

**Root Causes & Solutions**:
1. **DPAPI Scope Mismatch**:
   - Secrets protected with `DataProtectionScope.CurrentUser` can only be decrypted by the user who encrypted them.
   - Secrets must be encrypted using `DataProtectionScope.LocalMachine` (default in `Set-RegistrySecrets.ps1 -Encrypt`).
2. **Application Pool / Service Account Permissions**:
   - When running under `IIS AppPool\McgAppPool` or `NT AUTHORITY\NetworkService`, verify that the account has access to the machine key container (`C:\ProgramData\Microsoft\Crypto\RSA\MachineKeys`).

---

### 3. Integrated Windows Authentication Fails (401 Unauthorized)

**Symptom**: Requests to `/api/auth/me` return `401 Unauthorized` or fail to negotiate Kerberos tickets.

**Root Causes & Solutions**:
1. **IIS Windows Authentication Feature Not Installed**:
   - Run `Install-WindowsFeature Web-Windows-Auth`.
2. **Windows Authentication Disabled in IIS**:
   - In IIS Manager, select the site -> **Authentication** -> Enable **Windows Authentication**.
3. **Service Principal Name (SPN) Missing for Custom Domain**:
   - If accessing via custom hostname (e.g. `http://mcp.domain.local`), register SPNs on the service account:
     ```cmd
     setspn -s HTTP/mcp.domain.local DOMAIN\svc_mcp
     setspn -s HTTP/mcp DOMAIN\svc_mcp
     ```

---

### 4. Kestrel Port Conflict (`System.IO.IOException: Failed to bind to address`)

**Symptom**: Windows Service or Standalone Kestrel fails to start with port conflict error.

**Root Causes & Solutions**:
1. Identify process occupying the port:
   ```powershell
   Get-NetTCPConnection -LocalPort 8080 | Select-Object LocalAddress, LocalPort, OwningProcess
   ```
2. If another service is listening, either stop that service or reconfigure gateway to use an alternative port:
   ```powershell
   .\scripts\windows\Setup-WindowsService.ps1 -Action Restart -Port 8090
   ```

---

### 5. IIS HTTP Error 500.19 or 500.30 (In-Process Startup Failure)

**Symptom**: Browsing the website returns `HTTP Error 500.19 - Internal Server Error` or `HTTP Error 500.30 - ANCM In-Process Start Failure`.

**Root Causes & Solutions**:
1. **.NET Hosting Bundle Missing**:
   - Install the [.NET 10 Windows Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0) and restart IIS (`iisreset`).
2. **AppPool Managed Runtime Version Incorrect**:
   - Set .NET CLR Version in AppPool settings to `No Managed Code`.
3. **Diagnose via Stdout Logs**:
   - Edit `web.config`: set `stdoutLogEnabled="true"`.
   - Re-run the request and inspect the generated log in `C:\inetpub\mcg\logs\stdout\`.
