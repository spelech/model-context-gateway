using System.Text.Json;

namespace ModelContextGateway.Core.Routing
{
    public partial class ClientSession
    {
        private string RewriteRequestJson(string body, string paramKey, string newValue)
        {
            try
            {
                var docOptions = new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                };
                var node = System.Text.Json.Nodes.JsonNode.Parse(body, null, docOptions);
                if (node == null)
                {
                    return body;
                }

                if (node is System.Text.Json.Nodes.JsonObject obj)
                {
                    RewriteObject(obj, paramKey, newValue);
                }
                else if (node is System.Text.Json.Nodes.JsonArray array)
                {
                    foreach (var item in array)
                    {
                        if (item is System.Text.Json.Nodes.JsonObject itemObj)
                        {
                            RewriteObject(itemObj, paramKey, newValue);
                        }
                    }
                }
                return node.ToJsonString();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to parse and rewrite JSON body for key '{ParamKey}' to '{NewValue}'", paramKey, newValue);
                return body;
            }
        }

        private static void RewriteObject(System.Text.Json.Nodes.JsonObject obj, string paramKey, string newValue)
        {
            System.Text.Json.Nodes.JsonObject? paramsObj = null;
            if (obj.TryGetPropertyValue("params", out var paramsNode) && paramsNode is System.Text.Json.Nodes.JsonObject foundParams)
            {
                foundParams[paramKey] = newValue;
                paramsObj = foundParams;
            }

            // In JSON-RPC 2.0 / MCP spec, root request objects must only have jsonrpc, id, method, params.
            // Any _meta property belongs inside params._meta.
            if (obj.TryGetPropertyValue("_meta", out var rootMeta))
            {
                obj.Remove("_meta");
            }

            var currentActivity = System.Diagnostics.Activity.Current;
            if (paramsObj != null)
            {
                if (!paramsObj.TryGetPropertyValue("_meta", out var metaNode) || metaNode is not System.Text.Json.Nodes.JsonObject)
                {
                    if (rootMeta is System.Text.Json.Nodes.JsonObject rootMetaObj)
                    {
                        paramsObj["_meta"] = rootMetaObj;
                        metaNode = rootMetaObj;
                    }
                    else if (currentActivity != null && !string.IsNullOrEmpty(currentActivity.Id))
                    {
                        var metaObj = new System.Text.Json.Nodes.JsonObject();
                        paramsObj["_meta"] = metaObj;
                        metaNode = metaObj;
                    }
                }

                if (currentActivity != null && !string.IsNullOrEmpty(currentActivity.Id) && metaNode is System.Text.Json.Nodes.JsonObject targetMeta)
                {
                    if (targetMeta["traceparent"] == null)
                    {
                        targetMeta["traceparent"] = currentActivity.Id;
                    }
                    if (targetMeta["tracestate"] == null && !string.IsNullOrEmpty(currentActivity.TraceStateString))
                    {
                        targetMeta["tracestate"] = currentActivity.TraceStateString;
                    }
                }
            }
        }
    }
}
