using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;

namespace ModelContextGateway.Tests
{
    public class ResourceRoutingManagerTests
    {
        [Fact]
        [Requirement("MCP-05", "MCP", RequirementType.Positive, "ResourceRoutingManager returns all registered resources when search query is empty.")]
        public async Task SearchResourcesAsync_ReturnsAll_WhenQueryIsEmpty()
        {
            var manager = new ResourceRoutingManager();
            var resources = new List<object>
            {
                new Dictionary<string, object> { { "name", "docker logs" }, { "description", "Docker container log stream" } },
                new Dictionary<string, object> { { "name", "plex status" }, { "description", "Plex session status" } }
            };

            var results = await manager.SearchResourcesAsync("", resources);
            Assert.Equal(2, results.Count);
        }

        [Fact]
        [Requirement("MCP-05", "MCP", RequirementType.Positive, "ResourceRoutingManager filters registered resources matching query tokens across name and description fields.")]
        public async Task SearchResourcesAsync_FiltersByQuery_MatchingNameOrDescription()
        {
            var manager = new ResourceRoutingManager();
            var resources = new List<object>
            {
                new Dictionary<string, object> { { "name", "docker logs" }, { "description", "Docker container log stream" } },
                new Dictionary<string, object> { { "name", "plex status" }, { "description", "Plex session status" } }
            };

            var dockerResults = await manager.SearchResourcesAsync("docker", resources);
            Assert.Single(dockerResults);

            var plexResults = await manager.SearchResourcesAsync("plex", resources);
            Assert.Single(plexResults);
        }

        [Fact]
        [Requirement("MCP-05", "MCP", RequirementType.Positive, "ResourceRoutingManager reads built-in diagnostic and metrics resources (router://status, router://metrics, logs://).")]
        public async Task ReadResourceAsync_LocalBuiltInResources_ReturnCorrectJson()
        {
            var manager = new ResourceRoutingManager();
            var backendConnections = new ConcurrentDictionary<string, BackendConnection>();
            Func<Task> ensureInitialized = () => Task.CompletedTask;
            Func<string, string, string, string> rewriteJson = (body, key, val) => body;

            // 1. router://status
            var statusRes = await manager.ReadResourceAsync("router://status", "{}", backendConnections, ensureInitialized, rewriteJson);
            Assert.NotNull(statusRes);

            // 2. router://active-servers
            var serversRes = await manager.ReadResourceAsync("router://active-servers", "{}", backendConnections, ensureInitialized, rewriteJson);
            Assert.NotNull(serversRes);

            // 3. router://metrics
            var metricsRes = await manager.ReadResourceAsync("router://metrics", "{}", backendConnections, ensureInitialized, rewriteJson);
            Assert.NotNull(metricsRes);

            // 4. logs://docker/today
            var logsRes = await manager.ReadResourceAsync("logs://docker/today", "{}", backendConnections, ensureInitialized, rewriteJson);
            Assert.NotNull(logsRes);
        }

        [Fact]
        [Requirement("GUARD-ROUTING-UNREGISTERED-RESOURCE", "GUARD", RequirementType.Negative, "ResourceRoutingManager throws KeyNotFoundException when reading an unregistered resource URI.")]
        public async Task ReadResourceAsync_ThrowsKeyNotFound_WhenResourceNotRegistered()
        {
            var manager = new ResourceRoutingManager();
            var backendConnections = new ConcurrentDictionary<string, BackendConnection>();
            Func<Task> ensureInitialized = () => Task.CompletedTask;
            Func<string, string, string, string> rewriteJson = (body, key, val) => body;

            await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            {
                await manager.ReadResourceAsync("mcp://unknown-server/unregistered-resource", "{}", backendConnections, ensureInitialized, rewriteJson);
            });
        }

        [Fact]
        [Requirement("MCP-05", "MCP", RequirementType.Positive, "ResourceRoutingManager returns built-in dynamic resource templates.")]
        public async Task ListResourceTemplatesAsync_ReturnsBuiltInTemplates()
        {
            var manager = new ResourceRoutingManager();
            var backendConnections = new Dictionary<string, BackendConnection>();
            Func<Task> ensureInitialized = () => Task.CompletedTask;

            var templates = await manager.ListResourceTemplatesAsync("{}", backendConnections, NullLogger.Instance, ensureInitialized);
            Assert.NotEmpty(templates);
        }
    }
}
