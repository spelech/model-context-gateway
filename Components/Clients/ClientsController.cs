using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ModelContextGateway.Components.Clients
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "AdminPolicy")]
    public class ClientsController : ControllerBase
    {
        private const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        private readonly IOAuthClientRepository _oauthClientRepo;
        private readonly IAuditLogger _auditLogger;
        private readonly IDbConnectionFactory? _dbFactory;

        [ActivatorUtilitiesConstructor]
        public ClientsController(IOAuthClientRepository oauthClientRepo, IAuditLogger auditLogger, IDbConnectionFactory? dbFactory = null)
        {
            _oauthClientRepo = oauthClientRepo;
            _auditLogger = auditLogger;
            _dbFactory = dbFactory;
        }

        [HttpGet]
        public async Task<IActionResult> GetClients()
        {
            var oauthClients = await _oauthClientRepo.GetOAuthClientsAsync();

            var clients = oauthClients.Select(c =>
            {
                List<string> scopes;
                try { scopes = JsonSerializer.Deserialize<List<string>>(c.ScopesJson) ?? new List<string>(); }
                catch { scopes = new List<string>(); }

                List<string> redirectUris;
                try { redirectUris = JsonSerializer.Deserialize<List<string>>(c.RedirectUrisJson) ?? new List<string>(); }
                catch { redirectUris = new List<string>(); }

                List<string> grantTypes;
                try { grantTypes = JsonSerializer.Deserialize<List<string>>(c.GrantTypesJson) ?? new List<string>(); }
                catch { grantTypes = new List<string>(); }

                return new
                {
                    Id = c.ClientId,
                    ClientId = c.ClientId,
                    DisplayName = c.ClientName,
                    ClientName = c.ClientName,
                    ClientType = c.ClientType,
                    Scopes = scopes,
                    RedirectUris = redirectUris,
                    GrantTypes = grantTypes,
                    OwnerSid = c.OwnerSid,
                    CreatedBy = c.CreatedBy,
                    CreatedAt = (DateTime?)c.CreatedAt,
                    ExpiresAt = c.ExpiresAt,
                    IsDynamic = string.Equals(c.CreatedBy, "dcr", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(c.CreatedBy)
                };
            }).ToList();

            return Ok(clients);
        }

        [HttpPost]
        public async Task<IActionResult> CreateClient([FromBody] CreateClientModel model)
        {
            if (string.IsNullOrWhiteSpace(model.DisplayName))
            {
                return BadRequest("DisplayName is required.");
            }

            var clientId = Guid.NewGuid().ToString("N");
            var clientSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            var secretHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(clientSecret))).ToLowerInvariant();
            var scopes = model.Scopes ?? new List<string>();

            var username = User?.Identity?.Name ?? "unknown";
            UserIdentityContext? identity = null;

            var httpCtx = HttpContext;
            if (httpCtx?.RequestServices != null)
            {
                try
                {
                    var compositeProvider = httpCtx.RequestServices.GetService<CompositeIdentityProvider>();
                    if (compositeProvider != null)
                    {
                        identity = await compositeProvider.ResolveIdentityAsync(httpCtx);
                        if (identity != null)
                        {
                            username = identity.Username;
                        }
                    }
                }
                catch { }
            }

            var config = httpCtx?.RequestServices?.GetService<Microsoft.Extensions.Configuration.IConfiguration>();
            var isAdmin = (identity != null && SecurityValidationHelper.IsAdmin(identity, config))
                || User?.IsInRole("Admin") == true
                || User?.Claims.Any(c => c.Value == "full_admin" || c.Value == "S-1-5-32-544") == true
                || httpCtx == null;

            // Validate requested category scopes
            foreach (var scope in scopes)
            {
                if (string.IsNullOrWhiteSpace(scope))
                {
                    continue;
                }

                var trimmed = scope.Trim();
                if (trimmed.StartsWith("category:", StringComparison.OrdinalIgnoreCase))
                {
                    var catName = trimmed.Substring("category:".Length).Trim();
                    if (string.IsNullOrWhiteSpace(catName))
                    {
                        return BadRequest("Category scope cannot be empty.");
                    }

                    if (!isAdmin)
                    {
                        var registeredCategories = await GetRegisteredCategoriesAsync();
                        if (!registeredCategories.Any(c => string.Equals(c, catName, StringComparison.OrdinalIgnoreCase)))
                        {
                            return BadRequest($"Category '{catName}' does not exist among registered servers.");
                        }
                    }
                }
            }

            DateTime? expiresAt = model.ExpiresInDays.HasValue ? DateTime.UtcNow.AddDays(model.ExpiresInDays.Value) : null;

            try
            {
                // CRITICAL SECURITY: Decouple creator SID from client credentials.
                // Client credentials must NOT inherit administrative privileges or creator's SID.
                var oauthClient = new OAuthClient
                {
                    ClientId = clientId,
                    ClientSecretHash = secretHash,
                    ClientName = model.DisplayName,
                    ClientType = "confidential",
                    RedirectUrisJson = model.RedirectUris != null ? JsonSerializer.Serialize(model.RedirectUris) : "[]",
                    GrantTypesJson = model.GrantTypes != null ? JsonSerializer.Serialize(model.GrantTypes) : JsonSerializer.Serialize(new[] { "client_credentials", "authorization_code", "refresh_token" }),
                    ScopesJson = JsonSerializer.Serialize(scopes),
                    OwnerSid = string.Empty,
                    CreatedBy = username,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt
                };

                await _oauthClientRepo.SaveOAuthClientAsync(oauthClient);

                await _auditLogger.LogAdminActionAsync(username, "client.create", clientId, JsonSerializer.Serialize(new { model.DisplayName, Scopes = model.Scopes, model.ExpiresInDays }), true);

                return Ok(new
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret,
                    DisplayName = model.DisplayName,
                    ExpiresAt = expiresAt
                });
            }
            catch (Exception ex)
            {
                HttpContext?.RequestServices?.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger(GetType().Name)?.LogError(ex, "An unexpected error occurred.");
                await _auditLogger.LogAdminActionAsync(username, "client.create", "", JsonSerializer.Serialize(new { model.DisplayName }), false, ex.Message);
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }

        private async Task<List<string>> GetRegisteredCategoriesAsync()
        {
            var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (_dbFactory != null)
                {
                    using var conn = _dbFactory.CreateConnection();
                    var rawList = await conn.QueryAsync<string>("SELECT Categories FROM Servers WHERE Enabled = 1");
                    foreach (var rawCat in rawList)
                    {
                        if (string.IsNullOrWhiteSpace(rawCat))
                        {
                            continue;
                        }

                        try
                        {
                            var list = JsonSerializer.Deserialize<List<string>>(rawCat);
                            if (list != null)
                            {
                                foreach (var c in list)
                                {
                                    if (!string.IsNullOrWhiteSpace(c))
                                    {
                                        categories.Add(c.Trim());
                                    }
                                }
                            }
                        }
                        catch
                        {
                            var parts = rawCat.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            foreach (var p in parts)
                            {
                                categories.Add(p);
                            }
                        }
                    }
                }
            }
            catch
            {
                // If Servers table does not exist or DB error
            }
            return categories.ToList();
        }

        [HttpPost("{id}/rotate-secret")]
        public async Task<IActionResult> RotateSecret(string id)
        {
            var username = User?.Identity?.Name ?? "unknown";
            UserIdentityContext? identity = null;

            var httpCtx = HttpContext;
            if (httpCtx?.RequestServices != null)
            {
                try
                {
                    var compositeProvider = httpCtx.RequestServices.GetService<CompositeIdentityProvider>();
                    if (compositeProvider != null)
                    {
                        identity = await compositeProvider.ResolveIdentityAsync(httpCtx);
                        if (identity != null)
                        {
                            username = identity.Username;
                        }
                    }
                }
                catch { }
            }

            var config = httpCtx?.RequestServices?.GetService<Microsoft.Extensions.Configuration.IConfiguration>();
            var isAdmin = (identity != null && SecurityValidationHelper.IsAdmin(identity, config))
                || User?.IsInRole("Admin") == true
                || User?.Claims.Any(c => c.Value == "full_admin" || c.Value == "S-1-5-32-544") == true
                || httpCtx == null;

            try
            {
                var client = await _oauthClientRepo.GetOAuthClientByIdAsync(id);
                if (client == null)
                {
                    await _auditLogger.LogAdminActionAsync(username, "client.rotate_secret", id, "", false, "Client not found");
                    return NotFound();
                }

                if (!isAdmin && !string.Equals(client.CreatedBy, username, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }

                var plaintextSecret = RandomNumberGenerator.GetString(Base62Chars, 32);
                var secretHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintextSecret))).ToLowerInvariant();

                if (_dbFactory != null)
                {
                    using var conn = _dbFactory.CreateConnection();
                    await conn.ExecuteAsync(
                        "UPDATE OAuthClients SET ClientSecretHash = @Hash, LastUsedAt = NULL WHERE ClientId = @Id;",
                        new { Hash = secretHash, Id = id });
                }

                client.ClientSecretHash = secretHash;
                client.LastUsedAt = null;
                await _oauthClientRepo.SaveOAuthClientAsync(client);

                await _auditLogger.LogAdminActionAsync(username, "client.rotate_secret", id, "", true);

                return Ok(new { clientId = id, clientSecret = plaintextSecret });
            }
            catch (Exception ex)
            {
                HttpContext?.RequestServices?.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger(GetType().Name)?.LogError(ex, "An unexpected error occurred.");
                await _auditLogger.LogAdminActionAsync(username, "client.rotate_secret", id, "", false, ex.Message);
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteClient(string id)
        {
            var username = User?.Identity?.Name ?? "unknown";
            try
            {
                try
                {
                    var compositeProvider = HttpContext?.RequestServices?.GetService<CompositeIdentityProvider>();
                    if (compositeProvider != null && HttpContext != null)
                    {
                        var identity = await compositeProvider.ResolveIdentityAsync(HttpContext);
                        if (identity != null)
                        {
                            username = identity.Username;
                        }
                    }
                }
                catch { }

                var success = await _oauthClientRepo.DeleteOAuthClientAsync(id);
                if (!success)
                {
                    await _auditLogger.LogAdminActionAsync(username, "client.delete", id, "", false, "Client not found");
                    return NotFound();
                }

                await _auditLogger.LogAdminActionAsync(username, "client.delete", id, "", true);
                return NoContent();
            }
            catch (Exception ex)
            {
                HttpContext?.RequestServices?.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger(GetType().Name)?.LogError(ex, "An unexpected error occurred.");
                await _auditLogger.LogAdminActionAsync(username, "client.delete", id, "", false, ex.Message);
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }

        [HttpPost("cleanup")]
        public async Task<IActionResult> CleanupClients([FromQuery] int retentionDays = 30)
        {
            var username = User?.Identity?.Name ?? "unknown";
            try
            {
                try
                {
                    var compositeProvider = HttpContext?.RequestServices?.GetService<CompositeIdentityProvider>();
                    if (compositeProvider != null && HttpContext != null)
                    {
                        var identity = await compositeProvider.ResolveIdentityAsync(HttpContext);
                        if (identity != null)
                        {
                            username = identity.Username;
                        }
                    }
                }
                catch { }

                var count = await _oauthClientRepo.CleanupDcrClientsAsync(retentionDays);
                await _auditLogger.LogAdminActionAsync(username, "client.cleanup", "dcr", JsonSerializer.Serialize(new { cleanedCount = count, retentionDays }), true);
                return Ok(new { cleanedCount = count, message = $"Cleaned up {count} stale or duplicate client registrations." });
            }
            catch (Exception ex)
            {
                HttpContext?.RequestServices?.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger(GetType().Name)?.LogError(ex, "An unexpected error occurred.");
                await _auditLogger.LogAdminActionAsync(username, "client.cleanup", "dcr", "", false, ex.Message);
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }

        public class CreateClientModel
        {
            public string DisplayName { get; set; } = string.Empty;
            public List<string> Scopes { get; set; } = new();
            public List<string>? RedirectUris { get; set; }
            public List<string>? GrantTypes { get; set; }
            public int? ExpiresInDays { get; set; }
        }
    }
}


