using System.Text;
using System.Text.Json;

namespace ModelContextGateway.Core.CodeMode
{
    public static partial class CodeModeTypeGenerator
    {
        private static void AppendToolMethodDeclaration(StringBuilder sb, ParsedToolInfo tool, string indent)
        {
            var hasDesc = !string.IsNullOrWhiteSpace(tool.Description);
            var paramDocs = ExtractParameterDocs(tool.InputSchema);

            if (hasDesc || paramDocs.Count > 0)
            {
                sb.AppendLine($"{indent}/**");
                if (hasDesc)
                {
                    sb.AppendLine($"{indent} * {SanitizeComment(tool.Description!)}");
                }
                foreach (var p in paramDocs)
                {
                    sb.AppendLine($"{indent} * @param args.{p.Name} {SanitizeComment(p.Description)}");
                }
                sb.AppendLine($"{indent} */");
            }

            var methodKey = FormatPropertyKey(tool.RawToolName);
            var argsType = FormatInputSchemaAsTypeScriptType(tool.InputSchema, indent, out bool hasRequiredProps);
            var optionalMod = hasRequiredProps ? "" : "?";
            sb.AppendLine($"{indent}{methodKey}(args{optionalMod}: {argsType}, options?: McgCallOptions): Promise<any>;");
        }

        private static string FormatSingleToolSignature(ParsedToolInfo tool)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(tool.Description))
            {
                sb.AppendLine($"/** {SanitizeComment(tool.Description!)} */");
            }
            var argsType = FormatInlineObjectSchema(tool.InputSchema, out bool hasRequired);
            var optionalMod = hasRequired ? "" : "?";
            var serverProp = SafeIdentifierRegex.IsMatch(tool.ServerPrefix) ? $".{tool.ServerPrefix}" : $"[\"{tool.ServerPrefix}\"]";
            var toolProp = SafeIdentifierRegex.IsMatch(tool.RawToolName) ? $".{tool.RawToolName}" : $"[\"{tool.RawToolName}\"]";
            sb.Append($"await tools{serverProp}{toolProp}(args{optionalMod}: {argsType}): Promise<any>;");
            return sb.ToString();
        }

        private static string FormatInputSchemaAsTypeScriptType(JsonElement? schema, string indent, out bool hasRequiredProps)
        {
            hasRequiredProps = false;
            if (!schema.HasValue || schema.Value.ValueKind != JsonValueKind.Object)
            {
                return "Record<string, any>";
            }

            var root = schema.Value;
            if (!root.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object)
            {
                return "Record<string, any>";
            }

            var propList = props.EnumerateObject().ToList();
            if (propList.Count == 0)
            {
                return "Record<string, any>";
            }

            var requiredSet = new HashSet<string>(StringComparer.Ordinal);
            if (root.TryGetProperty("required", out var reqArr) && reqArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in reqArr.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && item.GetString() is string s)
                    {
                        requiredSet.Add(s);
                    }
                }
            }

            hasRequiredProps = requiredSet.Count > 0;
            var sb = new StringBuilder();
            sb.AppendLine("{");
            foreach (var prop in propList)
            {
                var isReq = requiredSet.Contains(prop.Name);
                var opt = isReq ? "" : "?";
                var tsType = MapJsonSchemaPropertyToTsType(prop.Value);
                if (prop.Value.ValueKind == JsonValueKind.Object &&
                    prop.Value.TryGetProperty("description", out var dProp) &&
                    dProp.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(dProp.GetString()))
                {
                    sb.AppendLine($"{indent}  /** {SanitizeComment(dProp.GetString()!)} */");
                }
                sb.AppendLine($"{indent}  {FormatPropertyKey(prop.Name)}{opt}: {tsType};");
            }
            sb.Append($"{indent}}}");
            return sb.ToString();
        }

        private static string FormatInlineObjectSchema(JsonElement? schema, out bool hasRequiredProps)
        {
            hasRequiredProps = false;
            if (!schema.HasValue || schema.Value.ValueKind != JsonValueKind.Object)
            {
                return "Record<string, any>";
            }

            var root = schema.Value;
            if (!root.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object)
            {
                return "Record<string, any>";
            }

            var propList = props.EnumerateObject().ToList();
            if (propList.Count == 0)
            {
                return "Record<string, any>";
            }

            var requiredSet = new HashSet<string>(StringComparer.Ordinal);
            if (root.TryGetProperty("required", out var reqArr) && reqArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in reqArr.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && item.GetString() is string s)
                    {
                        requiredSet.Add(s);
                    }
                }
            }

            hasRequiredProps = requiredSet.Count > 0;
            var parts = new List<string>(propList.Count);
            foreach (var prop in propList)
            {
                var isReq = requiredSet.Contains(prop.Name);
                var opt = isReq ? "" : "?";
                var tsType = MapJsonSchemaPropertyToTsType(prop.Value);
                parts.Add($"{FormatPropertyKey(prop.Name)}{opt}: {tsType}");
            }
            return "{ " + string.Join("; ", parts) + " }";
        }

        private static string MapJsonSchemaPropertyToTsType(JsonElement propSchema)
        {
            if (propSchema.ValueKind != JsonValueKind.Object)
            {
                return "any";
            }

            if (propSchema.TryGetProperty("enum", out var enumArr) && enumArr.ValueKind == JsonValueKind.Array)
            {
                var literals = new List<string>();
                foreach (var item in enumArr.EnumerateArray())
                {
                    literals.Add(item.ValueKind == JsonValueKind.String
                        ? JsonSerializer.Serialize(item.GetString())
                        : item.GetRawText());
                }
                if (literals.Count > 0)
                {
                    return string.Join(" | ", literals);
                }
            }

            string? typeStr = null;
            if (propSchema.TryGetProperty("type", out var typeProp))
            {
                if (typeProp.ValueKind == JsonValueKind.String)
                {
                    typeStr = typeProp.GetString();
                }
                else if (typeProp.ValueKind == JsonValueKind.Array)
                {
                    var unionTypes = new List<string>();
                    foreach (var t in typeProp.EnumerateArray())
                    {
                        if (t.ValueKind == JsonValueKind.String)
                        {
                            unionTypes.Add(MapPrimitiveType(t.GetString()));
                        }
                    }
                    if (unionTypes.Count > 0)
                    {
                        return string.Join(" | ", unionTypes.Distinct());
                    }
                }
            }

            if (typeStr == "array")
            {
                if (propSchema.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Object)
                {
                    var itemType = MapJsonSchemaPropertyToTsType(itemsProp);
                    return itemType.Contains(" | ") ? $"({itemType})[]" : $"{itemType}[]";
                }
                return "any[]";
            }

            if (typeStr == "object")
            {
                if (propSchema.TryGetProperty("properties", out _))
                {
                    return FormatInlineObjectSchema(propSchema, out _);
                }
                return "Record<string, any>";
            }

            return MapPrimitiveType(typeStr);
        }

        private static string MapPrimitiveType(string? jsonType) => jsonType switch
        {
            "string" => "string",
            "integer" or "number" => "number",
            "boolean" => "boolean",
            "null" => "null",
            _ => "any"
        };

        private static List<(string Name, string Description)> ExtractParameterDocs(JsonElement? schema)
        {
            var result = new List<(string Name, string Description)>();
            if (!schema.HasValue || schema.Value.ValueKind != JsonValueKind.Object)
            {
                return result;
            }
            if (schema.Value.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in props.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object &&
                        prop.Value.TryGetProperty("description", out var descProp) &&
                        descProp.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(descProp.GetString()))
                    {
                        result.Add((prop.Name, descProp.GetString()!));
                    }
                }
            }
            return result;
        }

        private static List<ParsedToolInfo> ParseToolList(IEnumerable<object> tools)
        {
            var list = new List<ParsedToolInfo>();
            foreach (var item in tools)
            {
                var parsed = ParseSingleTool(item);
                if (parsed != null)
                {
                    list.Add(parsed);
                }
            }
            return list;
        }

        private static ParsedToolInfo? ParseSingleTool(object? item)
        {
            if (item == null)
            {
                return null;
            }

            JsonElement element;
            if (item is JsonElement je)
            {
                element = je;
            }
            else
            {
                try
                {
                    element = JsonSerializer.SerializeToElement(item);
                }
                catch
                {
                    return null;
                }
            }

            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("name", out var nameProp))
            {
                return null;
            }

            var fullName = nameProp.GetString();
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            string serverPrefix = "router";
            string rawToolName = fullName;

            if (fullName.Contains("__", StringComparison.Ordinal))
            {
                var idx = fullName.IndexOf("__", StringComparison.Ordinal);
                serverPrefix = fullName.Substring(0, idx);
                rawToolName = fullName.Substring(idx + 2);
            }
            else if (fullName.Contains('/'))
            {
                var idx = fullName.IndexOf('/');
                serverPrefix = fullName.Substring(0, idx);
                rawToolName = fullName.Substring(idx + 1);
            }
            else if (fullName.Contains(':'))
            {
                var idx = fullName.IndexOf(':');
                serverPrefix = fullName.Substring(0, idx);
                rawToolName = fullName.Substring(idx + 1);
            }

            string? description = null;
            if (element.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String)
            {
                description = descProp.GetString();
            }

            JsonElement? inputSchema = null;
            if (element.TryGetProperty("inputSchema", out var schemaProp) && schemaProp.ValueKind == JsonValueKind.Object)
            {
                inputSchema = schemaProp.Clone();
            }

            return new ParsedToolInfo
            {
                FullName = fullName,
                ServerPrefix = serverPrefix,
                RawToolName = rawToolName,
                Description = description,
                InputSchema = inputSchema
            };
        }

        private static string ToSafeTypeIdentifier(string raw)
        {
            var chars = raw.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
            return new string(chars);
        }

        private static string FormatPropertyKey(string key)
        {
            return SafeIdentifierRegex.IsMatch(key) ? key : JsonSerializer.Serialize(key);
        }

        private static string SanitizeComment(string comment)
        {
            return comment.Replace("*/", "* /").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private sealed class ParsedToolInfo
        {
            public string FullName { get; set; } = string.Empty;
            public string ServerPrefix { get; set; } = string.Empty;
            public string RawToolName { get; set; } = string.Empty;
            public string? Description { get; set; }
            public JsonElement? InputSchema { get; set; }
        }
    }
}
