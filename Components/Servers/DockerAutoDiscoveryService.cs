using System.Net.Sockets;
using System.Text.Json;
using Dapper;

namespace ModelContextGateway.Components.Servers
{
    public class DockerAutoDiscoveryService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DockerAutoDiscoveryService> _logger;
        private readonly string _socketPath = "/var/run/docker.sock";

        public DockerAutoDiscoveryService(IServiceProvider serviceProvider, ILogger<DockerAutoDiscoveryService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Docker Auto-Discovery Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                if (!File.Exists(_socketPath))
                {
                    _logger.LogTrace("Docker socket not found at {SocketPath}. Skipping auto-discovery scan.", _socketPath);
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                try
                {
                    await ScanContainersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during Docker auto-discovery scan.");
                }

                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }

        private async Task ScanContainersAsync(CancellationToken stoppingToken)
        {
            var udsEndPoint = new UnixDomainSocketEndPoint(_socketPath);
            using var handler = new SocketsHttpHandler
            {
                ConnectCallback = async (context, cancellationToken) =>
                {
                    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    try
                    {
                        await socket.ConnectAsync(udsEndPoint, cancellationToken);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
            };

            using var httpClient = new HttpClient(handler);
            var response = await httpClient.GetStringAsync("http://localhost/containers/json", stoppingToken);

            using var doc = JsonDocument.Parse(response);
            using var configScope = _serviceProvider.CreateScope();
            var config = configScope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            var allowedIpRanges = config.GetSection("Security:AllowedIpRanges").Get<string[]>() ?? Array.Empty<string>();

            var discoveredServers = ParseDiscoveredServers(doc.RootElement, _logger, allowedIpRanges);

            using var scope = _serviceProvider.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            var sessionManager = scope.ServiceProvider.GetRequiredService<SessionManager>();

            UpsertDiscoveredServers(discoveredServers, dbFactory, sessionManager, _logger);
        }

        public static List<McpServer> ParseDiscoveredServers(JsonElement rootElement, Microsoft.Extensions.Logging.ILogger logger, string[] allowedIpRanges)
        {
            var discoveredServers = new List<McpServer>();

            if (rootElement.ValueKind != JsonValueKind.Array)
            {
                return discoveredServers;
            }

            foreach (var container in rootElement.EnumerateArray())
            {
                if (!container.TryGetProperty("Labels", out var labelsProp) || labelsProp.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                // Check if mcp.enabled is true
                bool mcpEnabled = false;
                if (labelsProp.TryGetProperty("mcp.enabled", out var enabledProp) &&
                    enabledProp.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
                {
                    mcpEnabled = true;
                }

                if (!mcpEnabled)
                {
                    continue;
                }

                // Parse ID
                if (!labelsProp.TryGetProperty("mcp.id", out var idProp) || string.IsNullOrWhiteSpace(idProp.GetString()))
                {
                    continue;
                }

                var id = idProp.GetString()!.Trim();

                // Parse Port
                if (!labelsProp.TryGetProperty("mcp.port", out var portProp) || string.IsNullOrWhiteSpace(portProp.GetString()))
                {
                    continue;
                }

                var port = portProp.GetString()!.Trim();

                // Resolve Container Name (strip leading slash)
                string containerName = "";
                if (container.TryGetProperty("Names", out var namesProp) && namesProp.ValueKind == JsonValueKind.Array && namesProp.GetArrayLength() > 0)
                {
                    var name = namesProp[0].GetString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        containerName = name.TrimStart('/');
                    }
                }

                if (string.IsNullOrEmpty(containerName))
                {
                    continue;
                }

                // Optional Display Name
                string displayName = id;
                if (labelsProp.TryGetProperty("mcp.displayName", out var dnProp) && !string.IsNullOrWhiteSpace(dnProp.GetString()))
                {
                    displayName = dnProp.GetString()!.Trim();
                }

                // Optional Alias (mcp.alias or fallback mcp.namespace)
                string? parsedAlias = null;
                if (labelsProp.TryGetProperty("mcp.alias", out var aliasProp) && !string.IsNullOrWhiteSpace(aliasProp.GetString()))
                {
                    parsedAlias = aliasProp.GetString()!.Trim();
                }
                else if (labelsProp.TryGetProperty("mcp.namespace", out var nsProp) && !string.IsNullOrWhiteSpace(nsProp.GetString()))
                {
                    parsedAlias = nsProp.GetString()!.Trim();
                }

                if (!string.IsNullOrEmpty(parsedAlias))
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(parsedAlias, "^[a-zA-Z0-9_-]+$"))
                    {
                        logger.LogWarning("Docker Auto-Discovery: Invalid alias '{Alias}' for '{Container}' - alias ignored.", parsedAlias, containerName);
                        parsedAlias = null;
                    }
                }

                // Optional Type
                string type = "sse";
                if (labelsProp.TryGetProperty("mcp.type", out var typeProp) && !string.IsNullOrWhiteSpace(typeProp.GetString()))
                {
                    type = typeProp.GetString()!.Trim().ToLowerInvariant();
                }

                // Optional Path
                string path = type == "http" ? "/mcp" : "/sse";
                if (labelsProp.TryGetProperty("mcp.path", out var pathProp) && !string.IsNullOrWhiteSpace(pathProp.GetString()))
                {
                    path = pathProp.GetString()!.Trim();
                    if (!path.StartsWith("/"))
                    {
                        path = "/" + path;
                    }
                }

                // Optional Categories
                var categories = new List<string>();
                if (labelsProp.TryGetProperty("mcp.categories", out var catProp) && !string.IsNullOrWhiteSpace(catProp.GetString()))
                {
                    categories = catProp.GetString()!.Split(',')
                        .Select(c => c.Trim())
                        .Where(c => !string.IsNullOrEmpty(c))
                        .ToList();
                }
                if (categories.Count == 0)
                {
                    categories.Add("default");
                }

                var serverUrl = $"http://{containerName}:{port}{path}";

                if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var parsedUri)
                    || (parsedUri.Scheme != "http" && parsedUri.Scheme != "https"))
                {
                    logger.LogWarning("Docker Auto-Discovery: Skipped '{Container}' — invalid URL '{Url}'.", containerName, serverUrl);
                    continue;
                }
                System.Net.IPAddress[] resolvedIps;
                try
                {
                    resolvedIps = System.Net.IPAddress.TryParse(parsedUri.Host, out var directIp)
                        ? new[] { directIp }
                        : System.Net.Dns.GetHostAddresses(parsedUri.Host);
                }
                catch (Exception exResolve)
                {
                    logger.LogDebug(exResolve, "Docker Auto-Discovery: Cannot resolve host '{Host}' for '{Container}' via DNS; permitting internal container.", parsedUri.Host, containerName);
                    resolvedIps = Array.Empty<System.Net.IPAddress>();
                }
                if (resolvedIps.Length > 0 &&
                    resolvedIps.Any(ip => SecurityValidationHelper.IsBlockedIp(ip, allowedIpRanges)))
                {
                    logger.LogWarning("Docker Auto-Discovery: Skipped '{Container}' — '{Url}' resolves to a blocked IP (SSRF).", containerName, serverUrl);
                    continue;
                }

                discoveredServers.Add(new McpServer
                {
                    Id = id,
                    Alias = parsedAlias,
                    DisplayName = displayName,
                    Url = serverUrl,
                    Type = type,
                    Enabled = true,
                    Hidden = false,
                    Categories = categories,
                    AutoDiscovered = true
                });
            }

            return discoveredServers;
        }

        public static void UpsertDiscoveredServers(List<McpServer> discoveredServers, IDbConnectionFactory dbFactory, SessionManager sessionManager, Microsoft.Extensions.Logging.ILogger logger)
        {
            using var conn = dbFactory.CreateConnection();
            ModelContextGateway.Infrastructure.Persistence.DatabaseInitializer.EnsureAliasColumn(conn);
            var rawExisting = conn.Query(@"SELECT Id, Alias, DisplayName, Url, Enabled, Hidden, Type, Categories, SecretProvider, SecretItemKey, AuthShape, CustomHeaderName, ApiKey, HeadersJson, AutoDiscovered FROM Servers").ToList();

            var existingMap = rawExisting.ToDictionary(
                s => Convert.ToString(s.Id) ?? string.Empty,
                s => new McpServer
                {
                    Id = Convert.ToString(s.Id) ?? string.Empty,
                    Alias = (string?)s.Alias,
                    DisplayName = Convert.ToString(s.DisplayName) ?? string.Empty,
                    Url = Convert.ToString(s.Url) ?? string.Empty,
                    Enabled = s.Enabled is long l ? l != 0L : Convert.ToBoolean(s.Enabled),
                    Hidden = s.Hidden is long lh ? lh != 0L : Convert.ToBoolean(s.Hidden),
                    Type = Convert.ToString(s.Type) ?? "sse",
                    Categories = !string.IsNullOrEmpty((string?)s.Categories) ? (JsonSerializer.Deserialize<List<string>>((string)s.Categories) ?? new()) : new(),
                    AutoDiscovered = s.AutoDiscovered is long ad ? ad != 0L : Convert.ToBoolean(s.AutoDiscovered)
                }
            );

            bool changed = false;

            foreach (var discovered in discoveredServers)
            {
                var catJson = JsonSerializer.Serialize(discovered.Categories);
                if (!existingMap.TryGetValue(discovered.Id, out var existing))
                {
                    logger.LogInformation("Auto-discovered new MCP server: '{DisplayName}' ({Id}) at {Url}", discovered.DisplayName, discovered.Id, discovered.Url);
                    conn.Execute(@"INSERT INTO Servers (Id, Alias, DisplayName, Url, Enabled, Hidden, Type, Categories, SecretProvider, AuthShape, AutoDiscovered) VALUES (@Id, @Alias, @DisplayName, @Url, 1, 0, @Type, @Categories, 'None', 'bearer', 1)",
                        new { discovered.Id, discovered.Alias, discovered.DisplayName, discovered.Url, discovered.Type, Categories = catJson });
                    changed = true;
                }
                else
                {
                    bool updated = false;
                    if (existing.Url != discovered.Url) { existing.Url = discovered.Url; updated = true; }
                    if (existing.Type != discovered.Type) { existing.Type = discovered.Type; updated = true; }
                    if (existing.DisplayName != discovered.DisplayName) { existing.DisplayName = discovered.DisplayName; updated = true; }
                    if (!existing.Enabled) { existing.Enabled = true; updated = true; }

                    if (!string.IsNullOrEmpty(discovered.Alias) && string.IsNullOrEmpty(existing.Alias))
                    {
                        existing.Alias = discovered.Alias;
                        updated = true;
                    }

                    var catMatch = existing.Categories.Count == discovered.Categories.Count && existing.Categories.All(c => discovered.Categories.Contains(c));
                    if (!catMatch)
                    {
                        existing.Categories = discovered.Categories;
                        updated = true;
                    }

                    if (updated)
                    {
                        logger.LogInformation("Updating auto-discovered MCP server: '{DisplayName}' ({Id})", discovered.DisplayName, discovered.Id);
                        conn.Execute(@"UPDATE Servers SET Alias = @Alias, DisplayName = @DisplayName, Url = @Url, Type = @Type, Enabled = 1, Categories = @Categories, AutoDiscovered = 1 WHERE Id = @Id",
                            new { discovered.Id, existing.Alias, discovered.DisplayName, discovered.Url, discovered.Type, Categories = catJson });
                        changed = true;
                    }
                }
            }

            var activeIds = discoveredServers.Select(s => s.Id).ToHashSet();
            var idsToDisable = new List<string>();

            foreach (var existing in existingMap.Values)
            {
                if (existing.AutoDiscovered && !activeIds.Contains(existing.Id) && existing.Enabled)
                {
                    logger.LogInformation("Auto-discovered MCP server container stopped/removed. Disabling: '{DisplayName}' ({Id})", existing.DisplayName, existing.Id);
                    idsToDisable.Add(existing.Id);
                }
            }

            if (idsToDisable.Count > 0)
            {
                conn.Execute(@"UPDATE Servers SET Enabled = 0 WHERE Id IN @Ids", new { Ids = idsToDisable });
                changed = true;
            }

            if (changed)
            {
                sessionManager.ResetAll();
            }
        }
    }
}


