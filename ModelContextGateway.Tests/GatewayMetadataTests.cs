using System.Text.Json;
using FluentAssertions;

namespace ModelContextGateway.Tests
{
    public class GatewayMetadataTests
    {
        [Theory]
        [InlineData("2026-07-28")]
        [InlineData("2025-11-25")]
        [InlineData("2025-06-18")]
        [InlineData("2025-03-26")]
        [InlineData("2024-11-05")]
        [InlineData("2024-10-07")]
        [InlineData(" 2026-07-28 ")]
        [Requirement("CORE-GATEWAY-METADATA-SUPPORTED-VERSIONS", "CORE", RequirementType.Positive, "IsSupportedProtocolVersion validates supported protocol versions case-insensitively with whitespace trimming.")]
        public void IsSupportedProtocolVersion_ReturnsTrue_ForKnownVersions(string version)
        {
            GatewayMetadata.IsSupportedProtocolVersion(version).Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [Requirement("CORE-GATEWAY-METADATA-SUPPORTED-VERSIONS", "CORE", RequirementType.Positive, "IsSupportedProtocolVersion permits null or whitespace protocol versions for permissive fallback.")]
        public void IsSupportedProtocolVersion_ReturnsTrue_ForNullOrWhitespace(string? version)
        {
            GatewayMetadata.IsSupportedProtocolVersion(version).Should().BeTrue();
        }

        [Theory]
        [InlineData("1999-01-01")]
        [InlineData("invalid-version")]
        [Requirement("CORE-GATEWAY-METADATA-UNSUPPORTED-VERSIONS", "CORE", RequirementType.Negative, "IsSupportedProtocolVersion rejects unrecognized protocol versions.")]
        public void IsSupportedProtocolVersion_ReturnsFalse_ForUnknownVersions(string version)
        {
            GatewayMetadata.IsSupportedProtocolVersion(version).Should().BeFalse();
        }

        [Fact]
        [Requirement("CORE-GATEWAY-METADATA-VERSION-NEGOTIATION", "CORE", RequirementType.Positive, "NegotiateProtocolVersion canonicalizes casing for known protocol versions.")]
        public void NegotiateProtocolVersion_ReturnsCanonicalVersion_WhenMatchFound()
        {
            var result = GatewayMetadata.NegotiateProtocolVersion("2024-11-05");
            result.Should().Be("2024-11-05");
        }

        [Fact]
        [Requirement("CORE-GATEWAY-METADATA-VERSION-NEGOTIATION", "CORE", RequirementType.Positive, "NegotiateProtocolVersion falls back to supported ProtocolVersion when requested version is unrecognized.")]
        public void NegotiateProtocolVersion_FallsBackToDefault_WhenUnrecognized()
        {
            var result = GatewayMetadata.NegotiateProtocolVersion("custom-experimental-v1");
            result.Should().Be(GatewayMetadata.ProtocolVersion);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [Requirement("CORE-GATEWAY-METADATA-VERSION-NEGOTIATION", "CORE", RequirementType.Positive, "NegotiateProtocolVersion falls back to default ProtocolVersion when null or empty.")]
        public void NegotiateProtocolVersion_FallsBackToDefault_WhenNullOrEmpty(string? requested)
        {
            var result = GatewayMetadata.NegotiateProtocolVersion(requested);
            result.Should().Be(GatewayMetadata.ProtocolVersion);
        }

        [Fact]
        [Requirement("CORE-GATEWAY-METADATA-EXTRACTION-STRING", "CORE", RequirementType.Positive, "ExtractRequestedProtocolVersion parses protocolVersion from initialize request payload or isolated params.")]
        public void ExtractRequestedProtocolVersion_FromString_ParsesValidVersion()
        {
            var json = "{\"jsonrpc\":\"2.0\",\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\"}}";
            var result = GatewayMetadata.ExtractRequestedProtocolVersion(json);
            result.Should().Be("2024-11-05");

            // Also test isolated params JSON string without outer jsonrpc envelope
            var isolatedJson = "{\"protocolVersion\":\"2024-11-05\"}";
            var isolatedResult = GatewayMetadata.ExtractRequestedProtocolVersion(isolatedJson);
            isolatedResult.Should().Be("2024-11-05");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-json")]
        [InlineData("{}")]
        [InlineData("{\"params\":{}}")]
        [InlineData("{\"params\":{\"protocolVersion\":\"\"}}")]
        [Requirement("CORE-GATEWAY-METADATA-EXTRACTION-STRING", "CORE", RequirementType.Positive, "ExtractRequestedProtocolVersion falls back safely to default protocol version on invalid or missing payloads.")]
        public void ExtractRequestedProtocolVersion_FromString_FallsBackToDefault(string? json)
        {
            var result = GatewayMetadata.ExtractRequestedProtocolVersion(json);
            result.Should().Be(GatewayMetadata.ProtocolVersion);
        }

        [Fact]
        [Requirement("CORE-GATEWAY-METADATA-EXTRACTION-JSONELEMENT", "CORE", RequirementType.Positive, "ExtractRequestedProtocolVersion parses protocolVersion from JsonElement params object.")]
        public void ExtractRequestedProtocolVersion_FromJsonElement_ParsesValidVersion()
        {
            using var doc = JsonDocument.Parse("{\"protocolVersion\":\"2024-11-05\"}");
            var result = GatewayMetadata.ExtractRequestedProtocolVersion(doc.RootElement);
            result.Should().Be("2024-11-05");
        }

        [Fact]
        [Requirement("CORE-GATEWAY-METADATA-EXTRACTION-JSONELEMENT", "CORE", RequirementType.Positive, "ExtractRequestedProtocolVersion falls back to default when JsonElement is not an object or lacks protocolVersion.")]
        public void ExtractRequestedProtocolVersion_FromJsonElement_FallsBackToDefault()
        {
            using var doc1 = JsonDocument.Parse("\"primitive-string\"");
            GatewayMetadata.ExtractRequestedProtocolVersion(doc1.RootElement).Should().Be(GatewayMetadata.ProtocolVersion);

            using var doc2 = JsonDocument.Parse("{\"otherProp\":\"value\"}");
            GatewayMetadata.ExtractRequestedProtocolVersion(doc2.RootElement).Should().Be(GatewayMetadata.ProtocolVersion);
        }

        [Fact]
        [Requirement("CORE-GATEWAY-METADATA-BUILD-INIT-REQUEST", "CORE", RequirementType.Positive, "BuildInitializeRequest formats standard JSON-RPC 2.0 initialize request with dynamic protocol version.")]
        public void BuildInitializeRequest_GeneratesValidJsonRpc()
        {
            var req = GatewayMetadata.BuildInitializeRequest("custom-id", "CustomClient", "2024-11-05");
            using var doc = JsonDocument.Parse(req);
            var root = doc.RootElement;

            root.GetProperty("jsonrpc").GetString().Should().Be("2.0");
            root.GetProperty("method").GetString().Should().Be("initialize");
            root.GetProperty("id").GetString().Should().Be("custom-id");

            var @params = root.GetProperty("params");
            @params.GetProperty("protocolVersion").GetString().Should().Be("2024-11-05");
            @params.GetProperty("clientInfo").GetProperty("name").GetString().Should().Be("CustomClient");
        }

        [Fact]
        [Requirement("CORE-GATEWAY-METADATA-CONSTANTS", "CORE", RequirementType.Positive, "Metadata constants and assembly version return consistent non-empty identifiers.")]
        public void MetadataConstants_ReturnExpectedValues()
        {
            GatewayMetadata.DefaultName.Should().Be("ModelContextGateway");
            GatewayMetadata.AdminServerName.Should().Be("Model-Context-Gateway-Admin");
            GatewayMetadata.ProtocolVersion.Should().Be("2026-07-28");
            GatewayMetadata.LegacyProtocolVersion.Should().Be("2024-11-05");
            GatewayMetadata.Version.Should().NotBeNullOrWhiteSpace();
            GatewayMetadata.SupportedProtocolVersions.Should().Contain("2026-07-28");
            GatewayMetadata.SupportedProtocolVersions.Should().Contain("2024-11-05");
        }
    }
}
