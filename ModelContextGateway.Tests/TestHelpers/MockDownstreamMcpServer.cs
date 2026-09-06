using System.Net;
using System.Text;
using System.Text.Json;

namespace ModelContextGateway.Tests.TestHelpers
{
    /// <summary>
    /// Reusable test helper and HttpMessageHandler simulating downstream MCP servers.
    /// Handles standard JSON-RPC 2.0 messages (initialize, notifications/initialized, tools/list, tools/call)
    /// and supports configurable 401 Unauthorized status codes for auth testing.
    /// </summary>
    public class MockDownstreamMcpServer : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public string ProtocolVersion { get; set; } = "2024-11-05";
        public string ServerName { get; set; } = "MockDownstreamServer";
        public string ServerVersion { get; set; } = "1.0.0";

        public bool ReturnUnauthorized { get; set; } = false;
        public bool ReturnUnauthorizedOnToolsCall { get; set; } = false;
        public Func<HttpRequestMessage, bool>? ShouldReturnUnauthorized { get; set; }

        public List<object> Tools { get; } = new();
        public List<object> Resources { get; } = new();
        public List<object> Prompts { get; } = new();
        public Func<string, JsonElement, object>? ToolCallHandler { get; set; }
        public object? DefaultToolCallResult { get; set; }
        public object? ToolCallError { get; set; }

        public List<(HttpRequestMessage Request, string Body)> ReceivedRequests { get; } = new();
        public Func<HttpRequestMessage, string, Task<HttpResponseMessage?>>? CustomRequestHandler { get; set; }

        public MockDownstreamMcpServer()
        {
            // Add a default tool
            Tools.Add(new
            {
                name = "mock_tool",
                description = "A mock tool for testing",
                inputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>()
                }
            });
        }

        public HttpClient CreateHttpClient()
        {
            return new HttpClient(this);
        }

        public void AddTool(string name, string description, object? inputSchema = null)
        {
            Tools.Add(new
            {
                name,
                description,
                inputSchema = inputSchema ?? new
                {
                    type = "object",
                    properties = new Dictionary<string, object>()
                }
            });
        }

        public void AddResource(string uri, string name, string? description = null, string? mimeType = null)
        {
            Resources.Add(new
            {
                uri,
                name,
                description,
                mimeType
            });
        }

        public void AddPrompt(string name, string? description = null)
        {
            Prompts.Add(new
            {
                name,
                description
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = string.Empty;
            if (request.Content != null)
            {
                body = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            ReceivedRequests.Add((request, body));

            if (ShouldReturnUnauthorized?.Invoke(request) == true || ReturnUnauthorized)
            {
                return CreateUnauthorizedResponse();
            }

            if (CustomRequestHandler != null)
            {
                var customResp = await CustomRequestHandler(request, body);
                if (customResp != null)
                {
                    return customResp;
                }
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            string? method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
            object? id = null;
            if (root.TryGetProperty("id", out var idElem))
            {
                id = idElem.ValueKind switch
                {
                    JsonValueKind.Number => idElem.GetInt64(),
                    JsonValueKind.String => idElem.GetString(),
                    _ => idElem.GetRawText()
                };
            }

            if (id == null)
            {
                // Notifications per JSON-RPC 2.0 §4 / MCP Streamable HTTP spec: no response body, HTTP 202 Accepted
                return new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
                };
            }

            switch (method)
            {
                case "initialize":
                    return HandleInitialize(id, root);

                case "notifications/initialized":
                    return new HttpResponseMessage(HttpStatusCode.Accepted)
                    {
                        Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
                    };

                case "tools/list":
                    return HandleToolsList(id);

                case "resources/list":
                    return HandleResourcesList(id);

                case "prompts/list":
                    return HandlePromptsList(id);

                case "tools/call":
                    if (ReturnUnauthorizedOnToolsCall)
                    {
                        return CreateUnauthorizedResponse();
                    }
                    return HandleToolsCall(id, root);

                default:
                    return CreateJsonRpcSuccess(id, new { });
            }
        }

        private HttpResponseMessage CreateUnauthorizedResponse()
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":\"401 Unauthorized\"}", Encoding.UTF8, "application/json")
            };
        }

        private HttpResponseMessage HandleInitialize(object? id, JsonElement root)
        {
            string protocolVer = ProtocolVersion;
            if (root.TryGetProperty("params", out var pElem) && pElem.TryGetProperty("protocolVersion", out var pvElem))
            {
                var reqVer = pvElem.GetString();
                if (!string.IsNullOrWhiteSpace(reqVer))
                {
                    protocolVer = ModelContextGateway.Core.GatewayMetadata.NegotiateProtocolVersion(reqVer);
                }
            }

            var result = new
            {
                protocolVersion = protocolVer,
                capabilities = new
                {
                    tools = new { listChanged = false },
                    resources = new { subscribe = false, listChanged = false },
                    prompts = new { listChanged = false }
                },
                serverInfo = new
                {
                    name = ServerName,
                    version = ServerVersion
                }
            };
            return CreateJsonRpcSuccess(id, result);
        }

        private HttpResponseMessage HandleToolsList(object? id)
        {
            var result = new
            {
                tools = Tools
            };
            return CreateJsonRpcSuccess(id, result);
        }

        private HttpResponseMessage HandleResourcesList(object? id)
        {
            var result = new
            {
                resources = Resources
            };
            return CreateJsonRpcSuccess(id, result);
        }

        private HttpResponseMessage HandlePromptsList(object? id)
        {
            var result = new
            {
                prompts = Prompts
            };
            return CreateJsonRpcSuccess(id, result);
        }

        private HttpResponseMessage HandleToolsCall(object? id, JsonElement root)
        {
            if (ToolCallError != null)
            {
                var errorResponse = new
                {
                    jsonrpc = "2.0",
                    id,
                    error = ToolCallError
                };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(errorResponse, JsonOptions), Encoding.UTF8, "application/json")
                };
            }

            string toolName = "";
            JsonElement arguments = default;
            if (root.TryGetProperty("params", out var @params))
            {
                if (@params.TryGetProperty("name", out var n))
                {
                    toolName = n.GetString() ?? "";
                }
                if (@params.TryGetProperty("arguments", out var a))
                {
                    arguments = a;
                }
            }

            object callResult;
            if (ToolCallHandler != null)
            {
                callResult = ToolCallHandler(toolName, arguments);
            }
            else if (DefaultToolCallResult != null)
            {
                callResult = DefaultToolCallResult;
            }
            else
            {
                callResult = new
                {
                    content = new[]
                    {
                        new { type = "text", text = $"Mock response for {toolName}" }
                    },
                    isError = false
                };
            }

            return CreateJsonRpcSuccess(id, callResult);
        }

        private HttpResponseMessage CreateJsonRpcSuccess(object? id, object result)
        {
            var response = new
            {
                jsonrpc = "2.0",
                id,
                result
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(response, JsonOptions), Encoding.UTF8, "application/json")
            };
        }
    }
}
