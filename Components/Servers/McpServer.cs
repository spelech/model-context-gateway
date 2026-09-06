namespace ModelContextGateway.Components.Servers
{
    public class McpServer
    {
        public string Id { get; set; } = string.Empty;
        public string? Alias { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public bool Hidden { get; set; }
        public string Type { get; set; } = "sse"; // "sse", "http", "streamable", "stdio", "custom"
        public string SecretProvider { get; set; } = "None"; // "Vault", "WindowsRegistry", "Environment", "UserProvided", "None"
        public string? SecretItemKey { get; set; }
        public string? SecretMount { get; set; }
        public string? SecretPath { get; set; }
        public string? SecretField { get; set; }
        public string AuthShape { get; set; } = "bearer"; // "bearer", "basic", "raw", "x-api-key", "custom-header", "query"
        public string? CustomHeaderName { get; set; }
        public List<string> Categories { get; set; } = new();
        public string? ApiKey { get; set; }
        public string? HeadersJson { get; set; } // JSON dictionary of custom headers
        public bool AutoDiscovered { get; set; } = false;
        public bool AllowPassThroughAuth { get; set; } = false;
        public string? DynamicAuthPrompt { get; set; }
    }

    public class BackendStatus
    {
        public string ServerId { get; set; } = string.Empty;
        public string Status { get; set; } = "Disconnected";
        public int Attempts { get; set; }
        public string Error { get; set; } = string.Empty;
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;
    }
}
