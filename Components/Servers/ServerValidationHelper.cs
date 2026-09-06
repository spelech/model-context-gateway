namespace ModelContextGateway.Components.Servers
{
    public static class ServerValidationHelper
    {
        public static bool IsValidStdioCommand(string? commandLine, out string? errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                errorMessage = "Command line cannot be empty.";
                return false;
            }

            var trimmed = commandLine.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "STDIO command cannot be an HTTP or HTTPS URL.";
                return false;
            }

            var parsed = StdioTransport.ParseCommandLine(commandLine);
            if (parsed.Count == 0)
            {
                errorMessage = "Command line must contain a valid executable.";
                return false;
            }

            var executable = parsed[0];
            char[] unsafeChars = { ';', '&', '|', '<', '>', '\n', '\r', '`', '$', '*' };
            if (executable.Any(c => unsafeChars.Contains(c)) || parsed.Any(p => p.Any(c => unsafeChars.Contains(c))))
            {
                errorMessage = "Command contains disallowed unsafe characters.";
                return false;
            }

            var lowerExec = Path.GetFileNameWithoutExtension(executable).ToLowerInvariant();
            string[] blockedExecutables = { "sh", "bash", "cmd", "powershell", "pwsh", "zsh" };
            if (blockedExecutables.Contains(lowerExec))
            {
                errorMessage = $"Direct invocation of shell '{executable}' is blocked under the security policy.";
                return false;
            }

            return true;
        }

        public static bool IsValidServerUrl(string? url, IConfiguration config, out string? errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                errorMessage = "Server URL cannot be empty.";
                return false;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                errorMessage = "Server URL must be a valid HTTP or HTTPS URI.";
                return false;
            }

            var host = uri.Host;
            var allowedIpRanges = config.GetSection("Security:AllowedIpRanges").Get<string[]>() ?? Array.Empty<string>();

            try
            {
                System.Net.IPAddress[] ipAddresses;
                if (System.Net.IPAddress.TryParse(host, out var directIp))
                {
                    ipAddresses = new[] { directIp };
                }
                else
                {
                    ipAddresses = System.Net.Dns.GetHostAddresses(host);
                }

                foreach (var ip in ipAddresses)
                {
                    if (SecurityValidationHelper.IsBlockedIp(ip, allowedIpRanges))
                    {
                        errorMessage = $"Access to IP address '{ip}' for host '{host}' is blocked for security (SSRF protection).";
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to resolve host '{host}': {ex.Message}";
                return false;
            }

            return true;
        }

        public static string? ValidateAlias(string? alias, string serverId, IEnumerable<McpServer> existingServers)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return null;
            }

            var trimmed = alias.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, "^[a-zA-Z0-9_-]+$"))
            {
                return "Server Alias may only contain letters, numbers, underscores, and hyphens.";
            }

            if (existingServers.Any(s => !string.Equals(s.Id, serverId, StringComparison.OrdinalIgnoreCase) &&
                                         string.Equals(s.Id, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return $"Server Alias '{trimmed}' collides with an existing server ID.";
            }

            if (existingServers.Any(s => !string.Equals(s.Id, serverId, StringComparison.OrdinalIgnoreCase) &&
                                         string.Equals(s.Alias, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return $"Server Alias '{trimmed}' is already in use by another server.";
            }

            return null;
        }
    }
}
