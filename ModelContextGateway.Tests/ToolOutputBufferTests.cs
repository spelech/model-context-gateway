using System.Text.Json;
using FluentAssertions;

namespace ModelContextGateway.Tests
{
    public class ToolOutputBufferTests
    {
        [Fact]
        [Requirement("SEC-OUTPUT-ISOLATION", "SEC", RequirementType.FailClosedGuardrail, "ToolOutputBuffer isolates buffered outputs strictly per ownerKey.")]
        public void TryReadChunk_EnforcesStrictOwnerIsolation()
        {
            var buffer = new ToolOutputBuffer();
            var outputId = buffer.Store("sid:user-alpha", "playwright__browser_read_text", "Sensitive page text for alpha");

            buffer.TryReadChunk("sid:user-alpha", outputId, 0, 100, out var ownerChunk).Should().BeTrue();
            ownerChunk.Should().NotBeNull();
            ownerChunk!.Text.Should().Be("Sensitive page text for alpha");

            buffer.TryReadChunk("sid:user-beta", outputId, 0, 100, out var intruderChunk).Should().BeFalse();
            intruderChunk.Should().BeNull();

            buffer.TryReadChunk("session:sess-1", outputId, 0, 100, out var otherSessChunk).Should().BeFalse();
            otherSessChunk.Should().BeNull();
        }

        [Fact]
        [Requirement("MCP-OUTPUT-BUFFER-LRU", "MCP", RequirementType.Positive, "ToolOutputBuffer evicts least-recently-used entries when entry count or char budget is exceeded.")]
        public void Store_EvictsLeastRecentlyUsedEntries_WhenLimitsExceeded()
        {
            var buffer = new ToolOutputBuffer(maxEntries: 2, maxTotalChars: 20);

            var id1 = buffer.Store("sid:u1", "toolA", "12345678"); // 8 chars
            var id2 = buffer.Store("sid:u1", "toolB", "abcdefgh"); // 8 chars (total 16)

            // Touch id1 so id2 becomes LRU
            buffer.TryReadChunk("sid:u1", id1, 0, 10, out _).Should().BeTrue();

            // Storing id3 (8 chars -> 24 > 20 and count 3 > 2) must evict id2 (LRU), keeping id1 & id3
            var id3 = buffer.Store("sid:u1", "toolC", "ijklmnop");

            buffer.Count.Should().Be(2);
            buffer.TotalChars.Should().Be(16);
            buffer.TryReadChunk("sid:u1", id2, 0, 10, out _).Should().BeFalse();
            buffer.TryReadChunk("sid:u1", id1, 0, 10, out _).Should().BeTrue();
            buffer.TryReadChunk("sid:u1", id3, 0, 10, out _).Should().BeTrue();
        }

        [Fact]
        [Requirement("MCP-OUTPUT-BUFFER-TTL", "MCP", RequirementType.Positive, "ToolOutputBuffer expires entries after sliding TTL elapses.")]
        public void TryReadChunk_PrunesExpiredEntries_BySlidingTtl()
        {
            var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            var buffer = new ToolOutputBuffer(ttl: TimeSpan.FromMinutes(10), utcNow: () => now);

            var id = buffer.Store("sid:u1", "toolA", "hello world");

            // Advance 8 minutes and access -> refreshes sliding TTL
            now = now.AddMinutes(8);
            buffer.TryReadChunk("sid:u1", id, 0, 5, out var chunk1).Should().BeTrue();
            chunk1!.Text.Should().Be("hello");

            // Advance another 8 minutes (16 total, 8 since last access) -> still alive
            now = now.AddMinutes(8);
            buffer.TryReadChunk("sid:u1", id, 5, 10, out var chunk2).Should().BeTrue();
            chunk2!.Text.Should().Be(" world");

            // Advance 11 minutes past last access -> expired
            now = now.AddMinutes(11);
            buffer.TryReadChunk("sid:u1", id, 0, 10, out var chunk3).Should().BeFalse();
            chunk3.Should().BeNull();
            buffer.Count.Should().Be(0);
        }

        [Fact]
        [Requirement("MCP-OUTPUT-PAGING", "MCP", RequirementType.Positive, "ShapeText truncates oversized payloads within max_chars budget and allows full reconstruction via TryReadChunk.")]
        public void ShapeText_TruncatesWithinLimit_AndPagesCleanlyToCompletion()
        {
            var buffer = new ToolOutputBuffer();
            var fullPayload = string.Join("\n", Enumerable.Range(1, 200).Select(i => $"Line-{i:D3}: McMaster SKU-{1000 + i} Zinc-Plated Bolt"));

            var first = buffer.ShapeText("sid:u1", "playwright__browser_read_text", fullPayload, maxChars: 600, offset: null, filter: null);

            first.WasTruncated.Should().BeTrue();
            first.Text.Length.Should().BeLessThanOrEqualTo(600);
            first.OutputId.Should().NotBeNullOrEmpty();
            first.HasMore.Should().BeTrue();
            first.NextOffset.Should().NotBeNull();
            first.Text.Should().Contain("read_output");
            first.Text.Should().Contain(first.OutputId!);

            // Reconstruct entire payload by paging from offset 0
            var reconstructed = new System.Text.StringBuilder();
            int cursor = 0;
            while (true)
            {
                buffer.TryReadChunk("sid:u1", first.OutputId!, cursor, 800, out var chunk).Should().BeTrue();
                reconstructed.Append(chunk!.Text);
                if (!chunk.HasMore)
                {
                    break;
                }
                cursor = chunk.NextOffset!.Value;
            }

            reconstructed.ToString().Should().Be(fullPayload);
        }

        [Fact]
        [Requirement("MCP-OUTPUT-FILTER", "MCP", RequirementType.Positive, "ShapeText supports line-level regex filtering and safe substring fallback on invalid regex.")]
        public void ShapeText_FiltersByRegexAndFallsBackToSubstringOnInvalidPattern()
        {
            var buffer = new ToolOutputBuffer();
            var text = "Header line\nSKU-101: Screw\nSKU-202: Nut\n[unclosed bracket match\nFooter line";

            var regexFiltered = buffer.ShapeText("sid:u1", "catalog", text, maxChars: 4000, offset: null, filter: @"^SKU-2\d+");
            regexFiltered.Filtered.Should().BeTrue();
            regexFiltered.Text.Should().Be("SKU-202: Nut");

            // Invalid regex pattern should not throw; falls back to case-insensitive substring match
            var fallbackFiltered = buffer.ShapeText("sid:u1", "catalog", text, maxChars: 4000, offset: null, filter: "[unclosed");
            fallbackFiltered.Filtered.Should().BeTrue();
            fallbackFiltered.Text.Should().Be("[unclosed bracket match");
        }

        [Fact]
        [Requirement("MCP-OUTPUT-STRUCTURED-CAP", "MCP", RequirementType.Positive, "ShapeToolResult replaces oversized structuredContent with compact paging metadata.")]
        public void ShapeToolResult_ReplacesOversizedStructuredContentWithCompactMetadata()
        {
            var buffer = new ToolOutputBuffer();
            var hugeItems = Enumerable.Range(1, 300).Select(i => new { sku = $"91251A{i:D3}", desc = "Stainless steel socket head cap screw" }).ToArray();
            var rawResult = new
            {
                resultType = "complete",
                structuredContent = new { items = hugeItems },
                content = new[]
                {
                    new { type = "text", text = string.Join("\n", hugeItems.Select(x => $"{x.sku}: {x.desc}")) }
                }
            };

            var shapedObj = buffer.ShapeToolResult(rawResult, "sid:u1", "catalog__list", maxChars: 500, offset: null, filter: null);
            var json = JsonSerializer.Serialize(shapedObj);
            using var doc = JsonDocument.Parse(json);

            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            text.Length.Should().BeLessThanOrEqualTo(500);

            var sc = doc.RootElement.GetProperty("structuredContent");
            sc.GetProperty("truncated").GetBoolean().Should().BeTrue();
            sc.TryGetProperty("items", out _).Should().BeFalse();
            sc.GetProperty("output_id").GetString().Should().StartWith("out_");
            sc.GetProperty("has_more").GetBoolean().Should().BeTrue();
        }
    }
}
