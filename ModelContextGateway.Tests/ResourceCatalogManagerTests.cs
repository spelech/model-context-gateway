using System.Text.Json;

namespace ModelContextGateway.Tests
{
    public class ResourceCatalogManagerTests
    {
        [Fact]
        [Requirement("MCP-05", "MCP", RequirementType.Positive, "SearchResourcesAsync returns all resources (up to 15) when query is null, empty, or whitespace.")]
        public async Task SearchResourcesAsync_ReturnsTop15_WhenQueryIsNullOrEmpty()
        {
            var manager = new ResourceCatalogManager();
            var resources = new List<object>();
            for (int i = 0; i < 20; i++)
            {
                resources.Add(new Dictionary<string, object>
                {
                    { "name", $"Resource {i}" },
                    { "description", $"Description {i}" }
                });
            }

            // Test null query
            var nullResults = await manager.SearchResourcesAsync(null!, resources);
            Assert.Equal(15, nullResults.Count);

            // Test empty query
            var emptyResults = await manager.SearchResourcesAsync("", resources);
            Assert.Equal(15, emptyResults.Count);

            // Test whitespace query
            var whitespaceResults = await manager.SearchResourcesAsync("   ", resources);
            Assert.Equal(15, whitespaceResults.Count);
        }

        [Fact]
        [Requirement("MCP-05", "MCP", RequirementType.Positive, "SearchResourcesAsync filters Dictionary resources matching query across name or description.")]
        public async Task SearchResourcesAsync_FiltersDictionaryResources_ByNameAndDescription()
        {
            var manager = new ResourceCatalogManager();
            var resources = new List<object>
            {
                new Dictionary<string, object> { { "name", "Docker Container Logs" }, { "description", "Fetches logs from docker" } },
                new Dictionary<string, object> { { "name", "Plex Server Status" }, { "description", "Monitors media server status" } },
                new Dictionary<string, object> { { "name", "System Metrics" }, { "description", "CPU and Docker RAM stats" } }
            };

            // Query matching name of item 1 & description of item 3
            var dockerResults = await manager.SearchResourcesAsync("docker", resources);
            Assert.Equal(2, dockerResults.Count);

            // Query matching description of item 2
            var mediaResults = await manager.SearchResourcesAsync("MEDIA", resources);
            Assert.Single(mediaResults);

            // Query matching nothing
            var noMatchResults = await manager.SearchResourcesAsync("nonexistent", resources);
            Assert.Empty(noMatchResults);
        }

        [Fact]
        [Requirement("MCP-05", "MCP", RequirementType.Positive, "SearchResourcesAsync filters JsonElement resources matching query across name or description.")]
        public async Task SearchResourcesAsync_FiltersJsonElementResources_ByNameAndDescription()
        {
            var manager = new ResourceCatalogManager();
            var json1 = JsonSerializer.Deserialize<JsonElement>(@"{""name"": ""Kubernetes Pods"", ""description"": ""Cluster pod status""}");
            var json2 = JsonSerializer.Deserialize<JsonElement>(@"{""name"": ""Database Metrics"", ""description"": ""Postgres connection pool""}");
            var json3 = JsonSerializer.Deserialize<JsonElement>(@"{""name"": ""Nginx Logs"", ""description"": ""Web server kubernetes access logs""}");

            var resources = new List<object> { json1, json2, json3 };

            // Query matching name of item 1 and description of item 3
            var k8sResults = await manager.SearchResourcesAsync("KUBERNETES", resources);
            Assert.Equal(2, k8sResults.Count);

            // Query matching description of item 2
            var postgresResults = await manager.SearchResourcesAsync("postgres", resources);
            Assert.Single(postgresResults);
        }

        [Fact]
        [Requirement("MCP-05", "MCP", RequirementType.Positive, "SearchResourcesAsync handles null values and missing properties in dictionary and JsonElement objects without throwing.")]
        public async Task SearchResourcesAsync_HandlesNullAndMissingPropertiesGracefully()
        {
            var manager = new ResourceCatalogManager();
            var dictWithNulls = new Dictionary<string, object?>
            {
                { "name", null },
                { "description", null }
            };
            var dictEmpty = new Dictionary<string, object>();

            var jsonNoProps = JsonSerializer.Deserialize<JsonElement>("{}");
            var jsonNullProps = JsonSerializer.Deserialize<JsonElement>(@"{""name"": null, ""description"": null}");
            var jsonNumProps = JsonSerializer.Deserialize<JsonElement>(@"{""name"": 123, ""description"": true}");

            var resources = new List<object> { dictWithNulls!, dictEmpty, jsonNoProps, jsonNullProps, jsonNumProps };

            // None of these should match a specific string search, nor throw exceptions
            var searchResults = await manager.SearchResourcesAsync("test", resources);
            Assert.Empty(searchResults);
        }

        [Fact]
        [Requirement("MCP-05", "MCP", RequirementType.Positive, "SearchResourcesAsync limits result size to 15 items.")]
        public async Task SearchResourcesAsync_LimitsResultsTo15Items()
        {
            var manager = new ResourceCatalogManager();
            var resources = new List<object>();
            for (int i = 0; i < 25; i++)
            {
                resources.Add(new Dictionary<string, object>
                {
                    { "name", $"Common Term Resource {i}" },
                    { "description", "Matches common term" }
                });
            }

            var results = await manager.SearchResourcesAsync("Common Term", resources);
            Assert.Equal(15, results.Count);
        }

        [Fact]
        [Requirement("MCP-05", "MCP", RequirementType.Positive, "TryMatchLogsTemplate returns true and extracts serverId for valid logs URI patterns.")]
        public void TryMatchLogsTemplate_MatchesValidUri_ExtractsServerId()
        {
            var manager = new ResourceCatalogManager();

            bool matched = manager.TryMatchLogsTemplate("logs://docker-srv/today", out var serverId);

            Assert.True(matched);
            Assert.Equal("docker-srv", serverId);
        }

        [Theory]
        [InlineData("logs://docker-srv/yesterday")]
        [InlineData("logs:///today")]
        [InlineData("http://docker-srv/today")]
        [InlineData("logs://docker-srv/today/extra")]
        [InlineData("logs://docker-srv")]
        [InlineData("")]
        [Requirement("MCP-05", "MCP", RequirementType.Positive, "TryMatchLogsTemplate returns false and empty serverId for invalid URI patterns.")]
        public void TryMatchLogsTemplate_ReturnsFalse_ForInvalidPatterns(string invalidUri)
        {
            var manager = new ResourceCatalogManager();

            bool matched = manager.TryMatchLogsTemplate(invalidUri, out var serverId);

            Assert.False(matched);
            Assert.Equal(string.Empty, serverId);
        }
    }
}
