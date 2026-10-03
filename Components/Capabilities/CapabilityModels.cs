using System.Text.Json;

namespace ModelContextGateway.Components.Capabilities
{
    public class TestToolCallModel
    {
        public string ServerId { get; set; } = string.Empty;
        public string ToolName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public JsonElement Arguments { get; set; }
        public string ResolvedToolName => !string.IsNullOrEmpty(ToolName) ? ToolName : Name;
    }

    public class TestCallModel
    {
        public string ServerId { get; set; } = string.Empty;
        public string ToolName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public JsonElement Arguments { get; set; }
        public string ResolvedToolName => !string.IsNullOrEmpty(ToolName) ? ToolName : Name;
    }

    public class TestPromptGetModel
    {
        public string ServerId { get; set; } = string.Empty;
        public string PromptName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public JsonElement Arguments { get; set; }
        public string ResolvedPromptName => !string.IsNullOrEmpty(PromptName) ? PromptName : Name;
    }

    public class TestResourceReadModel
    {
        public string ServerId { get; set; } = string.Empty;
        public string Uri { get; set; } = string.Empty;
    }

    public class SearchModel
    {
        public string Query { get; set; } = string.Empty;
        public string? Mode { get; set; } = "hybrid";
        public double? DenseWeight { get; set; } = 0.5;
        public int? Limit { get; set; } = 15;
    }
}
