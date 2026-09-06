using System.Text.Json;

namespace ModelContextGateway.Core.Routing
{
    public class ApiEmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private RouterSettings _settings;

        public ApiEmbeddingService(HttpClient httpClient, RouterSettings settings)
        {
            _httpClient = httpClient;
            _settings = settings;
        }

        public void ReloadSettings(RouterSettings settings)
        {
            _settings = settings;
        }

        public RouterSettings GetSettings() => _settings;

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            if (SecurityValidationHelper.IsPrivateOrLoopback(_settings.EmbeddingApiUrl))
            {
                var allowPrivate = (Environment.GetEnvironmentVariable("MCG_ALLOW_PRIVATE_IPS") ?? Environment.GetEnvironmentVariable("ALLOW_PRIVATE_IPS")) == "true";
                if (!allowPrivate)
                {
                    throw new InvalidOperationException("Access to private or loopback IP ranges is blocked for security reasons.");
                }
            }

            var request = new HttpRequestMessage(HttpMethod.Post, _settings.EmbeddingApiUrl);
            if (!string.IsNullOrEmpty(_settings.EmbeddingApiKey))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.EmbeddingApiKey);
            }

            var payload = new
            {
                model = _settings.EmbeddingApiModel,
                input = text
            };

            request.Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var dataArray = root.GetProperty("data").EnumerateArray();

            dataArray.MoveNext();
            var embeddingElement = dataArray.Current.GetProperty("embedding");

            var list = new System.Collections.Generic.List<float>();
            foreach (var val in embeddingElement.EnumerateArray())
            {
                list.Add(val.GetSingle());
            }

            return list.ToArray();
        }

        public double CosineSimilarity(float[] vector1, float[] vector2)
        {
            return CalculateCosineSimilarity(vector1, vector2);
        }

        public static double CalculateCosineSimilarity(float[] v1, float[] v2)
        {
            if (v1 == null || v2 == null || v1.Length != v2.Length || v1.Length == 0)
            {
                return 0;
            }

            double dotProduct = 0.0;
            double normA = 0.0;
            double normB = 0.0;
            for (int i = 0; i < v1.Length; i++)
            {
                dotProduct += v1[i] * v2[i];
                normA += Math.Pow(v1[i], 2);
                normB += Math.Pow(v2[i], 2);
            }
            if (normA == 0.0 || normB == 0.0)
            {
                return 0.0;
            }

            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }
    }
}



