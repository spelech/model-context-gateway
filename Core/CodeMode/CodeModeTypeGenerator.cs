using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ModelContextGateway.Core.CodeMode
{
    /// <summary>
    /// Synthesizes TypeScript (.d.ts) declarations, JSDoc comments, and compact tool call signatures
    /// from MCP JSON Schema tool definitions for Code Mode discovery and IDE/agent consumption.
    /// </summary>
    public static class CodeModeTypeGenerator
    {
        private static readonly Regex SafeIdentifierRegex = new("^[a-zA-Z_$][a-zA-Z0-9_$]*$", RegexOptions.Compiled);

        public static string GenerateFullSdkDeclarations(IEnumerable<object> tools, IEnumerable<McpServer>? servers = null, IReadOnlyDictionary<string, string>? serverInstructions = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// ============================================================================");
            sb.AppendLine("// Model Context Gateway (MCG) — Code Mode TypeScript SDK Declarations (sdk.d.ts)");
            sb.AppendLine("// URI: router://code-mode/sdk.d.ts");
            sb.AppendLine("// ============================================================================");
            sb.AppendLine();
            AppendSandboxGlobalTypes(sb);
            sb.AppendLine();

            var parsedTools = ParseToolList(tools);
            var serverMap = (servers ?? Enumerable.Empty<McpServer>())
                .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var grouped = parsedTools
                .GroupBy(t => t.ServerPrefix, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var namespaceEntries = new List<(string PropertyName, string InterfaceName, string? Comment)>();

            foreach (var group in grouped)
            {
                var prefix = group.Key;

                McpServer? matchedServer = null;
                if (!serverMap.TryGetValue(prefix, out matchedServer))
                {
                    matchedServer = serverMap.Values.FirstOrDefault(s =>
                        !string.IsNullOrWhiteSpace(s.Alias) &&
                        string.Equals(s.Alias, prefix, StringComparison.OrdinalIgnoreCase));
                }

                var canonicalId = matchedServer?.Id ?? prefix;
                var safeTypeSuffix = ToSafeTypeIdentifier(canonicalId);
                var interfaceName = $"Server_{safeTypeSuffix}_Tools";
                var displayComment = matchedServer != null && !string.IsNullOrWhiteSpace(matchedServer.DisplayName)
                    ? $"{matchedServer.DisplayName} ({canonicalId})"
                    : canonicalId;

                string? instructions = null;
                if (serverInstructions != null)
                {
                    if (!serverInstructions.TryGetValue(canonicalId, out instructions))
                    {
                        serverInstructions.TryGetValue(prefix, out instructions);
                    }
                }

                if (!string.IsNullOrWhiteSpace(instructions))
                {
                    sb.AppendLine("/**");
                    sb.AppendLine($" * Tools exposed by backend server: {SanitizeComment(displayComment)}");
                    sb.AppendLine($" * Server Instructions: {SanitizeComment(instructions!)}");
                    sb.AppendLine(" */");
                }
                else
                {
                    sb.AppendLine($"/** Tools exposed by backend server: {SanitizeComment(displayComment)} */");
                }
                sb.AppendLine($"interface {interfaceName} {{");

                foreach (var tool in group.OrderBy(t => t.RawToolName, StringComparer.OrdinalIgnoreCase))
                {
                    AppendToolMethodDeclaration(sb, tool, indent: "  ");
                }

                sb.AppendLine("}");
                sb.AppendLine();

                namespaceEntries.Add((canonicalId, interfaceName, $"Tools for {displayComment}"));
                if (matchedServer != null && !string.IsNullOrWhiteSpace(matchedServer.Alias) &&
                    !string.Equals(matchedServer.Alias, canonicalId, StringComparison.OrdinalIgnoreCase))
                {
                    namespaceEntries.Add((matchedServer.Alias, interfaceName, $"Alias for {canonicalId} ({displayComment})"));
                }
            }

            sb.AppendLine("/** Dynamic proxy mapping server IDs (and aliases) to their downstream MCP tools. */");
            sb.AppendLine("interface ToolsNamespace {");
            foreach (var entry in namespaceEntries.DistinctBy(e => e.PropertyName, StringComparer.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(entry.Comment))
                {
                    sb.AppendLine($"  /** {SanitizeComment(entry.Comment)} */");
                }
                var propKey = FormatPropertyKey(entry.PropertyName);
                sb.AppendLine($"  {propKey}: {entry.InterfaceName};");
            }
            sb.AppendLine("  [serverName: string]: Record<string, (args?: Record<string, any>, options?: McgCallOptions) => Promise<any>>;");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("/** Global hierarchical proxy: await tools.<server>.<tool>(args) */");
            sb.AppendLine("declare const tools: ToolsNamespace;");

            return sb.ToString();
        }

        public static string GenerateServerDeclarations(string serverIdOrAlias, IEnumerable<object> tools, IEnumerable<McpServer>? servers, IReadOnlyDictionary<string, string>? serverInstructions = null)
        {
            var matched = servers?.FirstOrDefault(s =>
                string.Equals(s.Id, serverIdOrAlias, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(s.Alias) && string.Equals(s.Alias, serverIdOrAlias, StringComparison.OrdinalIgnoreCase)));
            var targetId = matched?.Id ?? serverIdOrAlias;
            string? instructions = null;
            if (serverInstructions != null)
            {
                if (!serverInstructions.TryGetValue(targetId, out instructions))
                {
                    serverInstructions.TryGetValue(serverIdOrAlias, out instructions);
                }
            }
            return GenerateServerDeclarations(serverIdOrAlias, tools, matched, instructions);
        }

        public static string GenerateServerDeclarations(string serverIdOrAlias, IEnumerable<object> tools, McpServer? server = null, string? instructions = null)
        {
            var sb = new StringBuilder();
            var targetId = server?.Id ?? serverIdOrAlias;
            var targetAlias = server?.Alias;
            var displayName = !string.IsNullOrWhiteSpace(server?.DisplayName) ? server!.DisplayName : targetId;

            sb.AppendLine($"// ============================================================================");
            sb.AppendLine($"// Code Mode TypeScript Declarations for Server: {displayName} ({targetId})");
            sb.AppendLine($"// URI: router://code-mode/types/{targetId}");
            sb.AppendLine($"// ============================================================================");
            sb.AppendLine();

            var parsedTools = ParseToolList(tools)
                .Where(t =>
                    string.Equals(t.ServerPrefix, serverIdOrAlias, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.ServerPrefix, targetId, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(targetAlias) && string.Equals(t.ServerPrefix, targetAlias, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(t => t.RawToolName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var safeTypeSuffix = ToSafeTypeIdentifier(targetId);
            var interfaceName = $"Server_{safeTypeSuffix}_Tools";

            if (!string.IsNullOrWhiteSpace(instructions))
            {
                sb.AppendLine("/**");
                sb.AppendLine($" * Tools exposed by backend server: {SanitizeComment(displayName)}");
                sb.AppendLine($" * Server Instructions: {SanitizeComment(instructions!)}");
                sb.AppendLine(" */");
            }
            else
            {
                sb.AppendLine($"/** Tools exposed by backend server: {SanitizeComment(displayName)} */");
            }
            sb.AppendLine($"interface {interfaceName} {{");
            foreach (var tool in parsedTools)
            {
                AppendToolMethodDeclaration(sb, tool, indent: "  ");
            }
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("declare const tools: {");
            sb.AppendLine($"  {FormatPropertyKey(targetId)}: {interfaceName};");
            if (!string.IsNullOrWhiteSpace(targetAlias) && !string.Equals(targetAlias, targetId, StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"  {FormatPropertyKey(targetAlias)}: {interfaceName};");
            }
            sb.AppendLine("};");

            return sb.ToString();
        }

        public static string GenerateCompactToolSignatures(IEnumerable<object> tools)
        {
            var parsed = ParseToolList(tools);
            if (parsed.Count == 0)
            {
                return "// No matching tools found.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("// Matching MCP Tool TypeScript Signatures (Code Mode)");
            foreach (var tool in parsed)
            {
                sb.AppendLine(FormatSingleToolSignature(tool));
            }
            return sb.ToString().TrimEnd();
        }

        public static string GenerateSingleToolSignature(object toolObj)
        {
            var parsed = ParseSingleTool(toolObj);
            if (parsed == null)
            {
                return string.Empty;
            }
            return FormatSingleToolSignature(parsed);
        }

        public static string GenerateCodeModeGuideMarkdown(IEnumerable<string>? serverIds = null)
        {
            var serversNote = (serverIds != null && serverIds.Any())
                ? $"\n\n### Active Servers\n{string.Join(", ", serverIds)}\n"
                : string.Empty;
            return @"# MCP Code Mode (`execute_script`) Reference Guide" + serversNote + @"

Execute multi-step MCP workflows in a single turn using the sandboxed JavaScript engine (`execute_script`).

## 1. Sandbox Globals & Dispatchers

- **`await tools.<server>.<tool>(args, options?)`**: Ergonomic proxy for calling any backend tool (automatically unboxes single-text JSON responses into native JS objects/arrays).
- **`await mcg.callTool(name, args?, options?)`**: Explicit tool dispatcher (supports `'server__tool'`, `'server/tool'`, bare `'tool'`, or `'search_tools'`).
- **`await mcg.listServers()`**: Returns active authorized servers (`[{ id, alias, displayName, categories }]`).
- **`await mcg.listResources(serverId?)`**: Lists authorized MCP resources (optionally filtered to a specific server ID or alias).
- **`await mcg.searchResources(query)`**: Searches authorized MCP resources by keyword.
- **`await mcg.readResource(uri)`**: Reads any `router://`, `logs://`, or `mcp://<server>/<uri>` resource and unboxes JSON/text content.
- **`await mcg.listPrompts(serverId?)`**: Lists authorized MCP prompts.
- **`await mcg.getPrompt(name, args?)`**: Retrieves and compiles an MCP prompt (`router__<name>` or `<server>__<name>`).
- **`args` (or `mcg.args`)**: Caller-supplied JSON object passed via `arguments` in `execute_script`.
- **`console.log(...)` / `console.warn(...)` / `console.error(...)`**: Captured in the execution envelope `logs` array.

## 2. Discovering Tool Signatures & Resources

- **Full TypeScript SDK**: Read resource `router://code-mode/sdk.d.ts`
- **Per-Server TypeScript Definitions**: Read resource `router://code-mode/types/{server_name}`
- **Compact TypeScript Search**: Call `search_tools({ query: '...', format: 'typescript' })`
- **Resource & Prompt Catalogs**: Read `router://resources/catalog`, `router://resources/server/{server_name}`, or `router://prompts/catalog`

## 3. Example Multi-Step Script

```javascript
const containers = await tools.docker.list_containers({ all: true });
const stopped = containers.filter(c => c.state !== 'running');
console.log(`Found ${stopped.length} non-running containers`);
return {
  total: containers.length,
  stoppedNames: stopped.slice(0, 10).map(c => c.names?.[0] || c.id)
};
```
";
        }

        private static void AppendSandboxGlobalTypes(StringBuilder sb)
        {
            sb.AppendLine("interface McgCallOptions {");
            sb.AppendLine("  /** Optional dynamic pass-through bearer token for downstream authentication. */");
            sb.AppendLine("  authToken?: string;");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("interface McgServerInfo {");
            sb.AppendLine("  id: string;");
            sb.AppendLine("  alias?: string | null;");
            sb.AppendLine("  displayName: string;");
            sb.AppendLine("  categories: string[];");
            sb.AppendLine("  instructions?: string | null;");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("interface McgResourceDescriptor {");
            sb.AppendLine("  uri: string;");
            sb.AppendLine("  name?: string;");
            sb.AppendLine("  description?: string;");
            sb.AppendLine("  mimeType?: string;");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("interface McgPromptDescriptor {");
            sb.AppendLine("  name: string;");
            sb.AppendLine("  description?: string;");
            sb.AppendLine("  arguments?: Array<{ name: string; description?: string; required?: boolean }>;");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("interface McgSandbox {");
            sb.AppendLine("  /** Invoke any downstream tool or meta-tool ('server__tool' or 'search_tools') and auto-unbox JSON results. */");
            sb.AppendLine("  callTool(name: string, args?: Record<string, any>, options?: McgCallOptions): Promise<any>;");
            sb.AppendLine("  /** List all active authorized MCP backend servers. */");
            sb.AppendLine("  listServers(): Promise<McgServerInfo[]>;");
            sb.AppendLine("  /** List authorized MCP resources, optionally filtered by backend server ID or alias. */");
            sb.AppendLine("  listResources(serverId?: string): Promise<McgResourceDescriptor[]>;");
            sb.AppendLine("  /** Search authorized MCP resources by keyword or natural language query. */");
            sb.AppendLine("  searchResources(query: string): Promise<McgResourceDescriptor[]>;");
            sb.AppendLine("  /** Read an MCP resource ('router://...', 'logs://...', or 'mcp://<server>/<uri>') and unbox its content. */");
            sb.AppendLine("  readResource(uri: string): Promise<any>;");
            sb.AppendLine("  /** List authorized MCP prompts, optionally filtered by backend server ID or alias. */");
            sb.AppendLine("  listPrompts(serverId?: string): Promise<McgPromptDescriptor[]>;");
            sb.AppendLine("  /** Retrieve and compile an MCP prompt by name ('router__<prompt>' or '<server>__<prompt>'). */");
            sb.AppendLine("  getPrompt(name: string, args?: Record<string, string>): Promise<any>;");
            sb.AppendLine("  /** Proxy mapping server IDs to downstream tools. */");
            sb.AppendLine("  tools: ToolsNamespace;");
            sb.AppendLine("  /** Caller-supplied arguments object passed to execute_script. */");
            sb.AppendLine("  args: Record<string, any> | undefined;");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("declare const mcg: McgSandbox;");
            sb.AppendLine("declare const callTool: McgSandbox[\"callTool\"];");
            sb.AppendLine("declare const args: Record<string, any> | undefined;");
        }

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
