using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ModelContextGateway.Components.Authorization
{
    public static class SecurityValidationHelper
    {
        private static readonly Regex ServerIdRegex = new("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);

        /// <summary>
        /// Determines if an IP address is in a blocked range (loopback, link-local, private, CGNAT, multicast, ULA v6),
        /// unless it matches an explicitly allowed CIDR/IP range in allowedIpRanges.
        /// </summary>
        public static bool IsBlockedIp(IPAddress ip, string[]? allowedIpRanges)
        {
            if (ip == null)
            {
                return true;
            }

            // Unmap IPv4-mapped IPv6 address to IPv4 for accurate evaluation
            if (ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }

            // 1. Check explicitly allowed IP ranges first
            if (allowedIpRanges != null && allowedIpRanges.Length > 0)
            {
                foreach (var allowedRange in allowedIpRanges)
                {
                    if (IsInSubnet(ip, allowedRange))
                    {
                        return false; // Allowed explicitly
                    }
                }
            }

            // 2. Check if IP falls into any blocked ranges
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                if (IPAddress.IsLoopback(ip) || IsInSubnet(ip, "127.0.0.0/8"))
                {
                    return true; // Loopback
                }

                if (IsInSubnet(ip, "10.0.0.0/8"))
                {
                    return true;       // Private Class A
                }

                if (IsInSubnet(ip, "172.16.0.0/12"))
                {
                    return true;   // Private Class B (172.16.0.0 - 172.31.255.255)
                }

                if (IsInSubnet(ip, "192.168.0.0/16"))
                {
                    return true;  // Private Class C
                }

                if (IsInSubnet(ip, "169.254.0.0/16"))
                {
                    return true;  // Link-local (APIPA)
                }

                if (IsInSubnet(ip, "100.64.0.0/10"))
                {
                    return true;   // CGNAT (Carrier-Grade NAT)
                }

                if (IsInSubnet(ip, "224.0.0.0/4"))
                {
                    return true;     // Multicast
                }

                if (IsInSubnet(ip, "0.0.0.0/8"))
                {
                    return true;       // Current network / Broadcast
                }
            }
            else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (IPAddress.IsLoopback(ip) || IsInSubnet(ip, "::1/128"))
                {
                    return true; // IPv6 Loopback
                }

                if (ip.IsIPv6LinkLocal || IsInSubnet(ip, "fe80::/10"))
                {
                    return true;    // IPv6 Link-local
                }

                if (ip.IsIPv6Multicast || IsInSubnet(ip, "ff00::/8"))
                {
                    return true;    // IPv6 Multicast
                }

                if (IsInSubnet(ip, "fc00::/7"))
                {
                    return true;                         // IPv6 Unique Local Address (ULA fc00::/7)
                }

                if (IsInSubnet(ip, "::/128"))
                {
                    return true;                            // IPv6 Unspecified address
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if an IP address matches a CIDR range (e.g., "10.0.0.0/8", "127.0.0.1") or exact IP string or "loopback".
        /// </summary>
        public static bool IsInSubnet(IPAddress ip, string cidrOrIp)
        {
            if (ip == null || string.IsNullOrWhiteSpace(cidrOrIp))
            {
                return false;
            }

            if (ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }

            string cleanCidr = cidrOrIp.Trim();

            // Special handling for keyword "loopback" if passed as allowed range
            if (cleanCidr.Equals("loopback", StringComparison.OrdinalIgnoreCase))
            {
                return IPAddress.IsLoopback(ip);
            }

            string[] parts = cleanCidr.Split('/');
            if (!IPAddress.TryParse(parts[0], out var targetIp))
            {
                return false;
            }

            if (targetIp.IsIPv4MappedToIPv6)
            {
                targetIp = targetIp.MapToIPv4();
            }

            if (ip.AddressFamily != targetIp.AddressFamily)
            {
                return false;
            }

            byte[] ipBytes = ip.GetAddressBytes();
            byte[] targetBytes = targetIp.GetAddressBytes();
            int maxPrefix = ipBytes.Length * 8;

            int prefixLength = maxPrefix;
            if (parts.Length > 1)
            {
                if (!int.TryParse(parts[1], out prefixLength) || prefixLength < 0 || prefixLength > maxPrefix)
                {
                    return false;
                }
            }

            int fullBytes = prefixLength / 8;
            int remainingBits = prefixLength % 8;

            for (int i = 0; i < fullBytes; i++)
            {
                if (ipBytes[i] != targetBytes[i])
                {
                    return false;
                }
            }

            if (remainingBits > 0)
            {
                byte mask = (byte)(0xFF << (8 - remainingBits));
                if ((ipBytes[fullBytes] & mask) != (targetBytes[fullBytes] & mask))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsPrivateOrLoopback(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            // Ensure schema is HTTP or HTTPS
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            var host = uri.Host;

            // 1. Check if host is direct IP address
            if (IPAddress.TryParse(host, out var ipAddress))
            {
                return IsPrivateOrLoopbackAddress(ipAddress);
            }

            // 2. DNS resolve and check all resolved IPs
            try
            {
                var addresses = Dns.GetHostAddresses(host);
                if (addresses == null || addresses.Length == 0)
                {
                    return false;
                }

                return addresses.Any(IsPrivateOrLoopbackAddress);
            }
            catch
            {
                // Unresolvable host is not an active IP we can reach, but we return false
                return false;
            }
        }

        private static bool IsPrivateOrLoopbackAddress(IPAddress ipAddress)
        {
            if (IPAddress.IsLoopback(ipAddress))
            {
                return true;
            }

            if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
            {
                byte[] bytes = ipAddress.GetAddressBytes();
                // 10.0.0.0/8
                if (bytes[0] == 10)
                {
                    return true;
                }
                // 172.16.0.0/12
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                {
                    return true;
                }
                // 192.168.0.0/16
                if (bytes[0] == 192 && bytes[1] == 168)
                {
                    return true;
                }
                // 169.254.0.0/16 (Link-local)
                if (bytes[0] == 169 && bytes[1] == 254)
                {
                    return true;
                }
            }
            else if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (ipAddress.IsIPv6LinkLocal || ipAddress.IsIPv6SiteLocal)
                {
                    return true;
                }

                byte[] bytes = ipAddress.GetAddressBytes();
                // Unique Local Address (fc00::/7)
                if ((bytes[0] & 0xFE) == 0xFC)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsValidServerId(string serverId)
        {
            if (string.IsNullOrWhiteSpace(serverId))
            {
                return false;
            }

            return ServerIdRegex.IsMatch(serverId);
        }

        public static bool ValidateToolOrPromptName(string name, IEnumerable<string> validServerIds)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            // Allow native router tools and prompts
            if (name == "search_tools" || name == "execute_tool" || name == "execute_script" || name.StartsWith("router__"))
            {
                return true;
            }

            // Split must result in exactly 2 parts (one '__' separator)
            var parts = name.Split(new[] { "__" }, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                return false;
            }

            var serverId = parts[0];
            var targetName = parts[1];

            if (string.IsNullOrWhiteSpace(targetName))
            {
                return false;
            }

            return IsValidServerId(serverId) && validServerIds.Contains(serverId);
        }

        public static bool ValidateResourceUri(string uri, IEnumerable<string> validServerIds)
        {
            if (string.IsNullOrWhiteSpace(uri))
            {
                return false;
            }

            // Allow native/local resources
            if (uri.StartsWith("router://") || uri.StartsWith("logs://"))
            {
                return true;
            }

            if (uri.StartsWith("mcp://"))
            {
                // Format must be mcp://{serverId}/{resourceName}
                if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri))
                {
                    return false;
                }

                var serverId = parsedUri.Host;
                if (!IsValidServerId(serverId) || !validServerIds.Contains(serverId))
                {
                    return false;
                }

                // Prevent __ or other delimiters in the serverId/host segment
                if (serverId.Contains("__"))
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        public static void ValidateJsonUrlsRequireHttps(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in root.EnumerateObject())
                    {
                        var name = prop.Name.ToLowerInvariant();
                        if (name.Contains("url") || name.Contains("uri") || name.Contains("authority") || name.Contains("issuer") || name.Contains("endpoint") || name.Contains("address") || name.Contains("addr"))
                        {
                            var val = prop.Value.GetString();
                            if (!string.IsNullOrEmpty(val) && val.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                            {
                                if (Uri.TryCreate(val, UriKind.Absolute, out var uri))
                                {
                                    bool isLocalhost = uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                                                       uri.Host.Equals("127.0.0.1") ||
                                                       uri.Host.Equals("::1");
                                    bool isSimpleHost = !uri.Host.Contains('.');

                                    if (!isLocalhost && !isSimpleHost && !ModelContextGateway.Components.Authorization.SecurityValidationHelper.IsPrivateOrLoopback(val))
                                    {
                                        throw new ArgumentException($"URL field '{prop.Name}' must use the HTTPS scheme.");
                                    }
                                }
                                else
                                {
                                    throw new ArgumentException($"URL field '{prop.Name}' must use the HTTPS scheme.");
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // If invalid JSON, ignore and let downstream parse/handle it
            }
        }

        /// <summary>
        /// Checks whether client IP is authorized for standalone administrative access based on Admin:StandaloneAllowedNetworks.
        /// Defaults to loopback addresses (127.0.0.1, ::1).
        /// </summary>
        public static bool IsStandaloneAdminNetwork(IPAddress? clientIp, IConfiguration? config)
        {
            if (clientIp == null)
            {
                return false;
            }

            var effectiveIp = clientIp.IsIPv4MappedToIPv6 ? clientIp.MapToIPv4() : clientIp;

            var networks = new List<string>();

            var sectionValues = config?.GetSection("Admin:StandaloneAllowedNetworks")?.Get<string[]>();
            if (sectionValues != null)
            {
                foreach (var net in sectionValues)
                {
                    if (!string.IsNullOrWhiteSpace(net))
                    {
                        networks.Add(net.Trim());
                    }
                }
            }

            var singleVal = config?["Admin:StandaloneAllowedNetworks"]
                ?? config?["STANDALONE_ALLOWED_NETWORKS"]
                ?? config?["STANDALONE_ALLOWED_NETWORK"]
                ?? Environment.GetEnvironmentVariable("STANDALONE_ALLOWED_NETWORKS")
                ?? Environment.GetEnvironmentVariable("STANDALONE_ALLOWED_NETWORK");
            if (!string.IsNullOrWhiteSpace(singleVal))
            {
                var split = singleVal.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var net in split)
                {
                    if (!string.IsNullOrWhiteSpace(net) && !networks.Contains(net, StringComparer.OrdinalIgnoreCase))
                    {
                        networks.Add(net.Trim());
                    }
                }
            }

            if (networks.Count == 0)
            {
                networks.Add("127.0.0.1");
                networks.Add("::1");
            }

            foreach (var network in networks)
            {
                if (IsInSubnet(effectiveIp, network))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if an external Identity Provider (LDAP/Active Directory or OIDC) is configured.
        /// </summary>
        public static bool HasExternalIdp(IConfiguration? config, HttpContext? httpContext = null)
        {
            if (config != null)
            {
                if (!string.IsNullOrWhiteSpace(config["Ldap:Server"]) ||
                    !string.IsNullOrWhiteSpace(config["ActiveDirectory:Server"]) ||
                    !string.IsNullOrWhiteSpace(config["Oidc:Authority"]) ||
                    !string.IsNullOrWhiteSpace(config["Oidc:ClientId"]) ||
                    !string.IsNullOrWhiteSpace(config["Oidc:Issuer"]))
                {
                    return true;
                }

                if (bool.TryParse(config["Identity:ExternalIdpEnabled"], out var extEnabled) && extEnabled)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Single decision point for administrative authorization supporting both SIDs and configured Group Names,
        /// Admin AppKeys, and standalone network authorization.
        /// </summary>
        public static bool IsAdmin(
            UserIdentityContext? identity,
            IConfiguration? config,
            HttpContext? httpContext = null,
            IEnumerable<string>? mappedGroups = null)
        {
            var adminGroupSid = config?["Admin:GroupSid"] ?? "S-1-5-32-544";
            if (identity != null && identity.AllSids.Contains(adminGroupSid, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            var configuredAdminGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var singleGroupName = config?["Admin:GroupName"];
            if (!string.IsNullOrWhiteSpace(singleGroupName))
            {
                configuredAdminGroups.Add(singleGroupName.Trim());
            }
            else
            {
                configuredAdminGroups.Add("full_admin");
                configuredAdminGroups.Add("Administrator");
                configuredAdminGroups.Add("Administrators");
            }

            var adminGroupsSection = config?.GetSection("Admin:Groups")?.Get<string[]>();
            if (adminGroupsSection != null)
            {
                foreach (var g in adminGroupsSection)
                {
                    if (!string.IsNullOrWhiteSpace(g))
                    {
                        configuredAdminGroups.Add(g.Trim());
                    }
                }
            }

            if (identity != null && identity.GroupNames.Any(g => configuredAdminGroups.Contains(g)))
            {
                if (!string.IsNullOrWhiteSpace(identity.Username) &&
                    !identity.Username.Equals("guest", StringComparison.OrdinalIgnoreCase) &&
                    !identity.Username.Equals("anonymous", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (mappedGroups != null && mappedGroups.Any(mg => configuredAdminGroups.Contains(mg) || string.Equals(mg, adminGroupSid, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (httpContext != null)
            {
                var keyTypeObj = httpContext.Items.TryGetValue("AppKeyType", out var kt) ? kt as string : null;
                bool isExplicitPersonal = string.Equals(keyTypeObj, "personal", StringComparison.OrdinalIgnoreCase);

                if (!isExplicitPersonal && httpContext.Items.TryGetValue("AppKeyScopes", out var scopesObj) && scopesObj is string scopesJson)
                {
                    try
                    {
                        var scopes = JsonSerializer.Deserialize<List<string>>(scopesJson);
                        if (scopes != null && scopes.Any(s =>
                            string.Equals(s, "admin", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(s, "*", StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }
                    }
                    catch { }
                }

                if (httpContext.User?.IsInRole("Administrator") == true ||
                    httpContext.User?.HasClaim("Scope", "admin") == true)
                {
                    return true;
                }

                if (!HasExternalIdp(config, httpContext) &&
                    (identity == null || string.IsNullOrWhiteSpace(identity.Username) ||
                     identity.Username.Equals("guest", StringComparison.OrdinalIgnoreCase) ||
                     identity.Username.Equals("anonymous", StringComparison.OrdinalIgnoreCase)))
                {
                    if (IsStandaloneAdminNetwork(httpContext.Connection.RemoteIpAddress, config))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsAdmin(UserIdentityContext? identity, IConfiguration? config, IEnumerable<string>? mappedGroups)
        {
            return IsAdmin(identity, config, httpContext: null, mappedGroups: mappedGroups);
        }

        public static ValueTask<System.IO.Stream> ValidatingConnectCallback(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            return ValidatingConnectCallback(context, null, cancellationToken);
        }

        public static async ValueTask<System.IO.Stream> ValidatingConnectCallback(SocketsHttpConnectionContext context, string[]? allowedIpRanges, CancellationToken cancellationToken)
        {
            var host = context.DnsEndPoint.Host;
            var port = context.DnsEndPoint.Port;

            IPAddress[] ipAddresses;
            if (IPAddress.TryParse(host, out var directIp))
            {
                ipAddresses = new[] { directIp };
            }
            else
            {
                ipAddresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            }

            if (ipAddresses.Length == 0)
            {
                throw new HttpRequestException($"Unable to resolve host '{host}'.");
            }

            foreach (var ip in ipAddresses)
            {
                if (IsBlockedIp(ip, allowedIpRanges))
                {
                    throw new HttpRequestException($"Access to IP address '{ip}' for host '{host}' is blocked for security (SSRF protection).");
                }
            }

            Socket? socket = null;
            Exception? lastException = null;

            foreach (var ip in ipAddresses)
            {
                var s = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true
                };

                try
                {
                    await s.ConnectAsync(new IPEndPoint(ip, port), cancellationToken);
                    socket = s;
                    break;
                }
                catch (Exception ex)
                {
                    s.Dispose();
                    lastException = ex;
                }
            }

            if (socket == null)
            {
                throw new HttpRequestException($"Failed to connect to host '{host}' ({ipAddresses[0]}) on port {port}.", lastException);
            }

            return new NetworkStream(socket, ownsSocket: true);
        }
    }
}



