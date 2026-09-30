using System.ComponentModel.DataAnnotations;

namespace ModelContextGateway.Components.AppKeys
{
    public class AppKey
    {
        [Key]
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string KeyPrefix { get; set; } = string.Empty;
        public string EncryptedKey { get; set; } = string.Empty;
        public string ScopesJson { get; set; } = "[]"; // JSON array of scopes, e.g. ["all"], ["server:ha"], etc.
        public string OwnerSid { get; set; } = string.Empty;
        public string KeyType { get; set; } = "personal"; // "personal" | "system"
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAt { get; set; }
    }
}
