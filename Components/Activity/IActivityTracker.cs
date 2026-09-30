namespace ModelContextGateway.Components.Activity
{
    public interface IActivityTracker
    {
        Task RecordAppKeyUsageAsync(string keyId);
        Task RecordClientUsageAsync(string clientId);
    }
}
