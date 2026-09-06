using System.Text.Json;
using System.Text.Json.Nodes;

namespace ModelContextGateway.Middleware
{
    /// <summary>
    /// Implements MCP 2026-07-28 Spec-Compliant Header Annotation, Protocol Negotiation,
    /// Stateless Capabilities, Trace Context Extraction, and Legacy Body Fallback.
    /// Inspects Mcp-Method, Mcp-Name, Mcp-Session-Id, and MCP-Protocol-Version HTTP headers or JSON-RPC request bodies,
    /// storing parsed metadata in HttpContext.Items for downstream endpoint execution.
    /// </summary>
    public class McpSpecMiddleware
    {
        private readonly RequestDelegate _next;

        public McpSpecMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (IsMcpPath(context.Request.Path))
            {
                string method = string.Empty;
                string itemName = string.Empty;
                string rawBody = string.Empty;
                object? id = null;
                JsonNode? paramsNode = null;
                JsonNode? metaNode = null;
                string? requestedVersion = null;

                // 1. Primary: Parse 2026-07-28 Specification Headers
                if (context.Request.Headers.TryGetValue("Mcp-Method", out var methodHeader))
                {
                    method = methodHeader.ToString();
                    if (context.Request.Headers.TryGetValue("Mcp-Name", out var nameHeader))
                    {
                        itemName = nameHeader.ToString();
                    }
                }

                if (context.Request.Headers.TryGetValue("Mcp-Session-Id", out var sessionHeader))
                {
                    context.Items["MCP_SESSION_ID"] = sessionHeader.ToString();
                }

                if (context.Request.Headers.TryGetValue("MCP-Protocol-Version", out var protoHeader))
                {
                    requestedVersion = protoHeader.ToString();
                }

                // 2. Body Inspection (for Streamable HTTP POSTs and Legacy MCP Clients)
                if (context.Request.Method == "POST")
                {
                    context.Request.EnableBuffering();
                    using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                    rawBody = await reader.ReadToEndAsync();
                    context.Request.Body.Position = 0;

                    if (!string.IsNullOrWhiteSpace(rawBody))
                    {
                        try
                        {
                            var json = JsonNode.Parse(rawBody);
                            if (json is JsonObject obj)
                            {
                                if (string.IsNullOrEmpty(method) && obj.TryGetPropertyValue("method", out var mNode) && mNode != null)
                                {
                                    method = mNode.ToString();
                                }

                                if (obj.TryGetPropertyValue("id", out var idNode) && idNode != null)
                                {
                                    if (idNode is JsonValue val)
                                    {
                                        if (val.TryGetValue<long>(out var longVal))
                                        {
                                            id = longVal;
                                        }
                                        else if (val.TryGetValue<string>(out var strVal))
                                        {
                                            id = strVal;
                                        }
                                    }
                                    else
                                    {
                                        id = idNode.ToString();
                                    }
                                }

                                if (obj.TryGetPropertyValue("params", out var pNode) && pNode != null)
                                {
                                    paramsNode = pNode;
                                }

                                if (obj.TryGetPropertyValue("_meta", out var mObjNode) && mObjNode != null)
                                {
                                    metaNode = mObjNode;

                                    if (string.IsNullOrEmpty(requestedVersion))
                                    {
                                        if (metaNode["io.modelcontextprotocol/protocolVersion"] != null)
                                        {
                                            requestedVersion = metaNode["io.modelcontextprotocol/protocolVersion"]?.ToString();
                                        }
                                        else if (metaNode["protocolVersion"] != null)
                                        {
                                            requestedVersion = metaNode["protocolVersion"]?.ToString();
                                        }
                                    }

                                    if (metaNode["io.modelcontextprotocol/clientInfo"] != null)
                                    {
                                        context.Items["MCP_CLIENT_INFO"] = metaNode["io.modelcontextprotocol/clientInfo"]?.ToString();
                                    }

                                    if (metaNode["io.modelcontextprotocol/clientCapabilities"] != null)
                                    {
                                        context.Items["MCP_CLIENT_CAPABILITIES"] = metaNode["io.modelcontextprotocol/clientCapabilities"]?.ToString();
                                    }
                                }

                                if (string.IsNullOrEmpty(itemName) && !string.IsNullOrEmpty(method))
                                {
                                    itemName = method switch
                                    {
                                        "tools/call" => paramsNode?["name"]?.ToString() ?? string.Empty,
                                        "prompts/get" => paramsNode?["name"]?.ToString() ?? string.Empty,
                                        "resources/read" => paramsNode?["uri"]?.ToString() ?? string.Empty,
                                        _ => string.Empty
                                    };
                                }
                            }
                        }
                        catch
                        {
                            // If payload is not valid JSON, let downstream handlers process it
                        }
                    }
                }

                // 3. OpenTelemetry / W3C Trace Context Extraction
                JsonElement? metaJsonElement = null;
                if (metaNode != null)
                {
                    try
                    {
                        metaJsonElement = JsonSerializer.Deserialize<JsonElement>(metaNode.ToJsonString());
                    }
                    catch { }
                }
                TraceContextHelper.ExtractAndApplyTraceContext(context, metaJsonElement);

                // 4. Protocol Version Validation
                if (!string.IsNullOrEmpty(requestedVersion) && !GatewayMetadata.IsSupportedProtocolVersion(requestedVersion))
                {
                    context.Response.StatusCode = 400;
                    context.Response.Headers.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        jsonrpc = "2.0",
                        id = id,
                        error = new
                        {
                            code = -32021,
                            message = $"Unsupported protocol version: '{requestedVersion}'. Supported versions: {string.Join(", ", GatewayMetadata.SupportedProtocolVersions)}"
                        }
                    });
                    return;
                }

                if (!string.IsNullOrEmpty(method))
                {
                    bool isNotification = method.StartsWith("notifications/", StringComparison.OrdinalIgnoreCase);

                    context.Items["MCP_METHOD"] = method;
                    context.Items["MCP_ITEM_NAME"] = itemName;
                    context.Items["MCP_RAW_BODY"] = rawBody;
                    context.Items["MCP_REQ_ID"] = id;
                    context.Items["MCP_IS_NOTIFICATION"] = isNotification;
                    context.Items["MCP_SPEC_VERSION"] = requestedVersion ?? GatewayMetadata.ProtocolVersion;
                    if (metaNode != null)
                    {
                        context.Items["MCP_META"] = metaNode.ToJsonString();
                    }
                }
            }

            await _next(context);
        }

        public static bool IsMcpPath(PathString path)
        {
            if (!path.HasValue)
            {
                return false;
            }

            var val = path.Value;
            if (string.IsNullOrEmpty(val) || val == "/")
            {
                return false;
            }

            // Exclude static assets, frontend files, health, and API endpoints
            if (path.StartsWithSegments("/api") ||
                path.StartsWithSegments("/health") ||
                path.StartsWithSegments("/oauth") ||
                path.StartsWithSegments("/.well-known") ||
                path.StartsWithSegments("/css") ||
                path.StartsWithSegments("/js") ||
                path.StartsWithSegments("/assets") ||
                path.StartsWithSegments("/swagger") ||
                val.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                val.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
                val.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
                val.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
                val.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                val.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
    }
}
