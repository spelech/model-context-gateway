using System.Text.Json;

namespace ModelContextGateway.Core.CodeMode
{
    /// <summary>
    /// Host callbacks injected into the Code Mode JavaScript sandbox to enable
    /// RBAC-filtered server discovery, resource listing/searching/reading, and prompt retrieval.
    /// </summary>
    public class CodeModeSandboxCallbacks
    {
        public Func<Task<object>>? ListServersAsync { get; set; }
        public Func<string?, Task<object>>? ListResourcesAsync { get; set; }
        public Func<string, Task<object>>? SearchResourcesAsync { get; set; }
        public Func<string, Task<object?>>? ReadResourceAsync { get; set; }
        public Func<string?, Task<object>>? ListPromptsAsync { get; set; }
        public Func<string, JsonElement?, Task<object?>>? GetPromptAsync { get; set; }
    }
}
