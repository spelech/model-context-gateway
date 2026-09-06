using Dapper;

namespace ModelContextGateway.Core.Routing
{
    public class DynamicEmbeddingService : IEmbeddingService
    {
        private readonly ILogger<DynamicEmbeddingService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IServiceProvider _serviceProvider;

        private IEmbeddingService? _activeService;
        private RouterSettings _settings = new();
        private readonly object _lock = new();

        public DynamicEmbeddingService(HttpClient httpClient, ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
        {
            _httpClient = httpClient;
            _loggerFactory = loggerFactory;
            _serviceProvider = serviceProvider;
            _logger = loggerFactory.CreateLogger<DynamicEmbeddingService>();

            LoadSettings();
        }

        public virtual RouterSettings GetSettings()
        {
            lock (_lock)
            {
                return _settings;
            }
        }

        public bool IsReady
        {
            get
            {
                lock (_lock)
                {
                    return _activeService != null;
                }
            }
        }

        public string Status => IsReady ? "Ready" : "NotReady";

        public virtual void SaveSettings(RouterSettings newSettings)
        {
            if (newSettings.EmbeddingProvider != null && newSettings.EmbeddingProvider.Equals("api", StringComparison.OrdinalIgnoreCase))
            {
                if (SecurityValidationHelper.IsPrivateOrLoopback(newSettings.EmbeddingApiUrl))
                {
                    var allowPrivate = (Environment.GetEnvironmentVariable("MCG_ALLOW_PRIVATE_IPS") ?? Environment.GetEnvironmentVariable("ALLOW_PRIVATE_IPS")) == "true";
                    if (!allowPrivate)
                    {
                        throw new ArgumentException("Embedding URL points to a blocked private or loopback IP range.");
                    }
                }
            }

            lock (_lock)
            {
                _settings = newSettings;
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
                    using var conn = dbFactory.CreateConnection();

                    var exists = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Settings WHERE Id = 'default'");
                    if (exists == 0)
                    {
                        conn.Execute(@"INSERT INTO Settings (Id, DashboardTitle, DashboardIcon, EmbeddingProvider, EmbeddingApiUrl, EmbeddingApiKey, EmbeddingApiModel, EmbeddingModelDir, GlobalMaxKeys, UserMaxKeys)
                            VALUES ('default', @DashboardTitle, @DashboardIcon, @EmbeddingProvider, @EmbeddingApiUrl, @EmbeddingApiKey, @EmbeddingApiModel, @EmbeddingModelDir, @GlobalMaxKeys, @UserMaxKeys)", _settings);
                    }
                    else
                    {
                        conn.Execute(@"UPDATE Settings SET DashboardTitle = @DashboardTitle, DashboardIcon = @DashboardIcon, EmbeddingProvider = @EmbeddingProvider, EmbeddingApiUrl = @EmbeddingApiUrl, EmbeddingApiKey = @EmbeddingApiKey,
                            EmbeddingApiModel = @EmbeddingApiModel, EmbeddingModelDir = @EmbeddingModelDir,
                            GlobalMaxKeys = @GlobalMaxKeys, UserMaxKeys = @UserMaxKeys WHERE Id = 'default'", _settings);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save settings to the database");
                }
                ReloadActiveService();
            }
        }

        private void LoadSettings()
        {
            lock (_lock)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
                    using var conn = dbFactory.CreateConnection();

                    var dbSettings = conn.QueryFirstOrDefault<RouterSettings>("SELECT * FROM Settings WHERE Id = 'default'");
                    if (dbSettings != null)
                    {
                        _settings = dbSettings;
                    }
                    else
                    {
                        _settings = new RouterSettings();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load settings from DB, falling back to defaults");
                    _settings = new RouterSettings();
                }
                ReloadActiveService();
            }
        }

        private void ReloadActiveService()
        {
            if (_settings.EmbeddingProvider?.ToLower() == "api")
            {
                _logger.LogInformation("Activating external API embedding provider pointing to {Url}", _settings.EmbeddingApiUrl?.Replace(Environment.NewLine, "")?.Replace("\n", "")?.Replace("\r", ""));
                _activeService = new ApiEmbeddingService(_httpClient, _settings);
            }
            else
            {
                _logger.LogInformation("Activating local ONNX embedding provider (all-MiniLM-L6-v2)");
                _activeService = new OnnxEmbeddingService(_httpClient, _settings, _loggerFactory.CreateLogger<OnnxEmbeddingService>());
            }
        }

        public void ReloadSettings(RouterSettings settings)
        {
            lock (_lock)
            {
                _settings = settings;
                ReloadActiveService();
            }
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            if (_activeService == null)
            {
                ReloadActiveService();
            }
            return await _activeService!.GetEmbeddingAsync(text);
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text) => await GetEmbeddingAsync(text);

        public double CosineSimilarity(float[] vector1, float[] vector2)
        {
            if (_activeService != null)
            {
                return _activeService.CosineSimilarity(vector1, vector2);
            }

            if (vector1 == null || vector2 == null || vector1.Length != vector2.Length || vector1.Length == 0)
            {
                return 0.0;
            }

            double dotProduct = 0.0;
            double norm1 = 0.0;
            double norm2 = 0.0;

            for (int i = 0; i < vector1.Length; i++)
            {
                dotProduct += vector1[i] * vector2[i];
                norm1 += vector1[i] * vector1[i];
                norm2 += vector2[i] * vector2[i];
            }

            if (norm1 == 0.0 || norm2 == 0.0)
            {
                return 0.0;
            }

            return dotProduct / (Math.Sqrt(norm1) * Math.Sqrt(norm2));
        }

        public async Task PreWarmAsync()
        {
            if (_activeService == null)
            {
                ReloadActiveService();
            }
            await _activeService!.GetEmbeddingAsync("health check prewarm");
        }
    }
}


