using System.Text.Json;
using System.Text.RegularExpressions;

namespace ModelContextGateway.Core.Routing
{
    public sealed record ToolOutputChunk(
        string OutputId,
        string ToolName,
        string Text,
        int TotalChars,
        int ReturnedChars,
        int Offset,
        bool HasMore,
        int? NextOffset);

    public sealed record ShapedTextResult(
        string Text,
        bool WasTruncated,
        string? OutputId,
        int TotalChars,
        int ReturnedChars,
        int Offset,
        bool HasMore,
        int? NextOffset,
        bool Filtered);

    /// <summary>
    /// Thread-safe bounded LRU/TTL buffer for oversized tool outputs, scoped strictly by ownerKey
    /// so sessions and users can never read another principal's buffered output.
    /// </summary>
    public sealed class ToolOutputBuffer
    {
        public const int DefaultMaxEntries = 512;
        public const int DefaultMaxTotalChars = 20_000_000;
        public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(15);

        private sealed class OutputEntry
        {
            public required string OutputId { get; init; }
            public required string OwnerKey { get; init; }
            public required string ToolName { get; init; }
            public required string FullText { get; init; }
            public int TotalChars => FullText.Length;
            public DateTimeOffset LastAccessedAtUtc { get; set; }
            public LinkedListNode<string>? LruNode { get; set; }
        }

        private readonly int _maxEntries;
        private readonly long _maxTotalChars;
        private readonly TimeSpan _ttl;
        private readonly Func<DateTimeOffset> _utcNow;

        private readonly object _lock = new();
        private readonly Dictionary<string, OutputEntry> _entries = new(StringComparer.Ordinal);
        private readonly LinkedList<string> _lru = new();
        private long _totalChars;

        public ToolOutputBuffer(
            int maxEntries = DefaultMaxEntries,
            long maxTotalChars = DefaultMaxTotalChars,
            TimeSpan? ttl = null,
            Func<DateTimeOffset>? utcNow = null)
        {
            _maxEntries = Math.Max(1, maxEntries);
            _maxTotalChars = Math.Max(1, maxTotalChars);
            _ttl = ttl ?? DefaultTtl;
            _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    PruneExpiredLocked(_utcNow());
                    return _entries.Count;
                }
            }
        }

        public long TotalChars
        {
            get
            {
                lock (_lock)
                {
                    PruneExpiredLocked(_utcNow());
                    return _totalChars;
                }
            }
        }

        public string Store(string ownerKey, string toolName, string fullText)
        {
            ArgumentNullException.ThrowIfNull(fullText);
            var normalizedOwner = string.IsNullOrWhiteSpace(ownerKey) ? "anonymous" : ownerKey;
            var normalizedTool = string.IsNullOrWhiteSpace(toolName) ? "unknown" : toolName;
            var now = _utcNow();

            lock (_lock)
            {
                PruneExpiredLocked(now);

                while ((_entries.Count >= _maxEntries || _totalChars + fullText.Length > _maxTotalChars) && _lru.Last != null)
                {
                    RemoveEntryLocked(_lru.Last.Value);
                }

                var outputId = $"out_{Guid.NewGuid():N}";
                var node = _lru.AddFirst(outputId);
                var entry = new OutputEntry
                {
                    OutputId = outputId,
                    OwnerKey = normalizedOwner,
                    ToolName = normalizedTool,
                    FullText = fullText,
                    LastAccessedAtUtc = now,
                    LruNode = node
                };

                _entries[outputId] = entry;
                _totalChars += entry.TotalChars;
                return outputId;
            }
        }

        public bool TryReadChunk(string ownerKey, string outputId, int offset, int maxChars, out ToolOutputChunk? chunk)
        {
            chunk = null;
            if (string.IsNullOrWhiteSpace(outputId))
            {
                return false;
            }

            var normalizedOwner = string.IsNullOrWhiteSpace(ownerKey) ? "anonymous" : ownerKey;
            var now = _utcNow();

            lock (_lock)
            {
                PruneExpiredLocked(now);

                if (!_entries.TryGetValue(outputId, out var entry))
                {
                    return false;
                }

                // Strict per-owner isolation — return false identically to not-found to avoid oracle leaks
                if (!string.Equals(entry.OwnerKey, normalizedOwner, StringComparison.Ordinal))
                {
                    return false;
                }

                entry.LastAccessedAtUtc = now;
                if (entry.LruNode != null && !ReferenceEquals(_lru.First, entry.LruNode))
                {
                    _lru.Remove(entry.LruNode);
                    _lru.AddFirst(entry.LruNode);
                }

                int total = entry.TotalChars;
                int safeOffset = Math.Clamp(offset, 0, total);
                int effectiveMax = maxChars > 0 ? maxChars : 4000;
                int len = Math.Min(effectiveMax, total - safeOffset);
                string slice = len > 0 ? entry.FullText.Substring(safeOffset, len) : string.Empty;
                bool hasMore = safeOffset + len < total;
                int? nextOffset = hasMore ? safeOffset + len : null;

                chunk = new ToolOutputChunk(
                    entry.OutputId,
                    entry.ToolName,
                    slice,
                    total,
                    len,
                    safeOffset,
                    hasMore,
                    nextOffset);
                return true;
            }
        }

        public ShapedTextResult ShapeText(
            string ownerKey,
            string toolName,
            string text,
            int? maxChars,
            int? offset,
            string? filter)
        {
            text ??= string.Empty;
            bool filtered = !string.IsNullOrWhiteSpace(filter);
            string workingText = filtered ? FilterLines(text, filter!) : text;

            int totalChars = workingText.Length;
            int safeOffset = Math.Clamp(offset ?? 0, 0, totalChars);
            bool hasLimit = maxChars.HasValue && maxChars.Value > 0;

            if (!hasLimit && safeOffset == 0)
            {
                return new ShapedTextResult(
                    workingText,
                    WasTruncated: false,
                    OutputId: null,
                    TotalChars: totalChars,
                    ReturnedChars: workingText.Length,
                    Offset: 0,
                    HasMore: false,
                    NextOffset: null,
                    Filtered: filtered);
            }

            int available = totalChars - safeOffset;
            int limit = hasLimit ? maxChars!.Value : available;

            if (safeOffset == 0 && available <= limit)
            {
                return new ShapedTextResult(
                    workingText,
                    WasTruncated: false,
                    OutputId: null,
                    TotalChars: totalChars,
                    ReturnedChars: workingText.Length,
                    Offset: 0,
                    HasMore: false,
                    NextOffset: null,
                    Filtered: filtered);
            }

            // Store full workingText so caller can page through remaining chunks via read_output
            string outputId = Store(ownerKey, toolName, workingText);

            if (available <= limit)
            {
                string tailSlice = available > 0 ? workingText.Substring(safeOffset, available) : string.Empty;
                return new ShapedTextResult(
                    tailSlice,
                    WasTruncated: false,
                    OutputId: outputId,
                    TotalChars: totalChars,
                    ReturnedChars: tailSlice.Length,
                    Offset: safeOffset,
                    HasMore: false,
                    NextOffset: null,
                    Filtered: filtered);
            }

            // Need to truncate to fit <= limit (including notice when limit allows)
            string finalText;
            int returnedChars;
            int nextOffset;

            if (limit >= 220)
            {
                string BuildNotice(int shown, int next) =>
                    $"\n\n...[truncated: showing {shown} of {totalChars} chars (offset {safeOffset}, next_offset={next}). Call read_output(output_id=\"{outputId}\", offset={next}) to continue.]";

                // Two-pass fit so slice + notice <= limit strictly
                var roughNotice = BuildNotice(limit, safeOffset + limit);
                int sliceLen = Math.Max(1, limit - roughNotice.Length);
                var exactNotice = BuildNotice(sliceLen, safeOffset + sliceLen);
                if (sliceLen + exactNotice.Length > limit)
                {
                    sliceLen = Math.Max(1, sliceLen - (sliceLen + exactNotice.Length - limit));
                    exactNotice = BuildNotice(sliceLen, safeOffset + sliceLen);
                }

                string slice = workingText.Substring(safeOffset, sliceLen);
                finalText = slice + exactNotice;
                if (finalText.Length > limit)
                {
                    finalText = finalText.Substring(0, limit);
                }
                returnedChars = sliceLen;
                nextOffset = safeOffset + sliceLen;
            }
            else if (limit >= 80)
            {
                string BuildCompactNotice(int next) =>
                    $"\n...[truncated output_id={outputId} next_offset={next}]";

                var roughNotice = BuildCompactNotice(safeOffset + limit);
                int sliceLen = Math.Max(1, limit - roughNotice.Length);
                var exactNotice = BuildCompactNotice(safeOffset + sliceLen);
                if (sliceLen + exactNotice.Length > limit)
                {
                    sliceLen = Math.Max(1, sliceLen - (sliceLen + exactNotice.Length - limit));
                    exactNotice = BuildCompactNotice(safeOffset + sliceLen);
                }

                string slice = workingText.Substring(safeOffset, sliceLen);
                finalText = slice + exactNotice;
                if (finalText.Length > limit)
                {
                    finalText = finalText.Substring(0, limit);
                }
                returnedChars = sliceLen;
                nextOffset = safeOffset + sliceLen;
            }
            else
            {
                int sliceLen = Math.Max(0, Math.Min(limit, available));
                finalText = sliceLen > 0 ? workingText.Substring(safeOffset, sliceLen) : string.Empty;
                returnedChars = sliceLen;
                nextOffset = safeOffset + sliceLen;
            }

            return new ShapedTextResult(
                finalText,
                WasTruncated: true,
                OutputId: outputId,
                TotalChars: totalChars,
                ReturnedChars: returnedChars,
                Offset: safeOffset,
                HasMore: nextOffset < totalChars,
                NextOffset: nextOffset < totalChars ? nextOffset : null,
                Filtered: filtered);
        }

        public object ShapeToolResult(
            object result,
            string ownerKey,
            string toolName,
            int? maxChars,
            int? offset,
            string? filter)
        {
            if (result == null)
            {
                return new { };
            }

            bool hasOffset = offset.HasValue && offset.Value > 0;
            bool hasFilter = !string.IsNullOrWhiteSpace(filter);
            bool hasLimit = maxChars.HasValue && maxChars.Value > 0;

            if (!hasLimit && !hasOffset && !hasFilter)
            {
                return result;
            }

            JsonElement root;
            if (result is JsonElement je)
            {
                root = je;
            }
            else if (result is JsonDocument jd)
            {
                root = jd.RootElement;
            }
            else
            {
                root = JsonSerializer.SerializeToElement(result);
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                var raw = root.GetRawText();
                var shapedRaw = ShapeText(ownerKey, toolName, raw, maxChars, offset, filter);
                if (!shapedRaw.WasTruncated && !shapedRaw.Filtered && !hasOffset)
                {
                    return result;
                }

                return new
                {
                    resultType = "complete",
                    structuredContent = new
                    {
                        truncated = shapedRaw.WasTruncated,
                        output_id = shapedRaw.OutputId,
                        total_chars = shapedRaw.TotalChars,
                        returned_chars = shapedRaw.ReturnedChars,
                        offset = shapedRaw.Offset,
                        has_more = shapedRaw.HasMore,
                        next_offset = shapedRaw.NextOffset,
                        filtered = shapedRaw.Filtered
                    },
                    content = new[]
                    {
                        new { type = "text", text = shapedRaw.Text }
                    }
                };
            }

            bool isError = root.TryGetProperty("isError", out var errProp) && errProp.ValueKind == JsonValueKind.True;
            string resultType = root.TryGetProperty("resultType", out var rtProp) && rtProp.ValueKind == JsonValueKind.String
                ? (rtProp.GetString() ?? "complete")
                : "complete";

            var textBlocks = new List<string>();
            var nonTextContent = new List<JsonElement>();

            if (root.TryGetProperty("content", out var contentArr) && contentArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in contentArr.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object &&
                        item.TryGetProperty("type", out var tProp) &&
                        string.Equals(tProp.GetString(), "text", StringComparison.OrdinalIgnoreCase) &&
                        item.TryGetProperty("text", out var txtProp) &&
                        txtProp.ValueKind == JsonValueKind.String)
                    {
                        textBlocks.Add(txtProp.GetString() ?? string.Empty);
                    }
                    else
                    {
                        nonTextContent.Add(item.Clone());
                    }
                }
            }

            string? combinedText = textBlocks.Count > 0 ? string.Join("\n", textBlocks) : null;
            bool hasStructuredContent = root.TryGetProperty("structuredContent", out var scProp) &&
                                        scProp.ValueKind != JsonValueKind.Null &&
                                        scProp.ValueKind != JsonValueKind.Undefined;

            if (combinedText == null && hasStructuredContent)
            {
                var scRaw = scProp.GetRawText();
                if (!hasLimit || scRaw.Length > maxChars!.Value || hasFilter || hasOffset)
                {
                    combinedText = scRaw;
                }
            }

            if (combinedText == null)
            {
                return result;
            }

            var shaped = ShapeText(ownerKey, toolName, combinedText, maxChars, offset, filter);

            // Also check if structuredContent alone exceeds limit even if text didn't
            bool structuredOversized = hasLimit && hasStructuredContent && scProp.GetRawText().Length > maxChars!.Value;
            if (!shaped.WasTruncated && !shaped.Filtered && !hasOffset && !structuredOversized)
            {
                return result;
            }

            var newContent = new List<object>
            {
                new { type = "text", text = shaped.Text }
            };
            foreach (var extra in nonTextContent)
            {
                newContent.Add(extra);
            }

            object? newStructuredContent = null;
            if (shaped.WasTruncated || shaped.Filtered || hasOffset || structuredOversized)
            {
                newStructuredContent = new
                {
                    truncated = shaped.WasTruncated,
                    output_id = shaped.OutputId,
                    total_chars = shaped.TotalChars,
                    returned_chars = shaped.ReturnedChars,
                    offset = shaped.Offset,
                    has_more = shaped.HasMore,
                    next_offset = shaped.NextOffset,
                    filtered = shaped.Filtered
                };
            }
            else if (hasStructuredContent)
            {
                newStructuredContent = scProp.Clone();
            }

            var envelope = new Dictionary<string, object?>
            {
                ["resultType"] = resultType,
                ["content"] = newContent
            };
            if (isError)
            {
                envelope["isError"] = true;
            }
            if (newStructuredContent != null)
            {
                envelope["structuredContent"] = newStructuredContent;
            }

            return envelope;
        }

        private static string FilterLines(string text, string pattern)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(pattern))
            {
                return text;
            }

            var lines = text.Split('\n');
            var matched = new List<string>();
            Regex? regex = null;
            bool useRegex = true;

            try
            {
                regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
            }
            catch (ArgumentException)
            {
                useRegex = false;
            }

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r');
                bool isMatch;
                if (useRegex && regex != null)
                {
                    try
                    {
                        isMatch = regex.IsMatch(line);
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        isMatch = line.Contains(pattern, StringComparison.OrdinalIgnoreCase);
                    }
                }
                else
                {
                    isMatch = line.Contains(pattern, StringComparison.OrdinalIgnoreCase);
                }

                if (isMatch)
                {
                    matched.Add(line);
                }
            }

            return string.Join("\n", matched);
        }

        private void PruneExpiredLocked(DateTimeOffset now)
        {
            if (_ttl <= TimeSpan.Zero)
            {
                return;
            }

            // Scan entries (bounded by _maxEntries, default 512)
            List<string>? expiredKeys = null;
            foreach (var kvp in _entries)
            {
                if (now - kvp.Value.LastAccessedAtUtc > _ttl)
                {
                    expiredKeys ??= new List<string>();
                    expiredKeys.Add(kvp.Key);
                }
            }

            if (expiredKeys != null)
            {
                foreach (var key in expiredKeys)
                {
                    RemoveEntryLocked(key);
                }
            }
        }

        private void RemoveEntryLocked(string outputId)
        {
            if (_entries.Remove(outputId, out var removed))
            {
                _totalChars -= removed.TotalChars;
                if (_totalChars < 0)
                {
                    _totalChars = 0;
                }
                if (removed.LruNode != null)
                {
                    _lru.Remove(removed.LruNode);
                    removed.LruNode = null;
                }
            }
        }
    }
}
