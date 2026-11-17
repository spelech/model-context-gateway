using System.Diagnostics;
using System.Text.Json;
using Jint;
using Jint.Native;
using Jint.Runtime;

namespace ModelContextGateway.Core.CodeMode
{
    public class CodeModeExecutionEngine : ICodeModeExecutionEngine
    {
        public async Task<CodeModeExecutionResult> ExecuteAsync(
            string script,
            JsonElement? args,
            RouterSettings settings,
            Func<string, JsonElement, string?, Task<object>> toolDispatcher,
            CancellationToken cancellationToken,
            CodeModeSandboxCallbacks? sandboxCallbacks = null)
        {
            var sw = Stopwatch.StartNew();
            var logs = new List<string>();
            var logsLock = new object();
            var engineLock = new object();
            var result = new CodeModeExecutionResult
            {
                Logs = logs
            };

            void AddLog(string line)
            {
                lock (logsLock)
                {
                    logs.Add(line);
                }
            }

            using var timeoutCts = new CancellationTokenSource();
            if (settings.CodeModeExecutionTimeoutSeconds > 0)
            {
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(settings.CodeModeExecutionTimeoutSeconds));
            }
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            int toolCallsCount = 0;

            try
            {
                var engine = new Engine(options =>
                {
                    if (settings.CodeModeMaxMemoryMb > 0)
                    {
                        options.LimitMemory((long)settings.CodeModeMaxMemoryMb * 1024 * 1024);
                    }
                    if (settings.CodeModeMaxStatements > 0)
                    {
                        options.MaxStatements(settings.CodeModeMaxStatements + 50);
                    }
                    if (settings.CodeModeExecutionTimeoutSeconds > 0)
                    {
                        options.TimeoutInterval(TimeSpan.FromSeconds(settings.CodeModeExecutionTimeoutSeconds));
                    }
                    options.CancellationToken(linkedCts.Token);
                });

                // Inject console (synchronized for Promise.all concurrency)
                engine.SetValue("__console_log", new Action<object[]>(arguments =>
                {
                    var msg = string.Join(" ", arguments.Select(a => a?.ToString() ?? "null"));
                    AddLog($"[INFO] {msg}");
                }));
                engine.SetValue("__console_warn", new Action<object[]>(arguments =>
                {
                    var msg = string.Join(" ", arguments.Select(a => a?.ToString() ?? "null"));
                    AddLog($"[WARN] {msg}");
                }));
                engine.SetValue("__console_error", new Action<object[]>(arguments =>
                {
                    var msg = string.Join(" ", arguments.Select(a => a?.ToString() ?? "null"));
                    AddLog($"[ERROR] {msg}");
                }));
                engine.Evaluate("const console = { log: (...args) => __console_log(args), warn: (...args) => __console_warn(args), error: (...args) => __console_error(args) };");

                void IncrementAndEnforceQuota()
                {
                    int current = Interlocked.Increment(ref toolCallsCount);
                    if (settings.CodeModeMaxToolCalls > 0 && current > settings.CodeModeMaxToolCalls)
                    {
                        throw new InvalidOperationException($"Tool invocation quota exceeded: maximum tool invocation limit of {settings.CodeModeMaxToolCalls} calls reached.");
                    }
                }

                object UnboxToJsValue(object? rawResponse)
                {
                    if (rawResponse is null)
                    {
                        return null!;
                    }

                    if (rawResponse is string str)
                    {
                        return str;
                    }

                    if (rawResponse is int || rawResponse is long || rawResponse is double || rawResponse is float || rawResponse is bool)
                    {
                        return rawResponse;
                    }

                    JsonElement responseElement;
                    if (rawResponse is JsonElement je)
                    {
                        responseElement = je;
                    }
                    else if (rawResponse is JsonDocument jd)
                    {
                        responseElement = jd.RootElement;
                    }
                    else
                    {
                        responseElement = JsonSerializer.SerializeToElement(rawResponse);
                    }

                    // Auto-unboxing 1: Standard MCP tool content blocks (content: [{ type: "text", text: "..." }])
                    if (responseElement.ValueKind == JsonValueKind.Object &&
                        responseElement.TryGetProperty("content", out var contentArray) &&
                        contentArray.ValueKind == JsonValueKind.Array &&
                        contentArray.GetArrayLength() > 0)
                    {
                        var firstItem = contentArray[0];
                        if (firstItem.ValueKind == JsonValueKind.Object &&
                            firstItem.TryGetProperty("type", out var typeProp) &&
                            typeProp.GetString() == "text" &&
                            firstItem.TryGetProperty("text", out var textProp) &&
                            textProp.ValueKind == JsonValueKind.String)
                        {
                            var text = textProp.GetString();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                try
                                {
                                    using var jsonDoc = JsonDocument.Parse(text);
                                    lock (engineLock)
                                    {
                                        return engine.Evaluate($"JSON.parse({JsonSerializer.Serialize(text)})");
                                    }
                                }
                                catch (JsonException)
                                {
                                    if (contentArray.GetArrayLength() == 1)
                                    {
                                        return text;
                                    }
                                }
                            }
                        }
                    }

                    // Auto-unboxing 2: Standard MCP resources/read contents array (contents: [{ uri, mimeType, text }])
                    if (responseElement.ValueKind == JsonValueKind.Object &&
                        responseElement.TryGetProperty("contents", out var contentsArray) &&
                        contentsArray.ValueKind == JsonValueKind.Array &&
                        contentsArray.GetArrayLength() == 1)
                    {
                        var firstContent = contentsArray[0];
                        if (firstContent.ValueKind == JsonValueKind.Object &&
                            firstContent.TryGetProperty("text", out var resTextProp) &&
                            resTextProp.ValueKind == JsonValueKind.String)
                        {
                            var text = resTextProp.GetString();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                try
                                {
                                    using var jsonDoc = JsonDocument.Parse(text);
                                    lock (engineLock)
                                    {
                                        return engine.Evaluate($"JSON.parse({JsonSerializer.Serialize(text)})");
                                    }
                                }
                                catch (JsonException)
                                {
                                    return text;
                                }
                            }
                        }
                    }

                    lock (engineLock)
                    {
                        return engine.Evaluate($"JSON.parse({JsonSerializer.Serialize(responseElement.GetRawText())})");
                    }
                }

                engine.SetValue("__callTool", new Func<string, object?, object?, Task<object>>(async (toolName, toolArgs, options) =>
                {
                    IncrementAndEnforceQuota();

                    if (string.IsNullOrWhiteSpace(toolName))
                    {
                        throw new ArgumentException("Tool name must be specified.", nameof(toolName));
                    }

                    var callOptions = ParseCallOptions(options);

                    JsonElement argsElement;
                    if (toolArgs == null)
                    {
                        using var emptyDoc = JsonDocument.Parse("{}");
                        argsElement = emptyDoc.RootElement.Clone();
                    }
                    else
                    {
                        argsElement = JsonSerializer.SerializeToElement(toolArgs);
                    }

                    object rawResponse;
                    if (sandboxCallbacks?.CallToolWithOptionsAsync != null)
                    {
                        rawResponse = await sandboxCallbacks.CallToolWithOptionsAsync(toolName, argsElement, callOptions, linkedCts.Token);
                    }
                    else
                    {
                        rawResponse = await toolDispatcher(toolName, argsElement, callOptions?.AuthToken);
                    }

                    return UnboxToJsValue(rawResponse);
                }));

                engine.SetValue("__readOutput", new Func<string, object?, Task<object>>(async (outputId, options) =>
                {
                    IncrementAndEnforceQuota();
                    if (string.IsNullOrWhiteSpace(outputId))
                    {
                        throw new ArgumentException("outputId must be specified.", nameof(outputId));
                    }

                    int? offset = null;
                    int? maxChars = null;
                    if (options != null)
                    {
                        var optsEl = JsonSerializer.SerializeToElement(options);
                        if (optsEl.ValueKind == JsonValueKind.Object)
                        {
                            if (TryGetInt(optsEl, out var offVal, "offset"))
                            {
                                offset = offVal;
                            }
                            if (TryGetInt(optsEl, out var mcVal, "maxChars", "max_chars"))
                            {
                                maxChars = mcVal;
                            }
                        }
                    }

                    if (sandboxCallbacks?.ReadOutputAsync != null)
                    {
                        var res = await sandboxCallbacks.ReadOutputAsync(outputId, offset, maxChars);
                        return UnboxToJsValue(res);
                    }

                    var fallbackArgs = JsonSerializer.SerializeToElement(new
                    {
                        output_id = outputId,
                        offset = offset ?? 0,
                        max_chars = maxChars ?? 4000
                    });
                    var fallbackRes = await toolDispatcher("read_output", fallbackArgs, null);
                    return UnboxToJsValue(fallbackRes);
                }));

                engine.SetValue("__listServers", new Func<Task<object>>(async () =>
                {
                    IncrementAndEnforceQuota();
                    if (sandboxCallbacks?.ListServersAsync == null)
                    {
                        return UnboxToJsValue(Array.Empty<object>());
                    }
                    var res = await sandboxCallbacks.ListServersAsync();
                    return UnboxToJsValue(res);
                }));

                engine.SetValue("__listResources", new Func<string?, Task<object>>(async (serverId) =>
                {
                    IncrementAndEnforceQuota();
                    if (sandboxCallbacks?.ListResourcesAsync == null)
                    {
                        return UnboxToJsValue(Array.Empty<object>());
                    }
                    var res = await sandboxCallbacks.ListResourcesAsync(string.IsNullOrWhiteSpace(serverId) ? null : serverId);
                    return UnboxToJsValue(res);
                }));

                engine.SetValue("__searchResources", new Func<string?, Task<object>>(async (query) =>
                {
                    IncrementAndEnforceQuota();
                    if (sandboxCallbacks?.SearchResourcesAsync == null)
                    {
                        return UnboxToJsValue(Array.Empty<object>());
                    }
                    var res = await sandboxCallbacks.SearchResourcesAsync(query ?? string.Empty);
                    return UnboxToJsValue(res);
                }));

                engine.SetValue("__readResource", new Func<string, Task<object>>(async (uri) =>
                {
                    IncrementAndEnforceQuota();
                    if (string.IsNullOrWhiteSpace(uri))
                    {
                        throw new ArgumentException("Resource URI must be specified.", nameof(uri));
                    }
                    if (sandboxCallbacks?.ReadResourceAsync == null)
                    {
                        throw new InvalidOperationException("Resource reading is not available in this sandbox context.");
                    }
                    var res = await sandboxCallbacks.ReadResourceAsync(uri);
                    return UnboxToJsValue(res);
                }));

                engine.SetValue("__listPrompts", new Func<string?, Task<object>>(async (serverId) =>
                {
                    IncrementAndEnforceQuota();
                    if (sandboxCallbacks?.ListPromptsAsync == null)
                    {
                        return UnboxToJsValue(Array.Empty<object>());
                    }
                    var res = await sandboxCallbacks.ListPromptsAsync(string.IsNullOrWhiteSpace(serverId) ? null : serverId);
                    return UnboxToJsValue(res);
                }));

                engine.SetValue("__getPrompt", new Func<string, object?, Task<object>>(async (promptName, promptArgs) =>
                {
                    IncrementAndEnforceQuota();
                    if (string.IsNullOrWhiteSpace(promptName))
                    {
                        throw new ArgumentException("Prompt name must be specified.", nameof(promptName));
                    }
                    if (sandboxCallbacks?.GetPromptAsync == null)
                    {
                        throw new InvalidOperationException("Prompt retrieval is not available in this sandbox context.");
                    }
                    JsonElement? argsElem = promptArgs != null ? JsonSerializer.SerializeToElement(promptArgs) : null;
                    var res = await sandboxCallbacks.GetPromptAsync(promptName, argsElem);
                    return UnboxToJsValue(res);
                }));

                // Setup dynamic proxy `tools`, top-level helpers, and `mcg`
                engine.Evaluate(@"
                    const callTool = (toolName, toolArgs, options) => __callTool(toolName, toolArgs, options);
                    const readOutput = (outputId, options) => __readOutput(outputId, options ?? null);
                    const listServers = () => __listServers();
                    const getServerInstructions = async (serverId) => {
                        if (!serverId) return null;
                        const target = String(serverId).toLowerCase();
                        const servers = await __listServers();
                        if (!Array.isArray(servers)) return null;
                        const found = servers.find(s =>
                            (s && typeof s.id === 'string' && s.id.toLowerCase() === target) ||
                            (s && typeof s.alias === 'string' && s.alias.toLowerCase() === target)
                        );
                        return found && found.instructions ? found.instructions : null;
                    };
                    const listResources = (serverId) => __listResources(serverId ?? null);
                    const searchResources = (query) => __searchResources(query ?? '');
                    const readResource = (uri) => __readResource(uri);
                    const listPrompts = (serverId) => __listPrompts(serverId ?? null);
                    const getPrompt = (name, promptArgs) => __getPrompt(name, promptArgs ?? null);

                    const tools = new Proxy({}, {
                        get: (target, serverName) => {
                            if (typeof serverName !== 'string' || serverName === 'then' || serverName === 'toJSON' || serverName === 'toString' || serverName === 'valueOf') {
                                return undefined;
                            }
                            return new Proxy({}, {
                                get: (toolTarget, toolName) => {
                                    if (typeof toolName !== 'string' || toolName === 'then' || toolName === 'toJSON' || toolName === 'toString' || toolName === 'valueOf') {
                                        return undefined;
                                    }
                                    return (toolArgs, options) => callTool(`${serverName}__${toolName}`, toolArgs, options);
                                }
                            });
                        }
                    });
                ");

                // Inject caller args (global args & mcg)
                if (args.HasValue)
                {
                    var parsedArgs = engine.Evaluate($"JSON.parse({JsonSerializer.Serialize(args.Value.GetRawText())})");
                    engine.SetValue("args", parsedArgs);
                    engine.Evaluate("const mcg = { callTool, readOutput, listServers, getServerInstructions, listResources, searchResources, readResource, listPrompts, getPrompt, tools, args };");
                }
                else
                {
                    engine.Evaluate("const args = undefined; const mcg = { callTool, readOutput, listServers, getServerInstructions, listResources, searchResources, readResource, listPrompts, getPrompt, tools, args: undefined };");
                }

                // Execute script
                JsValue jsResult;
                var trimmed = script.Trim();
                string executableScript;
                if (trimmed.Contains("return ") || trimmed.StartsWith("return") || trimmed.Contains("return\n") || trimmed.Contains("return\t") || trimmed.Contains("return;") ||
                    trimmed.Contains("await ") || trimmed.Contains("await\n") || trimmed.Contains("await\t") || trimmed.StartsWith("await"))
                {
                    executableScript = $"(async () => {{\n{script}\n}})()";
                }
                else
                {
                    executableScript = script;
                }

                jsResult = await engine.EvaluateAsync(executableScript);
                if (!jsResult.IsUndefined())
                {
                    result.ResultJson = JsonSerializer.Serialize(jsResult.ToObject());
                }
            }
            catch (StatementsCountOverflowException ex)
            {
                result.IsError = true;
                result.ErrorMessage = $"Script exceeded maximum statement limit ({settings.CodeModeMaxStatements} statements). Execution terminated to prevent infinite loops: {ex.Message}";
            }
            catch (MemoryLimitExceededException ex)
            {
                result.IsError = true;
                result.ErrorMessage = $"Script exceeded allocated memory ceiling ({settings.CodeModeMaxMemoryMb} MB): {ex.Message}";
            }
            catch (TimeoutException ex)
            {
                result.IsError = true;
                result.ErrorMessage = $"Code mode execution exceeded timeout limit ({settings.CodeModeExecutionTimeoutSeconds}s): {ex.Message}";
            }
            catch (ExecutionCanceledException)
            {
                result.IsError = true;
                result.ErrorMessage = timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested
                    ? $"Code mode execution exceeded timeout limit ({settings.CodeModeExecutionTimeoutSeconds}s)."
                    : "Code mode execution was cancelled by client disconnect.";
            }
            catch (OperationCanceledException)
            {
                result.IsError = true;
                result.ErrorMessage = timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested
                    ? $"Code mode execution exceeded timeout limit ({settings.CodeModeExecutionTimeoutSeconds}s)."
                    : "Code mode execution was cancelled by client disconnect.";
            }
            catch (JavaScriptException ex)
            {
                result.IsError = true;
                result.ErrorMessage = $"JavaScript runtime error at line {ex.Location.Start.Line}, col {ex.Location.Start.Column}: {ex.Message}";
                AddLog($"[ERROR] {ex}");
            }
            catch (PromiseRejectedException ex)
            {
                result.IsError = true;
                result.ErrorMessage = $"JavaScript Promise rejected: {ex.Message}";
                AddLog($"[ERROR] {ex}");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("maximum tool invocation limit"))
            {
                result.IsError = true;
                result.ErrorMessage = ex.Message;
                AddLog($"[ERROR] {ex.Message}");
            }
            catch (Exception ex)
            {
                result.IsError = true;
                result.ErrorMessage = $"Script execution failed: {ex.Message}";
                AddLog($"[ERROR] {ex}");
            }
            finally
            {
                sw.Stop();
                result.ExecutionTimeMs = sw.ElapsedMilliseconds;
                result.ToolCallsCount = Volatile.Read(ref toolCallsCount);
            }

            return result;
        }

        private static CodeModeCallOptions? ParseCallOptions(object? options)
        {
            if (options == null)
            {
                return null;
            }

            var optsElement = JsonSerializer.SerializeToElement(options);
            if (optsElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? authToken = TryGetString(optsElement, "authToken", "auth_token");
            int? timeoutSeconds = TryGetInt(optsElement, out var tVal, "timeoutSeconds", "timeout_seconds") ? tVal : null;
            int? maxChars = TryGetInt(optsElement, out var mVal, "maxChars", "max_chars") ? mVal : null;
            int? offset = TryGetInt(optsElement, out var oVal, "offset") ? oVal : null;
            string? filter = TryGetString(optsElement, "filter");
            string? concurrencyKey = TryGetString(optsElement, "concurrencyKey", "concurrency_key");

            return new CodeModeCallOptions(authToken, timeoutSeconds, maxChars, offset, filter, concurrencyKey);
        }

        private static string? TryGetString(JsonElement obj, params string[] names)
        {
            foreach (var name in names)
            {
                if (obj.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    return prop.GetString();
                }
            }
            return null;
        }

        private static bool TryGetInt(JsonElement obj, out int value, params string[] names)
        {
            foreach (var name in names)
            {
                if (obj.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out value))
                {
                    return true;
                }
            }
            value = 0;
            return false;
        }
    }
}
