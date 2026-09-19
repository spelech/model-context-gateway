using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using ModelContextGateway.Core.VectorSearch;

namespace ModelContextGateway.Tests
{
    public class OpenAiEmbeddingProviderTests
    {
        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_handler(request));
            }
        }

        [Fact]
        [Requirement("MCP-33", "MCP", RequirementType.Positive, "OpenAiEmbeddingProvider generates embeddings via OpenAI and Ollama compatible endpoints.")]
        public async Task GenerateEmbeddingAsync_SendsValidPayload_AndParsesResponse()
        {
            string? capturedBody = null;
            string? capturedAuthHeader = null;

            var mockHandler = new MockHttpMessageHandler(req =>
            {
                capturedAuthHeader = req.Headers.Authorization?.ToString();
                capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();

                var responsePayload = new
                {
                    @object = "list",
                    data = new[]
                    {
                        new
                        {
                            @object = "embedding",
                            index = 0,
                            embedding = new float[] { 0.1f, 0.2f, 0.3f }
                        }
                    },
                    model = "text-embedding-3-small"
                };

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(responsePayload), Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(mockHandler);
            var provider = new OpenAiEmbeddingProvider(
                httpClient,
                endpoint: "https://api.openai.com/v1/embeddings",
                apiKey: "sk-test-key",
                model: "text-embedding-3-small"
            );

            var vector = await provider.GenerateEmbeddingAsync("restart Docker container");

            vector.Should().Equal(new float[] { 0.1f, 0.2f, 0.3f });
            capturedAuthHeader.Should().Be("Bearer sk-test-key");
            capturedBody.Should().Contain("\"model\":\"text-embedding-3-small\"");
            capturedBody.Should().Contain("\"input\":\"restart Docker container\"");
        }

        [Fact]
        [Requirement("MCP-33", "MCP", RequirementType.Positive, "OpenAiEmbeddingProvider generates batch embeddings preserving input text ordering.")]
        public async Task GenerateEmbeddingsAsync_PreservesInputOrder_AcrossBatch()
        {
            var mockHandler = new MockHttpMessageHandler(req =>
            {
                // Return response out-of-order in index to verify provider orders by index
                var responsePayload = new
                {
                    @object = "list",
                    data = new[]
                    {
                        new { @object = "embedding", index = 1, embedding = new float[] { 0.4f, 0.5f } },
                        new { @object = "embedding", index = 0, embedding = new float[] { 0.1f, 0.2f } }
                    }
                };

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(responsePayload), Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(mockHandler);
            var provider = new OpenAiEmbeddingProvider(httpClient, "https://api.openai.com/v1/embeddings", "test-key");

            var batchResults = await provider.GenerateEmbeddingsAsync(new[] { "first", "second" });

            batchResults.Should().HaveCount(2);
            batchResults[0].Should().Equal(new float[] { 0.1f, 0.2f });
            batchResults[1].Should().Equal(new float[] { 0.4f, 0.5f });
        }

        [Fact]
        [Requirement("MCP-34", "MCP", RequirementType.FailClosedGuardrail, "OpenAiEmbeddingProvider blocks private loopback IPs preventing SSRF.")]
        public async Task GenerateEmbeddingAsync_ThrowsOnLoopbackIp_WhenPrivateIpsDisallowed()
        {
            var originalAllow = Environment.GetEnvironmentVariable("MCG_ALLOW_PRIVATE_IPS");
            try
            {
                Environment.SetEnvironmentVariable("MCG_ALLOW_PRIVATE_IPS", "false");

                var httpClient = new HttpClient();
                var provider = new OpenAiEmbeddingProvider(httpClient, "http://127.0.0.1:11434/v1/embeddings");

                var act = () => provider.GenerateEmbeddingAsync("test");
                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("*blocked for security reasons*");
            }
            finally
            {
                Environment.SetEnvironmentVariable("MCG_ALLOW_PRIVATE_IPS", originalAllow);
            }
        }
    }
}
