using System.ComponentModel.DataAnnotations;

namespace ModelContextGateway.Models
{
    public class RouterSettings
    {
        [Key]
        public string Id { get; set; } = "default";
        public string DashboardTitle { get; set; } = "MCP Gateway";
        public string DashboardIcon { get; set; } = "fa-solid fa-network-wired";
        public string EmbeddingProvider { get; set; } = "local"; // "local" or "api"
        public string EmbeddingApiUrl { get; set; } = "http://litellm:4000/v1/embeddings";
        public string EmbeddingApiKey { get; set; } = "";
        public string EmbeddingApiModel { get; set; } = "all-MiniLM-L6-v2";
        public string EmbeddingModelDir { get; set; } = "data/models";
        public int GlobalMaxKeys { get; set; } = 0; // 0 = unlimited
        public int UserMaxKeys { get; set; } = 0; // 0 = unlimited
        public string UserSecretStorage { get; set; } = "Database"; // "Database" or "Vault"
        public bool AllowOpenClientRegistration { get; set; } = true;
        public int ToolSearchDefaultLimit { get; set; } = 5;
        public double ToolSearchMinScore { get; set; } = 0.35;
        public double ToolSearchScoreDropoffRatio { get; set; } = 0.60;
        public string MasterKeySource => DbKeyHelper.ActiveKeySource.ToString();
    }
}

