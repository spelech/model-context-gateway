using System.Text.Json;

namespace ModelContextGateway.Core.CodeMode
{
    public sealed record CodeModeCallOptions(
        string? AuthToken = null,
        int? TimeoutSeconds = null,
        int? MaxChars = null,
        int? Offset = null,
        string? Filter = null,
        string? ConcurrencyKey = null);

    /// <summary>
    /// Host callbacks injected into the Code Mode JavaScript sandbox to enable
    /// RBAC-filtered server discovery, resource listing/searching/reading, prompt retrieval,
    /// per-call execution options, and buffered output paging.
    /// </summary>
    public class CodeModeSandboxCallbacks
    {
        public Func<Task<object>>? ListServersAsync { get; set; }
        public Func<string?, Task<object>>? ListResourcesAsync { get; set; }
        public Func<string, Task<object>>? SearchResourcesAsync { get; set; }
        public Func<string, Task<object?>>? ReadResourceAsync { get; set; }
        public Func<string?, Task<object>>? ListPromptsAsync { get; set; }
        public Func<string, JsonElement?, Task<object?>>? GetPromptAsync { get; set; }
        public Func<string, JsonElement, CodeModeCallOptions?, CancellationToken, Task<object>>? CallToolWithOptionsAsync { get; set; }
        public Func<string, int?, int?, Task<object>>? ReadOutputAsync { get; set; }
    }
}
