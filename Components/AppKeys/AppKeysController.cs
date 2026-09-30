using System.Security.Claims;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ModelContextGateway.Components.AppKeys
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AppKeysController : ControllerBase
    {
        private readonly IAppKeyRepository _appKeyRepository;
        private readonly ISettingRepository _settingRepository;
        private readonly IConfiguration _config;
        private readonly IAuditLogger _auditLogger;
        private readonly ICredentialService _credentialService;
        private readonly IUserQuotaRepository? _userQuotaRepository;

        public AppKeysController(
            IAppKeyRepository appKeyRepository,
            ISettingRepository settingRepository,
            IConfiguration config,
            IAuditLogger auditLogger,
            ICredentialService credentialService,
            IUserQuotaRepository? userQuotaRepository = null)
        {
            _appKeyRepository = appKeyRepository;
            _settingRepository = settingRepository;
            _config = config;
            _auditLogger = auditLogger;
            _credentialService = credentialService;
            _userQuotaRepository = userQuotaRepository ?? (appKeyRepository as IUserQuotaRepository);
        }

        private async Task<UserIdentityContext> GetIdentityAsync()
        {
            var compositeProvider = HttpContext.RequestServices.GetRequiredService<CompositeIdentityProvider>();
            var identity = await compositeProvider.ResolveIdentityAsync(HttpContext);
            if ((identity == null || identity.Username == "anonymous") && HttpContext.User?.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(HttpContext.User.Identity.Name))
            {
                var groups = HttpContext.User.Claims
                    .Where(c => c.Type == ClaimTypes.Role || c.Type == "groups" || c.Type == "group")
                    .Select(c => c.Value)
                    .ToList();
                return new UserIdentityContext(HttpContext.User.Identity.Name, HttpContext.User.Identity.AuthenticationType ?? "TestAuth", groups);
            }
            return identity!;
        }

        private bool IsAdmin(UserIdentityContext identity)
        {
            return SecurityValidationHelper.IsAdmin(identity, _config, HttpContext);
        }

        [HttpGet]
        public async Task<IActionResult> GetAppKeys([FromQuery] string? keyType = null, [FromQuery] string? usernameFilter = null)
        {
            var identity = await GetIdentityAsync();
            var currentUser = identity.Username;
            var isAdmin = IsAdmin(identity);

            try
            {
                string? effectiveKeyType;
                string? effectiveUsernameFilter;

                if (!isAdmin)
                {
                    effectiveKeyType = "personal";
                    effectiveUsernameFilter = currentUser;
                }
                else
                {
                    effectiveKeyType = keyType;
                    effectiveUsernameFilter = usernameFilter;
                }

                var keys = await _appKeyRepository.GetAppKeysAsync(effectiveUsernameFilter, isAdmin, currentUser, effectiveKeyType);

                // Return sanitized keys (do not return the EncryptedKey string to prevent exposing DB cipher blocks)
                var result = keys.Select(k => new
                {
                    k.Id,
                    k.Name,
                    k.Username,
                    k.KeyType,
                    k.KeyPrefix,
                    Scopes = DeserializeScopes(k.ScopesJson),
                    k.ExpiresAt,
                    k.CreatedAt
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                HttpContext?.RequestServices?.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger(GetType().Name)?.LogError(ex, "An unexpected error occurred.");
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }

        [HttpGet("limits")]
        public async Task<IActionResult> GetAppKeysLimits()
        {
            var identity = await GetIdentityAsync();
            var currentUser = identity.Username;
            var isAdmin = IsAdmin(identity);

            try
            {
                int globalMax = 0;
                int userMax = 5;

                // Load active settings
                var settings = await _settingRepository.GetSettingsAsync();
                if (settings != null)
                {
                    globalMax = settings.GlobalMaxKeys;
                    userMax = settings.UserMaxKeys;
                }

                // Custom user quota override takes precedence for currentUser
                if (_userQuotaRepository != null)
                {
                    var customQuota = await _userQuotaRepository.GetUserQuotaAsync(currentUser);
                    if (customQuota != null)
                    {
                        userMax = customQuota.MaxKeys;
                    }
                }

                int totalActiveKeys = await _appKeyRepository.GetTotalActiveKeysAsync();
                int userActiveKeys = await _appKeyRepository.GetUserActiveKeysAsync(currentUser);

                return Ok(new
                {
                    globalMax,
                    userMax,
                    totalActiveKeys,
                    userActiveKeys,
                    isLimitReached = !isAdmin && ((globalMax > 0 && totalActiveKeys >= globalMax) || (userMax > 0 && userActiveKeys >= userMax))
                });
            }
            catch (Exception ex)
            {
                HttpContext?.RequestServices?.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger(GetType().Name)?.LogError(ex, "An unexpected error occurred.");
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateAppKey([FromBody] CreateAppKeyRequest model)
        {
            var identity = await GetIdentityAsync();
            var currentUser = identity.Username;
            var isAdmin = IsAdmin(identity);

            if (model == null || string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest(new { error = "Name is required." });
            }

            string targetUser;
            string keyType;
            string ownerSid;

            if (!isAdmin)
            {
                targetUser = currentUser;
                keyType = "personal";
                ownerSid = identity.AllSids.FirstOrDefault() ?? "";
            }
            else
            {
                targetUser = !string.IsNullOrWhiteSpace(model.Username) ? model.Username : currentUser;
                keyType = string.Equals(model.KeyType, "system", StringComparison.OrdinalIgnoreCase) ? "system" : "personal";

                if (keyType == "system")
                {
                    // System/service keys decouple from any user SID
                    ownerSid = "";
                }
                else if (!targetUser.Equals(currentUser, StringComparison.OrdinalIgnoreCase))
                {
                    ownerSid = ""; // Decouple admin's SID from target user's key
                    var ldapService = HttpContext.RequestServices.GetService<ILdapService>();
                    if (ldapService != null)
                    {
                        var targetSids = await ldapService.ResolveUserSidsAsync(targetUser);
                        var primarySid = targetSids.FirstOrDefault();
                        if (!string.IsNullOrEmpty(primarySid))
                        {
                            ownerSid = primarySid;
                        }
                    }
                }
                else
                {
                    ownerSid = identity.AllSids.FirstOrDefault() ?? "";
                }
            }

            try
            {
                // Check limits (admins bypass limits; 0 = unlimited)
                if (!isAdmin)
                {
                    // Retrieve max key limits (0 = unlimited)
                    int globalMax = 0;
                    int userMax = 5;

                    var settings = await _settingRepository.GetSettingsAsync();
                    if (settings != null)
                    {
                        globalMax = settings.GlobalMaxKeys;
                        userMax = settings.UserMaxKeys;
                    }

                    if (_userQuotaRepository != null)
                    {
                        var customQuota = await _userQuotaRepository.GetUserQuotaAsync(targetUser);
                        if (customQuota != null)
                        {
                            userMax = customQuota.MaxKeys;
                        }
                    }

                    if (globalMax > 0)
                    {
                        int totalActiveKeys = await _appKeyRepository.GetTotalActiveKeysAsync();
                        if (totalActiveKeys >= globalMax)
                        {
                            return BadRequest(new { error = $"Global app-key limit of {globalMax} has been reached. Please contact an administrator." });
                        }
                    }

                    if (userMax > 0)
                    {
                        int userActiveKeys = await _appKeyRepository.GetUserActiveKeysAsync(targetUser);
                        if (userActiveKeys >= userMax)
                        {
                            return BadRequest(new { error = $"You have reached your personal app-key limit of {userMax}. Please revoke an existing key first." });
                        }
                    }
                }

                var scopes = model.Scopes ?? new List<string> { "all" };

                // Non-admins cannot request administrative scopes
                if (!isAdmin && scopes.Any(s =>
                    string.Equals(s?.Trim(), "admin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s?.Trim(), "*", StringComparison.OrdinalIgnoreCase)))
                {
                    return BadRequest(new { error = "Non-admin users cannot request administrative scopes." });
                }

                // Validate category scopes
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
                            return BadRequest(new { error = "Category scope cannot be empty." });
                        }

                        if (!isAdmin)
                        {
                            var registeredCategories = await GetRegisteredCategoriesAsync();
                            if (!registeredCategories.Any(c => string.Equals(c, catName, StringComparison.OrdinalIgnoreCase)))
                            {
                                return BadRequest(new { error = $"Category '{catName}' does not exist among registered servers." });
                            }
                        }
                    }
                }

                var (appKey, plaintextKey) = await _credentialService.CreateCredentialAsync(
                    model.Name,
                    targetUser,
                    ownerSid,
                    scopes,
                    model.ExpiresInDays,
                    keyType
                );

                await _auditLogger.LogAdminActionAsync(
                    identity.Username, "appkey.create", appKey.Id,
                    $"name={appKey.Name};owner={targetUser};ownerSid={ownerSid};keyType={keyType}", true);

                // Return plaintext key ONCE to the user
                return Ok(new
                {
                    appKey.Id,
                    appKey.Name,
                    appKey.Username,
                    appKey.KeyType,
                    appKey.KeyPrefix,
                    PlaintextKey = plaintextKey,
                    Scopes = scopes,
                    appKey.ExpiresAt,
                    appKey.CreatedAt
                });
            }
            catch (Exception ex)
            {
                HttpContext?.RequestServices?.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger(GetType().Name)?.LogError(ex, "An unexpected error occurred.");
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }

        [HttpPost("{id}/rotate")]
        public async Task<IActionResult> RotateAppKey(string id)
        {
            var identity = await GetIdentityAsync();
            var currentUser = identity.Username;
            var isAdmin = IsAdmin(identity);

            try
            {
                var appKey = await _appKeyRepository.GetAppKeyByIdAsync(id);

                if (appKey == null)
                {
                    return NotFound(new { error = "AppKey not found." });
                }

                if (!isAdmin)
                {
                    // Non-admin can only rotate their own personal keys (not system keys or other users' keys)
                    if (!appKey.Username.Equals(currentUser, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(appKey.KeyType, "personal", StringComparison.OrdinalIgnoreCase))
                    {
                        return Forbid();
                    }
                }

                var (rotatedKey, plaintextKey) = await _credentialService.RotateCredentialAsync(id);

                await _auditLogger.LogAdminActionAsync(
                    identity.Username, "appkey.rotate", id,
                    $"owner={rotatedKey.Username};keyType={rotatedKey.KeyType}", true);

                return Ok(new RotateAppKeyResponse
                {
                    Id = rotatedKey.Id,
                    Name = rotatedKey.Name,
                    Username = rotatedKey.Username,
                    KeyType = rotatedKey.KeyType,
                    KeyPrefix = rotatedKey.KeyPrefix,
                    PlaintextKey = plaintextKey,
                    Scopes = DeserializeScopes(rotatedKey.ScopesJson),
                    ExpiresAt = rotatedKey.ExpiresAt,
                    CreatedAt = rotatedKey.CreatedAt,
                    LastUsedAt = null
                });
            }
            catch (Exception ex)
            {
                HttpContext?.RequestServices?.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger(GetType().Name)?.LogError(ex, "An unexpected error occurred.");
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> RevokeAppKey(string id)
        {
            var identity = await GetIdentityAsync();
            var currentUser = identity.Username;
            var isAdmin = IsAdmin(identity);

            try
            {
                var appKey = await _appKeyRepository.GetAppKeyByIdAsync(id);

                if (appKey == null)
                {
                    return NotFound(new { error = "AppKey not found." });
                }

                if (!isAdmin)
                {
                    // Non-admin can only delete their own personal keys (not system keys or other users' keys)
                    if (!appKey.Username.Equals(currentUser, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(appKey.KeyType, "personal", StringComparison.OrdinalIgnoreCase))
                    {
                        return Forbid();
                    }
                }

                await _credentialService.RevokeCredentialAsync(id);

                await _auditLogger.LogAdminActionAsync(
                    identity.Username, "appkey.revoke", id,
                    $"owner={appKey.Username};keyType={appKey.KeyType}", true);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                HttpContext?.RequestServices?.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger(GetType().Name)?.LogError(ex, "An unexpected error occurred.");
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }

        [HttpGet("quotas")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> GetUserQuotas()
        {
            try
            {
                if (_userQuotaRepository == null)
                {
                    return Ok(Enumerable.Empty<UserQuota>());
                }
                var quotas = await _userQuotaRepository.GetAllUserQuotasAsync();
                return Ok(quotas);
            }
            catch (Exception ex)
            {
                HttpContext?.RequestServices?.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger(GetType().Name)?.LogError(ex, "An unexpected error occurred.");
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }

        [HttpPost("quotas")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> SetUserQuota([FromBody] SetUserQuotaRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Username))
            {
                return BadRequest(new { error = "Username is required." });
            }

            if (model.MaxKeys < 0)
            {
                return BadRequest(new { error = "MaxKeys cannot be negative." });
            }

            var identity = await GetIdentityAsync();

            try
            {
                if (_userQuotaRepository != null)
                {
                    await _userQuotaRepository.SetUserQuotaAsync(model.Username, model.MaxKeys);
                }

                await _auditLogger.LogAdminActionAsync(
                    identity.Username, "userquota.set", model.Username,
                    $"maxKeys={model.MaxKeys}", true);

                return Ok(new { success = true, username = model.Username, maxKeys = model.MaxKeys });
            }
            catch (Exception ex)
            {
                HttpContext?.RequestServices?.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger(GetType().Name)?.LogError(ex, "An unexpected error occurred.");
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }

        [HttpDelete("quotas/{username}")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> DeleteUserQuota(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest(new { error = "Username is required." });
            }

            var identity = await GetIdentityAsync();

            try
            {
                if (_userQuotaRepository != null)
                {
                    await _userQuotaRepository.DeleteUserQuotaAsync(username);
                }

                await _auditLogger.LogAdminActionAsync(
                    identity.Username, "userquota.delete", username,
                    $"username={username}", true);

                return Ok(new { success = true, username });
            }
            catch (Exception ex)
            {
                HttpContext?.RequestServices?.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger(GetType().Name)?.LogError(ex, "An unexpected error occurred.");
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }

        private async Task<List<string>> GetRegisteredCategoriesAsync()
        {
            var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dbFactory = HttpContext.RequestServices.GetService<IDbConnectionFactory>();
            if (dbFactory != null)
            {
                try
                {
                    using var conn = dbFactory.CreateConnection();
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
                catch
                {
                    // If Servers table does not exist or DB error
                }
            }
            return categories.ToList();
        }

        private List<string> DeserializeScopes(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string> { json };
            }
        }
    }

    public class RotateAppKeyResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string KeyType { get; set; } = string.Empty;
        public string KeyPrefix { get; set; } = string.Empty;
        public string PlaintextKey { get; set; } = string.Empty;
        public List<string> Scopes { get; set; } = new();
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
    }
}



