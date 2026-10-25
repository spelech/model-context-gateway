namespace ModelContextGateway.Core.CodeMode
{
    public class CodeModeExecutionResult
    {
        public bool IsError { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ResultJson { get; set; }
        public List<string> Logs { get; set; } = new();
        public long ExecutionTimeMs { get; set; }
        public int ToolCallsCount { get; set; }
    }
}
