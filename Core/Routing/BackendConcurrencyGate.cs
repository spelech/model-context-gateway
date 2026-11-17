using System.Text.Json;

namespace ModelContextGateway.Core.Routing
{
    /// <summary>
    /// Enforces per-server and per-slot FIFO concurrency gating for serialized or rate-limited MCP backends
    /// (for example Playwright browser roles like 'main' and 'support').
    /// Uses an explicit FIFO queue under a lock rather than SemaphoreSlim so waiters are awakened strictly in arrival order
    /// and cancelled waiters never leak permits.
    /// </summary>
    public sealed class BackendConcurrencyGate
    {
        private sealed class WaiterNode
        {
            public readonly TaskCompletionSource<bool> Tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public bool Cancelled;
            public bool Granted;
        }

        private sealed class GateBucket
        {
            public readonly object Sync = new();
            public readonly Queue<WaiterNode> Waiters = new();
            public int ActiveCount;
            public int RefCount;
        }

        private sealed class NoopLease : IAsyncDisposable
        {
            public static readonly NoopLease Instance = new();
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private sealed class BucketLease : IAsyncDisposable
        {
            private readonly BackendConcurrencyGate _owner;
            private readonly string _bucketKey;
            private readonly GateBucket _bucket;
            private int _disposed;

            public BucketLease(BackendConcurrencyGate owner, string bucketKey, GateBucket bucket)
            {
                _owner = owner;
                _bucketKey = bucketKey;
                _bucket = bucket;
            }

            public ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _owner.Release(_bucketKey, _bucket);
                }
                return ValueTask.CompletedTask;
            }
        }

        private readonly object _bucketsLock = new();
        private readonly Dictionary<string, GateBucket> _buckets = new(StringComparer.OrdinalIgnoreCase);

        public static string ResolveSlotKey(McpServer? server, JsonElement args, string? explicitConcurrencyKey)
        {
            if (!string.IsNullOrWhiteSpace(explicitConcurrencyKey))
            {
                return explicitConcurrencyKey.Trim();
            }

            if (server != null && !string.IsNullOrWhiteSpace(server.ConcurrencyKeyField) && args.ValueKind == JsonValueKind.Object)
            {
                var fields = server.ConcurrencyKeyField.Split(
                    new[] { ',', ';' },
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var field in fields)
                {
                    if (TryGetPropertyCaseInsensitive(args, field, out var valProp))
                    {
                        var valStr = valProp.ValueKind switch
                        {
                            JsonValueKind.String => valProp.GetString(),
                            JsonValueKind.Number => valProp.GetRawText(),
                            JsonValueKind.True => "true",
                            JsonValueKind.False => "false",
                            _ => null
                        };

                        if (!string.IsNullOrWhiteSpace(valStr))
                        {
                            return valStr.Trim();
                        }
                    }
                }
            }

            return "__default__";
        }

        public async Task<IAsyncDisposable> AcquireAsync(
            string serverId,
            string slotKey,
            int? maxConcurrentRequests,
            CancellationToken cancellationToken = default)
        {
            if (!maxConcurrentRequests.HasValue || maxConcurrentRequests.Value <= 0)
            {
                return NoopLease.Instance;
            }

            int limit = maxConcurrentRequests.Value;
            string effectiveSlot = string.IsNullOrWhiteSpace(slotKey) ? "__default__" : slotKey.Trim();
            string bucketKey = $"{serverId}::{effectiveSlot}";

            GateBucket bucket;
            lock (_bucketsLock)
            {
                if (!_buckets.TryGetValue(bucketKey, out bucket!))
                {
                    bucket = new GateBucket();
                    _buckets[bucketKey] = bucket;
                }
                bucket.RefCount++;
            }

            WaiterNode? waiter = null;
            bool immediate = false;

            lock (bucket.Sync)
            {
                // Prune any cancelled waiters at head before deciding fast-path
                while (bucket.Waiters.Count > 0 && bucket.Waiters.Peek().Cancelled)
                {
                    bucket.Waiters.Dequeue();
                }

                if (bucket.Waiters.Count == 0 && bucket.ActiveCount < limit)
                {
                    bucket.ActiveCount++;
                    immediate = true;
                }
                else
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    waiter = new WaiterNode();
                    bucket.Waiters.Enqueue(waiter);
                }
            }

            if (immediate)
            {
                return new BucketLease(this, bucketKey, bucket);
            }

            using var reg = cancellationToken.Register(() =>
            {
                lock (bucket.Sync)
                {
                    if (!waiter!.Granted && !waiter.Cancelled)
                    {
                        waiter.Cancelled = true;
                        waiter.Tcs.TrySetCanceled(cancellationToken);
                    }
                }
            });

            try
            {
                await waiter!.Tcs.Task.ConfigureAwait(false);
                return new BucketLease(this, bucketKey, bucket);
            }
            catch
            {
                // If cancellation raced just after grant, transfer/release the granted slot cleanly
                bool mustRelease = false;
                lock (bucket.Sync)
                {
                    if (waiter!.Granted)
                    {
                        mustRelease = true;
                    }
                }

                if (mustRelease)
                {
                    Release(bucketKey, bucket);
                }
                else
                {
                    DecrementRefAndMaybeCleanup(bucketKey, bucket);
                }

                throw;
            }
        }

        private void Release(string bucketKey, GateBucket bucket)
        {
            WaiterNode? nextToWake = null;

            lock (bucket.Sync)
            {
                while (bucket.Waiters.Count > 0)
                {
                    var candidate = bucket.Waiters.Dequeue();
                    if (candidate.Cancelled)
                    {
                        continue;
                    }
                    candidate.Granted = true;
                    nextToWake = candidate;
                    break;
                }

                if (nextToWake == null)
                {
                    if (bucket.ActiveCount > 0)
                    {
                        bucket.ActiveCount--;
                    }
                }
                // Note: if nextToWake != null, the active slot transfers directly to nextToWake (strict FIFO, no barging).
            }

            DecrementRefAndMaybeCleanup(bucketKey, bucket);

            nextToWake?.Tcs.TrySetResult(true);
        }

        private void DecrementRefAndMaybeCleanup(string bucketKey, GateBucket bucket)
        {
            lock (_bucketsLock)
            {
                bucket.RefCount = Math.Max(0, bucket.RefCount - 1);
                if (bucket.RefCount == 0 &&
                    _buckets.TryGetValue(bucketKey, out var current) &&
                    ReferenceEquals(current, bucket))
                {
                    lock (bucket.Sync)
                    {
                        if (bucket.ActiveCount == 0 && bucket.Waiters.Count == 0)
                        {
                            _buckets.Remove(bucketKey);
                        }
                    }
                }
            }
        }

        private static bool TryGetPropertyCaseInsensitive(JsonElement obj, string name, out JsonElement value)
        {
            if (obj.TryGetProperty(name, out value))
            {
                return true;
            }

            foreach (var prop in obj.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}
