using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;

namespace ModelContextGateway.Core.Routing
{
    /// <summary>
    /// In-process virtual Admin MCP Server providing 10 consolidated entity tools
    /// covering 100% of the router gateway administration and diagnostics flows.
    /// </summary>
    public class AdminMcpServer
    {
        private readonly IServerRepository _serverRepository;
        private readonly IAppKeyRepository _appKeyRepository;
        private readonly ISecretProviderRepository _secretProviderRepository;
        private readonly IAuthProviderRepository _authProviderRepository;
        private readonly ISettingRepository _settingRepository;
        private readonly IDbConnectionFactory _dbFactory;
        private readonly IAuditLogger _auditLogger;
        private readonly ICredentialService _credentialService;
        private readonly BackendHealthCheckService _healthCheckService;
        private readonly DynamicEmbeddingService _dynamicEmbeddingService;
        private readonly SessionManager _sessionManager;
        private readonly ILdapService? _ldapService;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration? _configuration;
        private readonly ILogger<AdminMcpServer>? _logger;
        private readonly IMasterKeyManager? _masterKeyManager;
        private readonly ISecretRetriever? _secretRetriever;

        private const string DefaultProtocolVersion = "2026-07-28";
        private const string LegacyProtocolVersion = "2024-11-05";

        public AdminMcpServer(
            IServerRepository serverRepository,
            IAppKeyRepository appKeyRepository,
            ISecretProviderRepository secretProviderRepository,
            IAuthProviderRepository authProviderRepository,
            ISettingRepository settingRepository,
            IDbConnectionFactory dbFactory,
            IAuditLogger auditLogger,
            ICredentialService credentialService,
            BackendHealthCheckService healthCheckService,
            DynamicEmbeddingService dynamicEmbeddingService,
            SessionManager sessionManager,
            ILdapService? ldapService = null,
            HttpClient? httpClient = null,
            IConfiguration? configuration = null,
            ILogger<AdminMcpServer>? logger = null,
            IMasterKeyManager? masterKeyManager = null,
            ISecretRetriever? secretRetriever = null)
        {
            _serverRepository = serverRepository;
            _appKeyRepository = appKeyRepository;
            _secretProviderRepository = secretProviderRepository;
            _authProviderRepository = authProviderRepository;
            _settingRepository = settingRepository;
            _dbFactory = dbFactory;
            _auditLogger = auditLogger;
            _credentialService = credentialService;
            _healthCheckService = healthCheckService;
            _dynamicEmbeddingService = dynamicEmbeddingService;
            _sessionManager = sessionManager;
            _ldapService = ldapService;
            _httpClient = httpClient ?? new HttpClient();
            _configuration = configuration;
            _logger = logger;
            _masterKeyManager = masterKeyManager;
            _secretRetriever = secretRetriever;
        }

        /// <summary>
        /// Handles the MCP initialize handshake request and protocol version negotiation.
        /// </summary>
        public Task<object> HandleInitializeAsync(JsonElement? paramsElement)
        {
            string negotiatedVersion = DefaultProtocolVersion;

            if (paramsElement.HasValue && paramsElement.Value.ValueKind == JsonValueKind.Object)
            {
                if (paramsElement.Value.TryGetProperty("protocolVersion", out var versionProp))
                {
                    var requestedVersion = versionProp.GetString();
                    if (!string.IsNullOrEmpty(requestedVersion) && requestedVersion.StartsWith("2024", StringComparison.OrdinalIgnoreCase))
                    {
                        negotiatedVersion = LegacyProtocolVersion;
                    }
                }
            }

            var result = (object)new
            {
                protocolVersion = negotiatedVersion,
                capabilities = new
                {
                    tools = new { listChanged = false },
                    subscriptions = new { }
                },
                serverInfo = new
                {
                    name = GatewayMetadata.AdminServerName,
                    version = GatewayMetadata.Version
                },
                instructions = "In-process virtual Admin MCP Server for managing the Model Context Gateway configuration, servers, clients, policies, providers, settings, and diagnostics."
            };

            return Task.FromResult(result);
        }

        /// <summary>
        /// Returns the 10 consolidated admin tool definitions with complete JSON schemas.
        /// </summary>
        public Task<List<object>> ListToolsAsync()
        {
            return Task.FromResult(GetToolDefinitions());
        }

        /// <summary>
        /// Dispatches and executes an admin tool action with comprehensive error handling and audit logging.
        /// </summary>
        public async Task<object> CallToolAsync(string toolName, JsonElement arguments, string callerUsername = "admin")
        {
            var actionName = "unknown";
            string? targetIdentifier = null;
            string? argumentPayload = null;

            try
            {
                if (arguments.ValueKind == JsonValueKind.Object)
                {
                    var rawPayload = arguments.GetRawText();
                    argumentPayload = PiiSanitizer.SanitizePayload(ProviderConfigSecurityHelper.RedactConfigJson(rawPayload) ?? rawPayload);
                    if (arguments.TryGetProperty("action", out var actionProp))
                    {
                        actionName = actionProp.GetString() ?? "unknown";
                    }
                    if (arguments.TryGetProperty("id", out var idProp))
                    {
                        targetIdentifier = idProp.GetString();
                    }
                    else if (arguments.TryGetProperty("name", out var nameProp))
                    {
                        targetIdentifier = nameProp.GetString();
                    }
                    else if (arguments.TryGetProperty("serverId", out var srvProp))
                    {
                        targetIdentifier = srvProp.GetString();
                    }
                    else if (arguments.TryGetProperty("providerName", out var provProp))
                    {
                        targetIdentifier = provProp.GetString();
                    }
                }

                object resultData = toolName switch
                {
                    "manage_servers" => await HandleManageServersAsync(arguments, callerUsername),
                    "manage_appkeys" => await HandleManageAppKeysAsync(arguments, callerUsername),
                    "manage_clients" => await HandleManageClientsAsync(arguments, callerUsername),
                    "manage_policies" => await HandleManagePoliciesAsync(arguments, callerUsername),
                    "manage_group_mappings" => await HandleManageGroupMappingsAsync(arguments, callerUsername),
                    "manage_providers" => await HandleManageProvidersAsync(arguments, callerUsername),
                    "manage_settings" => await HandleManageSettingsAsync(arguments, callerUsername),
                    "manage_custom_files" => await HandleManageCustomFilesAsync(arguments, callerUsername),
                    "manage_system" => await HandleManageSystemAsync(arguments, callerUsername),
                    "test_tool_call" => await HandleTestToolCallAsync(arguments, callerUsername),
                    _ => throw new ArgumentException($"Unknown tool: '{toolName}'")
                };

                var formattedSuccess = FormatSuccessResponse(resultData);

                _ = _auditLogger.LogAdminActionAsync(
                    callerUsername,
                    $"mcp.{toolName}.{actionName}",
                    targetIdentifier ?? toolName,
                    argumentPayload ?? "",
                    true
                );

                return formattedSuccess;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing Admin MCP tool {ToolName} (action: {ActionName})", toolName, actionName);

                _ = _auditLogger.LogAdminActionAsync(
                    callerUsername,
                    $"mcp.{toolName}.{actionName}",
                    targetIdentifier ?? toolName,
                    argumentPayload ?? "",
                    false,
                    ex.Message
                );

                return FormatErrorResponse(ex.Message);
            }
        }

        /// <summary>
        /// Processes a generic JSON-RPC request directed at the Admin MCP Server.
        /// </summary>
        public async Task<JsonRpcResponse> ProcessRequestAsync(JsonRpcRequest request, string callerUsername = "admin")
        {
            var response = new JsonRpcResponse
            {
                Id = request.Id
            };

            try
            {
                switch (request.Method)
                {
                    case "initialize":
                        var initResult = await HandleInitializeAsync(request.Params);
                        response.Result = JsonSerializer.SerializeToElement(ProtocolHelper.EnsureResultType(initResult));
                        break;

                    case "server/discover":
                        response.Result = JsonSerializer.SerializeToElement(ProtocolHelper.EnsureResultType(new
                        {
                            supportedVersions = GatewayMetadata.SupportedProtocolVersions,
                            capabilities = new
                            {
                                tools = new { listChanged = false },
                                subscriptions = new { }
                            },
                            serverInfo = new
                            {
                                name = GatewayMetadata.AdminServerName,
                                version = GatewayMetadata.Version
                            },
                            instructions = "In-process virtual Admin MCP Server for managing the Model Context Gateway configuration, servers, clients, policies, providers, settings, and diagnostics."
                        }));
                        break;

                    case "subscriptions/listen":
                        response.Result = JsonSerializer.SerializeToElement(ProtocolHelper.EnsureResultType(new
                        {
                            status = "listening",
                            subscriptions = new { }
                        }));
                        break;

                    case "notifications/initialized":
                        response.Result = JsonSerializer.SerializeToElement(ProtocolHelper.EnsureResultType(new { }));
                        break;

                    case "ping":
                        response.Result = JsonSerializer.SerializeToElement(ProtocolHelper.EnsureResultType(new { }));
                        break;

                    case "tools/list":
                        var tools = await ListToolsAsync();
                        response.Result = JsonSerializer.SerializeToElement(ProtocolHelper.EnsureResultType(new { tools }));
                        break;

                    case "tools/call":
                        if (request.Params.HasValue && request.Params.Value.ValueKind == JsonValueKind.Object)
                        {
                            var paramsObj = request.Params.Value;
                            var toolName = paramsObj.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : string.Empty;
                            var arguments = paramsObj.TryGetProperty("arguments", out var argsProp) ? argsProp : JsonDocument.Parse("{}").RootElement;

                            if (string.IsNullOrEmpty(toolName))
                            {
                                response.Error = new JsonRpcError { Code = -32602, Message = "Missing 'name' in tools/call parameters" };
                            }
                            else
                            {
                                var toolResult = await CallToolAsync(toolName, arguments, callerUsername);
                                response.Result = JsonSerializer.SerializeToElement(ProtocolHelper.EnsureResultType(toolResult));
                            }
                        }
                        else
                        {
                            response.Error = new JsonRpcError { Code = -32602, Message = "Invalid parameters for tools/call" };
                        }
                        break;

                    default:
                        response.Error = new JsonRpcError { Code = -32601, Message = $"Method '{request.Method}' not found" };
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "An unexpected error occurred.");
                response.Error = new JsonRpcError { Code = -32603, Message = ex.Message };
            }

            return response;
        }

        #region Tool Implementations

        // 1. manage_servers
        private async Task<object> HandleManageServersAsync(JsonElement args, string callerUsername)
        {
            var action = GetRequiredStringProperty(args, "action");

            switch (action.ToLowerInvariant())
            {
                case "list":
                    {
                        var rawServers = (await _serverRepository.GetServersAsync()).ToList();
                        var statuses = _sessionManager.BackendStatuses;

                        return rawServers.Select(s =>
                        {
                            var idStr = s.Id ?? string.Empty;
                            statuses.TryGetValue(idStr, out var status);

                            return new
                            {
                                s.Id,
                                s.DisplayName,
                                s.Url,
                                s.Enabled,
                                s.Hidden,
                                Type = s.Type ?? "sse",
                                Categories = s.Categories ?? new List<string>(),
                                SecretProvider = s.SecretProvider ?? "None",
                                s.SecretItemKey,
                                AuthShape = s.AuthShape ?? "bearer",
                                s.CustomHeaderName,
                                HasApiKey = !string.IsNullOrEmpty(s.ApiKey),
                                ConnectionStatus = s.Enabled ? (status?.Status ?? "Disconnected") : "Disabled",
                                ConnectionAttempts = status?.Attempts ?? 0,
                                ConnectionError = status?.Error ?? string.Empty
                            };
                        }).ToList();
                    }

                case "get":
                    {
                        var id = GetRequiredStringProperty(args, "id");
                        var server = await _serverRepository.GetServerByIdAsync(id);
                        if (server == null)
                        {
                            throw new KeyNotFoundException($"Server '{id}' not found.");
                        }

                        _sessionManager.BackendStatuses.TryGetValue(id, out var status);
                        return new
                        {
                            server.Id,
                            server.DisplayName,
                            server.Url,
                            server.Enabled,
                            server.Hidden,
                            Type = server.Type ?? "sse",
                            Categories = server.Categories ?? new List<string>(),
                            SecretProvider = server.SecretProvider ?? "None",
                            server.SecretItemKey,
                            AuthShape = server.AuthShape ?? "bearer",
                            server.CustomHeaderName,
                            HasApiKey = !string.IsNullOrEmpty(server.ApiKey),
                            ConnectionStatus = server.Enabled ? (status?.Status ?? "Disconnected") : "Disabled",
                            ConnectionAttempts = status?.Attempts ?? 0,
                            ConnectionError = status?.Error ?? string.Empty
                        };
                    }

                case "create":
                    {
                        var server = ParseServerFromArgs(args);
                        if (string.IsNullOrWhiteSpace(server.Id))
                        {
                            server.Id = Guid.NewGuid().ToString("N")[..8];
                        }

                        ValidateServerConfig(server);

                        await _serverRepository.SaveServerAsync(server);
                        _sessionManager.ResetAll();

                        return new { success = true, server };
                    }

                case "update":
                    {
                        var id = GetRequiredStringProperty(args, "id");
                        var existing = await _serverRepository.GetServerByIdAsync(id);
                        if (existing == null)
                        {
                            throw new KeyNotFoundException($"Server '{id}' not found.");
                        }

                        UpdateServerFromArgs(existing, args);
                        ValidateServerConfig(existing);

                        await _serverRepository.SaveServerAsync(existing);
                        _sessionManager.RemoveServerCache(id);
                        _sessionManager.ResetAll();

                        return new { success = true, server = existing };
                    }

                case "delete":
                    {
                        var id = GetRequiredStringProperty(args, "id");
                        var existing = await _serverRepository.GetServerByIdAsync(id);
                        if (existing == null)
                        {
                            throw new KeyNotFoundException($"Server '{id}' not found.");
                        }

                        await _serverRepository.DeleteServerAsync(id);
                        _sessionManager.RemoveServerCache(id);
                        _sessionManager.ResetAll();

                        return new { success = true, id };
                    }

                case "toggle":
                    {
                        var id = GetRequiredStringProperty(args, "id");
                        var existing = await _serverRepository.GetServerByIdAsync(id);
                        if (existing == null)
                        {
                            throw new KeyNotFoundException($"Server '{id}' not found.");
                        }

                        if (args.TryGetProperty("enabled", out var enabledProp) && (enabledProp.ValueKind == JsonValueKind.True || enabledProp.ValueKind == JsonValueKind.False))
                        {
                            existing.Enabled = enabledProp.GetBoolean();
                        }
                        else
                        {
                            existing.Enabled = !existing.Enabled;
                        }

                        await _serverRepository.SaveServerAsync(existing);
                        _sessionManager.RemoveServerCache(id);
                        _sessionManager.ResetAll();

                        return new { success = true, id, enabled = existing.Enabled };
                    }

                case "reconnect":
                    {
                        var id = GetRequiredStringProperty(args, "id");
                        var existing = await _serverRepository.GetServerByIdAsync(id);
                        if (existing == null)
                        {
                            throw new KeyNotFoundException($"Server '{id}' not found.");
                        }

                        await _healthCheckService.ProbeServerAsync(existing);

                        var activeSessions = _sessionManager.GetActiveSessions();
                        foreach (var session in activeSessions)
                        {
                            session.StartInitializationForBackend(id);
                        }

                        return new { success = true, message = $"Reconnection triggered for server {existing.DisplayName}" };
                    }

                case "reconnect_all":
                    {
                        await _healthCheckService.ProbeAllServersAsync();

                        var allServers = (await _serverRepository.GetServersAsync())
                            .Where(s => s.Enabled && s.Type != "custom");
                        var activeSessions = _sessionManager.GetActiveSessions();
                        foreach (var srv in allServers)
                        {
                            foreach (var session in activeSessions)
                            {
                                session.StartInitializationForBackend(srv.Id);
                            }
                        }

                        return new { success = true, message = "Reconnection triggered for all servers." };
                    }

                default:
                    throw new ArgumentException($"Invalid action '{action}' for manage_servers.");
            }
        }

        // 2. manage_appkeys
        private async Task<object> HandleManageAppKeysAsync(JsonElement args, string callerUsername)
        {
            var action = GetRequiredStringProperty(args, "action");

            switch (action.ToLowerInvariant())
            {
                case "list":
                    {
                        string? usernameFilter = args.TryGetProperty("username", out var uProp) ? uProp.GetString() : null;
                        var keys = await _appKeyRepository.GetAppKeysAsync(usernameFilter, isAdmin: true, currentUser: callerUsername);

                        return keys.Select(k => new
                        {
                            k.Id,
                            k.Name,
                            k.Username,
                            k.KeyPrefix,
                            Scopes = DeserializeScopes(k.ScopesJson),
                            k.ExpiresAt,
                            k.CreatedAt
                        }).ToList();
                    }

                case "get_limits":
                    {
                        var targetUser = args.TryGetProperty("username", out var uProp) ? (uProp.GetString() ?? callerUsername) : callerUsername;
                        var settings = await _settingRepository.GetSettingsAsync();

                        int globalMax = settings?.GlobalMaxKeys ?? 0;
                        int userMax = settings?.UserMaxKeys ?? 0;
                        int totalActiveKeys = await _appKeyRepository.GetTotalActiveKeysAsync();
                        int userActiveKeys = await _appKeyRepository.GetUserActiveKeysAsync(targetUser);

                        return new
                        {
                            globalMax,
                            userMax,
                            totalActiveKeys,
                            userActiveKeys
                        };
                    }

                case "create":
                    {
                        var name = GetRequiredStringProperty(args, "name");
                        var targetUser = args.TryGetProperty("username", out var uProp) && !string.IsNullOrWhiteSpace(uProp.GetString())
                            ? uProp.GetString()!
                            : callerUsername;

                        var ownerSid = "";
                        if (!targetUser.Equals(callerUsername, StringComparison.OrdinalIgnoreCase) && _ldapService != null)
                        {
                            try
                            {
                                var targetSids = await _ldapService.ResolveUserSidsAsync(targetUser);
                                ownerSid = targetSids.FirstOrDefault() ?? "";
                            }
                            catch { }
                        }

                        var scopes = new List<string> { "all" };
                        if (args.TryGetProperty("scopes", out var scopesProp) && scopesProp.ValueKind == JsonValueKind.Array)
                        {
                            scopes = scopesProp.EnumerateArray().Select(s => s.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).ToList();
                            if (scopes.Count == 0)
                            {
                                scopes.Add("all");
                            }
                        }

                        int? expiresInDays = args.TryGetProperty("expiresInDays", out var expProp) && expProp.TryGetInt32(out var d) ? d : null;

                        var (appKey, plaintextKey) = await _credentialService.CreateCredentialAsync(
                            name,
                            targetUser,
                            ownerSid,
                            scopes,
                            expiresInDays
                        );

                        return new
                        {
                            appKey.Id,
                            appKey.Name,
                            appKey.Username,
                            appKey.KeyPrefix,
                            PlaintextKey = plaintextKey,
                            Scopes = scopes,
                            appKey.ExpiresAt,
                            appKey.CreatedAt
                        };
                    }

                case "revoke":
                    {
                        var id = GetRequiredStringProperty(args, "id");
                        var appKey = await _appKeyRepository.GetAppKeyByIdAsync(id);
                        if (appKey == null)
                        {
                            throw new KeyNotFoundException($"AppKey '{id}' not found.");
                        }

                        await _credentialService.RevokeCredentialAsync(id);
                        return new { success = true, id };
                    }

                default:
                    throw new ArgumentException($"Invalid action '{action}' for manage_appkeys.");
            }
        }

        // 3. manage_clients
        private async Task<object> HandleManageClientsAsync(JsonElement args, string callerUsername)
        {
            var action = GetRequiredStringProperty(args, "action");

            switch (action.ToLowerInvariant())
            {
                case "list":
                    {
                        using var conn = _dbFactory.CreateConnection();
                        var keys = await conn.QueryAsync<dynamic>("SELECT * FROM AppKeys");

                        return keys.Select(k =>
                        {
                            var scopesJson = Convert.ToString(k.ScopesJson) ?? "[]";
                            List<string> scopes;
                            try { scopes = JsonSerializer.Deserialize<List<string>>(scopesJson) ?? new List<string>(); }
                            catch { scopes = new List<string>(); }

                            return new
                            {
                                Id = Convert.ToString(k.Id),
                                ClientId = Convert.ToString(k.Username) ?? Convert.ToString(k.KeyPrefix),
                                DisplayName = Convert.ToString(k.Name) ?? "App Key",
                                Scopes = scopes,
                                ExpiresAt = k.ExpiresAt != null ? (DateTime?)Convert.ToDateTime(k.ExpiresAt) : null,
                                CreatedAt = k.CreatedAt != null ? (DateTime?)Convert.ToDateTime(k.CreatedAt) : null,
                                IsDynamic = false
                            };
                        }).ToList();
                    }

                case "register":
                    {
                        var displayName = GetRequiredStringProperty(args, "displayName");
                        var clientId = Guid.NewGuid().ToString("N");

                        var scopes = new List<string>();
                        if (args.TryGetProperty("scopes", out var scopesProp) && scopesProp.ValueKind == JsonValueKind.Array)
                        {
                            scopes = scopesProp.EnumerateArray().Select(s => s.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).ToList();
                        }

                        int? expiresInDays = args.TryGetProperty("expiresInDays", out var expProp) && expProp.TryGetInt32(out var d) ? d : null;

                        var (appKey, plaintextKey) = await _credentialService.CreateCredentialAsync(
                            displayName,
                            clientId,
                            string.Empty,
                            scopes,
                            expiresInDays
                        );

                        return new
                        {
                            Id = appKey.Id,
                            ClientId = clientId,
                            ClientSecret = plaintextKey,
                            DisplayName = displayName,
                            ExpiresAt = appKey.ExpiresAt
                        };
                    }

                case "delete":
                    {
                        var id = GetRequiredStringProperty(args, "id");
                        var success = await _credentialService.RevokeCredentialAsync(id);
                        if (!success)
                        {
                            using var conn = _dbFactory.CreateConnection();
                            var appKey = await conn.QueryFirstOrDefaultAsync<AppKey>("SELECT * FROM AppKeys WHERE Username = @Id OR Id = @Id;", new { Id = id });
                            if (appKey != null)
                            {
                                success = await _credentialService.RevokeCredentialAsync(appKey.Id);
                            }
                        }
                        if (!success)
                        {
                            throw new KeyNotFoundException($"Client '{id}' not found.");
                        }

                        return new { success = true, id };
                    }

                default:
                    throw new ArgumentException($"Invalid action '{action}' for manage_clients.");
            }
        }

        // 4. manage_policies
        private async Task<object> HandleManagePoliciesAsync(JsonElement args, string callerUsername)
        {
            var action = GetRequiredStringProperty(args, "action");

            switch (action.ToLowerInvariant())
            {
                case "list":
                    {
                        using var conn = _dbFactory.CreateConnection();
                        const string sql = "SELECT * FROM AccessPolicies;";
                        var policies = (await conn.QueryAsync<McpAccessPolicy>(sql)).ToList();
                        return policies;
                    }

                case "save":
                    {
                        var targetId = GetRequiredStringProperty(args, "targetId");
                        var requiredGroup = GetRequiredStringProperty(args, "requiredGroup");
                        bool isAllowed = !args.TryGetProperty("isAllowed", out var allowProp) || allowProp.GetBoolean();

                        if (targetId == "*" && !isAllowed)
                        {
                            throw new InvalidOperationException("Cannot save a wildcard deny policy as it will cause a global lockout.");
                        }

                        var id = args.TryGetProperty("id", out var idProp) && !string.IsNullOrWhiteSpace(idProp.GetString())
                            ? idProp.GetString()!
                            : Guid.NewGuid().ToString("N");

                        var policy = new McpAccessPolicy
                        {
                            Id = id,
                            TargetId = targetId,
                            RequiredGroup = requiredGroup,
                            IsAllowed = isAllowed
                        };

                        using var conn = _dbFactory.CreateConnection();
                        if (_dbFactory.ProviderName.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
                        {
                            const string sql = @"
                            INSERT INTO AccessPolicies (Id, TargetId, RequiredGroup, IsAllowed)
                            VALUES (@Id, @TargetId, @RequiredGroup, @IsAllowed)
                            ON CONFLICT(Id) DO UPDATE SET TargetId = @TargetId, RequiredGroup = @RequiredGroup, IsAllowed = @IsAllowed;";
                            await conn.ExecuteAsync(sql, policy);
                        }
                        else if (_dbFactory.ProviderName.Equals("mysql", StringComparison.OrdinalIgnoreCase))
                        {
                            const string mysqlSql = @"
                            INSERT INTO AccessPolicies (Id, TargetId, RequiredGroup, IsAllowed)
                            VALUES (@Id, @TargetId, @RequiredGroup, @IsAllowed)
                            ON DUPLICATE KEY UPDATE TargetId = VALUES(TargetId), RequiredGroup = VALUES(RequiredGroup), IsAllowed = VALUES(IsAllowed);";
                            await conn.ExecuteAsync(mysqlSql, policy);
                        }
                        else
                        {
                            const string mssqlSql = @"
                            MERGE AccessPolicies AS target
                            USING (SELECT @Id AS Id) AS source
                            ON (target.Id = source.Id)
                            WHEN MATCHED THEN
                                UPDATE SET TargetId = @TargetId, RequiredGroup = @RequiredGroup, IsAllowed = @IsAllowed
                            WHEN NOT MATCHED THEN
                                INSERT (Id, TargetId, RequiredGroup, IsAllowed)
                                VALUES (@Id, @TargetId, @RequiredGroup, @IsAllowed);";
                            await conn.ExecuteAsync(mssqlSql, policy);
                        }

                        return new { success = true, policy };
                    }

                case "delete":
                    {
                        var id = GetRequiredStringProperty(args, "id");
                        using var conn = _dbFactory.CreateConnection();
                        await conn.ExecuteAsync("DELETE FROM AccessPolicies WHERE Id = @Id;", new { Id = id });
                        return new { success = true, id };
                    }

                default:
                    throw new ArgumentException($"Invalid action '{action}' for manage_policies.");
            }
        }

        // 5. manage_group_mappings
        private async Task<object> HandleManageGroupMappingsAsync(JsonElement args, string callerUsername)
        {
            var action = GetRequiredStringProperty(args, "action");

            switch (action.ToLowerInvariant())
            {
                case "list":
                    {
                        using var conn = _dbFactory.CreateConnection();
                        const string sql = "SELECT * FROM GroupMappings;";
                        var mappings = (await conn.QueryAsync<GroupMapping>(sql)).ToList();
                        return mappings;
                    }

                case "save":
                    {
                        var externalId = GetRequiredStringProperty(args, "externalId");
                        var internalGroup = GetRequiredStringProperty(args, "internalGroup");
                        var id = args.TryGetProperty("id", out var idProp) && !string.IsNullOrWhiteSpace(idProp.GetString())
                            ? idProp.GetString()!
                            : Guid.NewGuid().ToString("N");

                        var mapping = new GroupMapping
                        {
                            Id = id,
                            ExternalId = externalId,
                            InternalGroup = internalGroup
                        };

                        using var conn = _dbFactory.CreateConnection();
                        if (_dbFactory.ProviderName.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
                        {
                            const string sql = @"
                            INSERT INTO GroupMappings (Id, ExternalId, InternalGroup)
                            VALUES (@Id, @ExternalId, @InternalGroup)
                            ON CONFLICT(Id) DO UPDATE SET ExternalId = @ExternalId, InternalGroup = @InternalGroup;";
                            await conn.ExecuteAsync(sql, mapping);
                        }
                        else if (_dbFactory.ProviderName.Equals("mysql", StringComparison.OrdinalIgnoreCase))
                        {
                            const string mysqlSql = @"
                            INSERT INTO GroupMappings (Id, ExternalId, InternalGroup)
                            VALUES (@Id, @ExternalId, @InternalGroup)
                            ON DUPLICATE KEY UPDATE ExternalId = VALUES(ExternalId), InternalGroup = VALUES(InternalGroup);";
                            await conn.ExecuteAsync(mysqlSql, mapping);
                        }
                        else
                        {
                            const string mssqlSql = @"
                            MERGE GroupMappings AS target
                            USING (SELECT @Id AS Id) AS source
                            ON (target.Id = source.Id)
                            WHEN MATCHED THEN
                                UPDATE SET ExternalId = @ExternalId, InternalGroup = @InternalGroup
                            WHEN NOT MATCHED THEN
                                INSERT (Id, ExternalId, InternalGroup)
                                VALUES (@Id, @ExternalId, @InternalGroup);";
                            await conn.ExecuteAsync(mssqlSql, mapping);
                        }

                        return new { success = true, mapping };
                    }

                case "delete":
                    {
                        var id = GetRequiredStringProperty(args, "id");
                        using var conn = _dbFactory.CreateConnection();
                        await conn.ExecuteAsync("DELETE FROM GroupMappings WHERE Id = @Id;", new { Id = id });
                        return new { success = true, id };
                    }

                default:
                    throw new ArgumentException($"Invalid action '{action}' for manage_group_mappings.");
            }
        }

        // 6. manage_providers
        private async Task<object> HandleManageProvidersAsync(JsonElement args, string callerUsername)
        {
            var action = GetRequiredStringProperty(args, "action");

            switch (action.ToLowerInvariant())
            {
                case "list":
                    {
                        var type = args.TryGetProperty("type", out var tProp) ? tProp.GetString() : "all";

                        var secretProviders = (await _secretProviderRepository.GetSecretProvidersAsync()).ToList();
                        foreach (var p in secretProviders)
                        {
                            p.ConfigJson = ProviderConfigSecurityHelper.RedactConfigJson(p.ConfigJson);
                        }

                        var authProviders = (await _authProviderRepository.GetAuthProvidersAsync()).ToList();
                        foreach (var p in authProviders)
                        {
                            p.ConfigJson = ProviderConfigSecurityHelper.RedactConfigJson(p.ConfigJson);
                        }

                        if (type?.Equals("secrets", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            return secretProviders;
                        }
                        if (type?.Equals("auth", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            return authProviders;
                        }

                        return new
                        {
                            secretProviders,
                            authProviders
                        };
                    }

                case "save_secret":
                    {
                        var providerName = GetRequiredStringProperty(args, "providerName");
                        var displayName = args.TryGetProperty("displayName", out var dProp) ? dProp.GetString() ?? providerName : providerName;
                        var configJson = args.TryGetProperty("configJson", out var cProp) ? cProp.GetString() ?? "{}" : "{}";
                        bool isEnabled = !args.TryGetProperty("isEnabled", out var eProp) || eProp.GetBoolean();

                        var dto = new SecretProviderDto
                        {
                            ProviderName = providerName,
                            DisplayName = displayName,
                            ConfigJson = configJson,
                            IsEnabled = isEnabled
                        };

                        ProviderConfigSecurityHelper.ValidateSecretProviderConfig(dto);

                        var existingProviders = await _secretProviderRepository.GetSecretProvidersAsync();
                        var existing = existingProviders?.FirstOrDefault(p =>
                            string.Equals(p.ProviderName, dto.ProviderName, StringComparison.OrdinalIgnoreCase));
                        if (existing != null && !string.IsNullOrEmpty(existing.ConfigJson))
                        {
                            dto.ConfigJson = ProviderConfigSecurityHelper.MergeWithExistingConfig(dto.ConfigJson, existing.ConfigJson);
                        }

                        await _secretProviderRepository.SaveSecretProviderAsync(dto);
                        return new { success = true, providerName = dto.ProviderName };
                    }

                case "test_vault":
                    {
                        var address = GetRequiredStringProperty(args, "address");
                        var authMethodName = args.TryGetProperty("authMethod", out var amProp) ? amProp.GetString() : "token";
                        var token = args.TryGetProperty("token", out var tokProp) ? tokProp.GetString() : null;
                        var roleId = args.TryGetProperty("roleId", out var rProp) ? rProp.GetString() : null;
                        var secretId = args.TryGetProperty("secretId", out var sProp) ? sProp.GetString() : null;

                        try
                        {
                            VaultSharp.V1.AuthMethods.IAuthMethodInfo authMethod;
                            if (string.Equals(authMethodName, "approle", StringComparison.OrdinalIgnoreCase) ||
                                (!string.IsNullOrEmpty(roleId) && !string.IsNullOrEmpty(secretId)))
                            {
                                if (string.IsNullOrEmpty(roleId) || string.IsNullOrEmpty(secretId))
                                {
                                    throw new ArgumentException("Vault AppRole requires both RoleId and SecretId.");
                                }
                                authMethod = new VaultSharp.V1.AuthMethods.AppRole.AppRoleAuthMethodInfo(roleId, secretId);
                            }
                            else
                            {
                                if (string.IsNullOrEmpty(token))
                                {
                                    throw new ArgumentException("Vault Token is required for token authentication.");
                                }
                                authMethod = new VaultSharp.V1.AuthMethods.Token.TokenAuthMethodInfo(token);
                            }

                            var settings = new VaultSharp.VaultClientSettings(address, authMethod);
                            var client = new VaultSharp.VaultClient(settings);
                            var tokenInfo = await client.V1.Auth.Token.LookupSelfAsync();

                            return new { success = true, message = $"Vault authentication successful. Token TTL: {tokenInfo?.Data?.TimeToLive ?? 0}s." };
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Vault connection failed.");
                            return new { success = false, error = "Vault connection failed." };
                        }
                    }

                case "save_auth":
                    {
                        var providerName = GetRequiredStringProperty(args, "providerName");
                        var displayName = args.TryGetProperty("displayName", out var dProp) ? dProp.GetString() ?? providerName : providerName;
                        var userHeader = args.TryGetProperty("userHeader", out var uProp) ? uProp.GetString() ?? "Remote-User" : "Remote-User";
                        var groupsHeader = args.TryGetProperty("groupsHeader", out var gProp) ? gProp.GetString() ?? "Remote-Groups" : "Remote-Groups";
                        var configJson = args.TryGetProperty("configJson", out var cProp) ? cProp.GetString() ?? "{}" : "{}";
                        bool isEnabled = !args.TryGetProperty("isEnabled", out var eProp) || eProp.GetBoolean();

                        var dto = new AuthProviderDto
                        {
                            ProviderName = providerName,
                            DisplayName = displayName,
                            UserHeader = userHeader,
                            GroupsHeader = groupsHeader,
                            ConfigJson = configJson,
                            IsEnabled = isEnabled
                        };

                        ProviderConfigSecurityHelper.ValidateAuthProviderConfig(dto);

                        var existingProviders = await _authProviderRepository.GetAuthProvidersAsync();
                        var existing = existingProviders?.FirstOrDefault(p =>
                            string.Equals(p.ProviderName, dto.ProviderName, StringComparison.OrdinalIgnoreCase));
                        if (existing != null && !string.IsNullOrEmpty(existing.ConfigJson))
                        {
                            dto.ConfigJson = ProviderConfigSecurityHelper.MergeWithExistingConfig(dto.ConfigJson, existing.ConfigJson);
                        }

                        await _authProviderRepository.SaveAuthProviderAsync(dto);

                        if (_ldapService is LdapActiveDirectoryService ldapAd)
                        {
                            ldapAd.Reload();
                        }

                        return new { success = true, providerName = dto.ProviderName };
                    }

                case "test_ldap":
                    {
                        var server = GetRequiredStringProperty(args, "server");
                        int port = args.TryGetProperty("port", out var pProp) && pProp.TryGetInt32(out var portVal) ? portVal : 636;
                        bool useSsl = args.TryGetProperty("useSsl", out var sslProp) ? sslProp.GetBoolean() : (port == 636);
                        var bindDn = args.TryGetProperty("bindDn", out var bdProp) ? bdProp.GetString() : null;
                        var bindPassword = args.TryGetProperty("bindPassword", out var bpProp) ? bpProp.GetString() : null;

                        if (port == 389 && !useSsl)
                        {
                            throw new ArgumentException("LDAP over plaintext (port 389) is disabled for security. Use LDAPS port 636 or set useSsl=true.");
                        }

                        try
                        {
                            var identifier = new System.DirectoryServices.Protocols.LdapDirectoryIdentifier(server, port);
                            System.Net.NetworkCredential? credential = null;
                            if (!string.IsNullOrEmpty(bindDn) && !string.IsNullOrEmpty(bindPassword))
                            {
                                credential = new System.Net.NetworkCredential(bindDn, bindPassword);
                            }

                            using var connection = new System.DirectoryServices.Protocols.LdapConnection(identifier, credential, System.DirectoryServices.Protocols.AuthType.Basic);
                            connection.SessionOptions.ProtocolVersion = 3;
                            connection.SessionOptions.SecureSocketLayer = useSsl;
                            connection.Bind();

                            return new { success = true, message = $"LDAP bind successful to '{server}:{port}'." };
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "LDAP connection/bind failed.");
                            return new { success = false, error = "LDAP connection/bind failed." };
                        }
                    }

                default:
                    throw new ArgumentException($"Invalid action '{action}' for manage_providers.");
            }
        }

        // 7. manage_settings
        private Task<object> HandleManageSettingsAsync(JsonElement args, string callerUsername)
        {
            var action = GetRequiredStringProperty(args, "action");

            switch (action.ToLowerInvariant())
            {
                case "get":
                    {
                        var settings = _dynamicEmbeddingService.GetSettings();
                        return Task.FromResult<object>(settings);
                    }

                case "update":
                    {
                        var current = _dynamicEmbeddingService.GetSettings();

                        if (args.TryGetProperty("settings", out var settingsProp) && settingsProp.ValueKind == JsonValueKind.Object)
                        {
                            var parsed = JsonSerializer.Deserialize<RouterSettings>(settingsProp.GetRawText());
                            if (parsed != null)
                            {
                                current = parsed;
                            }
                        }
                        else
                        {
                            if (args.TryGetProperty("dashboardTitle", out var dtProp))
                            {
                                current.DashboardTitle = dtProp.GetString() ?? current.DashboardTitle;
                            }

                            if (args.TryGetProperty("dashboardIcon", out var diProp))
                            {
                                current.DashboardIcon = diProp.GetString() ?? current.DashboardIcon;
                            }

                            if (args.TryGetProperty("embeddingProvider", out var epProp))
                            {
                                current.EmbeddingProvider = epProp.GetString() ?? current.EmbeddingProvider;
                            }

                            if (args.TryGetProperty("embeddingApiUrl", out var eauProp))
                            {
                                current.EmbeddingApiUrl = eauProp.GetString() ?? current.EmbeddingApiUrl;
                            }

                            if (args.TryGetProperty("embeddingApiKey", out var eakProp))
                            {
                                current.EmbeddingApiKey = eakProp.GetString() ?? current.EmbeddingApiKey;
                            }

                            if (args.TryGetProperty("embeddingApiModel", out var eamProp))
                            {
                                current.EmbeddingApiModel = eamProp.GetString() ?? current.EmbeddingApiModel;
                            }

                            if (args.TryGetProperty("embeddingModelDir", out var emdProp))
                            {
                                current.EmbeddingModelDir = emdProp.GetString() ?? current.EmbeddingModelDir;
                            }

                            if (args.TryGetProperty("globalMaxKeys", out var gmkProp) && gmkProp.TryGetInt32(out var gmk))
                            {
                                current.GlobalMaxKeys = gmk;
                            }

                            if (args.TryGetProperty("userMaxKeys", out var umkProp) && umkProp.TryGetInt32(out var umk))
                            {
                                current.UserMaxKeys = umk;
                            }

                            if (args.TryGetProperty("allowOpenClientRegistration", out var aocrProp))
                            {
                                current.AllowOpenClientRegistration = aocrProp.GetBoolean();
                            }
                        }

                        _dynamicEmbeddingService.SaveSettings(current);
                        return Task.FromResult<object>(new { success = true, settings = _dynamicEmbeddingService.GetSettings() });
                    }

                default:
                    throw new ArgumentException($"Invalid action '{action}' for manage_settings.");
            }
        }

        // 8. manage_custom_files
        private async Task<object> HandleManageCustomFilesAsync(JsonElement args, string callerUsername)
        {
            var action = GetRequiredStringProperty(args, "action");

            switch (action.ToLowerInvariant())
            {
                case "list":
                    {
                        var typeFilter = args.TryGetProperty("type", out var tProp) ? tProp.GetString() : "all";
                        var types = typeFilter?.ToLowerInvariant() switch
                        {
                            "prompts" => new[] { "prompts" },
                            "resources" => new[] { "resources" },
                            _ => new[] { "prompts", "resources" }
                        };

                        var result = new List<object>();
                        foreach (var type in types)
                        {
                            var dir = GetCustomFilesDirectory(type);
                            if (Directory.Exists(dir))
                            {
                                foreach (var file in Directory.GetFiles(dir))
                                {
                                    var info = new FileInfo(file);
                                    result.Add(new
                                    {
                                        type,
                                        name = info.Name,
                                        sizeBytes = info.Length,
                                        lastModified = info.LastWriteTimeUtc
                                    });
                                }
                            }
                        }
                        return result;
                    }

                case "get":
                    {
                        var type = GetRequiredStringProperty(args, "type");
                        var name = GetRequiredStringProperty(args, "name");

                        if (type != "prompts" && type != "resources")
                        {
                            throw new ArgumentException("Type must be 'prompts' or 'resources'.");
                        }

                        var cleanName = SanitizeFileName(name);
                        if (string.IsNullOrEmpty(cleanName))
                        {
                            throw new ArgumentException("Invalid file name.");
                        }

                        var dir = GetCustomFilesDirectory(type);
                        var filePath = Path.Combine(dir, cleanName);
                        if (!File.Exists(filePath))
                        {
                            throw new FileNotFoundException($"Custom file '{cleanName}' not found.");
                        }

                        var text = await File.ReadAllTextAsync(filePath);
                        return new { type, name = cleanName, content = text };
                    }

                case "save":
                    {
                        var type = GetRequiredStringProperty(args, "type");
                        var name = GetRequiredStringProperty(args, "name");
                        var content = GetRequiredStringProperty(args, "content");

                        if (type != "prompts" && type != "resources")
                        {
                            throw new ArgumentException("Type must be 'prompts' or 'resources'.");
                        }

                        var cleanName = SanitizeFileName(name);
                        if (string.IsNullOrEmpty(cleanName))
                        {
                            throw new ArgumentException("Invalid file name.");
                        }

                        if (type == "prompts" && !cleanName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        {
                            cleanName += ".json";
                        }

                        if (type == "prompts")
                        {
                            try
                            {
                                using var doc = JsonDocument.Parse(content);
                            }
                            catch (Exception ex)
                            {
                                throw new ArgumentException($"Invalid JSON format for prompt template: {ex.Message}");
                            }
                        }

                        var dir = GetCustomFilesDirectory(type);
                        var filePath = Path.Combine(dir, cleanName);
                        await File.WriteAllTextAsync(filePath, content);

                        return new { success = true, type, name = cleanName };
                    }

                case "delete":
                    {
                        var type = GetRequiredStringProperty(args, "type");
                        var name = GetRequiredStringProperty(args, "name");

                        if (type != "prompts" && type != "resources")
                        {
                            throw new ArgumentException("Type must be 'prompts' or 'resources'.");
                        }

                        var cleanName = SanitizeFileName(name);
                        if (string.IsNullOrEmpty(cleanName))
                        {
                            throw new ArgumentException("Invalid file name.");
                        }

                        var dir = GetCustomFilesDirectory(type);
                        var filePath = Path.Combine(dir, cleanName);
                        if (!File.Exists(filePath))
                        {
                            throw new FileNotFoundException($"Custom file '{cleanName}' not found.");
                        }

                        File.Delete(filePath);
                        return new { success = true, type, name = cleanName };
                    }

                default:
                    throw new ArgumentException($"Invalid action '{action}' for manage_custom_files.");
            }
        }

        // 9. manage_system
        private async Task<object> HandleManageSystemAsync(JsonElement args, string callerUsername)
        {
            var action = GetRequiredStringProperty(args, "action");

            switch (action.ToLowerInvariant())
            {
                case "diagnostics":
                    {
                        var proc = Process.GetCurrentProcess();
                        int fdCount = 0;

                        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                        {
                            try
                            {
                                fdCount = Directory.GetFiles($"/proc/{proc.Id}/fd").Length;
                            }
                            catch { }
                        }

                        return new
                        {
                            activeSessions = _sessionManager.ActiveSessionsCount,
                            workingSet64 = proc.WorkingSet64,
                            handleCount = fdCount > 0 ? fdCount : proc.HandleCount,
                            machineName = Environment.MachineName,
                            osVersion = Environment.OSVersion.ToString(),
                            processUptime = (DateTime.UtcNow - proc.StartTime.ToUniversalTime()).ToString(@"d\.hh\:mm\:ss")
                        };
                    }

                case "get_logs":
                    {
                        int limit = args.TryGetProperty("limit", out var lProp) && lProp.TryGetInt32(out var l) ? l : 100;
                        var logs = LogBuffer.GetLogs();
                        if (limit > 0 && logs.Count > limit)
                        {
                            logs = logs.TakeLast(limit).ToList();
                        }
                        return logs;
                    }

                case "clear_logs":
                    {
                        LogBuffer.Clear();
                        return new { success = true, message = "In-memory log buffer cleared." };
                    }

                case "query_audit":
                    {
                        string? user = args.TryGetProperty("user", out var uProp) ? uProp.GetString() : null;
                        string? server = args.TryGetProperty("server", out var sProp) ? sProp.GetString() : null;
                        DateTime? since = null;
                        if (args.TryGetProperty("since", out var sincProp) && DateTime.TryParse(sincProp.GetString(), out var parsedDate))
                        {
                            since = parsedDate;
                        }

                        int take = args.TryGetProperty("take", out var tProp) && tProp.TryGetInt32(out var tVal) ? tVal : 50;
                        int skip = args.TryGetProperty("skip", out var skProp) && skProp.TryGetInt32(out var skVal) ? skVal : 0;
                        take = Math.Clamp(take, 1, 1000);

                        using var conn = _dbFactory.CreateConnection();
                        string sql;
                        if (_dbFactory.ProviderName.Equals("mssql", StringComparison.OrdinalIgnoreCase))
                        {
                            sql = @"SELECT RequestId, UserPrincipalName, UserSid, ServerCodeName, ItemName, RequestMethod, StatusCode, ExecutionTimeMs, ErrorMessage, Timestamp
                                FROM AuditLogs
                                WHERE (@user   IS NULL OR UserPrincipalName = @user)
                                  AND (@server IS NULL OR ServerCodeName = @server)
                                  AND (@since  IS NULL OR Timestamp >= @since)
                                ORDER BY Timestamp DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;";
                        }
                        else
                        {
                            sql = @"SELECT RequestId, UserPrincipalName, UserSid, ServerCodeName, ItemName, RequestMethod, StatusCode, ExecutionTimeMs, ErrorMessage, Timestamp
                                FROM AuditLogs
                                WHERE (@user   IS NULL OR UserPrincipalName = @user)
                                  AND (@server IS NULL OR ServerCodeName = @server)
                                  AND (@since  IS NULL OR Timestamp >= @since)
                                ORDER BY Timestamp DESC LIMIT @take OFFSET @skip;";
                        }

                        var rows = (await conn.QueryAsync(sql, new { user, server, since, take, skip })).ToList();
                        return rows;
                    }

                case "set_master_key":
                    {
                        var newKey = args.TryGetProperty("newKey", out var nkProp) ? nkProp.GetString() :
                                     args.TryGetProperty("masterKey", out var mkProp) ? mkProp.GetString() :
                                     args.TryGetProperty("key", out var kProp) ? kProp.GetString() : null;

                        if (string.IsNullOrWhiteSpace(newKey))
                        {
                            throw new ArgumentException("Parameter 'newKey' is required for action 'set_master_key'.");
                        }

                        var trimmedKey = newKey.Trim();
                        if (trimmedKey.Length < 16)
                        {
                            throw new ArgumentException("Master key must be at least 16 characters long.");
                        }

                        if (DbKeyHelper.ActiveKeySource == MasterKeySource.External || DbKeyHelper.ActiveKeySource == MasterKeySource.Vault)
                        {
                            throw new InvalidOperationException($"Cannot set custom master key when key source is managed externally ({DbKeyHelper.ActiveKeySource}).");
                        }

                        var masterKeyManager = _masterKeyManager ?? new DatabaseRepository(_dbFactory, _configuration);
                        await masterKeyManager.ReencryptDatabaseSecretsAsync(trimmedKey);

                        await _auditLogger.LogAdminActionAsync(
                            callerUsername,
                            "masterkey.reencrypt",
                            "MasterKey",
                            "Re-encrypted database secrets and updated master key.",
                            true);

                        return new
                        {
                            success = true,
                            message = "Master encryption key updated and database secrets successfully re-encrypted.",
                            keySource = DbKeyHelper.ActiveKeySource.ToString()
                        };
                    }

                default:
                    throw new ArgumentException($"Invalid action '{action}' for manage_system.");
            }
        }

        // 10. test_tool_call
        private async Task<object> HandleTestToolCallAsync(JsonElement args, string callerUsername)
        {
            var serverId = GetRequiredStringProperty(args, "serverId");
            var toolName = GetRequiredStringProperty(args, "toolName");

            var server = await _serverRepository.GetServerByIdAsync(serverId);
            if (server == null)
            {
                throw new KeyNotFoundException($"Server '{serverId}' not found.");
            }

            using var conn = new BackendConnection(server, _httpClient, _logger ?? (ILogger)NullLogger.Instance, _secretRetriever);
            if (server.Type != "http" && server.Type != "streamable")
            {
                using var ctsTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await conn.ConnectAsync().WaitAsync(ctsTimeout.Token);
                conn.StartReader(_ => Task.CompletedTask);
            }

            using var ctsInit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var initReq = GatewayMetadata.BuildTestBenchInitializeRequest();
            await conn.SendRequestAsync("initialize", initReq).WaitAsync(ctsInit.Token);
            await conn.SendNotificationAsync("notifications/initialized", "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");

            object callArguments = new Dictionary<string, object>();
            if (args.TryGetProperty("arguments", out var callArgsProp) && callArgsProp.ValueKind == JsonValueKind.Object)
            {
                callArguments = callArgsProp;
            }

            var targetPayload = new
            {
                jsonrpc = "2.0",
                id = "admin-test-call-id",
                method = "tools/call",
                @params = new
                {
                    name = toolName,
                    arguments = callArguments
                }
            };

            var targetBody = JsonSerializer.Serialize(targetPayload);
            var result = await conn.SendRequestAsync("tools/call", targetBody);

            if (result.Error != null)
            {
                throw new InvalidOperationException(!string.IsNullOrWhiteSpace(result.Error.Message)
                    ? result.Error.Message
                    : $"Backend error code {result.Error.Code}");
            }

            return result.Result.HasValue ? result.Result.Value : (object)new { };
        }

        #endregion

        #region Helpers & Schemas

        private static readonly JsonSerializerOptions _responseJsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static object FormatSuccessResponse(object data)
        {
            var text = data is string s ? s : JsonSerializer.Serialize(data, _responseJsonOptions);
            return new
            {
                isError = false,
                content = new[]
                {
                    new { type = "text", text }
                }
            };
        }

        private static object FormatErrorResponse(string errorMessage)
        {
            return new
            {
                isError = true,
                content = new[]
                {
                    new { type = "text", text = errorMessage }
                }
            };
        }

        private static string GetRequiredStringProperty(JsonElement element, string propName)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propName, out var prop))
            {
                var val = prop.GetString();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    return val;
                }
            }
            throw new ArgumentException($"Missing required argument: '{propName}'");
        }

        private static McpServer ParseServerFromArgs(JsonElement args)
        {
            var server = new McpServer();
            if (args.TryGetProperty("server", out var sProp) && sProp.ValueKind == JsonValueKind.Object)
            {
                var deserialized = JsonSerializer.Deserialize<McpServer>(sProp.GetRawText());
                if (deserialized != null)
                {
                    server = deserialized;
                }
            }

            if (args.TryGetProperty("id", out var idProp) && !string.IsNullOrWhiteSpace(idProp.GetString()))
            {
                server.Id = idProp.GetString()!;
            }

            if (args.TryGetProperty("displayName", out var dnProp) && !string.IsNullOrWhiteSpace(dnProp.GetString()))
            {
                server.DisplayName = dnProp.GetString()!;
            }

            if (args.TryGetProperty("url", out var urlProp) && !string.IsNullOrWhiteSpace(urlProp.GetString()))
            {
                server.Url = urlProp.GetString()!;
            }

            if (args.TryGetProperty("type", out var typeProp) && !string.IsNullOrWhiteSpace(typeProp.GetString()))
            {
                server.Type = typeProp.GetString()!;
            }

            if (args.TryGetProperty("enabled", out var enProp))
            {
                server.Enabled = enProp.GetBoolean();
            }

            if (args.TryGetProperty("hidden", out var hidProp))
            {
                server.Hidden = hidProp.GetBoolean();
            }

            if (args.TryGetProperty("secretProvider", out var spProp))
            {
                server.SecretProvider = spProp.GetString() ?? "None";
            }

            if (args.TryGetProperty("secretItemKey", out var sikProp))
            {
                server.SecretItemKey = sikProp.GetString();
            }

            if (args.TryGetProperty("authShape", out var asProp))
            {
                server.AuthShape = asProp.GetString() ?? "bearer";
            }

            if (args.TryGetProperty("customHeaderName", out var chnProp))
            {
                server.CustomHeaderName = chnProp.GetString();
            }

            if (args.TryGetProperty("apiKey", out var akProp))
            {
                server.ApiKey = akProp.GetString();
            }

            if (args.TryGetProperty("headersJson", out var hjProp))
            {
                server.HeadersJson = hjProp.GetString();
            }

            if (args.TryGetProperty("categories", out var catProp) && catProp.ValueKind == JsonValueKind.Array)
            {
                server.Categories = catProp.EnumerateArray().Select(c => c.GetString()).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).ToList();
            }

            return server;
        }

        private static void UpdateServerFromArgs(McpServer server, JsonElement args)
        {
            if (args.TryGetProperty("server", out var sProp) && sProp.ValueKind == JsonValueKind.Object)
            {
                var deserialized = JsonSerializer.Deserialize<McpServer>(sProp.GetRawText());
                if (deserialized != null)
                {
                    server.DisplayName = deserialized.DisplayName;
                    server.Url = deserialized.Url;
                    server.Type = deserialized.Type;
                    server.Enabled = deserialized.Enabled;
                    server.Hidden = deserialized.Hidden;
                    server.SecretProvider = deserialized.SecretProvider;
                    server.SecretItemKey = deserialized.SecretItemKey;
                    server.AuthShape = deserialized.AuthShape;
                    server.CustomHeaderName = deserialized.CustomHeaderName;
                    server.Categories = deserialized.Categories;
                    server.ApiKey = deserialized.ApiKey;
                    server.HeadersJson = deserialized.HeadersJson;
                }
            }

            if (args.TryGetProperty("displayName", out var dnProp) && !string.IsNullOrWhiteSpace(dnProp.GetString()))
            {
                server.DisplayName = dnProp.GetString()!;
            }

            if (args.TryGetProperty("url", out var urlProp) && !string.IsNullOrWhiteSpace(urlProp.GetString()))
            {
                server.Url = urlProp.GetString()!;
            }

            if (args.TryGetProperty("type", out var typeProp) && !string.IsNullOrWhiteSpace(typeProp.GetString()))
            {
                server.Type = typeProp.GetString()!;
            }

            if (args.TryGetProperty("enabled", out var enProp))
            {
                server.Enabled = enProp.GetBoolean();
            }

            if (args.TryGetProperty("hidden", out var hidProp))
            {
                server.Hidden = hidProp.GetBoolean();
            }

            if (args.TryGetProperty("secretProvider", out var spProp))
            {
                server.SecretProvider = spProp.GetString() ?? server.SecretProvider;
            }

            if (args.TryGetProperty("secretItemKey", out var sikProp))
            {
                server.SecretItemKey = sikProp.GetString();
            }

            if (args.TryGetProperty("authShape", out var asProp))
            {
                server.AuthShape = asProp.GetString() ?? server.AuthShape;
            }

            if (args.TryGetProperty("customHeaderName", out var chnProp))
            {
                server.CustomHeaderName = chnProp.GetString();
            }

            if (args.TryGetProperty("apiKey", out var akProp))
            {
                server.ApiKey = akProp.GetString();
            }

            if (args.TryGetProperty("headersJson", out var hjProp))
            {
                server.HeadersJson = hjProp.GetString();
            }

            if (args.TryGetProperty("categories", out var catProp) && catProp.ValueKind == JsonValueKind.Array)
            {
                server.Categories = catProp.EnumerateArray().Select(c => c.GetString()).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).ToList();
            }
        }

        private void ValidateServerConfig(McpServer server)
        {
            var allowedTypes = new[] { "sse", "http", "streamable", "stdio", "custom" };
            var lowerType = (server.Type ?? "sse").ToLowerInvariant();
            if (!allowedTypes.Contains(lowerType))
            {
                throw new ArgumentException($"Transport type '{server.Type}' is not supported.");
            }
            server.Type = lowerType;

            if (server.Type == "stdio")
            {
                if (!ServerValidationHelper.IsValidStdioCommand(server.Url, out var err))
                {
                    throw new ArgumentException(err);
                }
            }
            else if (server.Type != "custom" && _configuration != null)
            {
                if (!ServerValidationHelper.IsValidServerUrl(server.Url, _configuration, out var err))
                {
                    throw new ArgumentException(err);
                }
            }
        }

        private static List<string> DeserializeScopes(string json)
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

        private static string SanitizeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return new string(name.Where(c => !invalidChars.Contains(c) && c != '/' && c != '\\').ToArray());
        }

        private static string GetCustomFilesDirectory(string type)
        {
            string folder = type == "prompts" ? "prompts" : "resources";
            var path = Path.Combine(AppContext.BaseDirectory, "data", folder);
            if (!Directory.Exists(path))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), "data", folder);
            }
            Directory.CreateDirectory(path);
            return path;
        }

        private static List<object> GetToolDefinitions()
        {
            return new List<object>
            {
                new
                {
                    name = "manage_servers",
                    description = "Manage backend MCP server configurations and connectivity in the router gateway.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            action = new { type = "string", @enum = new[] { "list", "get", "create", "update", "delete", "toggle", "reconnect", "reconnect_all" }, description = "Action to execute" },
                            id = new { type = "string", description = "Server identifier" },
                            displayName = new { type = "string", description = "Server display name" },
                            url = new { type = "string", description = "Server endpoint URL or command" },
                            type = new { type = "string", @enum = new[] { "sse", "http", "streamable", "stdio", "custom" }, description = "Transport protocol type" },
                            enabled = new { type = "boolean", description = "Server enabled state" },
                            hidden = new { type = "boolean", description = "Server hidden state" },
                            categories = new { type = "array", items = new { type = "string" }, description = "Server categories" },
                            secretProvider = new { type = "string", description = "Secret provider (None, Vault, Registry, Env)" },
                            secretItemKey = new { type = "string", description = "Key for secret resolution" },
                            authShape = new { type = "string", description = "Authentication shape (bearer, customHeader, etc.)" },
                            customHeaderName = new { type = "string", description = "Custom header name for authentication" },
                            apiKey = new { type = "string", description = "API key" },
                            headersJson = new { type = "string", description = "Custom headers in JSON format" }
                        },
                        required = new[] { "action" }
                    }
                },
                new
                {
                    name = "manage_appkeys",
                    description = "Manage user and application API keys, quotas, and expiration.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            action = new { type = "string", @enum = new[] { "list", "get_limits", "create", "revoke" }, description = "Action to execute" },
                            id = new { type = "string", description = "AppKey identifier (for revoke)" },
                            name = new { type = "string", description = "AppKey name (for create)" },
                            username = new { type = "string", description = "Target username" },
                            scopes = new { type = "array", items = new { type = "string" }, description = "Scopes array (e.g. ['all', 'admin', 'category:database'])" },
                            expiresInDays = new { type = "integer", description = "Expiration duration in days" }
                        },
                        required = new[] { "action" }
                    }
                },
                new
                {
                    name = "manage_clients",
                    description = "Manage OAuth2 / dynamic client credentials.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            action = new { type = "string", @enum = new[] { "list", "register", "delete" }, description = "Action to execute" },
                            id = new { type = "string", description = "Client identifier (for delete)" },
                            displayName = new { type = "string", description = "Client display name (for register)" },
                            scopes = new { type = "array", items = new { type = "string" }, description = "Client scopes" },
                            expiresInDays = new { type = "integer", description = "Expiration duration in days" }
                        },
                        required = new[] { "action" }
                    }
                },
                new
                {
                    name = "manage_policies",
                    description = "Manage role-based access control (RBAC) policies for MCP servers, tools, and resources.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            action = new { type = "string", @enum = new[] { "list", "save", "delete" }, description = "Action to execute" },
                            id = new { type = "string", description = "Policy identifier" },
                            targetId = new { type = "string", description = "Target identifier (server, tool, or wildcard '*')" },
                            requiredGroup = new { type = "string", description = "Required role, group, or SID" },
                            isAllowed = new { type = "boolean", description = "Whether access is granted or denied" }
                        },
                        required = new[] { "action" }
                    }
                },
                new
                {
                    name = "manage_group_mappings",
                    description = "Manage external identity provider group to internal group mappings.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            action = new { type = "string", @enum = new[] { "list", "save", "delete" }, description = "Action to execute" },
                            id = new { type = "string", description = "Mapping identifier" },
                            externalId = new { type = "string", description = "External group name or SID" },
                            internalGroup = new { type = "string", description = "Internal role or group" }
                        },
                        required = new[] { "action" }
                    }
                },
                new
                {
                    name = "manage_providers",
                    description = "Manage secret providers (Vault, etc.) and authentication providers (LDAP/AD, OIDC).",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            action = new { type = "string", @enum = new[] { "list", "save_secret", "test_vault", "save_auth", "test_ldap" }, description = "Action to execute" },
                            type = new { type = "string", description = "Provider type for list ('all', 'secrets', 'auth')" },
                            providerName = new { type = "string", description = "Provider name" },
                            displayName = new { type = "string", description = "Provider display name" },
                            configJson = new { type = "string", description = "Provider configuration JSON" },
                            isEnabled = new { type = "boolean", description = "Provider enabled status" },
                            userHeader = new { type = "string", description = "Header for username (auth provider)" },
                            groupsHeader = new { type = "string", description = "Header for groups (auth provider)" },
                            address = new { type = "string", description = "Vault server address (for test_vault)" },
                            authMethod = new { type = "string", description = "Vault auth method (token, approle)" },
                            token = new { type = "string", description = "Vault token" },
                            roleId = new { type = "string", description = "Vault AppRole RoleId" },
                            secretId = new { type = "string", description = "Vault AppRole SecretId" },
                            server = new { type = "string", description = "LDAP server address (for test_ldap)" },
                            port = new { type = "integer", description = "LDAP server port" },
                            useSsl = new { type = "boolean", description = "Use SSL for LDAP" },
                            bindDn = new { type = "string", description = "LDAP bind DN" },
                            bindPassword = new { type = "string", description = "LDAP bind password" }
                        },
                        required = new[] { "action" }
                    }
                },
                new
                {
                    name = "manage_settings",
                    description = "Manage global router settings, embedding models, and UI configuration.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            action = new { type = "string", @enum = new[] { "get", "update" }, description = "Action to execute" },
                            dashboardTitle = new { type = "string", description = "Dashboard header title" },
                            dashboardIcon = new { type = "string", description = "Dashboard icon class" },
                            embeddingProvider = new { type = "string", description = "Embedding provider (onnx, api)" },
                            embeddingApiUrl = new { type = "string", description = "API endpoint for embeddings" },
                            embeddingApiKey = new { type = "string", description = "API key for embeddings" },
                            embeddingApiModel = new { type = "string", description = "Embedding model name" },
                            embeddingModelDir = new { type = "string", description = "Local embedding directory" },
                            globalMaxKeys = new { type = "integer", description = "Global maximum active AppKeys" },
                            userMaxKeys = new { type = "integer", description = "User maximum active AppKeys" }
                        },
                        required = new[] { "action" }
                    }
                },
                new
                {
                    name = "manage_custom_files",
                    description = "Manage custom prompt templates and local resource files in the router data directory.",
                    inputSchema = new
                    {
                        type = "object",
                        properties =
                        new
                        {
                            action = new { type = "string", @enum = new[] { "list", "get", "save", "delete" }, description = "Action to execute" },
                            type = new { type = "string", @enum = new[] { "prompts", "resources" }, description = "Custom file category" },
                            name = new { type = "string", description = "File name" },
                            content = new { type = "string", description = "File contents (for save)" }
                        },
                        required = new[] { "action" }
                    }
                },
                new
                {
                    name = "manage_system",
                    description = "Router system diagnostics, runtime metrics, logs, audit trail, and master encryption key management.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            action = new { type = "string", @enum = new[] { "diagnostics", "get_logs", "clear_logs", "query_audit", "set_master_key" }, description = "Action to execute" },
                            limit = new { type = "integer", description = "Maximum log entries to return" },
                            user = new { type = "string", description = "Filter audit logs by user" },
                            server = new { type = "string", description = "Filter audit logs by server" },
                            since = new { type = "string", description = "Filter audit logs since timestamp" },
                            take = new { type = "integer", description = "Audit query page size" },
                            skip = new { type = "integer", description = "Audit query page offset" },
                            newKey = new { type = "string", description = "New master encryption key (required for set_master_key)" }
                        },
                        required = new[] { "action" }
                    }
                },
                new
                {
                    name = "test_tool_call",
                    description = "Test execution of a backend MCP tool directly via the router test bench.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            action = new { type = "string", description = "Action name (default 'execute')" },
                            serverId = new { type = "string", description = "Backend server ID" },
                            toolName = new { type = "string", description = "Backend tool name" },
                            arguments = new { type = "object", description = "Tool argument payload" }
                        },
                        required = new[] { "serverId", "toolName" }
                    }
                }
            };
        }

        #endregion
    }
}
