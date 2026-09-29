using FluentAssertions;
using ModelContextGateway.Core.VectorSearch;
using Moq;

namespace ModelContextGateway.Tests
{
    public class EmbeddingServiceAdapterTests
    {
        [Fact]
        [Requirement("MCP-12", "MCP", RequirementType.Negative, "EmbeddingServiceAdapter constructor throws ArgumentNullException when IEmbeddingService parameter is null.")]
        public void Constructor_NullEmbeddingService_ThrowsArgumentNullException()
        {
            Action act = () => new EmbeddingServiceAdapter(null!);

            act.Should().Throw<ArgumentNullException>()
               .WithParameterName("embeddingService");
        }

        [Fact]
        [Requirement("MCP-12", "MCP", RequirementType.Positive, "EmbeddingServiceAdapter InnerService property returns the injected IEmbeddingService instance.")]
        public void InnerService_ReturnsInjectedEmbeddingService()
        {
            var mockService = new Mock<IEmbeddingService>();
            var adapter = new EmbeddingServiceAdapter(mockService.Object);

            adapter.InnerService.Should().BeSameAs(mockService.Object);
        }

        [Fact]
        [Requirement("MCP-12", "MCP", RequirementType.Positive, "EmbeddingServiceAdapter GenerateEmbeddingAsync delegates vector generation to underlying GetEmbeddingAsync.")]
        public async Task GenerateEmbeddingAsync_DelegatesToGetEmbeddingAsync()
        {
            var mockService = new Mock<IEmbeddingService>();
            var expectedVector = new float[] { 0.1f, 0.2f, 0.3f };
            mockService.Setup(s => s.GetEmbeddingAsync("test text"))
                       .ReturnsAsync(expectedVector);

            var adapter = new EmbeddingServiceAdapter(mockService.Object);
            var result = await adapter.GenerateEmbeddingAsync("test text");

            result.Should().BeSameAs(expectedVector);
            mockService.Verify(s => s.GetEmbeddingAsync("test text"), Times.Once);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Requirement("MCP-12", "MCP", RequirementType.Positive, "EmbeddingServiceAdapter GenerateEmbeddingsAsync returns empty array for null or empty text collection.")]
        public async Task GenerateEmbeddingsAsync_NullOrEmptyList_ReturnsEmptyArray(bool isNull)
        {
            var mockService = new Mock<IEmbeddingService>();
            var adapter = new EmbeddingServiceAdapter(mockService.Object);

            IReadOnlyList<string>? texts = isNull ? null : Array.Empty<string>();

            var result = await adapter.GenerateEmbeddingsAsync(texts!);

            result.Should().BeEmpty();
            mockService.Verify(s => s.GetEmbeddingAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        [Requirement("MCP-12", "MCP", RequirementType.Positive, "EmbeddingServiceAdapter GenerateEmbeddingsAsync delegates batch generation to GetEmbeddingAsync and preserves response order.")]
        public async Task GenerateEmbeddingsAsync_ValidList_DelegatesToGetEmbeddingAsync_AndPreservesOrder()
        {
            var mockService = new Mock<IEmbeddingService>();
            var vector1 = new float[] { 0.1f, 0.2f };
            var vector2 = new float[] { 0.3f, 0.4f };

            mockService.Setup(s => s.GetEmbeddingAsync("query1")).ReturnsAsync(vector1);
            mockService.Setup(s => s.GetEmbeddingAsync("query2")).ReturnsAsync(vector2);

            var adapter = new EmbeddingServiceAdapter(mockService.Object);
            var inputs = new[] { "query1", "query2" };

            var results = await adapter.GenerateEmbeddingsAsync(inputs);

            results.Should().HaveCount(2);
            results[0].Should().BeSameAs(vector1);
            results[1].Should().BeSameAs(vector2);

            mockService.Verify(s => s.GetEmbeddingAsync("query1"), Times.Once);
            mockService.Verify(s => s.GetEmbeddingAsync("query2"), Times.Once);
        }
    }
}
