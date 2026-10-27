namespace ModelContextGateway.Core.Routing
{
    public partial class ClientSession
    {
        /// <summary>
        /// Evaluates whether the caller is authorized to view instructions or status metadata for a downstream server.
        /// </summary>
        internal async Task<bool> IsServerAuthorizedForInstructionsAsync(string serverId, HttpContext? httpContext = null)
        {
            var contextToUse = httpContext ?? _clientResponse?.HttpContext;
            if (contextToUse == null && _rootServices == null)
            {
                return true;
            }

            if (await IsUserAuthorizedAsync("tools/list", $"server:{serverId}", contextToUse))
            {
                return true;
            }

            var cachedTools = _sessionManager?.GetServerToolsCache(serverId);
            if (cachedTools != null)
            {
                foreach (var toolObj in cachedTools)
                {
                    if (toolObj is IDictionary<string, object> dict && dict.TryGetValue("name", out var nObj) && nObj is string toolName)
                    {
                        var namespaced = toolName.Contains("__", StringComparison.Ordinal) ? toolName : $"{serverId}__{toolName}";
                        if (await IsUserAuthorizedAsync("tools/list", namespaced, contextToUse))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves the MCP 2026-07-28 instructions for this session:
        /// - In direct proxy mode (!IsMetaMode), returns the downstream server's own instructions when available.
        /// - In Meta-Mode, returns the gateway instructions with an RBAC-filtered rollup of cached downstream server instructions.
        /// </summary>
        public async Task<string> ResolveSessionInstructionsAsync(
            bool enableCodeMode,
            string? initialRequestBody = null,
            TimeSpan? directWaitTimeout = null,
            HttpContext? httpContext = null)
        {
            if (!string.IsNullOrWhiteSpace(initialRequestBody))
            {
                StartInitialization(initialRequestBody);
            }

            var enabledServers = _servers.Where(s => s.Enabled && s.Type != "custom").ToList();

            if (!IsMetaMode && enabledServers.Count > 0)
            {
                bool anyMissing = enabledServers.Any(s => string.IsNullOrWhiteSpace(_sessionManager?.GetServerInstructionsCache(s.Id)));
                if (anyMissing)
                {
                    try
                    {
                        var waitMs = directWaitTimeout ?? TimeSpan.FromMilliseconds(500);
                        await Task.WhenAny(EnsureBackendsInitializedAsync(), Task.Delay(waitMs));
                    }
                    catch
                    {
                    }
                }

                if (enabledServers.Count == 1)
                {
                    var singleInstructions = _sessionManager?.GetServerInstructionsCache(enabledServers[0].Id);
                    if (!string.IsNullOrWhiteSpace(singleInstructions))
                    {
                        return singleInstructions!;
                    }
                }
                else
                {
                    var multiInstructions = new List<string>();
                    foreach (var srv in enabledServers)
                    {
                        if (!await IsServerAuthorizedForInstructionsAsync(srv.Id, httpContext))
                        {
                            continue;
                        }
                        var instr = _sessionManager?.GetServerInstructionsCache(srv.Id);
                        if (!string.IsNullOrWhiteSpace(instr))
                        {
                            var label = !string.IsNullOrWhiteSpace(srv.DisplayName) ? $"{srv.DisplayName} ({srv.Id})" : srv.Id;
                            multiInstructions.Add($"[{label}]: {instr}");
                        }
                    }

                    if (multiInstructions.Count == 1 && enabledServers.Count == 1)
                    {
                        return multiInstructions[0];
                    }
                    if (multiInstructions.Count > 0)
                    {
                        return string.Join("\n\n", multiInstructions);
                    }
                }
            }

            var cachedDownstream = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var srv in enabledServers)
            {
                if (!await IsServerAuthorizedForInstructionsAsync(srv.Id, httpContext))
                {
                    continue;
                }
                var instr = _sessionManager?.GetServerInstructionsCache(srv.Id);
                if (!string.IsNullOrWhiteSpace(instr))
                {
                    cachedDownstream[srv.Id] = instr!;
                }
            }

            return GatewayMetadata.BuildGatewayInstructions(enableCodeMode, cachedDownstream);
        }
    }
}
