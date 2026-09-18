# Enterprise Validation Runbook & Troubleshooting Guide

![Validation Runbook](https://img.shields.io/badge/Runbook-4--Scenario%20Validation-0052CC?style=for-the-badge&logo=testing-library&logoColor=white)
![Quality Gates](https://img.shields.io/badge/Quality%20Gate-Verified%20Zero--Drift-green?style=for-the-badge&logo=checkmarx&logoColor=white)
![Troubleshooting](https://img.shields.io/badge/Troubleshooting-RCA%20Matrix-red?style=for-the-badge&logo=datadog&logoColor=white)

This guide provides end-to-end verification procedures, automated diagnostic scripts, observability configurations, and a comprehensive root-cause analysis (RCA) troubleshooting matrix for **Model Context Gateway (MCG)** deployments.

---

## 🧪 4-Scenario End-to-End Validation Runbook

The following validation runbook provides step-by-step verification procedures across four core runtime scenarios:

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
        S4A["Test-WindowsEnvironment.ps1"] --> S4B["dotnet test ModelContextGateway.slnx"]
        S4B --> S4C["CatalogGenerator --verify-only"]
        S4C --> S4D["Zero-Drift & 100% Pass"]
    end

    classDef s1Style fill:#1a2332,stroke:#0078d6,stroke-width:1.5px,color:#fff;
    classDef s2Style fill:#161b22,stroke:#ff5f1f,stroke-width:1.5px,color:#fff;
    classDef s3Style fill:#0f2e1b,stroke:#00c853,stroke-width:1.5px,color:#fff;
    classDef s4Style fill:#2d1b4e,stroke:#a855f7,stroke-width:1.5px,color:#fff;
    class S1A,S1B,S1C,S1D s1Style;
    class S2A,S2B,S2C,S2D s2Style;
    class S3A,S3B,S3C,S3D s3Style;
    class S4A,S4B,S4C,S4D s4Style;
```

---

### Scenario 1: Active Directory & Windows Integrated Authentication

#### Objective
Verify that `IWindowsIdentityAccessor` successfully extracts user SIDs and group SIDs (including the default Local Administrators SID `S-1-5-32-544`), and that `ActiveDirectoryIdentityProvider` applies role-based access controls correctly under IIS or Kestrel Negotiate authentication.

#### Step-by-Step Validation

1. **Inspect Identity & SIDs in PowerShell**:
   ```powershell
   $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
   Write-Host "Current User: $($identity.Name)" -ForegroundColor Cyan
   Write-Host "User SID    : $($identity.User.Value)" -ForegroundColor Cyan
   Write-Host "Group SIDs  :" -ForegroundColor Cyan
   $identity.Groups | ForEach-Object { Write-Host " - $($_.Value)" -ForegroundColor DarkGray }
   ```

2. **Test Windows Integrated Authentication on IIS**:
   Issue an authenticated request passing default Kerberos/NTLM credentials:
   ```powershell
   $response = Invoke-RestMethod -Uri "http://localhost:8080/api/auth/me" -UseDefaultCredentials
   $response | ConvertTo-Json -Depth 4
   ```

   *Expected JSON Response:*
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
   The gateway automatically recognizes members of the local `Administrators` security group (`S-1-5-32-544`) and maps them to the `admin` role without requiring manual group mappings in the database.

---

### Scenario 2: Windows Registry Secrets & DPAPI Encryption

#### Objective
Verify that credentials stored in `HKLM:\SOFTWARE\McpRouter\Secrets` with DPAPI encryption (`DataProtectionScope.LocalMachine`) are accurately decrypted by `WindowsRegistrySecretRetriever` at runtime without credential leakage.

#### Step-by-Step Validation

1. **Store a DPAPI-Encrypted Secret using `Set-RegistrySecrets.ps1`**:
   ```powershell
   .\scripts\windows\Set-RegistrySecrets.ps1 -SecretName "DockerApiKey" -SecretValue "dckr_pat_secret_token_12345" -Encrypt
   ```

2. **Store a Plaintext String Secret**:
   ```powershell
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

5. **Test in Gateway Runtime**:
   Configure a backend server with `SecretProvider = "WindowsRegistry"`, `SecretPath = "SOFTWARE\McpRouter\Secrets"`, and `SecretItemKey = "DockerApiKey"`. Execute a tool from that server in the Test Bench (`http://localhost:8080/#/testbench`) and confirm successful execution.

---

### Scenario 3: STDIO Transport Subprocess Execution on Windows

#### Objective
Verify that the gateway spawns local Windows processes (`.exe`, `.cmd`, `node.exe`, `python.exe`), injects credentials through environment variables without command-line leakage, and manages child process lifecycles safely.

#### Step-by-Step Validation

1. **Verify Executable Path Resolution**:
   ```powershell
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
   While the tool runs, inspect active processes in PowerShell:
   ```powershell
   Get-CimInstance Win32_Process -Filter "Name like '%node%'" | Select-Object ProcessId, CommandLine
   ```
   Verify that secrets do **not** appear in `CommandLine`. Secrets pass exclusively inside `ProcessStartInfo.Environment["API_KEY"]`.

4. **Execute Tools in the Test Bench**:
   Open `http://localhost:8080/#/testbench`, select tools for `LocalFilesystemMcp` (such as `list_directory`), and verify tool execution.

---

### Scenario 4: Automated Environment Diagnostics & Quality Gates

#### Objective
Execute comprehensive environment diagnostic validation, run backend test suites, and verify living requirements catalog compliance.

#### Step-by-Step Validation

1. **Run Automated Diagnostic Tool (`Test-WindowsEnvironment.ps1`)**:
   ```powershell
   .\scripts\windows\Test-WindowsEnvironment.ps1 -JsonReportPath ".\diagnostics-report.json"
   ```

   *Expected Diagnostic Output:*
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

2. **Run Solution Test Suites**:
   ```bash
   dotnet test ModelContextGateway.slnx --logger "console;verbosity=normal"
   ```

3. **Verify Living Requirements Catalog**:
   ```bash
   dotnet run --project scripts/CatalogGenerator -- --verify-only
   ```

---

## 📊 Production Operations, Observability & Health

### Health Probes & Monitoring

The gateway provides a structured status endpoint at `GET /health` for monitoring tools (PRTG, Prometheus Blackbox, Uptime Kuma):

```bash
curl -s http://localhost:8080/health
```

*Response Schema:*
```json
{
  "status": "Healthy",
  "version": "5.11.0",
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
- `mcp_router_active_sessions_total`: Active SSE client sessions.
- `mcp_router_tool_executions_total`: Cumulative tool executions grouped by server ID and response status.
- `mcp_router_tool_execution_duration_seconds`: Tool execution latency histogram.

### Logging Architecture
1. **IIS W3C Logs**: Stored under `C:\inetpub\logs\LogFiles\W3SVC*`.
2. **ASP.NET Core Stdout Logs**: Enable in `web.config` (`stdoutLogEnabled="true"`) to capture crashes under `C:\inetpub\mcg\logs\stdout\*.log`.
3. **Windows Event Log**: Service startup and fatal runtime errors log to **Event Viewer -> Windows Logs -> Application** (Source: `IIS AspNetCore Module V2` or `ModelContextGateway`).
4. **Container Stdout/Stderr**: Streamed in real-time via `docker compose logs -f mcg`.

---

## 🛠️ Comprehensive Troubleshooting Matrix

| Symptom / Error | Root Cause | Solution Runbook |
| :--- | :--- | :--- |
| **SSE Streaming Hangs or Tools Timeout** | IIS ANCM output buffering or dynamic compression enabled. | 1. In `web.config`, ensure `<aspNetCore ... responseBufferLimit="0">` is present.<br>2. In `web.config`, ensure `<urlCompression doDynamicCompression="false" />` is configured.<br>3. If fronted by Caddy/Nginx, disable proxy buffering (`flush_interval -1` or `proxy_buffering off;`). |
| **DPAPI Decryption Failure (`CryptographicException: Keyset does not exist`)** | DPAPI scope mismatch or service account lacks access to machine keys. | 1. Ensure secrets were encrypted with `DataProtectionScope.LocalMachine` (default in `Set-RegistrySecrets.ps1 -Encrypt`), not `CurrentUser`.<br>2. Grant `IIS AppPool\ModelContextGatewayAppPool` or service account read access to `C:\ProgramData\Microsoft\Crypto\RSA\MachineKeys`. |
| **Windows Authentication Fails (`HTTP 401 Unauthorized`)** | `Web-Windows-Auth` feature missing, disabled in IIS, or missing SPN for custom hostname. | 1. Run `Install-WindowsFeature Web-Windows-Auth`.<br>2. In IIS Manager, enable **Windows Authentication** for the site.<br>3. If using custom hostname (e.g. `mcp.corp.local`), register SPNs: `setspn -s HTTP/mcp.corp.local DOMAIN\svc_mcp`. |
| **Kestrel Port Conflict (`Failed to bind to address`)** | Another service or orphaned `mcg.exe` process is listening on the target port. | 1. Identify owner: `Get-NetTCPConnection -LocalPort 8080 \| Select-Object LocalPort, OwningProcess`.<br>2. Terminate the conflicting process or change MCG port: `Setup-WindowsService.ps1 -Action Restart -Port 8090`. |
| **IIS Error 500.19 or 500.30 (In-Process Startup Failure)** | .NET 10 Windows Hosting Bundle missing or AppPool configured with managed runtime. | 1. Install [.NET 10 Windows Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0) and run `iisreset`.<br>2. Set AppPool .NET CLR Version to `No Managed Code`.<br>3. Enable stdout logging in `web.config` (`stdoutLogEnabled="true"`) and inspect `logs\stdout\`. |
| **Reverse Proxy Strips Identity Headers / Downgrades to Guest** | Upstream reverse proxy IP address is not listed in `Oidc:TrustedProxies`. | Set `Oidc__TrustedProxies` (e.g. `Oidc__TrustedProxies=10.0.0.1,172.18.0.1`) to include the internal container IP or reverse proxy IP. |
| **Master Key Missing or Decryption Error (`CryptographicException: The payload was invalid`)** | Database was created with a different master key than currently configured. | 1. Ensure `./data/.master.key` was not overwritten or deleted.<br>2. If using `MCG_MASTER_KEY`, ensure the base64 string matches the initial key used to create `./data/mcg.db`. |
| **SQLite Database Locked (`SQLite Error 5: 'database is locked'`)** | Multiple processes accessing SQLite without WAL mode or long uncommitted transaction. | 1. Ensure connection string includes `Mode=ReadWriteCreate;Cache=Shared;`.<br>2. MCG enables WAL mode automatically on startup; ensure disk is not mounted over network NFS with broken file locking. |
