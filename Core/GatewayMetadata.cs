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
        /// version (case-insensitively), it returns the canonical matching string. If unrecognized or omitted,
        /// it falls back to the primary supported ProtocolVersion per MCP specification.
        /// </summary>
        public static string NegotiateProtocolVersion(string? requestedVersion)
        {
            if (string.IsNullOrWhiteSpace(requestedVersion))
            {
                return ProtocolVersion;
            }
            var trimmed = requestedVersion.Trim();
            var match = SupportedProtocolVersions.FirstOrDefault(v => string.Equals(v, trimmed, StringComparison.OrdinalIgnoreCase));
            return match ?? ProtocolVersion;
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
                else if (doc.RootElement.TryGetProperty("protocolVersion", out _))
                {
                    return ExtractRequestedProtocolVersion(doc.RootElement);
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

        /// <summary>
        /// Builds the MCP 2026-07-28 top-level instructions string returned in InitializeResult
        /// so MCP clients and coding agents automatically discover Meta-Mode, Code Mode, and resource/prompt catalogs.
        /// </summary>
        public static string BuildGatewayInstructions(bool enableCodeMode = true)
        {
            if (enableCodeMode)
            {
                return "Model Context Gateway (MCG) unified MCP router. " +
                       "Use `search_tools` (supports `format: \"typescript\"` and `include_typescript: true`) to discover tools across connected servers. " +
                       "Use `execute_tool` for single tool calls or `execute_script` (Code Mode) to run multi-step JavaScript/TypeScript workflows in a sandboxed Jint engine using `await tools.<server>.<tool>(args)` and `await mcg.readResource(uri)` / `await mcg.searchResources(query)`. " +
                       "Read `router://code-mode/guide` and `router://code-mode/sdk.d.ts` (or `router://code-mode/types/{server_name}`) for TypeScript declarations, and `router://resources/catalog` or `router://resources/server/{server_name}` for compacted downstream resource discovery.";
            }

            return "Model Context Gateway (MCG) unified MCP router. " +
                   "Use `search_tools` to discover tools across connected servers and `execute_tool` to invoke them. " +
                   "Read `router://resources/catalog` or `router://resources/server/{server_name}` for compacted downstream resource discovery, and `router://prompts/catalog` for available prompts.";
        }
    }
}
