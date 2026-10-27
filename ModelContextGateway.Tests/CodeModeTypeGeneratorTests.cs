using System.Collections.Concurrent;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextGateway.Core.CodeMode;
using Moq;

namespace ModelContextGateway.Tests
{
    public class CodeModeTypeGeneratorTests
    {
        private static List<object> CreateSampleTools()
        {
            var dockerToolJson = @"{
                ""name"": ""docker__list_containers"",
                ""description"": ""List running or stopped Docker containers."",
                ""inputSchema"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""all"": { ""type"": ""boolean"", ""description"": ""Include stopped containers."" },
                        ""limit"": { ""type"": ""integer"", ""description"": ""Maximum containers to return."" },
                        ""status"": { ""type"": ""string"", ""enum"": [""running"", ""exited"", ""paused""], ""description"": ""Filter by status."" }
                    },
                    ""required"": [""all""]
                }
            }";

            var haToolJson = @"{
                ""name"": ""ha__turn_on"",
                ""description"": ""Turn on a Home Assistant entity."",
                ""inputSchema"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""entity_id"": { ""type"": ""string"", ""description"": ""Target entity ID (e.g. light.kitchen)."" },
                        ""brightness"": { ""type"": ""number"", ""description"": ""Brightness level 0-255."" },
                        ""rgb_color"": { ""type"": ""array"", ""items"": { ""type"": ""integer"" }, ""description"": ""RGB color tuple."" }
                    },
                    ""required"": [""entity_id""]
                }
            }";

            using var doc1 = JsonDocument.Parse(dockerToolJson);
            using var doc2 = JsonDocument.Parse(haToolJson);

            return new List<object>
            {
                doc1.RootElement.Clone(),
                doc2.RootElement.Clone()
            };
        }

        [Fact]
        [Requirement("MCP-52", "MCP", RequirementType.Positive, "CodeModeTypeGenerator synthesizes full SDK TypeScript declarations with sandbox globals and JSDoc comments.")]
        public void GenerateFullSdkDeclarations_IncludesSandboxGlobalsAndServerInterfaces()
        {
            var tools = CreateSampleTools();
            var servers = new List<McpServer>
            {
                new() { Id = "docker", DisplayName = "Docker Engine", Enabled = true },
                new() { Id = "ha", Alias = "homeassistant", DisplayName = "Home Assistant", Enabled = true }
            };

            var dts = CodeModeTypeGenerator.GenerateFullSdkDeclarations(tools, servers);

            dts.Should().Contain("interface McgSandbox");
            dts.Should().Contain("callTool(name: string, args?: Record<string, any>");
            dts.Should().Contain("listServers(): Promise<McgServerInfo[]>;");
            dts.Should().Contain("listResources(serverId?: string): Promise<McgResourceDescriptor[]>;");
            dts.Should().Contain("searchResources(query: string): Promise<McgResourceDescriptor[]>;");
            dts.Should().Contain("readResource(uri: string): Promise<any>;");
            dts.Should().Contain("listPrompts(serverId?: string): Promise<McgPromptDescriptor[]>;");
            dts.Should().Contain("getPrompt(name: string, args?: Record<string, string>): Promise<any>;");
            dts.Should().Contain("declare const tools: ToolsNamespace;");
            dts.Should().Contain("list_containers(args: {");
            dts.Should().Contain("all: boolean;");
            dts.Should().Contain("limit?: number;");
            dts.Should().Contain("\"running\" | \"exited\" | \"paused\"");
            dts.Should().Contain("turn_on(args: {");
            dts.Should().Contain("entity_id: string;");
            dts.Should().Contain("rgb_color?: number[];");
            dts.Should().Contain("homeassistant: Server_ha_Tools;");
        }

        [Fact]
        [Requirement("MCP-52", "MCP", RequirementType.Positive, "CodeModeTypeGenerator generates per-server TypeScript declarations matching server ID or alias.")]
        public void GenerateServerDeclarations_FiltersToTargetServerAndSupportsAlias()
        {
            var tools = CreateSampleTools();
            var haServer = new McpServer { Id = "ha", Alias = "homeassistant", DisplayName = "Home Assistant", Enabled = true };

            var dtsById = CodeModeTypeGenerator.GenerateServerDeclarations("ha", tools, haServer);
            dtsById.Should().Contain("turn_on(args:");
            dtsById.Should().NotContain("list_containers");

            var dtsByAlias = CodeModeTypeGenerator.GenerateServerDeclarations("homeassistant", tools, haServer);
            dtsByAlias.Should().Contain("turn_on(args:");
            dtsByAlias.Should().NotContain("list_containers");
        }

        [Fact]
        [Requirement("MCP-52", "MCP", RequirementType.Positive, "search_tools supports typescript format and include_typescript flag with structuredContent.")]
        public async Task SearchTools_SupportsTypeScriptFormatAndStructuredContent()
        {
            var router = new ToolRoutingManager();
            var servers = new List<McpServer>
            {
                new() { Id = "docker", DisplayName = "Docker", Enabled = true }
            };

            var sampleTools = CreateSampleTools();
            // Seed tools cache via reflection or filterAuthorizedToolsAsync
            var payload = new
            {
                jsonrpc = "2.0",
                id = "search-ts-1",
                method = "tools/call",
                @params = new
                {
                    name = "search_tools",
                    arguments = new
                    {
                        query = "docker containers",
                        format = "typescript"
                    }
                }
            };

            var result = await router.CallToolAsync(
                "search_tools",
                JsonSerializer.Serialize(payload),
                new Mock<IDbConnectionFactory>().Object,
                new ConcurrentDictionary<string, BackendConnection>(),
                servers,
                NullLogger.Instance,
                new HttpClient(),
                null,
                () => Task.CompletedTask,
                (b, k, v) => b,
                filterAuthorizedToolsAsync: _ => Task.FromResult(sampleTools)
            );

            var json = JsonSerializer.Serialize(result);
            using var doc = JsonDocument.Parse(json);
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            text.Should().Contain("tools.docker.list_containers");
            text.Should().Contain("all: boolean");

            doc.RootElement.TryGetProperty("structuredContent", out var sc).Should().BeTrue();
            sc.GetProperty("format").GetString().Should().Be("typescript");
            sc.GetProperty("count").GetInt32().Should().BeGreaterThan(0);
        }

        [Fact]
        [Requirement("MCP-55", "MCP", RequirementType.Positive, "GetMetaTools includes MCP 2026-07-28 annotations and outputSchema on meta-tools.")]
        public void GetMetaTools_IncludesAnnotationsAndOutputSchema()
        {
            var router = new ToolRoutingManager();
            var tools = router.GetMetaTools(enableCodeMode: true);

            var searchTool = tools.First(t => t.name == "search_tools");
            searchTool.annotations.Should().NotBeNull();
            searchTool.outputSchema.Should().NotBeNull();

            var execTool = tools.First(t => t.name == "execute_tool");
            execTool.annotations.Should().NotBeNull();

            var scriptTool = tools.First(t => t.name == "execute_script");
            scriptTool.annotations.Should().NotBeNull();
            scriptTool.outputSchema.Should().NotBeNull();
        }
    }
}
