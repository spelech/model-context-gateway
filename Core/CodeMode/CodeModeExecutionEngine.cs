using System.Diagnostics;
using System.Text.Json;
using Jint;
using Jint.Native;
using Jint.Runtime;

namespace ModelContextGateway.Core.CodeMode
{
    public class CodeModeExecutionEngine : ICodeModeExecutionEngine
    {
        public Task<CodeModeExecutionResult> ExecuteAsync(
            string script,
            JsonElement? args,
            RouterSettings settings,
            Func<string, JsonElement, string?, Task<object>> toolDispatcher,
            CancellationToken cancellationToken)
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
                        options.MaxStatements(settings.CodeModeMaxStatements);
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

                // Execute script
                // Note: LLM scripts often contain top-level return statements (e.g. `return { count: 1 };`)
                // or await expressions (e.g. `return await tools.srv.fn()`).
                // In standard JavaScript, top-level return is only legal inside a function body.
                // We wrap the script in an immediately invoked async function so top-level return works seamlessly.
                JsValue jsResult;
                var trimmed = script.Trim();
                // Wrap in IIFE function or async function if it contains return or await
                string executableScript;
                if (trimmed.Contains("return ") || trimmed.StartsWith("return") || trimmed.Contains("await "))
                {
                    executableScript = $"(function() {{\n{script}\n}})()";
                }
                else
                {
                    executableScript = script;
                }

                jsResult = engine.Evaluate(executableScript);
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

            return Task.FromResult(result);
        }
    }
}
