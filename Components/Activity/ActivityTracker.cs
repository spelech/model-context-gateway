using System.Collections.Concurrent;

namespace ModelContextGateway.Components.Activity
{
    public class ActivityTracker : IActivityTracker
    {
        private readonly IAppKeyRepository _appKeyRepo;
        private readonly IOAuthClientRepository _clientRepo;
        private readonly ILogger<ActivityTracker> _logger;
        private readonly TimeSpan _throttleWindow;
        private readonly ConcurrentDictionary<string, DateTime> _lastAppKeyActivity = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastClientActivity = new();

        public ActivityTracker(
            IAppKeyRepository appKeyRepo,
            IOAuthClientRepository clientRepo,
            ILogger<ActivityTracker> logger,
            TimeSpan throttleWindow = default)
        {
            _appKeyRepo = appKeyRepo;
            _clientRepo = clientRepo;
            _logger = logger;
            _throttleWindow = throttleWindow > TimeSpan.Zero ? throttleWindow : TimeSpan.FromSeconds(60);
        }

        public async Task RecordAppKeyUsageAsync(string keyId)
        {
            if (string.IsNullOrEmpty(keyId))
            {
                return;
            }

            var now = DateTime.UtcNow;
            bool shouldUpdate = false;
            _lastAppKeyActivity.AddOrUpdate(
                keyId,
                _ => { shouldUpdate = true; return now; },
                (_, lastTime) =>
                {
                    if (now - lastTime >= _throttleWindow)
                    {
                        shouldUpdate = true;
                        return now;
                    }
                    return lastTime;
                });

            if (!shouldUpdate)
            {
                return;
            }

            try
            {
                await _appKeyRepo.UpdateLastUsedAsync(keyId, now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update LastUsedAt for AppKey {KeyId}", keyId);
            }
        }

        public async Task RecordClientUsageAsync(string clientId)
        {
            if (string.IsNullOrEmpty(clientId))
            {
                return;
            }

            var now = DateTime.UtcNow;
            bool shouldUpdate = false;
            _lastClientActivity.AddOrUpdate(
                clientId,
                _ => { shouldUpdate = true; return now; },
                (_, lastTime) =>
                {
                    if (now - lastTime >= _throttleWindow)
                    {
                        shouldUpdate = true;
                        return now;
                    }
                    return lastTime;
                });

            if (!shouldUpdate)
            {
                return;
            }

            try
            {
                await _clientRepo.UpdateLastUsedAsync(clientId, now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update LastUsedAt for OAuthClient {ClientId}", clientId);
            }
        }
    }
}
