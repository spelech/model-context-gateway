using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace ModelContextGateway.Core.Routing
{
    public class OnnxEmbeddingService : IEmbeddingService
    {
        private readonly ILogger<OnnxEmbeddingService> _logger;
        private RouterSettings _settings;
        private string _modelDir = "";
        private string _modelPath = "";
        private string _vocabPath = "";

        private InferenceSession? _session;
        private Tokenizer? _tokenizer;
        private readonly object _initLock = new();

        public OnnxEmbeddingService(HttpClient httpClient, RouterSettings settings, ILogger<OnnxEmbeddingService> logger)
        {
            _ = httpClient;
            _logger = logger;
            _settings = settings;
            SetupPaths();
        }

        public void ReloadSettings(RouterSettings settings)
        {
            _settings = settings;
            SetupPaths();
            lock (_initLock)
            {
                _session?.Dispose();
                _session = null;
                _tokenizer = null;
            }
        }

        public RouterSettings GetSettings() => _settings;
        public string GetModelDir() => _modelDir;

        private void SetupPaths()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _modelDir = Path.IsPathRooted(_settings.EmbeddingModelDir)
                ? _settings.EmbeddingModelDir
                : Path.Combine(baseDir, _settings.EmbeddingModelDir);

            _modelPath = Path.Combine(_modelDir, "model.onnx");
            _vocabPath = Path.Combine(_modelDir, "vocab.txt");
        }

        private async Task EnsureInitializedAsync()
        {
            if (_session != null && _tokenizer != null)
            {
                return;
            }

            if (!Directory.Exists(_modelDir))
            {
                Directory.CreateDirectory(_modelDir);
            }

            // 1. Download model if missing
            if (!File.Exists(_modelPath))
            {
                _logger.LogInformation("Downloading local ONNX embedding model (all-MiniLM-L6-v2) from Hugging Face...");
                using var handler = new SocketsHttpHandler { AllowAutoRedirect = true };
                using var downloadClient = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
                var response = await downloadClient.GetAsync("https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx");
                response.EnsureSuccessStatusCode();
                using var fs = new FileStream(_modelPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
                _logger.LogInformation("Local model downloaded successfully.");
            }

            // 2. Download vocabulary file if missing
            if (!File.Exists(_vocabPath))
            {
                _logger.LogInformation("Downloading model vocabulary file from Hugging Face...");
                using var handler = new SocketsHttpHandler { AllowAutoRedirect = true };
                using var downloadClient = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(2) };
                var response = await downloadClient.GetAsync("https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main/vocab.txt");
                response.EnsureSuccessStatusCode();
                using var fs = new FileStream(_vocabPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
                _logger.LogInformation("Vocabulary file downloaded successfully.");
            }

            lock (_initLock)
            {
                if (_session == null && File.Exists(_modelPath))
                {
                    _session = new InferenceSession(_modelPath);
                }
                if (_tokenizer == null && File.Exists(_vocabPath))
                {
                    _tokenizer = BertTokenizer.Create(_vocabPath);
                }
            }
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            await EnsureInitializedAsync();

            if (_tokenizer == null || _session == null)
            {
                return new float[384];
            }

            var tokens = _tokenizer.EncodeToIds(text);

            int seqLength = tokens.Count;
            if (seqLength == 0)
            {
                return new float[384];
            }

            if (seqLength <= 512)
            {
                return await GetEmbeddingForChunkAsync(tokens, 0, seqLength);
            }

            var hiddenDim = 384;
            var averageVector = new float[hiddenDim];
            int chunkCount = 0;

            for (int offset = 0; offset < seqLength; offset += 512)
            {
                int count = Math.Min(512, seqLength - offset);
                var chunkVector = await GetEmbeddingForChunkAsync(tokens, offset, count);
                for (int i = 0; i < hiddenDim; i++)
                {
                    averageVector[i] += chunkVector[i];
                }
                chunkCount++;
            }

            double sumSquare = 0.0;
            for (int i = 0; i < hiddenDim; i++)
            {
                averageVector[i] /= chunkCount;
                sumSquare += (double)averageVector[i] * averageVector[i];
            }

            double magnitude = Math.Sqrt(sumSquare);
            if (magnitude > 0)
            {
                for (int i = 0; i < hiddenDim; i++)
                {
                    averageVector[i] = (float)(averageVector[i] / magnitude);
                }
            }

            return averageVector;
        }

        private async Task<float[]> GetEmbeddingForChunkAsync(IReadOnlyList<int> tokens, int offset, int count)
        {
            var inputIds = new long[count];
            var attentionMask = new long[count];
            var tokenTypeIds = new long[count];

            for (int i = 0; i < count; i++)
            {
                inputIds[i] = tokens[offset + i];
                attentionMask[i] = 1;
                tokenTypeIds[i] = 0;
            }

            var inputDimensions = new[] { 1, count };

            var inputIdsTensor = new DenseTensor<long>(inputIds, inputDimensions);
            var attentionMaskTensor = new DenseTensor<long>(attentionMask, inputDimensions);
            var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, inputDimensions);

            var inputs = new System.Collections.Generic.List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
                NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
            };

            using var results = _session!.Run(inputs);
            var output = results[0].AsTensor<float>();

            int hiddenDim = output.Dimensions[2];
            var pooled = new float[hiddenDim];

            for (int h = 0; h < hiddenDim; h++)
            {
                float sum = 0f;
                for (int s = 0; s < count; s++)
                {
                    sum += output[0, s, h];
                }
                pooled[h] = sum / count;
            }

            double sumSquare = 0.0;
            for (int i = 0; i < hiddenDim; i++)
            {
                sumSquare += (double)pooled[i] * pooled[i];
            }
            double magnitude = Math.Sqrt(sumSquare);

            if (magnitude > 0)
            {
                for (int i = 0; i < hiddenDim; i++)
                {
                    pooled[i] = (float)(pooled[i] / magnitude);
                }
            }

            return pooled;
        }

        public double CosineSimilarity(float[] vector1, float[] vector2)
        {
            return ApiEmbeddingService.CalculateCosineSimilarity(vector1, vector2);
        }
    }
}


