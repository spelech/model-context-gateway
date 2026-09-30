# Troubleshooting Subprocess & STDIO Transports

[Home](../../index.md) > [Troubleshooting & RCA Overview](../../mcp-routing-and-admin-issues.md) > Subprocess & STDIO Transports

## 1. Overview

Model Context Gateway (MCG) supports local process execution via the `stdio` transport. This allows the gateway to launch and communicate with STDIO-based MCP server executables (such as Node.js CLI packages, Python scripts, or native binaries) directly or inside container environments like `ghcr.io/spelech/model-context-gateway:latest-full`.

This runbook provides diagnostic and remediation steps for subprocess execution issues, timeouts, stdio line-buffering problems, and process signal handling.

---

## 2. Common Symptoms & Diagnostic Indicators

| Symptom | Observed Log Message / Behavior | Primary Cause |
| :--- | :--- | :--- |
| **Tool Execution Timeout** | `OperationCanceledException: Subprocess tool call timed out after 30000ms` | Process hung waiting on stdin input or network call |
| **Silent Hang / No Output** | Process started (`PID 1234`), but tool call never returns response | Stdio line-buffering enabled in Python or Node.js runtime |
| **Zombie Process Accumulation** | High memory/CPU, orphaned `node` or `python3` processes | Missing process termination traps or improper SIGTERM/SIGKILL handling |
| **Executable Not Found** | `Win32Exception: The system cannot find the file specified` | Binary (`uv`, `npx`, `bun`) missing from container `$PATH` |

---

## 3. Diagnostic Procedures & Root Cause Analysis

### Diagnostic Step 1: Verify Container Runtime & `$PATH` Availability
In STDIO mode, the gateway executes target commands directly on the host or inside the container.
- If using standard runtime image `latest`, only .NET dependencies are present. STDIO scripts requiring Node.js, Python, `uv`, or `bun` will fail with executable not found.
- **Remediation**: Use the batteries-included `latest-full` image variant (`ghcr.io/spelech/model-context-gateway:latest-full`).

### Diagnostic Step 2: Unbuffered STDIO Streams
Many runtimes (such as Python) buffer `stdout` by default when output is redirected to a pipe:
- **Python**: Pass environment variable `PYTHONUNBUFFERED=1` or command line flag `-u`:
  ```json
  {
    "command": "python3",
    "args": ["-u", "/app/scripts/mcp_server.py"]
  }
  ```
- **Node.js**: Ensure console output or JSON-RPC responses append trailing newline characters (`\n`) to flush stdio stream pipes.

### Diagnostic Step 3: Process Lifecycle & Signal Traps
When a client drops an SSE session or HTTP connection:
1. `ClientSession` cancels the `CancellationTokenSource`.
2. `StdioTransport` sends `SIGTERM` (or process `Kill()` on Windows) to the child process tree.
3. If the process does not terminate within 5 seconds, `StdioTransport` issues `SIGKILL`.

---

## 4. Subprocess Configuration Reference

### Correct STDIO Server Definition
```json
{
  "id": "postgres-mcp",
  "displayName": "PostgreSQL STDIO Server",
  "type": "stdio",
  "command": "npx",
  "args": ["-y", "@modelcontextprotocol/server-postgres", "postgresql://user:pass@db:5432/mydb"],
  "env": {
    "NODE_ENV": "production"
  }
}
```

---

## 5. Related Operations Guides

* [**Troubleshooting & RCA Overview**](../../mcp-routing-and-admin-issues.md)
* [**Authentication & Token Failures Runbook**](auth-and-token-failures.md)
* [**Database Locks & Migration Recovery Runbook**](database-locks-and-migrations.md)
* [**Transports & Subprocesses Guide**](../../architecture/transports-and-subprocesses.md)
