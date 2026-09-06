# Model Context Gateway (MCG) — Root Cause Analysis & Remediation Guide
**Document Version:** 1.0.0  
**Target Repository:** `/containers/dev/csharp-mcp-router`  
**Date:** September 5, 2026  

---

## 1. Executive Summary

During arr stack and Overseerr maintenance, automated tool calling via the Model Context Gateway (`mcg`) at `http://10.0.0.10:8026/sse` failed:
- `search_tools("seer")` succeeded and returned `seerr__seerr_get_requests`.
- `execute_tool("seerr__seerr_get_requests")` immediately failed with:
  ```text
  Error executing target tool seerr__seerr_get_requests: Tool seerr__seerr_get_requests not found in routing table.
  ```
- Meanwhile, direct diagnostic execution via the Admin MCP Server (`/admin/sse`) using `test_tool_call` succeeded without error.

A deep architectural inspection of `/containers/dev/csharp-mcp-router` revealed **seven distinct bugs** across session lifecycle management, tool routing cache synchronization, and the Admin MCP server implementation.

---

## 2. Issue Breakdown & Technical Root Causes

### Issue 1: Stateless Session Lifecycle & Disposed HttpContext Crash (Critical Root Cause)

- **Affected Files:**
  - `Components/Capabilities/ProxyEndpoints.cs` (lines 82–88)
  - `Core/Routing/ClientSession/ClientSession.BackendInitializer.cs` (lines 102–108)
  - `Core/Routing/ClientSession/ClientSession.Authorization.cs` (lines 16–45)
- **Observed Container Log:**
  ```text
  Attempting to connect to backend seerr (attempt 1/2) at http://seerr-mcp:8000/sse...
  Failed to connect to backend seerr at http://seerr-mcp:8000/sse (attempt 1/2). Error: IFeatureCollection has been disposed. Object name: 'Collection'.
  Attempting to connect to backend seerr (attempt 2/2) at http://seerr-mcp:8000/sse...
  Failed to connect to backend seerr at http://seerr-mcp:8000/sse (attempt 2/2). Error: IFeatureCollection has been disposed. Object name: 'Collection'.
  Stopped retrying connection to backend seerr after 2 failed attempts.
  ```
- **Mechanism:**
  1. MCP clients invoking the gateway via standard HTTP POST requests are mapped to `globalSessionId = "global-stateless-session"`.
  2. The session constructor stores `_clientResponse = httpContext.Response`.
  3. Unlike a persistent SSE stream (GET), a stateless HTTP POST request completes in < 5ms. When the HTTP pipeline finishes writing the response, ASP.NET Core immediately disposes the `HttpContext` and its underlying `IFeatureCollection`.
  4. In parallel, background tasks spawned by `InitializeBackendsAsync` or retry loops attempt to connect downstream backends:
     ```csharp
     var retriever = _rootServices?.GetService<CompositeSecretRetriever>()
         ?? _clientResponse?.HttpContext?.RequestServices?.GetService<CompositeSecretRetriever>();
     var identity = await ResolveUserIdentityAsync(_clientResponse?.HttpContext);
     ```
  5. Accessing `_clientResponse.HttpContext.RequestServices` or `contextToUse.Items` throws `ObjectDisposedException`.
  6. As a result, connection attempts for backends that finish initializing after the first HTTP request completes are permanently dropped from `_backendConnections`.

---

### Issue 2: Routing Table Asymmetry in Meta-Mode Cold-Start Fallback

- **Affected Files:**
  - `Core/Routing/ToolRoutingManager.Execution.cs` (lines 83–97, 242–256)
  - `Core/Routing/ToolRoutingManager.Cache.cs` (lines 70–80)
- **Mechanism:**
  1. When a client calls `search_tools`, `ToolRoutingManager` checks if `_cachedTools` is empty. Under "Cold-Start Fallback 1", it fetches tools from `sessionManager.GetAllCachedTools()`:
     ```csharp
     var globalCached = sessionManager.GetAllCachedTools();
     if (globalCached.Count > 0) {
         _cachedTools.AddRange(globalCached);
         _isCachePopulated = true;
     }
     ```
  2. **The Bug:** It populates `_cachedTools`, but **never populates `_toolRoutingTable`**!
  3. When `execute_tool("seerr__seerr_get_requests")` is subsequently dispatched, `ExecuteTargetToolAsync` checks `if (!_toolRoutingTable.ContainsKey(toolName))`. Because the table was never seeded, it executes a fallback query across `backendConnections`.
  4. Because `backendConnections` did not have `seerr` (due to Issue 1), the refresh failed, and line 255 evaluated to false:
     ```csharp
     if (_toolRoutingTable.TryGetValue(toolName, out var serverId) && backendConnections.TryGetValue(serverId, out var conn))
     ```
  5. Line 324 threw: `KeyNotFoundException($"Tool {toolName} not found in routing table.")`.

---

### Issue 3: Admin MCP `manage_servers: reconnect` Action Crashes on Stateless Sessions

- **Affected Files:**
  - `Core/Routing/AdminMcpServer.cs` (lines 448–457)
  - `Core/Routing/ClientSession/ClientSession.BackendInitializer.cs` (lines 233–244)
- **Mechanism:**
  When an admin triggers `manage_servers` with action `"reconnect"`:
  ```csharp
  var activeSessions = _sessionManager.GetActiveSessions();
  foreach (var session in activeSessions) {
      session.StartInitializationForBackend(id);
  }
  ```
  `StartInitializationForBackend` dispatches `ConnectAndInitializeBackendAsync(server)` in the background on the existing session. Because `global-stateless-session` contains a disposed `_clientResponse.HttpContext`, the reconnect attempt immediately crashes with `ObjectDisposedException: IFeatureCollection has been disposed`, leaving the backend permanently disconnected for all subsequent tool calls.

---

### Issue 4: Admin MCP `manage_servers: reconnect_all` Session Disconnect

- **Affected File:**
  - `Core/Routing/AdminMcpServer.cs` (lines 459–464)
- **Mechanism:**
  ```csharp
  case "reconnect_all":
  {
      await _healthCheckService.ProbeAllServersAsync();
      return new { success = true, message = "Reconnection triggered for all servers." };
  }
  ```
  `reconnect_all` only probes servers within `HealthCheckService`. It **completely omits notifying active sessions** (`session.StartInitializationForBackend`), so active client sessions never reconnect their downstream connections even after backends recover.

---

### Issue 5: Admin MCP `test_tool_call` Passes `null` SecretRetriever

- **Affected File:**
  - `Core/Routing/AdminMcpServer.cs` (line 1394)
- **Mechanism:**
  ```csharp
  using var conn = new BackendConnection(server, _httpClient, _logger ?? (ILogger)NullLogger.Instance, null);
  ```
  The fourth argument to `BackendConnection` is `ISecretRetriever? secretRetriever`. In `HandleTestToolCallAsync`, it is hardcoded to `null`.
  If an MCP backend server requires secrets stored in HashiCorp Vault, AES Master Key (`SecretProvider = "AesMasterKey"` / `"Vault"`), or custom header authentication, `test_tool_call` cannot resolve the credentials and fails with 401 Unauthorized.

---

### Issue 6: `McpSpecMiddleware` Classifying Requests Without ID as Notifications

- **Affected Files:**
  - `Middleware/McpSpecMiddleware.cs` (line 175)
  - `Components/Capabilities/AdminEndpoints.cs` (lines 128–132)
- **Mechanism:**
  In `McpSpecMiddleware.cs`:
  ```csharp
  bool isNotification = method.StartsWith("notifications/") || id == null;
  ```
  And in `AdminEndpoints.cs`:
  ```csharp
  if (isNotification)
  {
      httpContext.Response.StatusCode = 202;
      return;
  }
  ```
  If an MCP client sends a POST request without an explicit `id` property (or `id: null`), the request is treated as a one-way notification. The gateway returns HTTP 202 Accepted without executing the admin action or returning output.

---

### Issue 7: Reverse Proxy Missing `X-Forwarded-Host` on Admin SSE Stream Setup

- **Affected File:**
  - `Components/Capabilities/AdminEndpoints.cs` (lines 170–179)
- **Mechanism:**
  ```csharp
  var scheme = httpContext.Request.Headers["X-Forwarded-Proto"].ToString();
  if (string.IsNullOrEmpty(scheme)) scheme = httpContext.Request.Scheme;

  var host = httpContext.Request.Host.Value;
  var endpointPrefix = httpContext.Request.Path.Value?.StartsWith("/mcg-admin") == true ? "/mcg-admin" : "/admin";
  var absoluteUrl = $"{scheme}://{host}{endpointPrefix}/message?sessionId={sessionId}";
  ```
  `AdminEndpoints` inspects `X-Forwarded-Proto`, but does **not** inspect `X-Forwarded-Host`. When accessed through Caddy (e.g. `https://mcp.wileyriley.com/admin/sse`), `absoluteUrl` advertises the internal container IP or docker port rather than the public hostname, preventing external clients from posting messages back to `/admin/message`.

---

## 3. Concrete Remediation Plan

### Fix 1: Decouple Service & Identity Resolution from Request HttpContext
In `Core/Routing/ClientSession/ClientSession.BackendInitializer.cs`:
```csharp
// Use root services directly without touching disposed HttpContext
var retriever = _rootServices?.GetService<CompositeSecretRetriever>();

// Guard against ObjectDisposedException when checking identity in background loops
UserIdentityContext? identity = null;
try 
{
    if (_clientResponse?.HttpContext?.Features != null)
    {
        identity = await ResolveUserIdentityAsync(_clientResponse.HttpContext);
    }
}
catch (ObjectDisposedException)
{
    // Request has completed; fall back to system/service principal identity
}
```

### Fix 2: Synchronize `_toolRoutingTable` During Cold-Start
In `Core/Routing/ToolRoutingManager.Execution.cs` (lines 83–97):
```csharp
// Cold-start fallback 1: Seed from SessionManager's global server cache
if (tools.Count == 0 && sessionManager != null)
{
    var globalCached = sessionManager.GetAllCachedTools();
    if (globalCached.Count > 0)
    {
        lock (_cacheLock)
        {
            _cachedTools.Clear();
            _cachedTools.AddRange(globalCached);
            _isCachePopulated = true;

            // CRITICAL FIX: Also populate the routing table from cached tool names
            foreach (var item in globalCached)
            {
                if (item is IDictionary<string, object> dict && dict.TryGetValue("name", out var nObj))
                {
                    var fullName = nObj.ToString();
                    var splitIdx = fullName?.IndexOf("__") ?? -1;
                    if (splitIdx > 0)
                    {
                        var srvId = fullName.Substring(0, splitIdx);
                        _toolRoutingTable[fullName] = srvId;
                    }
                }
            }
        }
        tools.AddRange(globalCached);
    }
}
```

### Fix 3: Prefix-Based Resilient Routing in `ExecuteTargetToolAsync`
In `Core/Routing/ToolRoutingManager.Execution.cs` (lines 242–255):
```csharp
if (!_toolRoutingTable.ContainsKey(toolName))
{
    // Try extracting serverId from namespaced prefix: {serverId}__{toolName}
    var sepIdx = toolName.IndexOf("__", StringComparison.Ordinal);
    if (sepIdx > 0)
    {
        var candidateServerId = toolName.Substring(0, sepIdx);
        if (servers.Any(s => s.Id == candidateServerId && s.Enabled))
        {
            _toolRoutingTable[toolName] = candidateServerId;
        }
    }
}
```

### Fix 4: Inject SecretRetriever into Admin MCP `test_tool_call`
In `Core/Routing/AdminMcpServer.cs` (line 1394):
- Inject `CompositeSecretRetriever` into `AdminMcpServer` constructor.
- Pass the injected retriever to `new BackendConnection(server, _httpClient, _logger, _secretRetriever)`.

### Fix 5: Complete `reconnect_all` Implementation
In `Core/Routing/AdminMcpServer.cs` (lines 459–464):
```csharp
case "reconnect_all":
{
    await _healthCheckService.ProbeAllServersAsync();
    var allServers = (await _serverRepository.GetServersAsync()).Where(s => s.Enabled && s.Type != "custom");
    var activeSessions = _sessionManager.GetActiveSessions();
    foreach (var srv in allServers)
    {
        foreach (var session in activeSessions)
        {
            session.StartInitializationForBackend(srv.Id);
        }
    }
    return new { success = true, message = "Reconnection triggered for all servers." };
}
```

### Fix 6: Support `X-Forwarded-Host` on Admin Endpoints
In `Components/Capabilities/AdminEndpoints.cs` (lines 170–179):
```csharp
var host = httpContext.Request.Headers["X-Forwarded-Host"].ToString();
if (string.IsNullOrEmpty(host))
{
    host = httpContext.Request.Host.Value;
}
```

---

## 4. Verification Checklist

1. [ ] **Unit Tests:** Add test cases in `ModelContextGateway.Tests/ToolRoutingManagerTests.cs` verifying `_toolRoutingTable` contains tools after cold-start fallback.
2. [ ] **Session Lifetime Tests:** Add unit tests simulating background backend initialization when `HttpContext` is marked disposed.
3. [ ] **Admin MCP Parity Tests:** Run `dotnet test` across `AdminToolsParityTests.cs` and `AdminMcpServerTests.cs`.
4. [ ] **Empirical Verification:** Deploy container image and execute:
   - `search_tools("seer")` -> returns `seerr__seerr_get_requests`
   - `execute_tool("seerr__seerr_get_requests", {"take": 1})` -> returns valid request list (no KeyNotFoundException).
