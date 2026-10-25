using System.Text.Json;

namespace ModelContextGateway.Core.CodeMode
{
    public interface ICodeModeExecutionEngine
    {
        Task<CodeModeExecutionResult> ExecuteAsync(
            string script,
            JsonElement? args,
            RouterSettings settings,
            Func<string, JsonElement, string?, Task<object>> toolDispatcher,
            CancellationToken cancellationToken);
    }
}
