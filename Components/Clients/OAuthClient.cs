using System.ComponentModel.DataAnnotations;

namespace ModelContextGateway.Components.Clients
{
    public class OAuthClient
    {
        [Key]
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecretHash { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ClientType { get; set; } = "confidential"; // "confidential" | "public"
        public string RedirectUrisJson { get; set; } = "[]"; // JSON array of redirect URIs
        public string GrantTypesJson { get; set; } = "[]"; // JSON array of grant types
        public string ScopesJson { get; set; } = "[]"; // JSON array of scopes
        public string OwnerSid { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
    }
}
