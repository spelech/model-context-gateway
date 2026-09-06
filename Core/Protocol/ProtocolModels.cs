using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModelContextGateway.Core.Protocol
{
    // Base JSON-RPC 2.0 Types
    public class JsonRpcMessage
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";
    }

    public class JsonRpcMessageConverter : JsonConverter<JsonRpcMessage>
    {
        public override JsonRpcMessage? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using (var doc = JsonDocument.ParseValue(ref reader))
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                bool hasMethod = root.TryGetProperty("method", out _);
                bool hasId = root.TryGetProperty("id", out _);
                bool hasResult = root.TryGetProperty("result", out _);
                bool hasError = root.TryGetProperty("error", out _);

                // Create options copy with JsonRpcMessageConverter removed to avoid recursive calls
                var newOptions = new JsonSerializerOptions(options);
                for (int i = newOptions.Converters.Count - 1; i >= 0; i--)
                {
                    if (newOptions.Converters[i] is JsonRpcMessageConverter)
                    {
                        newOptions.Converters.RemoveAt(i);
                    }
                }

                // Prioritize response indicators result and error over method when id is present
                if (hasId && (hasResult || hasError))
                {
                    return JsonSerializer.Deserialize<JsonRpcResponse>(root.GetRawText(), newOptions);
                }

                // If it has id, but doesn't have method, it's definitely a response (even if result/error are missing or null)
                if (hasId && !hasMethod)
                {
                    return JsonSerializer.Deserialize<JsonRpcResponse>(root.GetRawText(), newOptions);
                }

                if (hasMethod)
                {
                    if (hasId)
                    {
                        return JsonSerializer.Deserialize<JsonRpcRequest>(root.GetRawText(), newOptions);
                    }
                    else
                    {
                        return JsonSerializer.Deserialize<JsonRpcNotification>(root.GetRawText(), newOptions);
                    }
                }
                else if (hasId)
                {
                    return JsonSerializer.Deserialize<JsonRpcResponse>(root.GetRawText(), newOptions);
                }
                else
                {
                    return JsonSerializer.Deserialize<JsonRpcMessage>(root.GetRawText(), newOptions);
                }
            }
        }

        public override void Write(Utf8JsonWriter writer, JsonRpcMessage value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            var newOptions = new JsonSerializerOptions(options);
            for (int i = newOptions.Converters.Count - 1; i >= 0; i--)
            {
                if (newOptions.Converters[i] is JsonRpcMessageConverter)
                {
                    newOptions.Converters.RemoveAt(i);
                }
            }
            JsonSerializer.Serialize(writer, value, value.GetType(), newOptions);
        }
    }

    public class JsonRpcRequest : JsonRpcMessage
    {
        [JsonPropertyName("id")]
        public object? Id { get; set; } // Can be string or number

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("params")]
        public JsonElement? Params { get; set; }

        [JsonPropertyName("_meta")]
        public JsonElement? Meta { get; set; }
    }

    public class JsonRpcNotification : JsonRpcMessage
    {
        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("params")]
        public JsonElement? Params { get; set; }

        [JsonPropertyName("_meta")]
        public JsonElement? Meta { get; set; }
    }

    public class JsonRpcResponse : JsonRpcMessage
    {
        [JsonPropertyName("id")]
        public object? Id { get; set; }

        [JsonPropertyName("result")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? Result { get; set; }

        [JsonPropertyName("error")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonRpcError? Error { get; set; }

        [JsonPropertyName("_meta")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? Meta { get; set; }
    }

    public class JsonRpcError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public JsonElement? Data { get; set; }
    }

    // Specific MCP Models
    public class McpInitializeParams
    {
        [JsonPropertyName("protocolVersion")]
        public string ProtocolVersion { get; set; } = GatewayMetadata.ProtocolVersion;

        [JsonPropertyName("capabilities")]
        public JsonElement? Capabilities { get; set; }

        [JsonPropertyName("clientInfo")]
        public McpClientInfo ClientInfo { get; set; } = new();
    }

    public class McpClientInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = GatewayMetadata.DefaultName;

        [JsonPropertyName("version")]
        public string Version { get; set; } = GatewayMetadata.Version;
    }

    public static class ProtocolHelper
    {
        public static object EnsureResultType(object? result, string defaultResultType = "complete")
        {
            if (result == null)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["resultType"] = defaultResultType
                };
            }

            if (result is JsonElement je && je.ValueKind == JsonValueKind.Object)
            {
                if (je.TryGetProperty("resultType", out _))
                {
                    return je;
                }

                var dict = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["resultType"] = defaultResultType
                };
                foreach (var prop in je.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.Clone();
                }
                return dict;
            }

            try
            {
                var element = JsonSerializer.SerializeToElement(result);
                if (element.ValueKind == JsonValueKind.Object)
                {
                    if (element.TryGetProperty("resultType", out _))
                    {
                        return result;
                    }

                    var dict = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["resultType"] = defaultResultType
                    };
                    foreach (var prop in element.EnumerateObject())
                    {
                        dict[prop.Name] = prop.Value.Clone();
                    }
                    return dict;
                }
            }
            catch
            {
                // Fallback if serialization fails
            }

            return result;
        }
    }

    public static class TraceContextHelper
    {
        public static readonly System.Diagnostics.ActivitySource ActivitySource = new("ModelContextGateway", GatewayMetadata.Version);

        public static void ExtractAndApplyTraceContext(Microsoft.AspNetCore.Http.HttpContext context, JsonElement? metaElement)
        {
            string? traceparent = context.Request.Headers["traceparent"].FirstOrDefault();
            string? tracestate = context.Request.Headers["tracestate"].FirstOrDefault();
            string? baggage = context.Request.Headers["baggage"].FirstOrDefault();

            if (metaElement.HasValue && metaElement.Value.ValueKind == JsonValueKind.Object)
            {
                var metaObj = metaElement.Value;
                if (string.IsNullOrEmpty(traceparent) && metaObj.TryGetProperty("traceparent", out var tpProp))
                {
                    traceparent = tpProp.GetString();
                }
                if (string.IsNullOrEmpty(tracestate) && metaObj.TryGetProperty("tracestate", out var tsProp))
                {
                    tracestate = tsProp.GetString();
                }
                if (string.IsNullOrEmpty(baggage) && metaObj.TryGetProperty("baggage", out var bgProp))
                {
                    baggage = bgProp.GetString();
                }
                if (metaObj.TryGetProperty("io.modelcontextprotocol/trace", out var traceObj) && traceObj.ValueKind == JsonValueKind.Object)
                {
                    if (string.IsNullOrEmpty(traceparent) && traceObj.TryGetProperty("traceparent", out var otpProp))
                    {
                        traceparent = otpProp.GetString();
                    }
                    if (string.IsNullOrEmpty(tracestate) && traceObj.TryGetProperty("tracestate", out var otsProp))
                    {
                        tracestate = otsProp.GetString();
                    }
                    if (string.IsNullOrEmpty(baggage) && traceObj.TryGetProperty("baggage", out var obgProp))
                    {
                        baggage = obgProp.GetString();
                    }
                }
            }

            if (!string.IsNullOrEmpty(traceparent))
            {
                context.Items["MCP_TRACE_PARENT"] = traceparent;
                if (System.Diagnostics.ActivityContext.TryParse(traceparent, tracestate, out var parentContext))
                {
                    var activity = ActivitySource.StartActivity("McpRequest", System.Diagnostics.ActivityKind.Server, parentContext);
                    if (activity != null)
                    {
                        if (!string.IsNullOrEmpty(baggage))
                        {
                            foreach (var pair in baggage.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            {
                                var parts = pair.Split('=', 2);
                                if (parts.Length == 2)
                                {
                                    activity.AddBaggage(Uri.UnescapeDataString(parts[0]), Uri.UnescapeDataString(parts[1]));
                                }
                            }
                        }
                    }
                }
            }
            if (!string.IsNullOrEmpty(tracestate))
            {
                context.Items["MCP_TRACE_STATE"] = tracestate;
            }
            if (!string.IsNullOrEmpty(baggage))
            {
                context.Items["MCP_BAGGAGE"] = baggage;
            }
        }
    }
}
