using Microsoft.Extensions.Logging;
using Moq;

namespace ModelContextGateway.Tests
{
    public class StdioTransportTests
    {
        public class CapturingLogger : ILogger
        {
            public List<string> LoggedMessages { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var msg = formatter(state, exception);
                LoggedMessages.Add($"{logLevel}: {msg}");
            }
        }

        private string GetMockScriptPath()
        {
            var paths = new[]
            {
                "mock_stdio.js",
                "ModelContextGateway.Tests/mock_stdio.js",
                "../ModelContextGateway.Tests/mock_stdio.js",
                "../../ModelContextGateway.Tests/mock_stdio.js",
                "../../../ModelContextGateway.Tests/mock_stdio.js",
                "../../../../ModelContextGateway.Tests/mock_stdio.js"
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }

            throw new FileNotFoundException("Could not find mock_stdio.js");
        }

        /// <summary>
        /// Verifies that STDIO transport spawns subprocess, handles JSON-RPC initialization and executes tool calls.
        /// </summary>
        [Fact]
        [Requirement("TRANS-STDIO-SPAWN-TOOL-CALL", "STDIO transport spawns subprocess, handles JSON-RPC initialization and executes tool calls", Type = RequirementType.Positive, Category = "TRANS")]
        public async Task StdioTransport_ShouldInitializeAndCallToolSuccessfully()
        {
            var scriptPath = GetMockScriptPath();
            var server = new McpServer
            {
                Id = "stdio-test",
                Url = $"node \"{scriptPath}\"",
                Type = "stdio",
                Enabled = true
            };

            var logger = new CapturingLogger();
            var stateManager = new JsonRpcStateManager();
            using var transport = new StdioTransport(server, logger, stateManager);

            await transport.ConnectAsync();

            var messagesReceived = new List<JsonRpcMessage>();
            transport.StartReader(async msg =>
            {
                lock (messagesReceived)
                {
                    messagesReceived.Add(msg);
                }
                await Task.CompletedTask;
            });

            // Send initialize request
            var initReq = "{\"jsonrpc\":\"2.0\",\"id\":\"init-1\",\"method\":\"initialize\",\"params\":{}}";
            var initResp = await transport.SendRequestAsync("initialize", initReq);
            Assert.NotNull(initResp);
            Assert.Null(initResp.Error);

            // Send tools/list request
            var listReq = "{\"jsonrpc\":\"2.0\",\"id\":\"list-1\",\"method\":\"tools/list\",\"params\":{}}";
            var listResp = await transport.SendRequestAsync("tools/list", listReq);
            Assert.NotNull(listResp);
            Assert.Null(listResp.Error);

            // Send tools/call request
            var callReq = "{\"jsonrpc\":\"2.0\",\"id\":\"call-1\",\"method\":\"tools/call\",\"params\":{\"name\":\"echo\",\"arguments\":{\"message\":\"hello stdio\"}}}";
            var callResp = await transport.SendRequestAsync("tools/call", callReq);
            Assert.NotNull(callResp);
            Assert.Null(callResp.Error);
            Assert.NotNull(callResp.Result);

            var resultStr = callResp.Result.ToString();
            Assert.Contains("hello stdio", resultStr);
        }

        /// <summary>
        /// Ensures STDIO transport rejects commands with shell metacharacters or dangerous commands.
        /// </summary>
        [Fact]
        [Requirement("GUARD-03", "STDIO transport rejects commands with shell metacharacters or dangerous commands", Type = RequirementType.Negative, Category = "GUARD")]
        public async Task StdioTransport_ShouldThrowSecurityExceptionForUnsafeExecutable()
        {
            var server = new McpServer
            {
                Id = "unsafe-test",
                Url = "node; rm -rf /",
                Type = "stdio",
                Enabled = true
            };

            var logger = new CapturingLogger();
            var stateManager = new JsonRpcStateManager();
            using var transport = new StdioTransport(server, logger, stateManager);

            await Assert.ThrowsAsync<System.Security.SecurityException>(() => transport.ConnectAsync());
        }

        /// <summary>
        /// Ensures STDIO transport rejects shell wrappers and script interpreters lacking explicit safe paths.
        /// </summary>
        [Fact]
        [Requirement("GUARD-03", "STDIO transport rejects shell wrappers and script interpreters lacking explicit safe paths", Type = RequirementType.Negative, Category = "GUARD")]
        public async Task StdioTransport_ShouldThrowSecurityExceptionForShellExecutable()
        {
            var server = new McpServer
            {
                Id = "shell-test",
                Url = "bash my_script.sh",
                Type = "stdio",
                Enabled = true
            };

            var logger = new CapturingLogger();
            var stateManager = new JsonRpcStateManager();
            using var transport = new StdioTransport(server, logger, stateManager);

            await Assert.ThrowsAsync<System.Security.SecurityException>(() => transport.ConnectAsync());
        }

        /// <summary>
        /// Ensures STDIO transport fails cleanly with InvalidOperationException when target binary is not found.
        /// </summary>
        [Fact]
        [Requirement("GUARD-03", "STDIO transport fails cleanly with InvalidOperationException when target binary is not found", Type = RequirementType.Negative, Category = "GUARD")]
        public async Task StdioTransport_ShouldThrowOnInvalidExecutable()
        {
            var server = new McpServer
            {
                Id = "invalid-test",
                Url = "non_existent_program_xyz_123",
                Type = "stdio",
                Enabled = true
            };

            var logger = new CapturingLogger();
            var stateManager = new JsonRpcStateManager();
            using var transport = new StdioTransport(server, logger, stateManager);

            await Assert.ThrowsAsync<InvalidOperationException>(() => transport.ConnectAsync());
        }

        /// <summary>
        /// Verifies that STDIO transport streams subprocess stderr asynchronously to logs.
        /// </summary>
        [Fact]
        [Requirement("TRANS-STDIO-STREAM-STDERR-LOGS", "STDIO transport streams subprocess stderr asynchronously to structured router diagnostic logs", Type = RequirementType.Positive, Category = "TRANS")]
        public async Task StdioTransport_ShouldRouteStderrToLogs()
        {
            var scriptPath = GetMockScriptPath();
            var server = new McpServer
            {
                Id = "stdio-test-stderr",
                Url = $"node \"{scriptPath}\"",
                Type = "stdio",
                Enabled = true
            };

            var logger = new CapturingLogger();
            var stateManager = new JsonRpcStateManager();
            using var transport = new StdioTransport(server, logger, stateManager);

            await transport.ConnectAsync();
            transport.StartReader(msg => Task.CompletedTask);

            // Call initialize first to establish session
            var initReq = "{\"jsonrpc\":\"2.0\",\"id\":\"init-1\",\"method\":\"initialize\",\"params\":{}}";
            await transport.SendRequestAsync("initialize", initReq);

            // Call stderr_tool to trigger stderr write in script
            var callReq = "{\"jsonrpc\":\"2.0\",\"id\":\"call-stderr\",\"method\":\"tools/call\",\"params\":{\"name\":\"stderr_tool\"}}";
            var resp = await transport.SendRequestAsync("tools/call", callReq);
            Assert.NotNull(resp);

            // Wait a small delay for stderr reader thread to flush to logs
            await Task.Delay(500);

            var warnings = logger.LoggedMessages.Where(m => m.Contains("Warning")).ToList();
            Assert.NotEmpty(warnings);
            Assert.Contains(warnings, w => w.Contains("LOG_FROM_STDERR_TOOL"));
        }

        /// <summary>
        /// Ensures STDIO transport times out requests exceeding configured execution duration limits.
        /// </summary>
        [Fact]
        [Requirement("GUARD-03", "STDIO transport cancels and times out requests exceeding configured execution duration limits", Type = RequirementType.Negative, Category = "GUARD")]
        public async Task StdioTransport_ShouldTimeoutOnSlowRequests()
        {
            var scriptPath = GetMockScriptPath();
            var server = new McpServer
            {
                Id = "stdio-test-timeout",
                Url = $"node \"{scriptPath}\"",
                Type = "stdio",
                Enabled = true
            };

            var logger = new CapturingLogger();
            var stateManager = new JsonRpcStateManager();
            using var transport = new StdioTransport(server, logger, stateManager);
            transport.RequestTimeout = TimeSpan.FromMilliseconds(500); // short timeout

            await transport.ConnectAsync();
            transport.StartReader(msg => Task.CompletedTask);

            var initReq = "{\"jsonrpc\":\"2.0\",\"id\":\"init-1\",\"method\":\"initialize\",\"params\":{}}";
            await transport.SendRequestAsync("initialize", initReq);

            var callReq = "{\"jsonrpc\":\"2.0\",\"id\":\"call-slow\",\"method\":\"tools/call\",\"params\":{\"name\":\"slow_tool\"}}";

            await Assert.ThrowsAsync<TimeoutException>(() => transport.SendRequestAsync("tools/call", callReq));
        }

        /// <summary>
        /// Verifies that STDIO transport terminates subprocess tree cleanly upon disposal or cancellation.
        /// </summary>
        [Fact]
        [Requirement("TRANS-STDIO-TERMINATE-CLEANLY", "STDIO transport terminates subprocess tree cleanly upon disposal or cancellation", Type = RequirementType.Positive, Category = "TRANS")]
        public async Task StdioTransport_ShouldSupportCancellationAndProcessTreeTermination()
        {
            var scriptPath = GetMockScriptPath();
            var server = new McpServer
            {
                Id = "stdio-test-cancel",
                Url = $"node \"{scriptPath}\"",
                Type = "stdio",
                Enabled = true
            };

            var logger = new CapturingLogger();
            var stateManager = new JsonRpcStateManager();
            System.Diagnostics.Process proc;
            int pid;
            using (var transport = new StdioTransport(server, logger, stateManager))
            {
                await transport.ConnectAsync();
                transport.StartReader(msg => Task.CompletedTask);

                // Get internal process using reflection for assertion
                var procField = typeof(StdioTransport).GetField("_process", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.NotNull(procField);
                proc = (System.Diagnostics.Process)procField.GetValue(transport)!;
                Assert.NotNull(proc);
                Assert.False(proc.HasExited);
                pid = proc.Id;
            }

            // After disposing, the process should be killed/terminated and the instance disposed
            await Task.Delay(500);
            try
            {
                var p = System.Diagnostics.Process.GetProcessById(pid);
                Assert.True(p.HasExited);
            }
            catch (ArgumentException)
            {
                // Process no longer exists in OS
            }
        }

        /// <summary>
        /// Ensures STDIO transport clears pending requests and fails closed upon unexpected child process termination.
        /// </summary>
        [Fact]
        [Requirement("GUARD-03", "STDIO transport clears pending requests and fails closed upon unexpected child process termination", Type = RequirementType.Negative, Category = "GUARD")]
        public async Task StdioTransport_ShouldHandleUnexpectedExit()
        {
            var scriptPath = GetMockScriptPath();
            var server = new McpServer
            {
                Id = "stdio-test-exit",
                Url = $"node \"{scriptPath}\"",
                Type = "stdio",
                Enabled = true
            };

            var logger = new CapturingLogger();
            var stateManager = new JsonRpcStateManager();
            using var transport = new StdioTransport(server, logger, stateManager);

            await transport.ConnectAsync();
            transport.StartReader(msg => Task.CompletedTask);

            // Setup a pending request
            var tcs = stateManager.CreateRequest("pending-request-id");

            // Kill process unexpectedly
            var procField = typeof(StdioTransport).GetField("_process", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var proc = (System.Diagnostics.Process)procField!.GetValue(transport)!;
            proc.Kill();

            // Wait a bit for EOF and HandleProcessExit
            await Task.Delay(500);

            // The pending request should have been cancelled/faulted because state manager was cleared on exit
            Assert.True(tcs.Task.IsCanceled || tcs.Task.IsFaulted || tcs.Task.IsCompleted);
        }

        /// <summary>
        /// Verifies that STDIO command-line tokenizer preserves quoted arguments and space escaping.
        /// </summary>
        [Fact]
        [Requirement("TRANS-STDIO-TOKENIZE-PRESERVE-QUOTES", "STDIO command-line tokenizer preserves quoted arguments and space escaping", Type = RequirementType.Positive, Category = "TRANS")]
        public void StdioTransport_ParseCommandLine_Handles_Quotes_And_Spaces()
        {
            var cmd = "node \"/path to/script.js\" --arg='val' plain_arg";
            var parsed = StdioTransport.ParseCommandLine(cmd);

            Assert.Equal(4, parsed.Count);
            Assert.Equal("node", parsed[0]);
            Assert.Equal("/path to/script.js", parsed[1]);
            Assert.Equal("--arg=val", parsed[2]);
            Assert.Equal("plain_arg", parsed[3]);
        }

        /// <summary>
        /// Verifies that STDIO transport securely injects secret credentials via environment variables rather than command-line arguments.
        /// </summary>
        [Fact]
        [Requirement("SEC-02", "STDIO transport securely injects secret credentials via environment variables rather than command-line arguments", Type = RequirementType.Positive, Category = "SEC")]
        public async Task StdioTransport_ShouldPassSecretViaEnvironmentVariables_AndNotCommandLine()
        {
            var scriptPath = GetMockScriptPath();
            var testSecret = "my_super_secret_env_token_998877";
            var server = new McpServer
            {
                Id = "stdio-env-test",
                Url = $"node \"{scriptPath}\"",
                Type = "stdio",
                Enabled = true,
                ApiKey = testSecret,
                SecretItemKey = "TEST_API_KEY"
            };

            var logger = new CapturingLogger();
            var stateManager = new JsonRpcStateManager();
            using var transport = new StdioTransport(server, logger, stateManager);

            await transport.ConnectAsync();
            transport.StartReader(msg => Task.CompletedTask);

            // Initialize
            var initReq = "{\"jsonrpc\":\"2.0\",\"id\":\"init-1\",\"method\":\"initialize\",\"params\":{}}";
            await transport.SendRequestAsync("initialize", initReq);

            // Ask mock script for environment variable TEST_API_KEY
            var callReq = "{\"jsonrpc\":\"2.0\",\"id\":\"call-env\",\"method\":\"tools/call\",\"params\":{\"name\":\"get_env\",\"arguments\":{\"key\":\"TEST_API_KEY\"}}}";
            var callResp = await transport.SendRequestAsync("tools/call", callReq);
            Assert.NotNull(callResp);
            Assert.Null(callResp.Error);
            Assert.NotNull(callResp.Result);

            var resultStr = callResp.Result.ToString();
            Assert.Contains(testSecret, resultStr);

            // Verify process startInfo command arguments do NOT contain the secret
            var procField = typeof(StdioTransport).GetField("_process", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var proc = (System.Diagnostics.Process)procField!.GetValue(transport)!;
            Assert.DoesNotContain(testSecret, proc.StartInfo.Arguments);
            foreach (var arg in proc.StartInfo.ArgumentList)
            {
                Assert.DoesNotContain(testSecret, arg);
            }
        }

        /// <summary>
        /// Ensures STDIO transport fails closed without spawning subprocess if secret resolution fails.
        /// </summary>
        [Fact]
        [Requirement("GUARD-02", "STDIO transport fails closed without spawning subprocess if secret resolution fails", Type = RequirementType.Negative, Category = "GUARD")]
        public async Task StdioTransport_ShouldFailClosed_WhenSecretResolutionFails()
        {
            var scriptPath = GetMockScriptPath();
            var server = new McpServer
            {
                Id = "stdio-failclosed-test",
                Url = $"node \"{scriptPath}\"",
                Type = "stdio",
                Enabled = true,
                SecretProvider = "Vault",
                SecretPath = "secret/data/mcp",
                SecretField = "api_key"
            };

            var mockRetriever = new Mock<ISecretRetriever>();
            mockRetriever.Setup(r => r.GetSecretAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new System.Security.SecurityException("Vault authentication failed"));

            var logger = new CapturingLogger();
            var stateManager = new JsonRpcStateManager();
            using var transport = new StdioTransport(server, logger, stateManager, mockRetriever.Object);

            // ConnectAsync must throw and fail closed without starting the subprocess
            await Assert.ThrowsAsync<System.Security.SecurityException>(() => transport.ConnectAsync());

            var procField = typeof(StdioTransport).GetField("_process", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var proc = procField!.GetValue(transport);
            Assert.Null(proc);
        }

        /// <summary>
        /// Verifies that subprocess logs and stderr streams are actively sanitized to mask sensitive tokens and credentials.
        /// </summary>
        [Fact]
        [Requirement("SEC-02", "Subprocess logs and stderr streams are actively sanitized to mask sensitive tokens and credentials", Type = RequirementType.Positive, Category = "SEC")]
        public async Task StdioTransport_ShouldSanitizeAndMaskSecretsInLogs()
        {
            var scriptPath = GetMockScriptPath();
            var sensitiveKey = "super_secret_token_value_12345";
            var server = new McpServer
            {
                Id = "stdio-mask-test",
                Url = $"node \"{scriptPath}\"",
                Type = "stdio",
                Enabled = true,
                ApiKey = sensitiveKey,
                SecretItemKey = "TEST_SECRET"
            };

            var logger = new CapturingLogger();
            var stateManager = new JsonRpcStateManager();
            using var transport = new StdioTransport(server, logger, stateManager);

            await transport.ConnectAsync();
            transport.StartReader(msg => Task.CompletedTask);

            var initReq = "{\"jsonrpc\":\"2.0\",\"id\":\"init-1\",\"method\":\"initialize\",\"params\":{}}";
            await transport.SendRequestAsync("initialize", initReq);

            // Call tool that outputs the secret to stderr and stdout
            var callReq = "{\"jsonrpc\":\"2.0\",\"id\":\"call-leak\",\"method\":\"tools/call\",\"params\":{\"name\":\"leak_secret_tool\"}}";
            var resp = await transport.SendRequestAsync("tools/call", callReq);
            Assert.NotNull(resp);

            await Task.Delay(500);

            // Ensure the raw secret does not appear anywhere in the captured logs
            foreach (var logMsg in logger.LoggedMessages)
            {
                Assert.DoesNotContain(sensitiveKey, logMsg);
            }
        }

        /// <summary>
        /// Verifies that STDIO transport drains buffered stdout/stderr streams to EOF when process exits rapidly.
        /// </summary>
        [Fact]
        [Requirement("TRANS-STDIO-DRAIN-BUFFER-EOF", "STDIO transport drains buffered stdout/stderr streams to EOF when process exits rapidly", Type = RequirementType.Positive, Category = "TRANS")]
        public async Task StdioTransport_ShouldDrainReaderStreamsToEOF_WhenProcessExitsImmediately()
        {
            var scriptPath = GetMockScriptPath();
            var server = new McpServer
            {
                Id = "stdio-eof-test",
                Url = $"node \"{scriptPath}\"",
                Type = "stdio",
                Enabled = true
            };

            var logger = new CapturingLogger();
            var stateManager = new JsonRpcStateManager();
            using var transport = new StdioTransport(server, logger, stateManager);

            await transport.ConnectAsync();
            transport.StartReader(msg => Task.CompletedTask);

            var initReq = "{\"jsonrpc\":\"2.0\",\"id\":\"init-1\",\"method\":\"initialize\",\"params\":{}}";
            await transport.SendRequestAsync("initialize", initReq);

            // Call exit_after_tool which writes response and exits after 10ms
            var callReq = "{\"jsonrpc\":\"2.0\",\"id\":\"call-exit\",\"method\":\"tools/call\",\"params\":{\"name\":\"exit_after_tool\"}}";
            var resp = await transport.SendRequestAsync("tools/call", callReq);

            Assert.NotNull(resp);
            Assert.Null(resp.Error);
            Assert.NotNull(resp.Result);
            Assert.Contains("drained_before_exit", resp.Result.ToString());
        }
    }
}
