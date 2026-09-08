# 05. Interactive Test Bench

The **Interactive Test Bench** (`Test Bench` tab) helps you test and diagnose the gateway. You can execute tools, read virtual resources, test prompt templates, run semantic searches, send JSON-RPC payloads, and view live logs.

---

## 🎛️ Test Bench Overview & Layout

![Interactive Test Bench View](../assets/test_bench_view.jpg)

The Test Bench provides six diagnostic tools:

```
+---------------------------------------------------------------------------------------------------------------+
| 🧪 Interactive Test Bench                                                                                     |
+---------------------------------------------------------------------------------------------------------------+
|  [ 🛠️ Tools ]   [ 📄 Resources ]   [ 💬 Prompts ]   [ 🧠 Semantic Router ]   [ 💻 Console ]   [ 📟 Logs ]     |
+---------------------------------------------------------------------------------------------------------------+
|                                                                                                               |
|  [ Active Tester Panel: Dynamic Forms, Schema Builder, Raw Arguments Editor, & Execution Controls ]          |
|                                                                                                               |
+---------------------------------------------------------------------------------------------------------------+
| 📟 Live Diagnostic Logs & Gateway Terminal                                                                     |
+---------------------------------------------------------------------------------------------------------------+
```

---

## 🛠️ 1. Tool Execution Tester (`ToolTesterCard`)

The Tool Tester executes any discovered or custom tool without an external AI client or IDE.

```
+-------------------------------------------------------------------------------+
| 🛠️ Tool Execution Tester                                                      |
+-------------------------------------------------------------------------------+
| Target Server: [ docker (Docker Infrastructure Daemon) ▾ ]                    |
| Tool Name:     [ docker__restart_container ▾             ]                    |
|                                                                               |
| Parameters (Generated from JSON Schema):                                      |
|   Container ID / Name (*): [ homewebservice                                 ] |
|   Timeout Seconds:         [ 30                                             ] |
|                                                                               |
| [ ▶ Execute Tool ]                                                            |
+-------------------------------------------------------------------------------+
| Result (200 OK - 42ms):                                                       |
| {                                                                             |
|   "content": [                                                                |
|     { "type": "text", "text": "Container homewebservice restarted successfully" }|
|   ]                                                                           |
| }                                                                             |
+-------------------------------------------------------------------------------+
```

### How to Use the Tool Tester
1. **Select Target Server**: Choose a server from the dropdown (for example, `docker`, `contextcortex`, `plex`, or `custom`).
2. **Select Tool**: Choose a tool for that server. The form displays input fields for the tool JSON Schema.
3. **Enter Parameters**:
   * **Booleans**: Use checkboxes or toggle switches.
   * **Strings and Numbers**: Enter values into the text boxes.
   * **Arrays and Objects**: Enter valid JSON strings (for example, `["item1", "item2"]` or `{"key": "value"}`).
4. **Optional - Raw JSON Mode**: Turn on the Raw JSON Editor to edit parameters as JSON:
   ```json
   {
     "container_id": "homewebservice",
     "timeout": 30,
     "force": true
   }
   ```
5. **Execute**: Click **Execute Tool**. The gateway sends the request and displays the result with execution metrics in the output panel.

### API & cURL Examples
The Test Bench sends requests to either `POST /api/test/call` or `POST /api/test/call-tool`. The gateway supports both endpoints.

#### Standard Invocation with `serverId` and `toolName`:
```bash
curl -X POST http://localhost:8080/api/test/call \
  -H "Authorization: Bearer <YOUR_APP_KEY>" \
  -H "Content-Type: application/json" \
  -d '{
    "serverId": "docker",
    "toolName": "docker__restart_container",
    "arguments": {
      "container_id": "homewebservice",
      "timeout": 30
    }
  }'
```

#### Alias Invocation with Unnamespaced Name Resolution:
```bash
curl -X POST http://localhost:8080/api/test/call-tool \
  -H "Authorization: Bearer <YOUR_APP_KEY>" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "docker__restart_container",
    "arguments": {
      "container_id": "homewebservice"
    }
  }'
```

---

## 📄 2. Virtual Resource Tester (`ResourceTesterCard`)

Use this tool to read virtual MCP resources from backend servers:

```
+-------------------------------------------------------------------------------+
| 📄 Virtual Resource Tester                                                    |
+-------------------------------------------------------------------------------+
| Target Server: [ docker ▾ ]                                                   |
| Select Resource: [ Container Status (mcp://docker/containers/status) ▾ ]      |
| Resource URI:  [ mcp://docker/containers/status                             ] |
|                                                                               |
| [ 📖 Read Resource ]                                                          |
+-------------------------------------------------------------------------------+
| MIME Type: application/json | Size: 1.4 KB                                    |
| {                                                                             |
|   "containers": [                                                             |
|     { "name": "caddy", "status": "running", "uptime": "14d 2h" },             |
|     { "name": "vault", "status": "running", "uptime": "30d 6h" }              |
|   ]                                                                           |
| }                                                                             |
+-------------------------------------------------------------------------------+
```

### How to Use the Resource Tester
1. **Select Server or Template**: Choose a server to display its available resources.
2. **Select Resource or Enter URI**: Select a resource from the dropdown, or type a URI (for example, `mcp://docker/logs/caddy` or `router://database`).
3. **Read Resource**: Click **Read Resource** to send the request.

### Supported Resource Types
* **Backend MCP Resources**: URIs that use the format `mcp://{serverId}/{path}`.
* **Router System Resources**:
  * `router://status`: Returns runtime health, active sessions, and connection pools.
  * `router://database`: Returns database metadata and server configurations.
  * `logs://recent`: Returns the most recent in-memory log messages.

### API & cURL Example
```bash
curl -X POST http://localhost:8080/api/test/resources/read \
  -H "Authorization: Bearer <YOUR_APP_KEY>" \
  -H "Content-Type: application/json" \
  -d '{
    "uri": "mcp://docker/containers/status"
  }'
```

---

## 💬 3. Prompt Template Tester (`PromptTesterCard`)

Use this tool to test prompt templates from backend servers or custom files:

```
+-------------------------------------------------------------------------------+
| 💬 Prompt Template Tester                                                     |
+-------------------------------------------------------------------------------+
| Target Server:   [ notes-rag (SilverBullet / Notes MCP) ▾ ]                   |
| Prompt Template: [ summarize_architecture ▾               ]                   |
|                                                                               |
| Arguments:                                                                    |
|   Topic (*):     [ Model Context Gateway Security Hardening                 ] |
|   Max Length:    [ 500                                                      ] |
|                                                                               |
| [ 📑 Render Prompt ]                                                          |
+-------------------------------------------------------------------------------+
| Rendered Messages:                                                            |
| [System]: "You are an expert systems architect reviewing homelab security..." |
| [User]:   "Summarize the architecture for Model Context Gateway Security..."  |
+-------------------------------------------------------------------------------+
```

### How to Use the Prompt Tester
1. **Select Server & Template**: Select the backend server and the prompt template.
2. **Enter Arguments**: Enter values for each required prompt argument.
3. **Render Prompt**: Click **Render Prompt**. The gateway evaluates the template and displays the rendered messages.

### API & cURL Example
```bash
curl -X POST http://localhost:8080/api/test/prompts/get \
  -H "Authorization: Bearer <YOUR_APP_KEY>" \
  -H "Content-Type: application/json" \
  -d '{
    "serverId": "notes-rag",
    "promptName": "summarize_architecture",
    "arguments": {
      "topic": "Model Context Gateway Security Hardening",
      "max_length": "500"
    }
  }'
```

---

## 🧠 4. Semantic Router Simulator (`SemanticRouterCard`)

Simulate how the gateway scores and ranks tools when an AI client calls `search_tools`:

```
+-------------------------------------------------------------------------------+
| 🧠 Semantic Search Simulator (Meta-Mode Test)                                 |
+-------------------------------------------------------------------------------+
| Natural Language Query: [ restart web proxy container                       ] |
| Search Limit:           [ 5 ▾ ]                                               |
|                                                                               |
| [ 🔍 Simulate Semantic Search ]                                               |
+-------------------------------------------------------------------------------+
| Search Results (Embedding Latency: 12ms):                                     |
|                                                                               |
| 1. docker__restart_container  [Score: 2.942] 🟢 High Match                    |
|    "Restart a running Docker container by name or container ID."              |
|                                                                               |
| 2. docker__stop_container     [Score: 1.815] 🟡 Moderate                      |
|    "Stop a running Docker container."                                         |
|                                                                               |
| 3. caddy__reload_config       [Score: 1.748] 🟡 Moderate                      |
|    "Triggers an in-process reload of the Caddy web reverse proxy config."     |
+-------------------------------------------------------------------------------+
```

### Understanding the Score Breakdown
The final score combines vector similarity with keyword matching:
* **Vector Cosine Similarity** (`0.00` to `1.00`): Measures semantic similarity from dense vectors (Local ONNX `all-MiniLM-L6-v2` or an external API).
* **Exact Substring Match**: Adds `+2.0` when the tool name contains the full query. Adds `+1.5` when the description contains the full query.
* **Word Token Match**: Adds `+1.0` for each matching word in the tool name. Adds `+0.5` for each matching word in the description.
* **Multi-Word Bonus**: Adds score multipliers when multiple query terms match across metadata.

### API & cURL Example
```bash
curl -X POST http://localhost:8080/api/test/semantic-search \
  -H "Authorization: Bearer <YOUR_APP_KEY>" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "restart web proxy container"
  }'
```

---

## 💻 5. Direct JSON-RPC Raw Console (`ConsoleCard`)

Send raw JSON-RPC 2.0 messages directly to the gateway:

```
+-------------------------------------------------------------------------------+
| 💻 Direct JSON-RPC Raw Console                                                |
+-------------------------------------------------------------------------------+
| Request:                                                                      |
| {                                                                             |
|   "jsonrpc": "2.0",                                                           |
|   "id": 1,                                                                    |
|   "method": "tools/list",                                                     |
|   "params": {}                                                                |
| }                                                                             |
|                                                                               |
| [ 🚀 Send Request ]                                                           |
+-------------------------------------------------------------------------------+
| Response (200 OK):                                                            |
| {                                                                             |
|   "jsonrpc": "2.0",                                                           |
|   "id": 1,                                                                    |
|   "result": { "tools": [...] }                                                |
| }                                                                             |
+-------------------------------------------------------------------------------+
```

### Useful Raw JSON-RPC Payloads for Testing

#### 1. Discover Meta-Mode Tools
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/list",
  "params": {}
}
```

#### 2. Perform Dynamic Semantic Tool Search
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/call",
  "params": {
    "name": "search_tools",
    "arguments": {
      "query": "find active database connections"
    }
  }
}
```

#### 3. Execute Downstream Target Tool via Meta-Mode
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "execute_tool",
    "arguments": {
      "name": "postgres__list_connections",
      "arguments": {}
    }
  }
}
```

---

## 📟 6. Live Diagnostic Logs Terminal (`LogsTerminalCard`)

The Live Logs Terminal sits at the bottom of the Test Bench view. It shows real-time gateway activity:

```
+-------------------------------------------------------------------------------+
| 📟 Live Diagnostic Logs & Gateway Activity             [ Clear ] [ Auto-Scroll ]|
| Filter: [ ALL ▾ ] [ INFO ▾ ] [ WARN ▾ ] [ ERROR ▾ ]                           |
+-------------------------------------------------------------------------------+
| [13:45:02.112] [INF] [McpSession:c8b4] Client authenticated as 'admin' via OIDC
| [13:45:02.115] [INF] [McpSession:c8b4] Initialized Meta-Mode stream (2 tools)
| [13:45:04.220] [INF] [ToolExecution] docker__restart_container invoked by admin
| [13:45:04.262] [INF] [ToolExecution] docker__restart_container completed in 42ms
| [13:45:10.512] [WRN] [HealthCheck] Backend 'plex' responded slowly (1250ms)
+-------------------------------------------------------------------------------+
```

### Key Capabilities
* **In-Memory Stream**: Streams live logs directly from memory without writing to disk.
* **PII Redaction**: Masks authorization headers, tokens, and passwords as `[REDACTED]`.
* **Severity Filters**: Filters log messages by `INFO`, `WARN`, or `ERROR` levels.
* **Auto-Scroll and Pause**: Controls log scrolling while you inspect events.
* **Clear Buffer**: Clears log messages with the **Clear** button or `DELETE /api/logs`.

### API & cURL Example for Logs
```bash
# Fetch recent logs
curl -s http://localhost:8080/api/logs -H "Authorization: Bearer <YOUR_APP_KEY>"

# Clear in-memory log buffer
curl -s -X DELETE http://localhost:8080/api/logs -H "Authorization: Bearer <YOUR_APP_KEY>"
```

