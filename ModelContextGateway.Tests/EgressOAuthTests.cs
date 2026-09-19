using System.Data;
using System.Security.Claims;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextGateway.Components.OAuth;
using Moq;

namespace ModelContextGateway.Tests
{
    public class EgressOAuthTests : IDisposable
    {
        private class NonDisposingConnection : IDbConnection
        {
            private readonly IDbConnection _inner;
            public NonDisposingConnection(IDbConnection inner) => _inner = inner;
            [System.Diagnostics.CodeAnalysis.AllowNull]
            public string ConnectionString { get => _inner.ConnectionString ?? string.Empty; set => _inner.ConnectionString = value ?? string.Empty; }
            public int ConnectionTimeout => _inner.ConnectionTimeout;
            public string Database => _inner.Database;
            public ConnectionState State => _inner.State;
            public IDbTransaction BeginTransaction() => _inner.BeginTransaction();
            public IDbTransaction BeginTransaction(IsolationLevel il) => _inner.BeginTransaction(il);
            public void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
            public void Close() { }
            public IDbCommand CreateCommand() => _inner.CreateCommand();
            public void Dispose() { }
            public void Open()
            {
                if (_inner.State != ConnectionState.Open)
                {
                    _inner.Open();
                }
            }
        }

        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;
            public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) => _handler = handler;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => _handler(request);
        }

        private readonly SqliteConnection _rawConnection;
        private readonly IDbConnectionFactory _dbFactory;
        private readonly IMemoryCache _memoryCache;

        public EgressOAuthTests()
        {
            _rawConnection = new SqliteConnection($"DataSource=file:mem_egress_oauth_{Guid.NewGuid():N}?mode=memory&cache=shared");
            _rawConnection.Open();
            DatabaseInitializer.InitializeDatabase(new NonDisposingConnection(_rawConnection));

            var mockFactory = new Mock<IDbConnectionFactory>();
            mockFactory.Setup(f => f.CreateConnection()).Returns(() => new NonDisposingConnection(_rawConnection));
            mockFactory.Setup(f => f.ProviderName).Returns("sqlite");
            _dbFactory = mockFactory.Object;

            _memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        public void Dispose()
        {
            _rawConnection.Dispose();
            _memoryCache.Dispose();
        }

        [Fact]
        [Requirement("AUTH-133", "AUTH", RequirementType.Positive, "Verify initiating OAuth 3LO egress flow generates secure state and authorization redirect URL.")]
        public async Task Authorize_WithValidServer_Initiates3LoFlowWithSecureStateAndRedirectUrl()
        {
            // Arrange
            using var conn = _dbFactory.CreateConnection();
            await conn.ExecuteAsync(@"
                INSERT INTO Servers (Id, DisplayName, Url, Enabled, EnableOAuth3Lo, OAuthClientId, OAuthClientSecret, OAuthAuthorizationUrl, OAuthTokenUrl, OAuthScopes)
                VALUES (@Id, @DisplayName, @Url, @Enabled, @EnableOAuth3Lo, @OAuthClientId, @OAuthClientSecret, @OAuthAuthorizationUrl, @OAuthTokenUrl, @OAuthScopes)",
                new
                {
                    Id = "google-drive-oauth",
                    DisplayName = "Google Drive",
                    Url = "http://localhost:8080/mcp",
                    Enabled = 1,
                    EnableOAuth3Lo = 1,
                    OAuthClientId = "test-client-id-123",
                    OAuthClientSecret = "test-client-secret-456",
                    OAuthAuthorizationUrl = "https://accounts.google.com/o/oauth2/v2/auth",
                    OAuthTokenUrl = "https://oauth2.googleapis.com/token",
                    OAuthScopes = "https://www.googleapis.com/auth/drive.readonly"
                });

            var mockSecretStore = new Mock<IUserSecretStore>();
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());

            var mockIdp = new Mock<IIdentityProvider>();
            var identityContext = new UserIdentityContext("alice", "TestAuth", new List<string>());
            mockIdp.Setup(i => i.ResolveIdentityAsync(It.IsAny<HttpContext>())).ReturnsAsync(identityContext);
            var compositeIdp = new CompositeIdentityProvider(new[] { mockIdp.Object });

            var controller = new OAuthEgressController(
                _dbFactory,
                mockSecretStore.Object,
                _memoryCache,
                mockHttpClientFactory.Object,
                compositeIdp,
                NullLogger<OAuthEgressController>.Instance);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("mcg.internal:8080");
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "alice") }, "TestAuth"));
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var result = await controller.Authorize("google-drive-oauth");

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.NotNull(redirectResult.Url);

            var uri = new Uri(redirectResult.Url);
            Assert.Equal("accounts.google.com", uri.Host);
            Assert.Equal("/o/oauth2/v2/auth", uri.AbsolutePath);

            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
            Assert.Equal("code", query["response_type"]);
            Assert.Equal("test-client-id-123", query["client_id"]);
            Assert.Equal("https://mcg.internal:8080/api/oauth/egress/callback", query["redirect_uri"]);
            Assert.Equal("https://www.googleapis.com/auth/drive.readonly", query["scope"]);

            var state = query["state"].ToString();
            Assert.False(string.IsNullOrWhiteSpace(state));

            // Verify state stored in cache
            var cacheKey = $"oauth_egress_state:{state}";
            Assert.True(_memoryCache.TryGetValue<OAuthEgressState>(cacheKey, out var cachedState));
            Assert.NotNull(cachedState);
            Assert.Equal("alice", cachedState.Username);
            Assert.Equal("google-drive-oauth", cachedState.ServerId);
        }

        [Fact]
        [Requirement("AUTH-134", "AUTH", RequirementType.Positive, "Verify OAuth egress callback exchanges authorization code and stores credentials in user secret store.")]
        public async Task Callback_WithValidStateAndCode_ExchangesTokenAndPersistsInUserSecretStore()
        {
            // Arrange
            using var conn = _dbFactory.CreateConnection();
            await conn.ExecuteAsync(@"
                INSERT INTO Servers (Id, DisplayName, Url, Enabled, EnableOAuth3Lo, OAuthClientId, OAuthClientSecret, OAuthAuthorizationUrl, OAuthTokenUrl)
                VALUES (@Id, @DisplayName, @Url, @Enabled, @EnableOAuth3Lo, @OAuthClientId, @OAuthClientSecret, @OAuthAuthorizationUrl, @OAuthTokenUrl)",
                new
                {
                    Id = "github-oauth",
                    DisplayName = "GitHub Copilot",
                    Url = "http://localhost:8080/mcp",
                    Enabled = 1,
                    EnableOAuth3Lo = 1,
                    OAuthClientId = "gh-client-id",
                    OAuthClientSecret = "gh-client-secret",
                    OAuthAuthorizationUrl = "https://github.com/login/oauth/authorize",
                    OAuthTokenUrl = "https://github.com/login/oauth/access_token"
                });

            var state = "secure-random-state-token-xyz";
            _memoryCache.Set($"oauth_egress_state:{state}", new OAuthEgressState
            {
                Username = "bob",
                ServerId = "github-oauth",
                CreatedAt = DateTimeOffset.UtcNow
            });

            string? capturedSavedUser = null;
            string? capturedSavedServerId = null;
            string? capturedSavedSecretJson = null;

            var mockSecretStore = new Mock<IUserSecretStore>();
            mockSecretStore
                .Setup(s => s.SaveSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((u, s, j) =>
                {
                    capturedSavedUser = u;
                    capturedSavedServerId = s;
                    capturedSavedSecretJson = j;
                })
                .Returns(Task.CompletedTask);

            var httpHandler = new MockHttpMessageHandler(async req =>
            {
                Assert.Equal("https://github.com/login/oauth/access_token", req.RequestUri?.ToString());
                Assert.Equal(HttpMethod.Post, req.Method);

                var body = await req.Content!.ReadAsStringAsync();
                Assert.Contains("grant_type=authorization_code", body);
                Assert.Contains("code=mock-auth-code-123", body);
                Assert.Contains("client_id=gh-client-id", body);
                Assert.Contains("client_secret=gh-client-secret", body);

                var jsonResp = @"{
                    ""access_token"": ""gho_testaccesstoken123456"",
                    ""token_type"": ""Bearer"",
                    ""expires_in"": 7200,
                    ""refresh_token"": ""ghr_testrefreshtoken987654""
                }";

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonResp, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(httpHandler));

            var mockIdp = new Mock<IIdentityProvider>();
            var identityContext = new UserIdentityContext("bob", "TestAuth", new List<string>());
            mockIdp.Setup(i => i.ResolveIdentityAsync(It.IsAny<HttpContext>())).ReturnsAsync(identityContext);
            var compositeIdp = new CompositeIdentityProvider(new[] { mockIdp.Object });

            var controller = new OAuthEgressController(
                _dbFactory,
                mockSecretStore.Object,
                _memoryCache,
                mockHttpClientFactory.Object,
                compositeIdp,
                NullLogger<OAuthEgressController>.Instance);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("mcg.internal:8080");
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var result = await controller.Callback(code: "mock-auth-code-123", state: state, error: null, error_description: null);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Equal("/my-servers?connected=github-oauth", redirectResult.Url);

            // Verify state was evicted from cache
            Assert.False(_memoryCache.TryGetValue($"oauth_egress_state:{state}", out _));

            // Verify secret persisted in store
            Assert.Equal("bob", capturedSavedUser);
            Assert.Equal("github-oauth", capturedSavedServerId);
            Assert.NotNull(capturedSavedSecretJson);

            var parsed = JsonNode.Parse(capturedSavedSecretJson) as JsonObject;
            Assert.NotNull(parsed);
            Assert.Equal("gho_testaccesstoken123456", parsed["access_token"]?.GetValue<string>());
            Assert.Equal("ghr_testrefreshtoken987654", parsed["refresh_token"]?.GetValue<string>());
            Assert.Equal("Bearer", parsed["token_type"]?.GetValue<string>());
            Assert.Equal(7200, parsed["expires_in"]?.GetValue<int>());
            Assert.True(parsed["expires_at"]?.GetValue<long>() > DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        [Fact]
        [Requirement("AUTH-135", "AUTH", RequirementType.Positive, "Verify upstream dispatch automatically refreshes expired OAuth egress token and updates user secret store.")]
        public async Task UpstreamDispatch_WithExpiredOAuthToken_RefreshesTokenAutomatically()
        {
            // Arrange
            var server = new McpServer
            {
                Id = "notion-oauth",
                DisplayName = "Notion MCP",
                Url = "http://localhost:8080/mcp",
                Enabled = true,
                EnableOAuth3Lo = true,
                OAuthClientId = "notion-client-id",
                OAuthClientSecret = "notion-client-secret",
                OAuthTokenUrl = "https://api.notion.com/v1/oauth/token"
            };

            // Existing secret with expired token
            var expiredSecretJson = @"{
                ""access_token"": ""expired-access-token-000"",
                ""token_type"": ""Bearer"",
                ""refresh_token"": ""valid-refresh-token-111"",
                ""expires_in"": 3600,
                ""expires_at"": 1000
            }";

            string? updatedSecretJson = null;
            var mockSecretStore = new Mock<IUserSecretStore>();
            mockSecretStore.Setup(s => s.GetSecretAsync("charlie", "notion-oauth")).ReturnsAsync(expiredSecretJson);
            mockSecretStore.Setup(s => s.SaveSecretAsync("charlie", "notion-oauth", It.IsAny<string>()))
                .Callback<string, string, string>((u, s, j) => updatedSecretJson = j)
                .Returns(Task.CompletedTask);

            bool refreshEndpointCalled = false;
            var httpHandler = new MockHttpMessageHandler(async req =>
            {
                if (req.RequestUri?.ToString() == "https://api.notion.com/v1/oauth/token")
                {
                    refreshEndpointCalled = true;
                    Assert.Equal(HttpMethod.Post, req.Method);
                    var body = await req.Content!.ReadAsStringAsync();
                    Assert.Contains("grant_type=refresh_token", body);
                    Assert.Contains("refresh_token=valid-refresh-token-111", body);
                    Assert.Contains("client_id=notion-client-id", body);
                    Assert.Contains("client_secret=notion-client-secret", body);

                    var newTokens = @"{
                        ""access_token"": ""refreshed-fresh-access-token-222"",
                        ""token_type"": ""Bearer"",
                        ""refresh_token"": ""refreshed-refresh-token-333"",
                        ""expires_in"": 3600
                    }";

                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent(newTokens, System.Text.Encoding.UTF8, "application/json")
                    };
                }

                // Upstream MCP tool call
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"status\":\"ok\"}}", System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(httpHandler);

            // Act - Test Token Extraction & Refresh through OAuthEgressTokenManager
            var resolvedToken = await OAuthEgressTokenManager.ExtractOrRefreshTokenAsync(
                server,
                null,
                "charlie",
                mockSecretStore.Object,
                httpClient,
                NullLogger.Instance);

            // Assert
            Assert.True(refreshEndpointCalled);
            Assert.Equal("refreshed-fresh-access-token-222", resolvedToken);

            // Verify updated secret persisted
            Assert.NotNull(updatedSecretJson);
            var parsed = JsonNode.Parse(updatedSecretJson) as JsonObject;
            Assert.NotNull(parsed);
            Assert.Equal("refreshed-fresh-access-token-222", parsed["access_token"]?.GetValue<string>());
            Assert.Equal("refreshed-refresh-token-333", parsed["refresh_token"]?.GetValue<string>());
            Assert.True(parsed["expires_at"]?.GetValue<long>() > DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            // Act 2 - Test HttpTransport resolution with the new refreshed secret
            mockSecretStore.Setup(s => s.GetSecretAsync("charlie", "notion-oauth")).ReturnsAsync(updatedSecretJson);
            refreshEndpointCalled = false; // Reset

            var transport = new HttpTransport(server, httpClient, NullLogger.Instance, userSecretStore: mockSecretStore.Object, forwardedUser: "charlie");
            var tokenFromTransport = await transport.ResolveTokenAsync();

            // Assert 2: Unexpired token resolved directly without calling refresh endpoint again
            Assert.False(refreshEndpointCalled);
            Assert.Equal("refreshed-fresh-access-token-222", tokenFromTransport);
        }

        [Fact]
        [Requirement("AUTH-136", "AUTH", RequirementType.Positive, "Verify disconnect endpoint deletes stored user OAuth credentials.")]
        public async Task Disconnect_DeletesUserSecretAndReturnsSuccess()
        {
            // Arrange
            string? deletedUser = null;
            string? deletedServerId = null;

            var mockSecretStore = new Mock<IUserSecretStore>();
            mockSecretStore.Setup(s => s.DeleteSecretAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((u, s) =>
                {
                    deletedUser = u;
                    deletedServerId = s;
                })
                .Returns(Task.CompletedTask);

            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            var mockIdp = new Mock<IIdentityProvider>();
            var identityContext = new UserIdentityContext("dave", "TestAuth", new List<string>());
            mockIdp.Setup(i => i.ResolveIdentityAsync(It.IsAny<HttpContext>())).ReturnsAsync(identityContext);
            var compositeIdp = new CompositeIdentityProvider(new[] { mockIdp.Object });

            var controller = new OAuthEgressController(
                _dbFactory,
                mockSecretStore.Object,
                _memoryCache,
                mockHttpClientFactory.Object,
                compositeIdp,
                NullLogger<OAuthEgressController>.Instance);

            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "dave") }, "TestAuth"));
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var result = await controller.Disconnect("slack-oauth");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            Assert.Equal("dave", deletedUser);
            Assert.Equal("slack-oauth", deletedServerId);
        }

        [Fact]
        [Requirement("AUTH-137", "AUTH", RequirementType.Positive, "Verify GetOAuthServers returns servers with correct isConnected status.")]
        public async Task GetOAuthServers_ReturnsConfiguredServersWithConnectionStatus()
        {
            // Arrange
            using var conn = _dbFactory.CreateConnection();
            await conn.ExecuteAsync(@"
                INSERT INTO Servers (Id, DisplayName, Url, Enabled, EnableOAuth3Lo)
                VALUES (@Id, @DisplayName, @Url, 1, 1)",
                new { Id = "s1-connected", DisplayName = "Server 1", Url = "http://s1/mcp" });

            await conn.ExecuteAsync(@"
                INSERT INTO Servers (Id, DisplayName, Url, Enabled, EnableOAuth3Lo)
                VALUES (@Id, @DisplayName, @Url, 1, 1)",
                new { Id = "s2-disconnected", DisplayName = "Server 2", Url = "http://s2/mcp" });

            var mockSecretStore = new Mock<IUserSecretStore>();
            mockSecretStore.Setup(s => s.GetServerIdsAsync("eve")).ReturnsAsync(new[] { "s1-connected" });

            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            var mockIdp = new Mock<IIdentityProvider>();
            var identityContext = new UserIdentityContext("eve", "TestAuth", new List<string>());
            mockIdp.Setup(i => i.ResolveIdentityAsync(It.IsAny<HttpContext>())).ReturnsAsync(identityContext);
            var compositeIdp = new CompositeIdentityProvider(new[] { mockIdp.Object });

            var controller = new OAuthEgressController(
                _dbFactory,
                mockSecretStore.Object,
                _memoryCache,
                mockHttpClientFactory.Object,
                compositeIdp,
                NullLogger<OAuthEgressController>.Instance);

            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "eve") }, "TestAuth"));
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var result = await controller.GetOAuthServers();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var jsonNode = System.Text.Json.JsonSerializer.SerializeToNode(okResult.Value) as JsonArray;
            Assert.NotNull(jsonNode);

            var s1 = jsonNode.FirstOrDefault(x => x?["id"]?.GetValue<string>() == "s1-connected");
            Assert.NotNull(s1);
            Assert.True(s1["isConnected"]?.GetValue<bool>());

            var s2 = jsonNode.FirstOrDefault(x => x?["id"]?.GetValue<string>() == "s2-disconnected");
            Assert.NotNull(s2);
            Assert.False(s2["isConnected"]?.GetValue<bool>());
        }
    }
}
