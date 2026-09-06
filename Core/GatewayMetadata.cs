using System.Reflection;
using System.Text.Json;

namespace ModelContextGateway.Core
{
    /// <summary>
    /// Central runtime metadata provider for Model Context Gateway (MCG).
    /// Dynamically resolves assembly version information to ensure consistent
    /// versioning across health probes, protocol handshakes, and virtual servers.
    /// </summary>
    public static class GatewayMetadata
    {
        /// <summary>
        /// Default service name for Model Context Gateway.
        /// </summary>
        public const string DefaultName = "ModelContextGateway";

        /// <summary>
        /// Service name for the in-process virtual Admin MCP server.
        /// </summary>
        public const string AdminServerName = "Model-Context-Gateway-Admin";

        /// <summary>
        /// Supported Model Context Protocol specification version.
        /// </summary>
        public const string ProtocolVersion = "2026-07-28";

        /// <summary>
        /// Legacy Model Context Protocol specification version.
        /// </summary>
        public const string LegacyProtocolVersion = "2024-11-05";

        /// <summary>
        /// List of protocol versions advertised for discovery.
        /// </summary>
        public static readonly string[] SupportedProtocolVersions = new[]
        {
            "2026-07-28",
            "2025-11-25",
            "2025-06-18",
            "2025-03-26",
            "2024-11-05",
            "2024-10-07"
        };

        /// <summary>
        /// Checks whether a protocol version string is supported by the gateway.
        /// Dynamically accepts any valid non-empty protocol version string.
        /// </summary>
        public static bool IsSupportedProtocolVersion(string? version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return true;
            }
            return SupportedProtocolVersions.Any(v => string.Equals(v, version.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Negotiates the highest compatible protocol version. If requestedVersion matches a supported
        /// version (case-insensitively), it returns the canonical matching string. If unrecognized but non-empty,
        /// it echoes the requested version to support future or custom protocol extensions gracefully.
        /// If null or whitespace, it defaults to the primary ProtocolVersion.
        /// </summary>
        public static string NegotiateProtocolVersion(string? requestedVersion)
        {
            if (string.IsNullOrWhiteSpace(requestedVersion))
            {
                return ProtocolVersion;
            }
            var trimmed = requestedVersion.Trim();
            var match = SupportedProtocolVersions.FirstOrDefault(v => string.Equals(v, trimmed, StringComparison.OrdinalIgnoreCase));
            return match ?? trimmed;
        }

        /// <summary>
        /// Extracts the client's requested protocol version from an initialize JSON-RPC payload,
        /// falling back to ProtocolVersion if omitted or malformed.
        /// </summary>
        public static string ExtractRequestedProtocolVersion(string? jsonRpcBody)
        {
            if (string.IsNullOrWhiteSpace(jsonRpcBody))
            {
                return ProtocolVersion;
            }
            try
            {
                using var doc = JsonDocument.Parse(jsonRpcBody);
                if (doc.RootElement.TryGetProperty("params", out var pElem))
                {
                    return ExtractRequestedProtocolVersion(pElem);
                }
            }
            catch
            {
            }
            return ProtocolVersion;
        }

        /// <summary>
        /// Extracts the client's requested protocol version from an initialize params JsonElement,
        /// falling back to ProtocolVersion if omitted or malformed.
        /// </summary>
        public static string ExtractRequestedProtocolVersion(JsonElement paramsElement)
        {
            try
            {
                if (paramsElement.ValueKind == JsonValueKind.Object &&
                    paramsElement.TryGetProperty("protocolVersion", out var pvElem))
                {
                    var ver = pvElem.GetString();
                    if (!string.IsNullOrWhiteSpace(ver))
                    {
                        return NegotiateProtocolVersion(ver);
                    }
                }
            }
            catch
            {
            }
            return ProtocolVersion;
        }

        /// <summary>
        /// Canonical semantic version dynamically resolved from the executing assembly.
        /// </summary>
        public static string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "5.6.0";

        /// <summary>
        /// Builds a standard JSON-RPC 2.0 initialize request payload with dynamic versioning.
        /// </summary>
        public static string BuildInitializeRequest(string id = "auto-init", string clientName = "ModelContextGatewayAuto", string? protocolVersion = null)
        {
            return JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "initialize",
                id,
                @params = new
                {
                    protocolVersion = protocolVersion ?? ProtocolVersion,
                    capabilities = new { },
                    clientInfo = new
                    {
                        name = clientName,
                        version = Version
                    }
                }
            });
        }

        /// <summary>
        /// Builds an initialize request payload for the interactive test bench.
        /// </summary>
        public static string BuildTestBenchInitializeRequest(string id = "test-init", string? protocolVersion = null)
        {
            return BuildInitializeRequest(id, "McpTestBench", protocolVersion);
        }
    }
}
