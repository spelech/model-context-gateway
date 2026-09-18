# 🚀 Transport Subsystem & Subprocess Lifecycle

This document details the transport abstraction layer, STDIO subprocess isolation, process tree lifecycle management, signal handling, and secure stream multiplexing in the **Model Context Gateway (MCG)**.

---

## 📑 Table of Contents

1. [Strategy Pattern (`ITransport`)](#1-strategy-pattern-itransport)
2. [Subprocess STDIO Architecture & Security Hardening](#2-subprocess-stdio-architecture-security-hardening)
3. [Child Process Tree Lifecycle & Signal Handling](#3-child-process-tree-lifecycle-signal-handling)
4. [Stderr Log Capture & PII Token Masking](#4-stderr-log-capture-pii-token-masking)
5. [Sequence Diagram: STDIO Subprocess Execution](#5-sequence-diagram-stdio-subprocess-execution)

---

## 1. Strategy Pattern (`ITransport`)

The gateway shields core routing coordinators from network and IPC transport protocols through the [`ITransport`](https://github.com/spelech/model-context-gateway/blob/main/Infrastructure/Transports/ITransport.cs) strategy interface:

```csharp
public interface ITransport : IAsyncDisposable
{
    string TransportType { get; }
    bool IsConnected { get; }
    Task InitializeAsync(McpServer server, CancellationToken cancellationToken = default);
    Task<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default);
    Task SendNotificationAsync(JsonRpcNotification notification, CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
```

The gateway provides three production transport implementations:

| Transport Implementation | Protocol / Channel | Concurrency & Multiplexing | Target Systems |
| :--- | :--- | :--- | :--- |
| **`SseTransport`** | HTTP Server-Sent Events + POST | Full-Duplex Multiplexed | Docker containers, remote network MCP servers, persistent services |
| **`HttpTransport`** | Half-Duplex HTTP POST / Stream | Stateless / Single-Shot | Serverless functions, cloud endpoints, webhooks |
| **`StdioTransport`** | Standard Input / Output (NDJSON) | Serialized via Async Lock | Local CLI tools, Python scripts (`uvx`), Node.js packages (`npx`) |

---

## 2. Subprocess STDIO Architecture & Security Hardening

The [`StdioTransport`](https://github.com/spelech/model-context-gateway/blob/main/Infrastructure/Transports/StdioTransport.cs) manages local CLI processes, Python virtual environments, and Node runtimes with strict security sandboxing:

1. **Command Line Tokenization**:
   [`StdioTransport.ParseCommandLine`](https://github.com/spelech/model-context-gateway/blob/main/Infrastructure/Transports/StdioTransport.cs) parses single/double quoted paths and arguments cleanly into an executable and distinct argument array without passing raw string blocks to the OS.

2. **Strict Process Security Policy**:
   - `UseShellExecute = false` and `CreateNoWindow = true`.
   - Executables are invoked **directly** without intermediate command interpreters (`sh`, `bash`, `cmd.exe`, `powershell.exe`). This completely prevents shell injection vulnerabilities and command chaining attacks (`&&`, `;`, `|`, `$(...)`).

3. **Zero-Leakage Credential Injection**:
   - Downstream secrets resolved from Vault, DPAPI, or Gateway Environment are injected **strictly** via `ProcessStartInfo.Environment`.
   - Credentials are **never** passed as command-line arguments, preventing credential exposure in host process listings (`ps aux`, `/proc/$PID/cmdline`, Windows Task Manager).

4. **Stream Synchronization & Buffer Draining**:
   - Requests are written to `StandardInput` as newline-delimited JSON (NDJSON) guarded by asynchronous write locks (`SemaphoreSlim`).
   - `StandardOutput` is read line-by-line using asynchronous loops to avoid standard pipe buffer deadlocks.

---

## 3. Child Process Tree Lifecycle & Signal Handling

Subprocesses follow a deterministic lifecycle that ensures processes and their spawned worker threads exit cleanly without leaving zombie or orphaned processes:

```mermaid
stateDiagram-v2
    [*] --> Starting: Spawn Process (UseShellExecute=false)
    Starting --> Running: Redirect stdin/stdout/stderr & Inject Env Secrets
    Running --> Executing: Write NDJSON Request to stdin
    Executing --> Running: Read NDJSON Response from stdout
    
    Running --> Draining: Gateway Shutdown / Session Close
    Draining --> Terminated: Close stdin (EOF) -> Process Exits Cleanly
    
    Running --> Killing: Timeout / Request Cancellation
    Killing --> Terminated: Kill(entireProcessTree: true)
    
    Terminated --> [*]: Dispose Process & Free Pipes
```

### Lifecycle Rules

* **Graceful Termination**:
  When a session terminates or the gateway shuts down, the router closes `StandardInput`. MCP compliant tools detect EOF on standard input and exit cleanly.
* **Process Tree Killing**:
  If a subprocess hangs or fails to terminate within the configured grace period (default: 5 seconds), [`process.Kill(entireProcessTree: true)`](https://github.com/spelech/model-context-gateway/blob/main/Infrastructure/Transports/StdioTransport.cs) is invoked. This recursively terminates the root process and all child processes spawned by it (e.g., Python scripts spawning native binaries or compilers).

---

## 4. Stderr Log Capture & PII Token Masking

Local CLI processes often emit diagnostic, debugging, or error information to `StandardError`. The gateway continuously monitors this stream while safeguarding sensitive data:

1. **Continuous Drainer Loop**:
   An asynchronous background worker continuously drains `StandardError` to prevent standard pipe buffer saturation from blocking process execution.
2. **Mandatory PII Masking**:
   Every log line emitted by the subprocess passes through [`PiiSanitizer.SanitizePayload`](https://github.com/spelech/model-context-gateway/blob/main/Infrastructure/Logging/PiiSanitizer.cs). Regex patterns automatically redact Bearer tokens, API keys, passwords, and private connection strings before the entry is written to `AuditLogs` or forwarded to console monitors.

---

## 5. Sequence Diagram: STDIO Subprocess Execution

The following sequence diagram illustrates the lifecycle of a tool request dispatched over the STDIO transport, including background stderr log capture, secret injection, and asynchronous response completion:

```mermaid
sequenceDiagram
    autonumber
    participant Session as ClientSession
    participant Transport as StdioTransport
    participant Proc as Subprocess (stdin / stdout / stderr)
    participant Pii as PiiSanitizer & Logger
    participant State as JsonRpcStateManager

    Session->>Transport: InitializeAsync(serverConfig)
    Transport->>Transport: ParseCommandLine(serverConfig.Url)
    Transport->>Transport: Inject Secrets into ProcessStartInfo.Environment
    Transport->>Proc: Process.Start() (Redirect Standard Streams)
    
    par Stderr Background Drainer
        loop Continuous Drain
            Proc-->>Transport: Read Stderr Line
            Transport->>Pii: SanitizePayload(stderrLine)
            Pii-->>Transport: Redacted Log Line
            Transport->>Pii: LogDebug / LogWarning
        end
    end

    Session->>Transport: SendRequestAsync(JsonRpcRequest: tools/call, id="up-42")
    Transport->>State: CreateTrackedRequest("up-42")
    Transport->>Proc: Write to stdin ("{\"jsonrpc\":\"2.0\",\"id\":\"up-42\",...}\n")
    Transport->>Proc: Flush stdin
    
    Proc-->>Transport: Read line from stdout ("{\"jsonrpc\":\"2.0\",\"id\":\"up-42\",\"result\":{...}}\n")
    Transport->>State: TryCompleteRequest("up-42", response)
    State-->>Session: Return JsonRpcResponse
```

---

*Related Specifications:*
- [System Architecture Index](index.md)
- [Protocol & Routing Engine](routing-and-meta-mode.md)
- [Transport Capability & Configuration Guide](../transports.md)
- [Database Persistence & Envelope Encryption](database-and-encryption.md)
