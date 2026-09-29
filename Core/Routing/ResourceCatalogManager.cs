using System.Text.Json;
using System.Text.RegularExpressions;

namespace ModelContextGateway.Core.Routing
{
    /// <summary>
    /// Manages resource catalog searching and template pattern matching for MCP resources.
    /// </summary>
    public class ResourceCatalogManager
    {
        /// <summary>
        /// Searches through a list of resources matching the provided natural language query.
        /// </summary>
        /// <param name="query">The search term or query string.</param>
        /// <param name="resources">The raw list of resource definitions.</param>
        /// <returns>A task returning the top 15 matching resource items.</returns>
        public async Task<List<object>> SearchResourcesAsync(string query, List<object> resources)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return resources.Take(15).ToList();
            }

            var queryLower = query.ToLower();
            var results = new List<object>();

            foreach (var res in resources)
            {
                string name = "";
                string description = "";

                if (res is Dictionary<string, object> dict)
                {
                    if (dict.TryGetValue("name", out var n))
                    {
                        name = n?.ToString() ?? "";
                    }

                    if (dict.TryGetValue("description", out var d))
                    {
                        description = d?.ToString() ?? "";
                    }
                }
                else if (res is JsonElement je)
                {
                    if (je.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                    {
                        name = n.GetString() ?? "";
                    }

                    if (je.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String)
                    {
                        description = d.GetString() ?? "";
                    }
                }

                if (name.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ||
                    description.Contains(queryLower, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(res);
                }
            }

            return results.Take(15).ToList();
        }

        /// <summary>
        /// Attempts to match a virtual resource URI template against known patterns (e.g. logs://{serverId}/today).
        /// </summary>
        /// <param name="uri">The target resource URI to evaluate.</param>
        /// <param name="serverId">The extracted server identifier if matched.</param>
        /// <returns><c>true</c> if matched successfully; otherwise <c>false</c>.</returns>
        public bool TryMatchLogsTemplate(string uri, out string serverId)
        {
            var match = Regex.Match(uri, @"^logs://([^/]+)/today$");
            if (match.Success)
            {
                serverId = match.Groups[1].Value;
                return true;
            }
            serverId = string.Empty;
            return false;
        }
    }
}
