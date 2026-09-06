using System.Text.Json;
using Dapper;

namespace ModelContextGateway.Core.Routing
{
    /// <summary>
    /// Partial class implementation providing RBAC policy authorization, user identity resolution, and invocation audit logging.
    /// </summary>
    public partial class ClientSession
    {
        /// <summary>
        /// Resolves the caller's <see cref="UserIdentityContext"/> from claim principal data or configured identity providers.
        /// </summary>
        /// <param name="httpContext">Optional HTTP context override; defaults to the current session's response context.</param>
        /// <returns>A task returning the resolved <see cref="UserIdentityContext"/>.</returns>
        public async Task<UserIdentityContext> ResolveUserIdentityAsync(HttpContext? httpContext = null)
        {
            HttpContext? contextToUse = null;
            try
            {
                contextToUse = httpContext ?? _clientResponse?.HttpContext;
            }
            catch (ObjectDisposedException)
            {
                return new UserIdentityContext("system", "System", new List<string>());
            }

            if (contextToUse == null)
            {
                return new UserIdentityContext("anonymous", "None", new List<string>());
            }

            try
            {
                if (contextToUse.Items.TryGetValue("ResolvedUserIdentity", out var cachedIdentityObj) && cachedIdentityObj is UserIdentityContext cachedIdentity)
                {
                    return cachedIdentity;
                }
            }
            catch (ObjectDisposedException)
            {
                return new UserIdentityContext("system", "System", new List<string>());
            }

            UserIdentityContext identity;

            try
            {
                if (contextToUse.User?.Identity?.IsAuthenticated == true)
                {
                    var username = contextToUse.User.Identity.Name ?? "anonymous";
                    var sids = contextToUse.User.Claims
                        .Where(c => c.Type == "Sid" || c.Type == "GroupSid" || c.Type == System.Security.Claims.ClaimTypes.GroupSid || c.Type == System.Security.Claims.ClaimTypes.PrimaryGroupSid)
                        .Select(c => c.Value)
                        .Distinct()
                        .ToList();
                    var groupNames = contextToUse.User.Claims
                        .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "Group" || c.Type == "group" || c.Type == "roles" || c.Type == "groups")
                        .Select(c => c.Value)
                        .Distinct()
                        .ToList();

                    identity = new UserIdentityContext(username, contextToUse.User.Identity.AuthenticationType ?? "Claims", GroupNames: groupNames, Sid: "", Sids: sids);
                }
                else
                {
                    IServiceProvider? services = null;
                    try
                    {
                        services = contextToUse.RequestServices ?? _rootServices;
                    }
                    catch (ObjectDisposedException)
                    {
                        services = _rootServices;
                    }

                    if (services != null)
                    {
                        try
                        {
                            var compositeProvider = services.GetService<CompositeIdentityProvider>();
                            if (compositeProvider != null)
                            {
                                identity = await compositeProvider.ResolveIdentityAsync(contextToUse);
                            }
                            else
                            {
                                identity = new UserIdentityContext("anonymous", "None", new List<string>());
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            identity = new UserIdentityContext("system", "System", new List<string>());
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to resolve user identity via CompositeIdentityProvider");
                            identity = new UserIdentityContext("anonymous", "None", new List<string>());
                        }
                    }
                    else
                    {
                        identity = new UserIdentityContext("anonymous", "None", new List<string>());
                    }
                }

                try
                {
                    contextToUse.Items["ResolvedUserIdentity"] = identity;
                }
                catch (ObjectDisposedException)
                {
                    // Context disposed while caching identity
                }

                return identity;
            }
            catch (ObjectDisposedException)
            {
                return new UserIdentityContext("system", "System", new List<string>());
            }
        }

        public async Task<bool> IsUserAuthorizedAsync(string requestMethod, string targetId, HttpContext? httpContext = null)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return false;
            }

            var contextToUse = httpContext ?? _clientResponse?.HttpContext;
            // Extract serverId across all URI and namespaced formats
            string serverId;
            if (targetId.StartsWith("mcp://", StringComparison.OrdinalIgnoreCase))
            {
                if (Uri.TryCreate(targetId, UriKind.Absolute, out var parsedUri))
                {
                    serverId = parsedUri.Host;
                }
                else
                {
                    serverId = targetId.Substring("mcp://".Length).Split('/')[0];
                }
            }
            else if (targetId.StartsWith("logs://", StringComparison.OrdinalIgnoreCase))
            {
                if (Uri.TryCreate(targetId, UriKind.Absolute, out var parsedUri))
                {
                    serverId = parsedUri.Host;
                }
                else
                {
                    serverId = targetId.Substring("logs://".Length).Split('/')[0];
                }
            }
            else if (targetId.StartsWith("router://", StringComparison.OrdinalIgnoreCase))
            {
                serverId = "router";
            }
            else if (targetId.StartsWith("server:", StringComparison.OrdinalIgnoreCase))
            {
                serverId = targetId.Substring("server:".Length);
            }
            else if (targetId.Contains("__"))
            {
                serverId = targetId.Split("__", 2)[0];
            }
            else if (targetId.Contains('/'))
            {
                serverId = targetId.Split('/', 2)[0];
            }
            else if (targetId.Contains(':'))
            {
                serverId = targetId.Split(':', 2)[0];
            }
            else if (targetId.StartsWith("plex_", StringComparison.OrdinalIgnoreCase))
            {
                serverId = "plex";
            }
            else if (targetId.StartsWith("seerr_", StringComparison.OrdinalIgnoreCase))
            {
                serverId = "seerr";
            }
            else
            {
                serverId = targetId;
            }

            // If authenticated via AppKey, check key-level scopes first
            if (contextToUse?.Items.TryGetValue("AppKeyUsed", out var appKeyUsedObj) == true && appKeyUsedObj is bool appKeyUsed && appKeyUsed)
            {
                if (contextToUse.Items.TryGetValue("AppKeyScopes", out var scopesObj) == true && scopesObj is string scopesJson)
                {
                    bool scopeAllowed = false;
                    try
                    {
                        var scopes = JsonSerializer.Deserialize<List<string>>(scopesJson);
                        if (scopes != null)
                        {
                            List<string>? serverCategories = null;

                            foreach (var s in scopes)
                            {
                                var cleanScope = s.Trim().ToLowerInvariant();
                                if (cleanScope == "all" || cleanScope == "mcp_client" || cleanScope == "*")
                                {
                                    scopeAllowed = true;
                                    break;
                                }
                                if ((targetId == "search_tools" || targetId == "execute_tool") && (IsMetaMode || requestMethod.StartsWith("tools/")))
                                {
                                    scopeAllowed = true;
                                    break;
                                }
                                if (cleanScope == $"server:{serverId}".ToLowerInvariant() ||
                                    cleanScope == $"server:{targetId}".ToLowerInvariant() ||
                                    cleanScope == serverId.ToLowerInvariant())
                                {
                                    scopeAllowed = true;
                                    break;
                                }
                                if (cleanScope == targetId.ToLowerInvariant() ||
                                    cleanScope == $"tool:{targetId}".ToLowerInvariant() ||
                                    cleanScope == $"prompt:{targetId}".ToLowerInvariant() ||
                                    cleanScope == $"resource:{targetId}".ToLowerInvariant() ||
                                    cleanScope == $"resource_template:{targetId}".ToLowerInvariant() ||
                                    cleanScope == $"template:{targetId}".ToLowerInvariant() ||
                                    cleanScope == $"completion:{targetId}".ToLowerInvariant())
                                {
                                    scopeAllowed = true;
                                    break;
                                }

                                if (cleanScope.StartsWith("category:") || cleanScope.StartsWith("group:"))
                                {
                                    var scopeCategory = cleanScope.StartsWith("category:")
                                        ? cleanScope.Substring("category:".Length).Trim()
                                        : cleanScope.Substring("group:".Length).Trim();

                                    if (!string.IsNullOrEmpty(scopeCategory))
                                    {
                                        if (string.Equals(targetId, scopeCategory, StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(serverId, scopeCategory, StringComparison.OrdinalIgnoreCase))
                                        {
                                            scopeAllowed = true;
                                            break;
                                        }

                                        serverCategories ??= await GetServerCategoriesAsync(serverId, contextToUse);
                                        if (serverCategories.Any(c => string.Equals(c, scopeCategory, StringComparison.OrdinalIgnoreCase)))
                                        {
                                            scopeAllowed = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception exScopes)
                    {
                        _logger.LogWarning(exScopes, "Failed to parse AppKey scopes JSON: {ScopesJson}", scopesJson);
                    }

                    if (!scopeAllowed)
                    {
                        _logger.LogWarning("AppKey rejected: requested target '{TargetId}' is outside the key's allowed scopes '{ScopesJson}'", targetId, scopesJson);
                        return false;
                    }
                }
            }

            var identity = await ResolveUserIdentityAsync(contextToUse);

            // 1. Admin SID bypass check
            var config = contextToUse?.RequestServices?.GetService<IConfiguration>();
            if (SecurityValidationHelper.IsAdmin(identity, config))
            {
                return true;
            }

            if (contextToUse?.RequestServices == null)
            {
                // If there's no HttpContext, we default to false (fail closed)
                return false;
            }

            try
            {
                var dbFactory = contextToUse.RequestServices.GetService<IDbConnectionFactory>();
                if (dbFactory == null)
                {
                    return false;
                }

                using var conn = dbFactory.CreateConnection();

                var targetKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    targetId,
                    $"tool:{targetId}",
                    $"prompt:{targetId}",
                    $"resource:{targetId}",
                    $"resource_template:{targetId}",
                    $"template:{targetId}",
                    $"completion:{targetId}",
                    $"server:{serverId}"
                };

                var serverCategoriesForRbac = await GetServerCategoriesAsync(serverId, contextToUse);
                foreach (var cat in serverCategoriesForRbac)
                {
                    targetKeys.Add($"category:{cat}".ToLowerInvariant());
                    targetKeys.Add($"group:{cat}".ToLowerInvariant());
                }

                var externalIds = identity.GroupNames.Concat(identity.AllSids).Distinct().ToList();
                if (!string.IsNullOrEmpty(identity.Username) && !externalIds.Contains(identity.Username))
                {
                    externalIds.Add(identity.Username);
                }

                var mappedGroups = new List<string>();
                try
                {
                    const string mapSql = "SELECT InternalGroup FROM GroupMappings WHERE ExternalId IN @ExternalIds;";
                    mappedGroups = (await conn.QueryAsync<string>(mapSql, new { ExternalIds = externalIds.ToArray() })).ToList();
                }
                catch (Exception exMap)
                {
                    _logger.LogWarning(exMap, "Failed to query GroupMappings, assuming empty");
                }

                var allUserGroups = identity.GroupNames
                    .Concat(identity.AllSids)
                    .Concat(mappedGroups)
                    .Concat(!string.IsNullOrEmpty(identity.Username) ? new[] { identity.Username } : Array.Empty<string>())
                    .Where(g => !string.IsNullOrEmpty(g))
                    .Distinct()
                    .ToList();

                if (dbFactory.ProviderName == "sqlite")
                {
                    // Check if there's an explicit deny for any of the user's groups
                    const string denySql = "SELECT COUNT(*) FROM AccessPolicies WHERE TargetId IN @TargetIds AND RequiredGroup IN @GroupNames AND IsAllowed = 0;";
                    int denyCount = await conn.ExecuteScalarAsync<int>(denySql, new { TargetIds = targetKeys.ToArray(), GroupNames = allUserGroups.ToArray() });
                    if (denyCount > 0)
                    {
                        return false;
                    }

                    // Check if there are any policies for the targets first to default-allow (inverted to fail closed)
                    const string countSql = "SELECT COUNT(*) FROM AccessPolicies WHERE TargetId IN @TargetIds;";
                    int policyCount = await conn.ExecuteScalarAsync<int>(countSql, new { TargetIds = targetKeys.ToArray() });
                    if (policyCount == 0)
                    {
                        // When an AppKey was validated against its scopes, allow by default unless explicitly denied
                        bool isAppKeyUsed = contextToUse?.Items.TryGetValue("AppKeyUsed", out var aku) == true && aku is bool bAku && bAku;
                        if (isAppKeyUsed)
                        {
                            return true;
                        }
                        return false;
                    }

                    // Check if there's an allow for any of the user's groups
                    const string allowSql = "SELECT COUNT(*) FROM AccessPolicies WHERE TargetId IN @TargetIds AND RequiredGroup IN @GroupNames AND IsAllowed = 1;";
                    int allowCount = await conn.ExecuteScalarAsync<int>(allowSql, new { TargetIds = targetKeys.ToArray(), GroupNames = allUserGroups.ToArray() });
                    return allowCount > 0;
                }
                else
                {
                    // Call stored procedure with mapped groups!
                    var groupNamesCsv = string.Join(",", allUserGroups);
                    object parameters = dbFactory.ProviderName == "mysql"
                        ? new
                        {
                            p_GroupNames = groupNamesCsv,
                            p_ItemName = targetId,
                            p_RequestMethod = requestMethod
                        }
                        : new
                        {
                            GroupNames = groupNamesCsv,
                            ItemName = targetId,
                            RequestMethod = requestMethod
                        };
                    int isAllowed = await conn.ExecuteScalarAsync<int>(
                        "sp_EvaluateUserAccess",
                        parameters,
                        commandType: System.Data.CommandType.StoredProcedure
                    );
                    return isAllowed == 1;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user authorization for target '{TargetId}'", targetId);
                return false; // Fail closed fallback
            }
        }

        private async Task<List<string>> GetServerCategoriesAsync(string serverId, HttpContext? httpContext)
        {
            if (string.IsNullOrWhiteSpace(serverId))
            {
                return new List<string>();
            }

            IServiceProvider? services;
            try
            {
                services = httpContext?.RequestServices ?? _clientResponse?.HttpContext?.RequestServices ?? _rootServices;
            }
            catch (ObjectDisposedException)
            {
                services = _rootServices;
            }
            var dbFactory = services?.GetService<IDbConnectionFactory>();

            if (dbFactory != null)
            {
                try
                {
                    using var conn = dbFactory.CreateConnection();
                    var rawCat = await conn.ExecuteScalarAsync<string>("SELECT Categories FROM Servers WHERE Id = @Id", new { Id = serverId });
                    if (!string.IsNullOrEmpty(rawCat))
                    {
                        try
                        {
                            var categories = JsonSerializer.Deserialize<List<string>>(rawCat);
                            if (categories != null && categories.Count > 0)
                            {
                                return categories;
                            }
                        }
                        catch
                        {
                            var parts = rawCat.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                            if (parts.Count > 0)
                            {
                                return parts;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to query server categories from DB for server '{ServerId}'", serverId);
                }
            }

            // Fallback to local session servers list
            var server = _servers.FirstOrDefault(s => string.Equals(s.Id, serverId, StringComparison.OrdinalIgnoreCase));
            if (server?.Categories != null && server.Categories.Count > 0)
            {
                return server.Categories;
            }

            return new List<string>();
        }

        private async Task<List<object>> FilterAuthorizedAsync(List<object> items, string method, string idProp, HttpContext? httpContext)
        {
            var allowed = new List<object>(items.Count);
            foreach (var item in items)
            {
                string? id = null;
                try
                {
                    using var doc = JsonDocument.Parse(JsonSerializer.Serialize(item));
                    if (doc.RootElement.TryGetProperty(idProp, out var p))
                    {
                        id = p.GetString();
                    }
                }
                catch { /* fall through to fail-closed exclude */ }
                if (string.IsNullOrEmpty(id))
                {
                    continue;                       // can't identify → exclude
                }

                if (await IsUserAuthorizedAsync(method, id, httpContext))
                {
                    allowed.Add(item);
                }
            }
            return allowed;
        }

        private async Task AuditInvocationAsync(
            string requestMethod,
            string itemName,
            string? payload,
            int statusCode,
            long executionTimeMs,
            string? responsePayload,
            string? errorMessage,
            HttpContext? httpContext = null)
        {
            IServiceProvider? services;
            try
            {
                services = httpContext?.RequestServices ?? _clientResponse?.HttpContext?.RequestServices ?? _rootServices;
            }
            catch (ObjectDisposedException)
            {
                services = _rootServices;
            }
            var config = services?.GetService<IConfiguration>();
            var failClosedRaw = config?["Audit:FailClosed"];
            bool failClosed = !bool.TryParse(failClosedRaw, out var parsedFailClosed) || parsedFailClosed;

            var auditLogger = services?.GetService<IAuditLogger>();
            if (auditLogger == null)
            {
                if (failClosed)
                {
                    throw new System.Security.SecurityException("Audit logger service unavailable and fail-closed policy is active.");
                }
                return;
            }

            try
            {
                var identity = await ResolveUserIdentityAsync(httpContext);

                var effectiveItemName = itemName;
                if (itemName == "execute_tool" && !string.IsNullOrEmpty(payload))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(payload);
                        if (doc.RootElement.TryGetProperty("params", out var pProp) &&
                            pProp.TryGetProperty("arguments", out var aProp) &&
                            aProp.TryGetProperty("name", out var nProp))
                        {
                            var target = nProp.GetString();
                            if (!string.IsNullOrEmpty(target))
                            {
                                effectiveItemName = target;
                            }
                        }
                    }
                    catch { }
                }

                string serverId;
                if (effectiveItemName.StartsWith("mcp://", StringComparison.OrdinalIgnoreCase))
                {
                    if (Uri.TryCreate(effectiveItemName, UriKind.Absolute, out var parsedUri))
                    {
                        serverId = parsedUri.Host;
                    }
                    else
                    {
                        serverId = effectiveItemName.Substring("mcp://".Length).Split('/')[0];
                    }
                }
                else if (effectiveItemName.StartsWith("logs://", StringComparison.OrdinalIgnoreCase))
                {
                    if (Uri.TryCreate(effectiveItemName, UriKind.Absolute, out var parsedUri))
                    {
                        serverId = parsedUri.Host;
                    }
                    else
                    {
                        serverId = effectiveItemName.Substring("logs://".Length).Split('/')[0];
                    }
                }
                else if (effectiveItemName.StartsWith("router://", StringComparison.OrdinalIgnoreCase))
                {
                    serverId = "router";
                }
                else if (effectiveItemName.Contains("__"))
                {
                    serverId = effectiveItemName.Split("__", 2)[0];
                }
                else
                {
                    serverId = effectiveItemName;
                }

                // Try to extract requestId from request payload
                string? requestId = null;
                if (!string.IsNullOrEmpty(payload))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(payload);
                        if (doc.RootElement.TryGetProperty("id", out var idProp))
                        {
                            var extractedId = idProp.ValueKind == JsonValueKind.String ? idProp.GetString() : idProp.GetRawText();
                            if (!string.IsNullOrEmpty(extractedId))
                            {
                                requestId = $"{extractedId}_{Guid.NewGuid().ToString("N")[..6]}";
                            }
                        }
                    }
                    catch (JsonException exJson)
                    {
                        _logger.LogDebug(exJson, "Could not parse payload JSON to extract requestId in audit log.");
                    }
                }
                requestId ??= Guid.NewGuid().ToString("N");

                await auditLogger.LogInvocationAsync(
                    requestId,
                    identity.Username,
                    identity.AllSids.Count > 0 ? string.Join(";", identity.AllSids) : "",
                    serverId,
                    effectiveItemName,
                    requestMethod,
                    (int)executionTimeMs,
                    statusCode,
                    payload,
                    responsePayload,
                    errorMessage
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write invocation audit log");
                if (failClosed)
                {
                    throw new System.Security.SecurityException("Audit logging failed and fail-closed security policy is active.", ex);
                }
            }
        }
    }
}
