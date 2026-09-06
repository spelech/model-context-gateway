namespace ModelContextGateway.Tests
{
    public class ApiEmbeddingServiceTests
    {
        [Fact]
        [Requirement("MCP-12", "MCP", RequirementType.Positive, "ApiEmbeddingService computes vector cosine similarity accurately for semantic tool ranking.")]
        public void CalculateCosineSimilarity_ComputesSimilarity()
        {
            var service = new ApiEmbeddingService(new HttpClient(), new RouterSettings());

            float[] vecA = new float[] { 1.0f, 0.0f };
            float[] vecB = new float[] { 1.0f, 0.0f };

            Assert.Equal(1.0, service.CosineSimilarity(vecA, vecB), precision: 5);
        }

        [Fact]
        [Requirement("MCP-12", "MCP", RequirementType.Positive, "ApiEmbeddingService dynamically reloads provider configuration and API endpoints without gateway restarts.")]
        public void ReloadSettings_UpdatesSettings()
        {
            var service = new ApiEmbeddingService(new HttpClient(), new RouterSettings());
            var settings = new RouterSettings { EmbeddingProvider = "ollama", EmbeddingApiUrl = "http://localhost:11434" };
            service.ReloadSettings(settings);
            Assert.Equal("http://localhost:11434", service.GetSettings().EmbeddingApiUrl);
            Assert.Equal("ollama", service.GetSettings().EmbeddingProvider);
        }
    }
}
