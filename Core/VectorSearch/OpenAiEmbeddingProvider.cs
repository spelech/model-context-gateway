using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ModelContextGateway.Core.VectorSearch
{
    /// <summary>
    /// Configuration options for OpenAI-compatible embedding endpoints.
    /// </summary>
    public class OpenAiEmbeddingOptions
    {
        public string Endpoint { get; set; } = "https://api.openai.com/v1/embeddings";
        public string? ApiKey { get; set; }
        public string Model { get; set; } = "text-embedding-3-small";
        public int? Dimensions { get; set; }
    }

    /// <summary>
    /// Implements OpenAI-compatible vector embedding generation supporting OpenAI, Ollama (/v1/embeddings),
    /// Azure OpenAI, and LiteLLM proxies via HttpClient.
    /// </summary>
    public class OpenAiEmbeddingProvider : IEmbeddingProvider
    {
        private readonly HttpClient _httpClient;
        private readonly OpenAiEmbeddingOptions _options;
        private readonly ILogger<OpenAiEmbeddingProvider>? _logger;

        public OpenAiEmbeddingProvider(
            HttpClient httpClient,
            OpenAiEmbeddingOptions? options = null,
            ILogger<OpenAiEmbeddingProvider>? logger = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options ?? new OpenAiEmbeddingOptions();
            _logger = logger;
        }

        public OpenAiEmbeddingProvider(
            HttpClient httpClient,
            string endpoint,
            string? apiKey = null,
            string model = "text-embedding-3-small",
            ILogger<OpenAiEmbeddingProvider>? logger = null)
            : this(httpClient, new OpenAiEmbeddingOptions
            {
                Endpoint = endpoint,
                ApiKey = apiKey,
                Model = model
            }, logger)
        {
        }

        public OpenAiEmbeddingOptions Options => _options;

        /// <inheritdoc />
        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(text);
            var results = await GenerateEmbeddingsAsync(new[] { text }, cancellationToken);
            if (results.Count == 0)
            {
                throw new InvalidOperationException("Embedding provider returned empty response.");
            }
            return results[0];
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            if (texts == null || texts.Count == 0)
            {
                return Array.Empty<float[]>();
            }

            if (SecurityValidationHelper.IsPrivateOrLoopback(_options.Endpoint))
            {
                var allowPrivate = (Environment.GetEnvironmentVariable("MCG_ALLOW_PRIVATE_IPS") ?? Environment.GetEnvironmentVariable("ALLOW_PRIVATE_IPS")) == "true";
                if (!allowPrivate)
                {
                    throw new InvalidOperationException("Access to private or loopback IP ranges is blocked for security reasons.");
                }
            }

            _logger?.LogDebug("Generating embeddings for {Count} texts using model {Model} at {Endpoint}", texts.Count, _options.Model, _options.Endpoint);

            var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
                // Also attach api-key header for Azure OpenAI compatibility
                request.Headers.TryAddWithoutValidation("api-key", _options.ApiKey);
            }

            var payload = new Dictionary<string, object>
            {
                ["model"] = _options.Model,
                ["input"] = texts.Count == 1 ? (object)texts[0] : texts
            };

            if (_options.Dimensions.HasValue)
            {
                payload["dimensions"] = _options.Dimensions.Value;
            }

            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var dataArray) || dataArray.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Invalid embedding API response: missing or invalid 'data' array.");
            }

            var parsed = new List<(int Index, float[] Vector)>();
            int fallbackIdx = 0;

            foreach (var item in dataArray.EnumerateArray())
            {
                int index = item.TryGetProperty("index", out var idxProp) ? idxProp.GetInt32() : fallbackIdx++;
                var embProp = item.GetProperty("embedding");
                var vec = new float[embProp.GetArrayLength()];
                int vIdx = 0;
                foreach (var val in embProp.EnumerateArray())
                {
                    vec[vIdx++] = val.GetSingle();
                }
                parsed.Add((index, vec));
            }

            return parsed.OrderBy(p => p.Index).Select(p => p.Vector).ToList();
        }
    }
}
