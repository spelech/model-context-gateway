using System.Collections.Concurrent;
using System.Text.Json;
using FluentAssertions;

namespace ModelContextGateway.Tests
{
    public class BackendConcurrencyGateTests
    {
        [Fact]
        [Requirement("MCP-CONCURRENCY-FIFO", "MCP", RequirementType.Positive, "BackendConcurrencyGate serializes requests in strict FIFO arrival order when limit is 1.")]
        public async Task AcquireAsync_WithLimitOne_SerializesInStrictFifoOrder()
        {
            var gate = new BackendConcurrencyGate();
            var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var acquiredOrder = new ConcurrentQueue<int>();
            int active = 0;
            int maxObserved = 0;

            var t0 = Task.Run(async () =>
            {
                await using var lease = await gate.AcquireAsync("playwright", "main", maxConcurrentRequests: 1);
                int cur = Interlocked.Increment(ref active);
                maxObserved = Math.Max(maxObserved, cur);
                acquiredOrder.Enqueue(0);
                firstEntered.SetResult();
                await releaseFirst.Task;
                Interlocked.Decrement(ref active);
            });

            await firstEntered.Task;

            // Enqueue waiters 1, 2, 3 sequentially so arrival order at the FIFO queue is deterministic
            var waiterTasks = new List<Task>();
            for (int i = 1; i <= 3; i++)
            {
                int id = i;
                var queuedGate = Task.Run(async () =>
                {
                    await using var lease = await gate.AcquireAsync("playwright", "main", maxConcurrentRequests: 1);
                    int cur = Interlocked.Increment(ref active);
                    maxObserved = Math.Max(maxObserved, cur);
                    acquiredOrder.Enqueue(id);
                    await Task.Delay(15);
                    Interlocked.Decrement(ref active);
                });
                waiterTasks.Add(queuedGate);
                // Small pause ensures deterministic arrival order into the FIFO queue
                await Task.Delay(25);
            }

            releaseFirst.SetResult();
            await t0;
            await Task.WhenAll(waiterTasks);

            maxObserved.Should().Be(1);
            acquiredOrder.ToArray().Should().Equal(0, 1, 2, 3);
        }

        [Fact]
        [Requirement("MCP-CONCURRENCY-SLOTS", "MCP", RequirementType.Positive, "BackendConcurrencyGate isolates distinct slot keys such as 'main' and 'support' for concurrent execution.")]
        public async Task AcquireAsync_DistinctSlots_RunInParallelWhileSameSlotSerializes()
        {
            var gate = new BackendConcurrencyGate();
            var mainEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var supportEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseBoth = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var argsMain = JsonSerializer.SerializeToElement(new { role = "main" });
            var argsSupport = JsonSerializer.SerializeToElement(new { role = "support" });
            var server = new McpServer { Id = "playwright", ConcurrencyKeyField = "role,browser", MaxConcurrentRequests = 1 };

            var mainSlot = BackendConcurrencyGate.ResolveSlotKey(server, argsMain, null);
            var supportSlot = BackendConcurrencyGate.ResolveSlotKey(server, argsSupport, null);

            mainSlot.Should().Be("main");
            supportSlot.Should().Be("support");

            var mainTask = Task.Run(async () =>
            {
                await using var lease = await gate.AcquireAsync(server.Id, mainSlot, server.MaxConcurrentRequests);
                mainEntered.SetResult();
                await releaseBoth.Task;
            });

            var supportTask = Task.Run(async () =>
            {
                await using var lease = await gate.AcquireAsync(server.Id, supportSlot, server.MaxConcurrentRequests);
                supportEntered.SetResult();
                await releaseBoth.Task;
            });

            // Both main and support must enter concurrently without blocking each other
            await Task.WhenAll(mainEntered.Task, supportEntered.Task).WaitAsync(TimeSpan.FromSeconds(2));

            releaseBoth.SetResult();
            await Task.WhenAll(mainTask, supportTask);
        }

        [Fact]
        [Requirement("MCP-CONCURRENCY-KEY-OVERRIDE", "MCP", RequirementType.Positive, "ResolveSlotKey prioritizes explicit concurrency_key over server ConcurrencyKeyField.")]
        public void ResolveSlotKey_PrioritizesExplicitKeyOverConfiguredField()
        {
            var server = new McpServer { Id = "playwright", ConcurrencyKeyField = "role,browser" };
            var args = JsonSerializer.SerializeToElement(new { browser = "main" });

            BackendConcurrencyGate.ResolveSlotKey(server, args, "custom-slot").Should().Be("custom-slot");
            BackendConcurrencyGate.ResolveSlotKey(server, args, null).Should().Be("main");
            BackendConcurrencyGate.ResolveSlotKey(server, default, null).Should().Be("__default__");
        }

        [Fact]
        [Requirement("MCP-CONCURRENCY-CANCEL-SAFETY", "MCP", RequirementType.Positive, "Cancelling a queued waiter never leaks permits or blocks subsequent FIFO waiters.")]
        public async Task AcquireAsync_CancelledWaiter_DoesNotLeakSlot()
        {
            var gate = new BackendConcurrencyGate();
            var firstLease = await gate.AcquireAsync("playwright", "main", maxConcurrentRequests: 1);

            using var cts = new CancellationTokenSource();
            var waiter1 = gate.AcquireAsync("playwright", "main", maxConcurrentRequests: 1, cts.Token);
            var waiter2Acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var waiter2 = Task.Run(async () =>
            {
                await using var l2 = await gate.AcquireAsync("playwright", "main", maxConcurrentRequests: 1);
                waiter2Acquired.SetResult();
            });

            await Task.Delay(30);
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await waiter1);

            // Release first lease -> waiter2 must acquire cleanly without deadlock
            await firstLease.DisposeAsync();
            await waiter2Acquired.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await waiter2;
        }
    }
}
