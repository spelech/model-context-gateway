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
            var result = new CodeModeExecutionResult
            {
                Logs = logs
            };

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (settings.CodeModeExecutionTimeoutSeconds > 0)
                {
                    cts.CancelAfter(TimeSpan.FromSeconds(settings.CodeModeExecutionTimeoutSeconds));
                }

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
                    options.CancellationToken(cts.Token);
                });

                // Inject console
                engine.SetValue("__console_log", new Action<object[]>(arguments =>
                {
                    var msg = string.Join(" ", arguments.Select(a => a?.ToString() ?? "null"));
                    logs.Add($"[INFO] {msg}");
                }));
                engine.SetValue("__console_warn", new Action<object[]>(arguments =>
                {
                    var msg = string.Join(" ", arguments.Select(a => a?.ToString() ?? "null"));
                    logs.Add($"[WARN] {msg}");
                }));
                engine.SetValue("__console_error", new Action<object[]>(arguments =>
                {
                    var msg = string.Join(" ", arguments.Select(a => a?.ToString() ?? "null"));
                    logs.Add($"[ERROR] {msg}");
                }));
                engine.Evaluate("const console = { log: (...args) => __console_log(args), warn: (...args) => __console_warn(args), error: (...args) => __console_error(args) };");

                // Tool & sandbox operation dispatcher & quota enforcement
                int toolCallsCount = 0;
                void IncrementAndEnforceQuota()
                {
                    toolCallsCount++;
                    result.ToolCallsCount = toolCallsCount;

                    if (settings.CodeModeMaxToolCalls > 0 && toolCallsCount > settings.CodeModeMaxToolCalls)
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
                                    return engine.Evaluate($"JSON.parse({JsonSerializer.Serialize(text)})");
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
                                    return engine.Evaluate($"JSON.parse({JsonSerializer.Serialize(text)})");
                                }
                                catch (JsonException)
                                {
                                    return text;
                                }
                            }
                        }
                    }

                    return engine.Evaluate($"JSON.parse({JsonSerializer.Serialize(responseElement.GetRawText())})");
                }

                engine.SetValue("__callTool", new Func<string, object?, object?, Task<object>>(async (toolName, toolArgs, options) =>
                {
                    IncrementAndEnforceQuota();

                    if (string.IsNullOrWhiteSpace(toolName))
                    {
                        throw new ArgumentException("Tool name must be specified.", nameof(toolName));
                    }

                    string? authToken = null;
                    if (options != null)
                    {
                        var optsElement = JsonSerializer.SerializeToElement(options);
                        if (optsElement.ValueKind == JsonValueKind.Object)
                        {
                            if (optsElement.TryGetProperty("authToken", out var tokenProp) && tokenProp.ValueKind == JsonValueKind.String)
                            {
                                authToken = tokenProp.GetString();
                            }
                            else if (optsElement.TryGetProperty("auth_token", out var snakeTokenProp) && snakeTokenProp.ValueKind == JsonValueKind.String)
                            {
                                authToken = snakeTokenProp.GetString();
                            }
                        }
                    }

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

                    var rawResponse = await toolDispatcher(toolName, argsElement, authToken);
                    return UnboxToJsValue(rawResponse);
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

                // Setup dynamic proxy `tools`, top-level `callTool`, and `mcg`
                engine.Evaluate(@"
                    const callTool = (toolName, toolArgs, options) => __callTool(toolName, toolArgs, options);
                    const listServers = () => __listServers();
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
                    engine.Evaluate("const mcg = { callTool, listServers, listResources, searchResources, readResource, listPrompts, getPrompt, tools, args };");
                }
                else
                {
                    engine.Evaluate("const args = undefined; const mcg = { callTool, listServers, listResources, searchResources, readResource, listPrompts, getPrompt, tools, args: undefined };");
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
                result.ErrorMessage = "Code mode execution was cancelled by timeout or client disconnect.";
            }
            catch (OperationCanceledException)
            {
                result.IsError = true;
                result.ErrorMessage = "Code mode execution was cancelled by timeout or client disconnect.";
            }
            catch (JavaScriptException ex)
            {
                result.IsError = true;
                result.ErrorMessage = $"JavaScript runtime error at line {ex.Location.Start.Line}, col {ex.Location.Start.Column}: {ex.Message}";
                logs.Add($"[ERROR] {ex}");
            }
            catch (PromiseRejectedException ex)
            {
                result.IsError = true;
                result.ErrorMessage = $"JavaScript Promise rejected: {ex.Message}";
                logs.Add($"[ERROR] {ex}");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("maximum tool invocation limit"))
            {
                result.IsError = true;
                result.ErrorMessage = ex.Message;
                logs.Add($"[ERROR] {ex.Message}");
            }
            catch (Exception ex)
            {
                result.IsError = true;
                result.ErrorMessage = $"Script execution failed: {ex.Message}";
                logs.Add($"[ERROR] {ex}");
            }
            finally
            {
                sw.Stop();
                result.ExecutionTimeMs = sw.ElapsedMilliseconds;
            }

            return result;
        }
    }
}
