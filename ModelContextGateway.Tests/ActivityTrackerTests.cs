using Microsoft.Extensions.Logging.Abstractions;
using ModelContextGateway.Components.Activity;
using Moq;

namespace ModelContextGateway.Tests
{
    public class ActivityTrackerTests
    {
        [Fact]
        [Requirement("AUTH-120", "AUTH", RequirementType.Positive, "ActivityTracker throttles database updates to at most once per 60 seconds per key.")]
        public async Task RecordAppKeyUsage_DebouncesWithinWindow()
        {
            var appKeyRepoMock = new Mock<IAppKeyRepository>();
            var clientRepoMock = new Mock<IOAuthClientRepository>();

            var tracker = new ActivityTracker(
                appKeyRepoMock.Object,
                clientRepoMock.Object,
                NullLogger<ActivityTracker>.Instance,
                throttleWindow: TimeSpan.FromSeconds(60));

            // First call triggers DB update
            await tracker.RecordAppKeyUsageAsync("key-123");
            // Immediate subsequent calls within 60s should be debounced
            await tracker.RecordAppKeyUsageAsync("key-123");
            await tracker.RecordAppKeyUsageAsync("key-123");

            appKeyRepoMock.Verify(r => r.UpdateLastUsedAsync("key-123", It.IsAny<DateTime>()), Times.Once);

            // A different key triggers its own update
            await tracker.RecordAppKeyUsageAsync("key-456");
            appKeyRepoMock.Verify(r => r.UpdateLastUsedAsync("key-456", It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        [Requirement("AUTH-121", "AUTH", RequirementType.Positive, "ActivityTracker throttles OAuth client updates to at most once per 60 seconds per client.")]
        public async Task RecordClientUsage_DebouncesWithinWindow()
        {
            var appKeyRepoMock = new Mock<IAppKeyRepository>();
            var clientRepoMock = new Mock<IOAuthClientRepository>();

            var tracker = new ActivityTracker(
                appKeyRepoMock.Object,
                clientRepoMock.Object,
                NullLogger<ActivityTracker>.Instance,
                throttleWindow: TimeSpan.FromSeconds(60));

            await tracker.RecordClientUsageAsync("client-abc");
            await tracker.RecordClientUsageAsync("client-abc");

            clientRepoMock.Verify(r => r.UpdateLastUsedAsync("client-abc", It.IsAny<DateTime>()), Times.Once);
        }
    }
}
