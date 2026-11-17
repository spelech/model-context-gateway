using System.Text.Json;

namespace ModelContextGateway.Core.Routing
{
    public static class ToolErrorFormatter
    {
        public static string TransformError(JsonRpcError error, string toolName, string serverId)
        {
            var suggestion = GetActionableSuggestion(error.Message, toolName, serverId);
            var errObj = new
            {
                error = error.Message,
                code = error.Code,
                suggestion = suggestion,
                remediation = $"Check the logs using resources path: logs://{serverId}/today to diagnose connectivity issues."
            };
            return JsonSerializer.Serialize(errObj, new JsonSerializerOptions { WriteIndented = true });
        }

        public static string TransformException(Exception ex, string toolName, string serverId)
        {
            var suggestion = GetActionableSuggestion(ex.Message, toolName, serverId);
            var errObj = new
            {
                error = ex.Message,
                code = -32603,
                suggestion = suggestion,
                remediation = $"Check the logs using resources path: logs://{serverId}/today to diagnose connectivity issues."
            };
            return JsonSerializer.Serialize(errObj, new JsonSerializerOptions { WriteIndented = true });
        }

        public static string GetActionableSuggestion(string message, string toolName, string serverId)
        {
            var msg = message.ToLowerInvariant();
            if (msg.Contains("auth") || msg.Contains("unauthorized") || msg.Contains("forbidden") || msg.Contains("key") || msg.Contains("token"))
            {
                return $"Authentication/Authorization failure on backend '{serverId}'. Please verify your credentials or api keys in settings.";
            }
            if (msg.Contains("timeout") || msg.Contains("deadline") || msg.Contains("timed out"))
            {
                return $"Request to '{serverId}' timed out. You can pass a higher 'timeout_seconds' argument (or set RequestTimeoutSeconds on server '{serverId}') for slow operations, or check if the service is running and responsive.";
            }
            if (msg.Contains("conn") || msg.Contains("refused") || msg.Contains("socket") || msg.Contains("unreachable"))
            {
                return $"Network connection refused by backend '{serverId}'. Ensure that the container is running and host/port routes are open.";
            }
            if (msg.Contains("argument") || msg.Contains("parameter") || msg.Contains("invalid value") || msg.Contains("required"))
            {
                return $"Invalid arguments passed to tool '{toolName}'. Please call search_tools to audit the parameters schema.";
            }
            return $"An unexpected error occurred while executing '{toolName}' on backend '{serverId}'. Review the backend logs.";
        }
    }
}
