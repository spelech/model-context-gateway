namespace ModelContextGateway.Middleware
{
    public class McpAuthorizationSpecMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration? _configuration;

        public McpAuthorizationSpecMiddleware(RequestDelegate next, IConfiguration? configuration = null)
        {
            _next = next;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                if (context.Response.StatusCode == 401 || context.Response.StatusCode == 403)
                {
                    var canonicalUrl = ProtectedResourceHelper.GetCanonicalUrl(context, _configuration);
                    var rawPath = context.Request.Path.Value?.TrimStart('/') ?? "";

                    string resourceMetadataUrl;
                    if (string.IsNullOrEmpty(rawPath) ||
                        rawPath.Equals("sse", StringComparison.OrdinalIgnoreCase) ||
                        rawPath.Equals("message", StringComparison.OrdinalIgnoreCase) ||
                        rawPath.Equals("mcp/message", StringComparison.OrdinalIgnoreCase))
                    {
                        resourceMetadataUrl = $"{canonicalUrl}/.well-known/oauth-protected-resource";
                    }
                    else if (McpSpecMiddleware.IsMcpPath(context.Request.Path))
                    {
                        var targetServerId = rawPath.Split('/')[0];
                        resourceMetadataUrl = $"{canonicalUrl}/.well-known/oauth-protected-resource/{targetServerId}";
                    }
                    else
                    {
                        resourceMetadataUrl = $"{canonicalUrl}/.well-known/oauth-protected-resource";
                    }

                    if (context.Response.StatusCode == 401)
                    {
                        context.Response.Headers["WWW-Authenticate"] = $"Bearer realm=\"mcp\", resource_metadata=\"{resourceMetadataUrl}\"";
                    }
                    else if (context.Response.StatusCode == 403)
                    {
                        context.Response.Headers["WWW-Authenticate"] = $"Bearer realm=\"mcp\", error=\"insufficient_scope\", scope=\"mcp_client\", resource_metadata=\"{resourceMetadataUrl}\", error_description=\"Access denied: insufficient permissions or scope.\"";
                    }
                }
                return Task.CompletedTask;
            });

            await _next(context);
        }
    }
}

