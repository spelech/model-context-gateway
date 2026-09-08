# Windows Deployment, Enterprise Hosting and Validation Guide

![Windows Server](https://img.shields.io/badge/Windows%20Server-2022%20%7C%202025-0078D6?style=for-the-badge&logo=windows&logoColor=white)
![IIS In-Process](https://img.shields.io/badge/IIS-In--Process%20ANCM-0052CC?style=for-the-badge&logo=windows-terminal&logoColor=white)
![Windows Service](https://img.shields.io/badge/Service-SCM%20Auto--Recovery-2ea44f?style=for-the-badge&logo=powershell&logoColor=white)
![DPAPI Protected](https://img.shields.io/badge/Secrets-Registry%20DPAPI-orange?style=for-the-badge&logo=shield&logoColor=white)
![Living Catalog](https://img.shields.io/badge/Quality%20Gate-Verified%20Zero--Drift-green?style=for-the-badge)

This guide explains how to deploy, configure, validate, and operate **Model Context Gateway (MCG)** on Microsoft Windows Server and Windows 10/11. Model Context Gateway routes and secures communication for the Model Context Protocol (MCP).

---

## 📑 Table of Contents

1. [Executive Architecture and Windows Subsystems](#1-executive-architecture-and-windows-subsystems)
   - [Architectural Topology and Gateway Model](#architectural-topology-and-gateway-model)
   - [Native Windows Subsystems](#native-windows-subsystems)
   - [Hosting Options Comparison Matrix](#hosting-options-comparison-matrix)
2. [Prerequisites and Host Preparation](#2-prerequisites-and-host-preparation)
   - [Operating System and Hardware](#operating-system-and-hardware)
   - [Runtimes and SDKs](#runtimes-and-sdks)
   - [IIS Roles and Features Installation](#iis-roles-and-features-installation)
   - [PowerShell and Security Privileges](#powershell-and-security-privileges)
3. [Option 1: Production IIS In-Process Deployment](#3-option-1-production-iis-in-process-deployment)
   - [Overview and In-Process Benefits](#overview-and-in-process-benefits)
   - [Automated Deployment with Deploy-IIS.ps1](#automated-deployment-with-deploy-iisps1)
   - [web.config Architectural Deep Dive](#webconfig-architectural-deep-dive)
   - [SSE Streaming and Zero-Buffering Architecture](#sse-streaming-and-zero-buffering-architecture)
   - [Application Pool Tuning and Lifecycle](#application-pool-tuning-and-lifecycle)
   - [Manual IIS Setup Reference](#manual-iis-setup-reference)
4. [Option 2: Managed Windows Service (SCM)](#4-option-2-managed-windows-service-scm)
   - [Overview and Service Architecture](#overview-and-service-architecture)
   - [Automated Lifecycle with Setup-WindowsService.ps1](#automated-lifecycle-with-setup-windowsserviceps1)
   - [Auto-Recovery and SCM Crash Action Configuration](#auto-recovery-and-scm-crash-action-configuration)
   - [Service Account and Security Permissions](#service-account-and-security-permissions)
5. [Option 3: Standalone Kestrel Console](#5-option-3-standalone-kestrel-console)
   - [Developer and Interactive Execution](#developer-and-interactive-execution)
   - [Command-Line Overrides and Ports](#command-line-overrides-and-ports)
6. [End-to-End Validation Runbook (4 Key Scenarios)](#6-end-to-end-validation-runbook-4-key-scenarios)
   - [Scenario 1: Active Directory and Windows Integrated Authentication](#scenario-1-active-directory-and-windows-integrated-authentication)
   - [Scenario 2: Windows Registry Secrets and DPAPI Encryption](#scenario-2-windows-registry-secrets-and-dpapi-encryption)
   - [Scenario 3: STDIO Transport Subprocess Execution on Windows](#scenario-3-stdio-transport-subprocess-execution-on-windows)
   - [Scenario 4: Automated Environment Diagnostics and Quality Gates](#scenario-4-automated-environment-diagnostics-and-quality-gates)
7. [Production Operations, Security Hardening and Observability](#7-production-operations-security-hardening-and-observability)
   - [SSL/TLS Certificates and HTTPS Bindings](#ssltls-certificates-and-https-bindings)
   - [Health Probes and Monitoring](#health-probes-and-monitoring)
   - [Prometheus Metrics Scraping](#prometheus-metrics-scraping)
   - [Logging Architecture (IIS, Stdout, Windows Event Log)](#logging-architecture-iis-stdout-windows-event-log)
   - [Database Backup and Recovery on Windows](#database-backup-and-recovery-on-windows)
8. [Comprehensive Troubleshooting Guide](#8-comprehensive-troubleshooting-guide)

---

## 🏛️ 1. Executive Architecture and Windows Subsystems

### Architectural Topology and Gateway Model

Model Context Gateway (MCG) acts as a gateway and proxy for the Model Context Protocol (MCP). On Windows Server, it connects native Windows infrastructure (Active Directory, DPAPI, IIS, and Windows Services) to downstream MCP tools.

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

1. **Active Directory and Windows Integrated Authentication (`WindowsIdentity`)**:
   - Implemented in [`IWindowsIdentityAccessor.cs`](https://github.com/spelech/model-context-gateway/blob/main/Infrastructure/Identity/IWindowsIdentityAccessor.cs) and [`ActiveDirectoryIdentityProvider.cs`](https://github.com/spelech/model-context-gateway/blob/main/Infrastructure/Identity/ActiveDirectoryIdentityProvider.cs).
   - Reads user identity, primary Security Identifier (SID), and group SIDs directly from `WindowsIdentity.Groups` when running under IIS or Kestrel Negotiate authentication.
   - Identifies default security groups such as `S-1-5-32-544` (Local Administrators) for role access checks without external LDAP queries.

2. **Windows Registry and DPAPI Cryptography (`WindowsRegistrySecretRetriever`)**:
   - Implemented in [`WindowsRegistrySecretRetriever.cs`](https://github.com/spelech/model-context-gateway/blob/main/Infrastructure/Secrets/WindowsRegistrySecretRetriever.cs).
   - Reads secrets from `HKLM:\SOFTWARE\McpRouter\Secrets`.
   - Supports plaintext `REG_SZ` strings and encrypted `REG_BINARY` values protected by the Windows Data Protection API (DPAPI) under `DataProtectionScope.LocalMachine`.
   - Stores machine-level secrets securely without putting credentials in source files.

3. **Subprocess STDIO Transport Isolation (`StdioTransport`)**:
   - Implemented in [`StdioTransport.cs`](https://github.com/spelech/model-context-gateway/blob/main/Infrastructure/Transports/StdioTransport.cs).
   - Runs Windows programs (`.exe`, `.cmd`, `.bat`, `node.exe`, `python.exe`) with Standard Input and Output (STDIO) pipe redirection.
   - Enforces **Zero Command-Line Secret Leakage**: The gateway passes secrets only through `ProcessStartInfo.Environment`, never as command-line arguments.

---

### Hosting Options Comparison Matrix

| Feature and Attribute | Option 1: IIS In-Process (Recommended) | Option 2: Managed Windows Service | Option 3: Standalone Kestrel Console |
| :--- | :--- | :--- | :--- |
| **Primary Use Case** | Enterprise production servers, shared web hosts | Dedicated application servers, background daemons | Local testing, debugging, CI pipelines |
| **Hosting Model** | In-Process inside `w3wp.exe` through `AspNetCoreModuleV2` | Standalone executable managed by Windows SCM | Direct `dotnet run` or interactive binary |
| **Throughput and Latency** | **Fastest** (direct in-memory execution in IIS worker process) | High (direct Kestrel HTTP pipeline) | High (development pipeline) |
| **Port Sharing and Bindings** | Full HTTP.sys port sharing (ports 80, 443, multiple hosts) | Dedicated port binding (such as `http://0.0.0.0:8080`) | Dedicated port binding (such as `http://localhost:5000`) |
| **SSL/TLS Termination** | Windows Certificate Store, SNI, win-acme Let's Encrypt | Kestrel certificate binding or reverse proxy | Developer certificates or HTTP only |
| **SSE Streaming Settings** | Supported with `responseBufferLimit="0"` | Native (zero buffering in Kestrel) | Native |
| **Automatic Crash Recovery** | IIS Application Pool restart and health monitoring | Service Control Manager failure restart triggers | Manual restart or console loop |
| **Integrated Authentication** | Native IIS Negotiate, Kerberos, and NTLM modules | Kestrel Negotiate or header authentication | Negotiate or Anonymous |
| **Automation Script** | [`Deploy-IIS.ps1`](https://github.com/spelech/model-context-gateway/blob/main/scripts/windows/Deploy-IIS.ps1) | [`Setup-WindowsService.ps1`](https://github.com/spelech/model-context-gateway/blob/main/scripts/windows/Setup-WindowsService.ps1) | Command prompt or PowerShell |

---

## 🛠️ 2. Prerequisites and Host Preparation

### Operating System and Hardware

- **Operating System**: Windows Server 2025, Windows Server 2022, Windows Server 2019, or Windows 10/11 (64-bit x64 or arm64).
- **CPU and Memory**: Minimum 2 Cores and 4 GB RAM (8 GB or more recommended for local ONNX embeddings).
- **Disk Space**: Minimum 2 GB free disk space for application files, SQLite databases, and models.

### Runtimes and SDKs

1. **.NET 10 SDK and ASP.NET Core 10 Windows Hosting Bundle**:
   - Install the [.NET 10 Windows Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0). This bundle includes the runtime and the **ASP.NET Core Module v2 (ANCM)** for IIS.
   - Verify installation in PowerShell:
     ```powershell
     dotnet --info
     ```

2. **Node.js and npm (Required for Web Dashboard Build)**:
   - Install Node.js LTS (v20.x or v22.x) from [nodejs.org](https://nodejs.org/).
   - Check installed versions:
     ```powershell
     node -v
     npm -v
     ```

### IIS Roles and Features Installation

To install IIS and required components on Windows Server, run PowerShell as Administrator:

```powershell
# Install IIS, management tools, WebSockets, and Windows Authentication
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

*For Windows 10/11 workstations, enable optional features with `dism`:*
```powershell
Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole, IIS-WebServer, IIS-CommonHttpFeatures, IIS-StaticContent, IIS-DefaultDocument, IIS-DirectoryBrowsing, IIS-HttpErrors, IIS-ApplicationDevelopment, IIS-WebSockets, IIS-Security, IIS-WindowsAuthentication, IIS-RequestFiltering, IIS-WebServerManagementTools, IIS-ManagementConsole -All
```

### PowerShell and Security Privileges

Run all deployment commands from an **Elevated Administrator PowerShell Prompt**. Configure the execution policy to permit script execution:

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope Process -Force
```

---

## 🚀 3. Option 1: Production IIS In-Process Deployment

### Overview and In-Process Benefits

In-Process hosting (`hostingModel="inprocess"`) loads the gateway inside the IIS worker process (`w3wp.exe`). This model provides three key advantages:
1. **Zero Loopback Latency**: Requests process in memory without extra network hops to an external process.
2. **Native Windows Authentication**: Kerberos and NTLM tokens pass directly to `HttpContext.User` as `WindowsIdentity` objects.
3. **Automated Lifecycle Management**: IIS manages worker recycling, idle timeouts, and CPU limits.

---

### Automated Deployment with Deploy-IIS.ps1

The repository includes an automation script: [`scripts/windows/Deploy-IIS.ps1`](https://github.com/spelech/model-context-gateway/blob/main/scripts/windows/Deploy-IIS.ps1).

#### Parameter Reference

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `-SiteName` | string | `"ModelContextGateway"` | Name of the IIS website. |
| `-AppPoolName` | string | `"ModelContextGatewayAppPool"` | Dedicated IIS Application Pool name. |
| `-Port` | int | `8080` | HTTP port for the site binding. |
| `-HostName` | string | `""` | Optional hostname binding (such as `mcp.corp.local`). |
| `-PhysicalPath` | string | `"C:\inetpub\mcg"` | Deployment destination folder on disk. |
| `-Configuration` | string | `"Release"` | Build configuration (`Release` or `Debug`). |
| `-RepoRoot` | string | Auto-resolved | Path to the repository root directory. |
| `-SkipFrontend` | switch | `false` | Skips building the React frontend. |
| `-SkipBuild` | switch | `false` | Skips compilation when binaries exist. |
| `-SelfContained`| switch | `false` | Builds a self-contained binary including the .NET runtime. |
| `-RuntimeIdentifier` | string | `"win-x64"` | Target platform (`win-x64` or `win-arm64`). |
| `-EnableWindowsAuth` | switch | `false` | Turns on Windows Authentication in IIS. |

#### Deployment Commands

```powershell
# Standard deployment on port 8080 with Windows Authentication:
.\scripts\windows\Deploy-IIS.ps1 -SiteName "ModelContextGateway" -Port 8080 -EnableWindowsAuth

# Host header binding deployment (e.g. mcp.company.internal):
.\scripts\windows\Deploy-IIS.ps1 -SiteName "ModelContextGateway" -Port 80 -HostName "mcp.company.internal" -EnableWindowsAuth

# Self-contained deployment to a custom directory:
.\scripts\windows\Deploy-IIS.ps1 -PhysicalPath "D:\Apps\ModelContextGateway" -Port 8443 -SelfContained
```

---

### web.config Architectural Deep Dive

The IIS site uses an optimized `web.config` file based on [`scripts/windows/web.config.example`](https://github.com/spelech/model-context-gateway/blob/main/scripts/windows/web.config.example):

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

### SSE Streaming and Zero-Buffering Architecture

The Model Context Protocol uses Server-Sent Events (`/sse` and `/message`) for real-time JSON-RPC messages and streaming responses.

> [!CAUTION]
> **Why `responseBufferLimit="0"` is Mandatory:**
> By default, IIS buffers outbound HTTP traffic in chunks up to 4 KB before flushing. This buffering breaks MCP SSE streams, causing requests (`search_tools`, `execute_tool`) to hang. Setting `responseBufferLimit="0"` disables ANCM output buffering completely.

> [!IMPORTANT]
> **Disabling Dynamic Compression:**
> IIS dynamic compression (`<urlCompression doDynamicCompression="false" />`) buffers response streams to calculate compression dictionaries. This delay blocks real-time events. Keep static compression enabled for static files, but keep dynamic compression disabled.

---

### Application Pool Tuning and Lifecycle

Configure these Application Pool settings to keep long-running connections active (automatically handled by `Deploy-IIS.ps1`):

1. **.NET CLR Version**: Set to `No Managed Code` (`""`). ANCM loads the .NET runtime directly.
2. **Start Mode**: Set to `AlwaysRunning`. This stops IIS from putting the worker process to sleep.
3. **Idle Time-out**: Set to `0` (Disabled). Prevents dropping downstream MCP connections during idle periods.
4. **Permissions**: Grant the Application Pool identity (`IIS AppPool\McgAppPool`) read and write permissions on the application directory and log folder:
   ```powershell
   icacls "C:\inetpub\mcg" /grant "IIS AppPool\McgAppPool:(OI)(CI)M" /T /Q
   ```

---

### Manual IIS Setup Reference

For offline or manual installations without scripts:

1. Build and publish the gateway:
   ```powershell
   cd frontend; npm run build; cd ..
   dotnet publish ModelContextGateway.csproj -c Release -o C:\inetpub\mcg
   ```
2. Copy `scripts\windows\web.config.example` to `C:\inetpub\mcg\web.config`.
3. Open **IIS Manager (`inetmgr`)**:
   - Create an Application Pool named `McgAppPool` with .NET CLR Version set to `No Managed Code`.
   - In AppPool **Advanced Settings**, set `Start Mode` to `AlwaysRunning` and `Idle Time-out (minutes)` to `0`.
   - Add a website: Name = `ModelContextGateway`, Physical Path = `C:\inetpub\mcg`, Port = `8080`.
   - Under **Authentication**, enable `Windows Authentication` and `Anonymous Authentication`.
4. Grant filesystem permissions to `IIS AppPool\McgAppPool`.

---

## ⚙️ 4. Option 2: Managed Windows Service (SCM)

### Overview and Service Architecture

You can run the gateway directly as a **Windows Service** without IIS. The Windows Service Control Manager (SCM) manages process lifecycle.

- Uses ASP.NET Core Kestrel directly on a configured TCP port.
- Configures automatic restart actions after unexpected errors.
- Runs under `NT AUTHORITY\LocalSystem`, `NT AUTHORITY\NetworkService`, or a domain Group Managed Service Account (gMSA).

---

### Automated Lifecycle with Setup-WindowsService.ps1

The repository includes a service management script: [`scripts/windows/Setup-WindowsService.ps1`](https://github.com/spelech/model-context-gateway/blob/main/scripts/windows/Setup-WindowsService.ps1).

#### Supported Commands

```powershell
# 1. Install and start the service on Port 8080:
.\scripts\windows\Setup-WindowsService.ps1 -Action Install -Port 8080

# 2. Check service status and health:
.\scripts\windows\Setup-WindowsService.ps1 -Action Status

# 3. Restart the service:
.\scripts\windows\Setup-WindowsService.ps1 -Action Restart

# 4. Stop the service:
.\scripts\windows\Setup-WindowsService.ps1 -Action Stop

# 5. Start the service:
.\scripts\windows\Setup-WindowsService.ps1 -Action Start

# 6. Uninstall the service:
.\scripts\windows\Setup-WindowsService.ps1 -Action Uninstall
```

#### Parameter Reference

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `-Action` | string | *(Mandatory)* | `Install`, `Uninstall`, `Start`, `Stop`, `Restart`, or `Status`. |
| `-ServiceName` | string | `"ModelContextGateway"` | Service name in Windows SCM. |
| `-DisplayName` | string | `"Model Context Gateway (MCG) Service"` | Display name shown in Services console. |
| `-InstallDir` | string | `"C:\Program Files\McpRouter"` | Destination folder for published binaries. |
| `-Port` | int | `8080` | Port for the Kestrel HTTP listener. |
| `-Urls` | string | `"http://0.0.0.0:8080"` | Complete URL bindings. |
| `-ServiceAccount` | string | `"NT AUTHORITY\LocalSystem"` | Account running the service (such as `DOMAIN\svc_mcp$`). |
| `-Configuration` | string | `"Release"` | Build mode (`Release` or `Debug`). |
| `-SelfContained` | switch | `false` | Publishes a self-contained executable. |

---

### Auto-Recovery and SCM Crash Action Configuration

`Setup-WindowsService.ps1` configures restart triggers automatically with `sc.exe`:

```powershell
# Automatically restart the service after 60 seconds on crashes:
sc.exe failure McpRouter reset= 86400 actions= restart/60000/restart/60000/restart/60000
sc.exe failureflag McpRouter 1
```

---

### Service Account and Security Permissions

When you run under a restricted domain account or gMSA (`DOMAIN\svc_mcp`):
1. Grant the service account read and write permissions on `C:\Program Files\McpRouter`.
2. Grant read permissions on the registry key `HKLM:\SOFTWARE\McpRouter\Secrets`.
3. Reserve the URL port binding with `netsh`:
   ```cmd
   netsh http add urlacl url=http://+:8080/ user="DOMAIN\svc_mcp"
   ```

---

## 💻 5. Option 3: Standalone Kestrel Console

Use standalone console mode for local development, testing, or debugging on Windows:

### Developer and Interactive Execution

```powershell
# Run from repository root in Development mode:
dotnet run --project ModelContextGateway.csproj --urls "http://localhost:5000"
```

### Command-Line Overrides and Ports

```powershell
# Run published binary in Production mode:
$env:ASPNETCORE_ENVIRONMENT="Production"
$env:DB_PROVIDER="sqlite"
$env:MCG_MASTER_KEY="your_32_char_secure_hex_master_key_here"
.\bin\Release\net10.0\win-x64\publish\mcg.exe --urls "http://0.0.0.0:8080"
```

---

## 🧪 6. End-to-End Validation Runbook (4 Key Scenarios)

This section provides verification procedures for native Windows features across 4 core scenarios.

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

### Scenario 1: Active Directory and Windows Integrated Authentication

#### Objective
Verify that `IWindowsIdentityAccessor` extracts user SIDs and group SIDs (including `S-1-5-32-544`), and that `ActiveDirectoryIdentityProvider` applies role-based access controls correctly.

#### Step-by-Step Validation

1. **Check Identity and SIDs in PowerShell**:
   ```powershell
   # Inspect current token identity and security SIDs:
   $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
   Write-Host "Current User: $($identity.Name)" -ForegroundColor Cyan
   Write-Host "User SID    : $($identity.User.Value)" -ForegroundColor Cyan
   Write-Host "Group SIDs  :" -ForegroundColor Cyan
   $identity.Groups | ForEach-Object { Write-Host " - $($_.Value)" -ForegroundColor DarkGray }
   ```

2. **Test Windows Integrated Authentication on IIS**:
   ```powershell
   # Test with current logon credentials:
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

3. **Verify Builtin Administrator Access (`S-1-5-32-544`)**:
   - The gateway automatically recognizes members of the local `Administrators` group (`S-1-5-32-544`) and grants administrative access without manual database mappings.

---

### Scenario 2: Windows Registry Secrets and DPAPI Encryption

#### Objective
Verify that credentials stored in `HKLM:\SOFTWARE\McpRouter\Secrets` with DPAPI encryption (`DataProtectionScope.LocalMachine`) are decrypted by `WindowsRegistrySecretRetriever` at runtime.

#### Step-by-Step Validation

1. **Store a DPAPI-Encrypted Secret using `Set-RegistrySecrets.ps1`**:
   ```powershell
   # Write an encrypted token:
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

5. **Test in the Gateway Runtime**:
   - Configure a backend server with `SecretProvider = "WindowsRegistry"`, `SecretPath = "SOFTWARE\McpRouter\Secrets"`, and `SecretItemKey = "DockerApiKey"`.
   - Run a tool from that server in the Test Bench. The gateway reads and decrypts the value automatically.

---

### Scenario 3: STDIO Transport Subprocess Execution on Windows

#### Objective
Verify that the gateway spawns local Windows processes, injects credentials through environment variables without command-line leakage, and manages child processes safely.

#### Step-by-Step Validation

1. **Verify Executable Path Resolution**:
   ```powershell
   # Verify node and python tools:
   Get-Command node, python, npx, uvx -ErrorAction SilentlyContinue | Select-Object Name, Source
   ```

2. **Register a Local STDIO Server**:
   Create a sample STDIO server in the gateway:
   - **Name**: `LocalFilesystemMcp`
   - **Transport**: `stdio`
   - **Command / URL**: `npx -y @modelcontextprotocol/server-filesystem C:\data`
   - **Secret Provider**: `WindowsRegistry`
   - **Secret Path**: `SOFTWARE\McpRouter\Secrets`
   - **Secret Key**: `FilesystemSecretKey`

3. **Confirm Zero Command-Line Secret Leakage**:
   - While the tool runs, inspect active processes in PowerShell:
     ```powershell
     Get-CimInstance Win32_Process -Filter "Name like '%node%'" | Select-Object ProcessId, CommandLine
     ```
   - Verify that secrets do **not** appear in `CommandLine`. Secrets pass exclusively inside `ProcessStartInfo.Environment["API_KEY"]`.

4. **Execute Tools in the Test Bench**:
   - Open the dashboard: `http://localhost:8080/#/testbench`.
   - Select tools for `LocalFilesystemMcp` (such as `list_directory`).
   - Run the tool and verify streaming output.

---

### Scenario 4: Automated Environment Diagnostics and Quality Gates

#### Objective
Run automated diagnostic checks, execute backend test suites, and confirm living requirements catalog compliance.

#### Step-by-Step Validation

1. **Run the Diagnostic Tool (`Test-WindowsEnvironment.ps1`)**:
   ```powershell
   # Run full diagnostics and output a JSON report:
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

2. **Run Solution Tests**:
   ```powershell
   dotnet test McpRouter.slnx --logger "console;verbosity=normal"
   ```

3. **Verify Living Requirements Catalog**:
   ```powershell
   dotnet run --project scripts/CatalogGenerator -- --verify-only
   ```

---

## 🔒 7. Production Operations, Security Hardening and Observability

### SSL/TLS Certificates and HTTPS Bindings

For enterprise production deployments on IIS, configure HTTPS bindings:
1. **Enterprise Certificate**: Import your certificate into `Certificates (Local Computer) -> Personal`.
2. **Automated ACME (Let's Encrypt)**: Use `win-acme` (`wacs.exe`) to renew certificates automatically:
   ```cmd
   wacs.exe --target iissite --siteid 1 --host mcp.domain.local
   ```
3. In IIS Manager, add an HTTPS binding on port 443 with Server Name Indication (SNI) enabled.

### Health Probes and Monitoring

The gateway provides a structured status endpoint at `/health` for monitoring tools (such as PRTG, Uptime Kuma, and Nagios):

```powershell
# Probe health endpoint:
Invoke-RestMethod -Uri "http://localhost:8080/health"
```

*Response Schema:*
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

Configure Prometheus to collect metrics from `GET /metrics`:
- `mcp_router_active_sessions_total`: Active SSE client sessions.
- `mcp_router_tool_executions_total`: Tool call throughput grouped by server ID and response status code.
- `mcp_router_tool_execution_duration_seconds`: Tool execution latency histogram.

### Logging Architecture (IIS, Stdout, Windows Event Log)

1. **IIS W3C Logs**: Stored under `C:\inetpub\logs\LogFiles\W3SVC*`.
2. **ASP.NET Core Stdout Logs**: Enable in `web.config` (`stdoutLogEnabled="true"`) to capture crashes under `C:\inetpub\mcg\logs\stdout\*.log`.
3. **Windows Event Log**: Service startup and fatal runtime errors log to **Event Viewer -> Windows Logs -> Application** (Source: `IIS AspNetCore Module V2` or `ModelContextGateway`).

### Database Backup and Recovery on Windows

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

**Symptom**: LLM clients connect to `/sse`, but tool output arrives in large chunks or times out.

**Root Causes and Solutions**:
1. **IIS Output Buffering Enabled**:
   - Check `web.config`. Ensure `<aspNetCore ... responseBufferLimit="0">` is present.
2. **IIS Dynamic Compression Enabled**:
   - Check `web.config`. Ensure `<urlCompression doDynamicCompression="false" />` is set.
3. **Reverse Proxy Buffering**:
   - If an external reverse proxy (NGINX, Caddy, Cloudflare) sits in front of IIS, disable response buffering on that proxy.

---

### 2. DPAPI Decryption Failure (`CryptographicException`)

**Symptom**: `WindowsRegistrySecretRetriever` logs `CryptographicException: The system cannot find the file specified` or `Keyset does not exist`.

**Root Causes and Solutions**:
1. **DPAPI Scope Mismatch**:
   - Secrets encrypted with `DataProtectionScope.CurrentUser` can only be decrypted by that specific user.
   - Encrypt secrets using `DataProtectionScope.LocalMachine` (default in `Set-RegistrySecrets.ps1 -Encrypt`).
2. **Application Pool Permissions**:
   - If running under `IIS AppPool\McgAppPool` or `NT AUTHORITY\NetworkService`, verify read access to machine keys (`C:\ProgramData\Microsoft\Crypto\RSA\MachineKeys`).

---

### 3. Integrated Windows Authentication Fails (HTTP 401 Unauthorized)

**Symptom**: Calls to `/api/auth/me` return `401 Unauthorized` or fail Kerberos ticket exchange.

**Root Causes and Solutions**:
1. **Windows Authentication Missing**:
   - Install the role with `Install-WindowsFeature Web-Windows-Auth`.
2. **Windows Authentication Disabled in IIS**:
   - In IIS Manager, select the site -> **Authentication** -> Enable **Windows Authentication**.
3. **Service Principal Name (SPN) Missing for Custom Domain**:
   - When using custom domain names, register Service Principal Names (SPNs) on the service account:
     ```cmd
     setspn -s HTTP/mcp.domain.local DOMAIN\svc_mcp
     setspn -s HTTP/mcp DOMAIN\svc_mcp
     ```

---

### 4. Kestrel Port Conflict (`System.IO.IOException: Failed to bind to address`)

**Symptom**: Service fails to start due to port conflicts.

**Root Causes and Solutions**:
1. Identify the process using the port:
   ```powershell
   Get-NetTCPConnection -LocalPort 8080 | Select-Object LocalAddress, LocalPort, OwningProcess
   ```
2. Stop the conflicting process, or reconfigure the gateway port:
   ```powershell
   .\scripts\windows\Setup-WindowsService.ps1 -Action Restart -Port 8090
   ```

---

### 5. IIS Error 500.19 or 500.30 (In-Process Startup Failure)

**Symptom**: Browsing the site returns `HTTP Error 500.19 - Internal Server Error` or `HTTP Error 500.30 - ANCM In-Process Start Failure`.

**Root Causes and Solutions**:
1. **Hosting Bundle Missing**:
   - Install the [.NET 10 Windows Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0) and restart IIS (`iisreset`).
2. **Application Pool CLR Version**:
   - Set .NET CLR Version in AppPool settings to `No Managed Code`.
3. **Inspect Logs**:
   - In `web.config`, set `stdoutLogEnabled="true"`.
   - Send a request and check log files under `C:\inetpub\mcg\logs\stdout\`.
