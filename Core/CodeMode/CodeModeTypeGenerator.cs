using System.Text;
using System.Text.RegularExpressions;

namespace ModelContextGateway.Core.CodeMode
{
    /// <summary>
    /// Synthesizes TypeScript (.d.ts) declarations, JSDoc comments, and compact tool call signatures
    /// from MCP JSON Schema tool definitions for Code Mode discovery and IDE/agent consumption.
    /// </summary>
    public static partial class CodeModeTypeGenerator
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
                var displayName = !string.IsNullOrWhiteSpace(matchedServer?.DisplayName)
                    ? $"{matchedServer!.DisplayName} ({canonicalId})"
                    : canonicalId;

                string? srvInstructions = null;
                if (serverInstructions != null)
                {
                    if (!serverInstructions.TryGetValue(canonicalId, out srvInstructions))
                    {
                        serverInstructions.TryGetValue(prefix, out srvInstructions);
                    }
                }

                if (!string.IsNullOrWhiteSpace(srvInstructions))
                {
                    sb.AppendLine("/**");
                    sb.AppendLine($" * Tools exposed by backend server: {SanitizeComment(displayName)}");
                    sb.AppendLine($" * Server Instructions: {SanitizeComment(srvInstructions!)}");
                    sb.AppendLine(" */");
                }
                else
                {
                    sb.AppendLine($"/** Tools exposed by backend server: {SanitizeComment(displayName)} */");
                }
                sb.AppendLine($"interface {interfaceName} {{");

                foreach (var tool in group.OrderBy(t => t.RawToolName, StringComparer.OrdinalIgnoreCase))
                {
                    AppendToolMethodDeclaration(sb, tool, indent: "  ");
                }

                sb.AppendLine("}");
                sb.AppendLine();

                var namespaceComment = !string.IsNullOrWhiteSpace(srvInstructions)
                    ? $"{displayName} — {srvInstructions}"
                    : displayName;
                namespaceEntries.Add((canonicalId, interfaceName, namespaceComment));
                if (matchedServer != null &&
                    !string.IsNullOrWhiteSpace(matchedServer.Alias) &&
                    !string.Equals(matchedServer.Alias, canonicalId, StringComparison.OrdinalIgnoreCase))
                {
                    namespaceEntries.Add((matchedServer.Alias, interfaceName, $"Alias for {canonicalId} ({displayName})"));
                }
                else if (!string.Equals(prefix, canonicalId, StringComparison.OrdinalIgnoreCase))
                {
                    namespaceEntries.Add((prefix, interfaceName, $"Alias for {canonicalId}"));
                }
            }

            sb.AppendLine("interface ToolsNamespace {");
            foreach (var entry in namespaceEntries
                .GroupBy(e => e.PropertyName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First()))
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

        public static List<string> ExtractAuthorizedServerIds(IEnumerable<object> tools, IEnumerable<McpServer>? servers)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var parsed in ParseToolList(tools))
            {
                var matched = servers?.FirstOrDefault(s =>
                    string.Equals(s.Id, parsed.ServerPrefix, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(s.Alias) && string.Equals(s.Alias, parsed.ServerPrefix, StringComparison.OrdinalIgnoreCase)));
                result.Add(matched?.Id ?? parsed.ServerPrefix);
            }
            return result.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static bool IsServerInAuthorizedSet(string serverIdOrAlias, IEnumerable<string> authorizedServerIds, IEnumerable<McpServer>? servers)
        {
            var set = new HashSet<string>(authorizedServerIds, StringComparer.OrdinalIgnoreCase);
            if (set.Contains(serverIdOrAlias))
            {
                return true;
            }
            var matched = servers?.FirstOrDefault(s =>
                string.Equals(s.Id, serverIdOrAlias, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(s.Alias) && string.Equals(s.Alias, serverIdOrAlias, StringComparison.OrdinalIgnoreCase)));
            return matched != null && set.Contains(matched.Id);
        }

        public static IReadOnlyDictionary<string, string>? FilterInstructionsByAuthorizedServers(
            IReadOnlyDictionary<string, string>? instructions,
            IEnumerable<string> authorizedServerIds,
            IEnumerable<McpServer>? servers)
        {
            if (instructions == null)
            {
                return null;
            }
            var filtered = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in instructions)
            {
                if (IsServerInAuthorizedSet(kvp.Key, authorizedServerIds, servers))
                {
                    filtered[kvp.Key] = kvp.Value;
                }
            }
            return filtered;
        }
    }
}
