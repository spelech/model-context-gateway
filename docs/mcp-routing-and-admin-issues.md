# Troubleshooting & Root Cause Analysis (RCA) Overview

[Home](index.md) > Operations Troubleshooting & RCA

This guide provides an overview of operational troubleshooting, root cause analysis (RCA), and remediation runbooks for Model Context Gateway (MCG).

---

## 1. Modular Troubleshooting Runbooks

* [**Subprocess & STDIO Transports Runbook**](operations/troubleshooting/subprocess-and-stdio.md)
  Subprocess execution timeouts, stdio line-buffering, process signal traps (`SIGTERM`/`SIGKILL`), and container STDIO execution using the `latest-full` image variant.

* [**Authentication & Token Failures Runbook**](operations/troubleshooting/auth-and-token-failures.md)
  OIDC audience/issuer mismatches, RFC 9207 security enforcement, AppKey scope rejection, expired secret providers (Vault/DPAPI), and HTTP 401/403 header handling.

* [**Database Locks & Migration Recovery Runbook**](operations/troubleshooting/database-locks-and-migrations.md)
  SQLite lock contention (`database is locked`), WAL journal mode, DDL migration recovery across SQLite/MSSQL/MySQL, master key re-encryption recovery, and database seeder idempotency.

---

## 2. Root Cause Analysis (RCA): Routing & Admin Issues

The sections below document a comprehensive Root Cause Analysis (RCA) performed on gateway session lifecycle management, tool routing synchronization, and Admin MCP server execution.

### Executive Summary
During maintenance operations, automated tool execution via `/sse` failed with `Tool not found in routing table`, despite successful `search_tools` discovery and direct `test_tool_call` execution via `/admin/sse`. Deep architectural inspection revealed **seven distinct bugs** across session lifecycle management, cache synchronization, and Admin endpoints.

---

### Key Issues & Architectural Root Causes

#### Issue 1: Stateless Session Lifecycle & Disposed HttpContext
* **Mechanism**: MCP clients making HTTP POST requests use stateless global session mappings. When an HTTP request completes (< 5ms), ASP.NET Core disposes `HttpContext` and its `IFeatureCollection`. Parallel background tasks in `InitializeBackendsAsync` or retry loops that accessed `_clientResponse.HttpContext.RequestServices` threw `ObjectDisposedException`.
* **Remediation**: Decoupled service and identity resolution from request `HttpContext`, utilizing root `IServiceProvider` directly and guarding background accessors.

#### Issue 2: Routing Table Asymmetry in Meta-Mode Cold-Start
* **Mechanism**: When `search_tools` executed under "Cold-Start Fallback 1", it populated `_cachedTools` from `SessionManager.GetAllCachedTools()` but omitted populating `_toolRoutingTable`. Subsequent `execute_tool` calls failed with `KeyNotFoundException`.
* **Remediation**: Updated cold-start fallback in `ToolRoutingManager` to populate `_toolRoutingTable` whenever `_cachedTools` is seeded.

#### Issue 3: Resilient Prefix-Based Tool Routing
* **Mechanism**: Direct tool execution failed when routing table entries were missing or cold-started.
* **Remediation**: Implemented resilient prefix parsing in `ExecuteTargetToolAsync` to dynamically resolve `{serverId}__{toolName}` or `{serverId}/{toolName}` prefixes directly against active enabled backends.

#### Issue 4: Admin MCP `manage_servers: reconnect_all` Session Sync
* **Mechanism**: `reconnect_all` probed servers in `HealthCheckService` but failed to trigger backend initialization on active client sessions.
* **Remediation**: Updated `reconnect_all` to notify all active `ClientSession` instances to start backend initialization.

#### Issue 5: SecretRetriever Injection into Admin `test_tool_call`
* **Mechanism**: `test_tool_call` constructed `BackendConnection` with a `null` `ISecretRetriever`, preventing credential resolution for Vault or encrypted key backends.
* **Remediation**: Injected `CompositeSecretRetriever` into `AdminMcpServer` and passed it to `BackendConnection`.

---

## 3. Operations & Diagnostic Commands

Run diagnostic checks and query audit history using the Admin MCP Server:

```json
{
  "name": "manage_system",
  "arguments": {
    "action": "diagnostics"
  }
}
```

```json
{
  "name": "manage_system",
  "arguments": {
    "action": "query_audit",
    "limit": 50
  }
}
```

---

## 4. Related Operations Guides

* [**Operations Runbook**](runbook.md)
* [**Administrator Guide**](admin-guide.md)
* [**Admin MCP Automation Guide**](admin-mcp-automation-guide.md)
* [**Admin MCP Tools Reference**](admin-mcp-features.md)
