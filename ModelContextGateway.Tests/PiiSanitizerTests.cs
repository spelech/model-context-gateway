namespace ModelContextGateway.Tests
{
    public class PiiSanitizerTests
    {
        [Fact]
        [Requirement("SEC-PII-BEARER-TOKEN", "SEC", RequirementType.Positive, "PiiSanitizer redacts Bearer authentication tokens from payload strings.")]
        public void SanitizePayload_Redacts_Bearer_Tokens()
        {
            string raw = "{\"headers\": {\"Authorization\": \"Bearer secret_token_xyz_123\"}}";
            string clean = PiiSanitizer.SanitizePayload(raw);

            Assert.DoesNotContain("secret_token_xyz_123", clean);
            Assert.Contains("[REDACTED]", clean);
        }

        [Fact]
        [Requirement("SEC-PII-JSON-APIKEY-PASSWORD", "SEC", RequirementType.Positive, "PiiSanitizer redacts apiKey, password, and secret properties in JSON payloads.")]
        public void SanitizePayload_Redacts_Api_Keys_And_Passwords()
        {
            string raw = "{\"apiKey\":\"my_secret_key\",\"password\":\"super_secret\"}";
            string clean = PiiSanitizer.SanitizePayload(raw);

            Assert.DoesNotContain("my_secret_key", clean);
            Assert.DoesNotContain("super_secret", clean);
            Assert.Contains("\"apiKey\":\"[REDACTED]\"", clean);
            Assert.Contains("\"password\":\"[REDACTED]\"", clean);
        }

        [Fact]
        [Requirement("SEC-PII-CONNECTION-STRING-PASSWORD", "SEC", RequirementType.Positive, "PiiSanitizer redacts database connection string passwords.")]
        public void SanitizePayload_Redacts_ConnectionString_Passwords()
        {
            string raw = "Data Source=mcp_router.db;Password=MySecretPassword123;Version=3;";
            string clean = PiiSanitizer.SanitizePayload(raw);

            Assert.DoesNotContain("MySecretPassword123", clean);
            Assert.Contains("Password=[REDACTED]", clean);
        }

        [Fact]
        [Requirement("SEC-PII-LOGBUFFER-IN-MEMORY", "SEC", RequirementType.Positive, "LogBuffer sanitizes PII and credentials prior to buffering in memory.")]
        public void LogBuffer_Add_Sanitizes_PII_Payloads()
        {
            LogBuffer.Clear();
            LogBuffer.Add(Microsoft.Extensions.Logging.LogLevel.Information, "TestCategory", "This is an API key check: {\"apiKey\":\"super_secret_key_123\"}", null);

            var logs = LogBuffer.GetLogs();
            Assert.Single(logs);
            Assert.DoesNotContain("super_secret_key_123", logs[0].Message);
            Assert.Contains("[REDACTED]", logs[0].Message);
        }

        [Fact]
        [Requirement("SEC-PII-BASIC-COOKIE-QUERY-USERINFO", "SEC", RequirementType.Positive, "PiiSanitizer redacts Basic auth credentials, session cookies, query access tokens, and URL userinfo credentials.")]
        public void PiiSanitizer_Redacts_Basic_ApiKey_Cookie_QueryToken_UrlUserInfo()
        {
            string rawToken = "Basic dXNlcjpwYXNz";
            string rawHeader = "Authorization: Basic dXNlcjpwYXNz\r\nCookie: session=12345";
            string rawQuery = "https://example.com/api?access_token=secret_token_abc";
            string rawUserInfo = "https://user:pass123@example.com/mcp";

            string cleanToken = PiiSanitizer.SanitizePayload(rawToken);
            string cleanHeader = PiiSanitizer.SanitizePayload(rawHeader);
            string cleanQuery = PiiSanitizer.SanitizePayload(rawQuery);
            string cleanUserInfo = PiiSanitizer.SanitizePayload(rawUserInfo);

            Assert.DoesNotContain("dXNlcjpwYXNz", cleanToken);
            Assert.DoesNotContain("12345", cleanHeader);
            Assert.DoesNotContain("secret_token_abc", cleanQuery);
            Assert.DoesNotContain("pass123", cleanUserInfo);
        }
    }
}
