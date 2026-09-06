using System.Text.Json;

namespace ModelContextGateway.Core.Routing
{
    /// <summary>
    /// Partial class implementation handling downstream MCP backend connection management, auto-initialization, and retry loops.
    /// </summary>
    public partial class ClientSession
    {
        /// <summary>
        /// Ensures all enabled downstream backend servers are initialized before proceeding with method execution.
        /// </summary>
        /// <returns>A task representing asynchronous initialization completion.</returns>
        public async Task EnsureBackendsInitializedAsync()
        {
            if (_initializeTask == null)
            {
                lock (_initLock)
                {
                    if (_initializeTask == null)
                    {
                        var initReq = GatewayMetadata.BuildInitializeRequest();
                        _initializeTask = Task.Run(async () => await InitializeBackendsAsync(initReq));
                    }
                }
            }
            await _initializeTask;

            List<Task> pending;
            lock (_backendInitTasks)
            {
                pending = _backendInitTasks.ToList();
            }
            if (pending.Count > 0)
            {
                await Task.WhenAll(pending);
            }
        }

        public void StartInitialization(string initializeRequest)
        {
            lock (_initLock)
            {
                if (_initializeTask == null)
                {
                    var finalRequest = initializeRequest;
                    if (initializeRequest.Contains("server/discover"))
                    {
                        finalRequest = GatewayMetadata.BuildInitializeRequest();
                    }
                    _initializeTask = Task.Run(async () => await InitializeBackendsAsync(finalRequest));
                }
            }
        }

        public async Task InitializeBackendsAsync(string initializeRequest)
        {
            _lastInitializeRequest = initializeRequest;
            var tasks = new List<Task>();
            foreach (var server in _servers.Where(s => s.Enabled && s.Type != "custom"))
            {
                var task = Task.Run(async () => await ConnectAndInitializeBackendAsync(server));
                tasks.Add(task);
            }
            lock (_backendInitTasks)
            {
                _backendInitTasks.AddRange(tasks);
            }

            // We do NOT block on backend initialization, but we trigger a background tools cache population
            _ = Task.Run(async () =>
            {
                // Wait a couple seconds for some backends to finish initial connection
                await Task.Delay(3000);
                try
                {
                    await _toolRoutingManager.PopulateToolsCacheAsync("{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\",\"id\":\"init-list\"}", _backendConnections, _logger, _servers, _sessionManager);
                    _logger.LogInformation("Completed initial background tools cache population.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to run initial background tools cache population.");
                }
            });

            await Task.WhenAll(tasks);
        }

        private async Task ConnectAndInitializeBackendAsync(McpServer server)
        {
            int maxAttempts = 2;
            int attempt = 0;
            while (!_cts.Token.IsCancellationRequested && attempt < maxAttempts)
            {
                attempt++;
                BackendConnection? conn = null;
                try
                {
                    _logger.LogInformation("Attempting to connect to backend {ServerId} (attempt {Attempt}/{MaxAttempts}) at {Url}...", server.Id, attempt, maxAttempts, server.Url);
                    _sessionManager?.UpdateBackendStatus(server.Id, "Connecting", attempt, "");

                    var retriever = _rootServices?.GetService<CompositeSecretRetriever>();
                    if (retriever == null)
                    {
                        try
                        {
                            retriever = _clientResponse?.HttpContext?.RequestServices?.GetService<CompositeSecretRetriever>();
                        }
                        catch (ObjectDisposedException)
                        {
                            // Disposed HttpContext
                        }
                    }
                    string? passThroughToken = null;

                    UserIdentityContext identity;
                    try
                    {
                        identity = await ResolveUserIdentityAsync(_clientResponse?.HttpContext);
                    }
                    catch (ObjectDisposedException)
                    {
                        identity = new UserIdentityContext("system", "System", new List<string>());
                    }
                    string? forwardedUser = string.IsNullOrEmpty(identity.Username) ? null : identity.Username;

                    if (server.SecretProvider == "UserProvided")
                    {
                        var userSecretStore = _rootServices?.GetService<IUserSecretStore>();
                        if (userSecretStore == null)
                        {
                            try
                            {
                                userSecretStore = _clientResponse?.HttpContext?.RequestServices?.GetService<IUserSecretStore>();
                            }
                            catch (ObjectDisposedException)
                            {
                                // Disposed HttpContext
                            }
                        }
                        if (userSecretStore != null)
                        {
                            var secretJson = await userSecretStore.GetSecretAsync(identity.Username, server.Id);
                            if (string.IsNullOrEmpty(secretJson))
                            {
                                throw new Exception($"User credential required but not found for server '{server.Id}'");
                            }

                            passThroughToken = secretJson;
                        }
                        else
                        {
                            throw new Exception("IUserSecretStore is not registered in DI.");
                        }
                    }
                    else if (server.AllowPassThroughAuth)
                    {
                        try
                        {
                            if (_clientResponse?.HttpContext?.Request.Headers.TryGetValue("X-Target-Auth", out var tokenVals) == true)
                            {
                                passThroughToken = tokenVals.ToString();
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            // Disposed HttpContext
                        }
                    }
                    conn = new BackendConnection(server, _httpClient, _logger, retriever, passThroughToken, forwardedUser);
                    if (server.Type != "http" && server.Type != "streamable")
                    {
                        using var ctsTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await conn.ConnectAsync().WaitAsync(ctsTimeout.Token);
                    }

                    // Start background reader
                    conn.StartReader(async (message) =>
                    {
                        // If message is a response, complete the TaskCompletionSource
                        if (message is JsonRpcResponse response && response.Id != null)
                        {
                            var idStr = response.Id.ToString();
                            if (idStr != null && conn.TryCompleteRequest(idStr, response))
                            {
                                return;
                            }
                        }

                        if (message is JsonRpcNotification notification)
                        {
                            if (notification.Method == "notifications/tools/list_changed")
                            {
                                _logger.LogInformation("Received notifications/tools/list_changed from server {ServerId}. Invalidating tools cache.", server.Id);
                                _sessionManager?.RemoveServerToolsCache(server.Id);
                                _toolRoutingManager.InvalidateCache();
                            }

                            else if (notification.Method == "notifications/resources/list_changed")
                            {
                                _logger.LogInformation("Received notifications/resources/list_changed from server {ServerId}. Invalidating resources cache.", server.Id);
                                _sessionManager?.RemoveServerResourcesCache(server.Id);
                            }
                            else if (notification.Method == "notifications/prompts/list_changed")
                            {
                                _logger.LogInformation("Received notifications/prompts/list_changed from server {ServerId}. Invalidating prompts cache.", server.Id);
                                _sessionManager?.RemoveServerPromptsCache(server.Id);
                            }
                        }

                        // Otherwise, it is a notification (e.g. logMessage, resourceUpdated) - forward to client
                        var serialized = JsonSerializer.Serialize(message, message.GetType(), _jsonOptions);
                        using var doc = JsonDocument.Parse(serialized);
                        await WriteMessageAsync(doc.RootElement.Clone());
                    });

                    // Send initialize request to this backend with protocol version negotiation & fallback
                    using (var ctsInit = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        var initReq = string.IsNullOrEmpty(_lastInitializeRequest)
                            ? GatewayMetadata.BuildInitializeRequest()
                            : _lastInitializeRequest;
                        var resp = await conn.SendRequestAsync("initialize", initReq).WaitAsync(ctsInit.Token);
                        if (resp.Error != null && resp.Error.Code == -32601)
                        {
                            // Modern v2.0 (July 2026) MCP server where initialize handshake is omitted
                            _logger.LogInformation("Backend server {ServerId} does not implement 'initialize' (-32601). Proceeding in stateless v2.0 mode.", server.Id);
                        }
                        else if (resp.Error != null && IsProtocolVersionMismatch(resp.Error))
                        {
                            var candidateVersions = ResolveCandidateVersions(resp.Error);
                            bool negotiated = false;

                            foreach (var fallbackVer in candidateVersions)
                            {
                                _logger.LogInformation("Attempting protocol negotiation fallback to '{ProtocolVersion}' for backend {ServerId}...", fallbackVer, server.Id);
                                var fallbackReq = CreateInitializeRequestWithVersion(initReq, fallbackVer);
                                var retryResp = await conn.SendRequestAsync("initialize", fallbackReq).WaitAsync(ctsInit.Token);
                                if (retryResp.Error == null)
                                {
                                    _logger.LogInformation("Successfully negotiated protocol version '{ProtocolVersion}' with backend {ServerId}.", fallbackVer, server.Id);
                                    resp = retryResp;
                                    negotiated = true;
                                    break;
                                }
                            }

                            if (!negotiated && resp.Error != null)
                            {
                                throw new Exception($"Initialize failed after protocol version negotiation: {resp.Error.Message}");
                            }
                        }
                        else if (resp.Error != null)
                        {
                            throw new Exception($"Initialize failed: {resp.Error.Message}");
                        }
                    }

                    // Send initialized notification to this backend
                    await conn.SendNotificationAsync("notifications/initialized", "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");

                    if (_backendConnections.TryRemove(server.Id, out var prevConn))
                    {
                        prevConn.Dispose();
                    }
                    _backendConnections[server.Id] = conn;
                    _logger.LogInformation("Successfully connected and initialized backend server: {ServerId}", server.Id);
                    _sessionManager?.UpdateBackendStatus(server.Id, "Connected", attempt, "");
                    return; // Success, exit method
                }
                catch (Exception ex)
                {
                    conn?.Dispose();
                    _logger.LogError("Failed to connect to backend {ServerId} at {Url} (attempt {Attempt}/{MaxAttempts}). Error: {Error}",
                        server.Id, server.Url, attempt, maxAttempts, ex.Message);

                    _sessionManager?.UpdateBackendStatus(server.Id, attempt >= maxAttempts ? "Failed" : "Retrying", attempt, ex.Message);

                    if (attempt >= maxAttempts)
                    {
                        _logger.LogError("Stopped retrying connection to backend {ServerId} after {MaxAttempts} failed attempts.", server.Id, maxAttempts);
                        break;
                    }

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), _cts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        public virtual void StartInitializationForBackend(string serverId)
        {
            var server = _servers.FirstOrDefault(s => s.Id == serverId);
            if (server != null && server.Enabled && server.Type != "custom")
            {
                if (_backendConnections.TryRemove(serverId, out var oldConn))
                {
                    oldConn.Dispose();
                }
                _ = Task.Run(async () => await ConnectAndInitializeBackendAsync(server));
            }
        }

        private static bool IsProtocolVersionMismatch(JsonRpcError error)
        {
            if (error.Code == -32022)
            {
                return true;
            }
            var msg = error.Message ?? string.Empty;
            return msg.Contains("protocol version", StringComparison.OrdinalIgnoreCase) ||
                   msg.Contains("unsupported protocol", StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> ResolveCandidateVersions(JsonRpcError error)
        {
            var candidates = new List<string>();

            if (error.Data.HasValue && error.Data.Value.ValueKind == JsonValueKind.Object)
            {
                if (error.Data.Value.TryGetProperty("supported", out var supportedElem) && supportedElem.ValueKind == JsonValueKind.Array)
                {
                    var serverSupported = new List<string>();
                    foreach (var item in supportedElem.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            var s = item.GetString();
                            if (!string.IsNullOrWhiteSpace(s))
                            {
                                serverSupported.Add(s.Trim());
                            }
                        }
                    }

                    // Dynamically sort descending by ISO date string (latest/highest version first)
                    candidates.AddRange(serverSupported.OrderByDescending(v => v));
                }
            }

            // Universal fallback baseline if no supported versions were provided by the server
            if (candidates.Count == 0)
            {
                candidates.Add(GatewayMetadata.LegacyProtocolVersion);
            }

            return candidates;
        }

        private static string CreateInitializeRequestWithVersion(string baseRequest, string targetVersion)
        {
            try
            {
                using var doc = JsonDocument.Parse(baseRequest);
                var root = doc.RootElement;
                var id = root.TryGetProperty("id", out var idElem) ? idElem.ToString() : "auto-init";

                string clientName = "ModelContextGatewayAuto";
                string clientVersion = GatewayMetadata.Version;
                JsonElement capabilities = default;

                if (root.TryGetProperty("params", out var paramsElem) && paramsElem.ValueKind == JsonValueKind.Object)
                {
                    if (paramsElem.TryGetProperty("capabilities", out var capsElem))
                    {
                        capabilities = capsElem.Clone();
                    }
                    if (paramsElem.TryGetProperty("clientInfo", out var clientElem) && clientElem.ValueKind == JsonValueKind.Object)
                    {
                        if (clientElem.TryGetProperty("name", out var nameElem))
                        {
                            clientName = nameElem.GetString() ?? clientName;
                        }
                        if (clientElem.TryGetProperty("version", out var verElem))
                        {
                            clientVersion = verElem.GetString() ?? clientVersion;
                        }
                    }
                }

                return JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    method = "initialize",
                    id,
                    @params = new
                    {
                        protocolVersion = targetVersion,
                        capabilities = capabilities.ValueKind != JsonValueKind.Undefined ? (object)capabilities : new { },
                        clientInfo = new
                        {
                            name = clientName,
                            version = clientVersion
                        }
                    }
                });
            }
            catch
            {
                return GatewayMetadata.BuildInitializeRequest(protocolVersion: targetVersion);
            }
        }
    }
}
