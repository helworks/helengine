using helengine.tools.buildwaiter;

namespace helengine.tools.buildwaiter.tests {
    /// <summary>
    /// Verifies the build waiter combines child-process completion with required-artifact validation.
    /// </summary>
    public sealed class BuildWaiterTests {
        /// <summary>
        /// Ensures a successful child process that writes current successful state and the required artifact produces a successful wait result.
        /// </summary>
        [Fact]
        public async Task WaitAsync_WhenChildWritesCurrentSuccessfulStateAndRequiredArtifact_ReturnsSuccess() {
            string outputRootPath = CreateOutputRoot();
            try {
                BuildWaiterOptions options = CreatePowerShellOptions(outputRootPath, true, "succeeded", 0, false, false, 0);

                BuildWaiterResult result = await CreateWaiter().WaitAsync(options, CancellationToken.None);

                Assert.True(result.Succeeded);
                Assert.Equal(0, result.ExitCode);
                Assert.Contains("fresh", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a failed child process reports its process exit code without accepting prior or missing artifacts.
        /// </summary>
        [Fact]
        public async Task WaitAsync_WhenChildFails_ReturnsFailureWithChildExitCode() {
            string outputRootPath = CreateOutputRoot();
            try {
                BuildWaiterOptions options = new(outputRootPath, ["game.iso"], "cmd.exe", ["/c", "exit 7"]);

                BuildWaiterResult result = await CreateWaiter().WaitAsync(options, CancellationToken.None);

                Assert.False(result.Succeeded);
                Assert.Equal(7, result.ExitCode);
                Assert.Contains("code 7", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a successful child process remains a failed build when it does not publish the required artifact.
        /// </summary>
        [Fact]
        public async Task WaitAsync_WhenChildOmitsRequiredArtifact_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                BuildWaiterOptions options = CreatePowerShellOptions(outputRootPath, false, "succeeded", 0, false, false, 0);

                BuildWaiterResult result = await CreateWaiter().WaitAsync(options, CancellationToken.None);

                Assert.False(result.Succeeded);
                Assert.Equal(1, result.ExitCode);
                Assert.Contains("missing", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a fresh successful state bearing a different build identity cannot satisfy the waiter.
        /// </summary>
        [Fact]
        public async Task WaitAsync_WhenChildWritesForeignBuildId_ReturnsStateFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                BuildWaiterOptions options = CreatePowerShellOptions(outputRootPath, true, "succeeded", 0, false, false, 0, true);

                BuildWaiterResult result = await CreateWaiter().WaitAsync(options, CancellationToken.None);

                Assert.False(result.Succeeded);
                Assert.Equal(1, result.ExitCode);
                Assert.Contains("build id", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures fresh artifacts cannot override a state that reports build failure.
        /// </summary>
        [Fact]
        public async Task WaitAsync_WhenChildWritesFreshArtifactAndFailedState_ReturnsStateFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                BuildWaiterOptions options = CreatePowerShellOptions(outputRootPath, true, "failed", 17, false, false, 0);

                BuildWaiterResult result = await CreateWaiter().WaitAsync(options, CancellationToken.None);

                Assert.False(result.Succeeded);
                Assert.Equal(1, result.ExitCode);
                Assert.Contains("state", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("failed", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures fresh artifacts cannot override state that still reports an active build.
        /// </summary>
        [Fact]
        public async Task WaitAsync_WhenChildWritesFreshArtifactAndRunningState_ReturnsStateFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                BuildWaiterOptions options = CreatePowerShellOptions(outputRootPath, true, "running", null, false, false, 0);

                BuildWaiterResult result = await CreateWaiter().WaitAsync(options, CancellationToken.None);

                Assert.False(result.Succeeded);
                Assert.Equal(1, result.ExitCode);
                Assert.Contains("state", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("running", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures fresh artifacts cannot override a missing state file.
        /// </summary>
        [Fact]
        public async Task WaitAsync_WhenChildWritesFreshArtifactWithoutState_ReturnsStateFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                BuildWaiterOptions options = CreatePowerShellOptions(outputRootPath, true, string.Empty, null, false, false, 0);

                BuildWaiterResult result = await CreateWaiter().WaitAsync(options, CancellationToken.None);

                Assert.False(result.Succeeded);
                Assert.Equal(1, result.ExitCode);
                Assert.Contains("state", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("missing", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures fresh artifacts cannot override malformed state JSON.
        /// </summary>
        [Fact]
        public async Task WaitAsync_WhenChildWritesFreshArtifactAndMalformedState_ReturnsStateFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                BuildWaiterOptions options = CreatePowerShellOptions(outputRootPath, true, string.Empty, null, false, true, 0);

                BuildWaiterResult result = await CreateWaiter().WaitAsync(options, CancellationToken.None);

                Assert.False(result.Succeeded);
                Assert.Equal(1, result.ExitCode);
                Assert.Contains("state", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("malformed", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures fresh artifacts cannot override successful state from an earlier invocation.
        /// </summary>
        [Fact]
        public async Task WaitAsync_WhenChildWritesFreshArtifactAndStaleState_ReturnsStateFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                BuildWaiterOptions options = CreatePowerShellOptions(outputRootPath, true, "succeeded", 0, true, false, 0);

                BuildWaiterResult result = await CreateWaiter().WaitAsync(options, CancellationToken.None);

                Assert.False(result.Succeeded);
                Assert.Equal(1, result.ExitCode);
                Assert.Contains("state", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("stale", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a subsequent build that takes the same output and overwrites shared compatibility state cannot race the first waiter's identity check.
        /// </summary>
        [Fact]
        public async Task WaitAsync_WhenSubsequentBuildOverwritesSharedStateBeforeVerification_UsesInvocationProof() {
            string outputRootPath = CreateOutputRoot();
            try {
                BuildWaiterOptions options = CreatePowerShellOptions(
                    outputRootPath,
                    true,
                    "succeeded",
                    0,
                    false,
                    false,
                    0,
                    overwriteSharedStateWithForeignBuild: true);

                BuildWaiterResult result = await CreateWaiter().WaitAsync(options, CancellationToken.None);

                Assert.True(result.Succeeded);
                Assert.Equal(0, result.ExitCode);
                Assert.Contains(
                    "22222222-2222-2222-2222-222222222222",
                    File.ReadAllText(Path.Combine(outputRootPath, ".helengine-build-state.json")),
                    StringComparison.Ordinal);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures both verifier dependencies are mandatory.
        /// </summary>
        [Fact]
        public void Constructor_WhenStateVerifierIsNull_ThrowsArgumentNullException() {
            Assert.Throws<ArgumentNullException>(() => new BuildWaiter(new BuildArtifactVerifier(), null));
        }

        /// <summary>
        /// Ensures the artifact verifier dependency remains mandatory with state verification enabled.
        /// </summary>
        [Fact]
        public void Constructor_WhenArtifactVerifierIsNull_ThrowsArgumentNullException() {
            Assert.Throws<ArgumentNullException>(() => new BuildWaiter(null, new BuildStateVerifier()));
        }

        /// <summary>
        /// Creates one build waiter with its production verifiers.
        /// </summary>
        /// <returns>A waiter configured for state-first artifact verification.</returns>
        static BuildWaiter CreateWaiter() {
            return new BuildWaiter(new BuildArtifactVerifier(), new BuildStateVerifier());
        }

        /// <summary>
        /// Creates one disposable output root for waiter integration testing.
        /// </summary>
        /// <returns>Absolute path to the created output root.</returns>
        static string CreateOutputRoot() {
            string outputRootPath = Path.Combine(Path.GetTempPath(), "build-waiter-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputRootPath);
            return outputRootPath;
        }

        /// <summary>
        /// Creates a PowerShell child command that independently controls artifact and state publication.
        /// </summary>
        /// <param name="outputRootPath">Output root receiving child-process files.</param>
        /// <param name="writeArtifact">Whether the child writes the required artifact.</param>
        /// <param name="stateStatus">State status to write, or empty to omit a normal state document.</param>
        /// <param name="stateExitCode">State exit code to write, when present.</param>
        /// <param name="writeStaleState">Whether the recorded state start predates waiter invocation.</param>
        /// <param name="writeMalformedState">Whether the state file contains malformed JSON.</param>
        /// <param name="childExitCode">Exit code returned by the child process.</param>
        /// <param name="writeForeignBuildId">Whether the expected proof contains a foreign build identity.</param>
        /// <param name="overwriteSharedStateWithForeignBuild">Whether a subsequent build replaces only shared compatibility state before verification.</param>
        /// <returns>Waiter options for the generated child command.</returns>
        static BuildWaiterOptions CreatePowerShellOptions(
            string outputRootPath,
            bool writeArtifact,
            string stateStatus,
            int? stateExitCode,
            bool writeStaleState,
            bool writeMalformedState,
            int childExitCode,
            bool writeForeignBuildId = false,
            bool overwriteSharedStateWithForeignBuild = false) {
            string outputRootLiteral = ConvertToPowerShellLiteral(outputRootPath);
            string sharedStatePath = ConvertToPowerShellLiteral(Path.Combine(outputRootPath, ".helengine-build-state.json"));
            string artifactPath = ConvertToPowerShellLiteral(Path.Combine(outputRootPath, "game.iso"));
            List<string> statements = [
                "$ErrorActionPreference = 'Stop'",
                $"$proofPath = Join-Path {outputRootLiteral} ('.helengine-build-state.' + $env:HELENGINE_BUILD_INVOCATION_ID + '.json')"
            ];
            statements.Add(writeStaleState
                ? "$startedUtc = [DateTime]::UtcNow.AddMinutes(-5)"
                : "$startedUtc = [DateTime]::UtcNow");
            if (writeArtifact) {
                statements.Add($"[System.IO.File]::WriteAllText({artifactPath}, 'iso')");
            }

            if (writeMalformedState) {
                statements.Add("[System.IO.File]::WriteAllText($proofPath, '{ not-json')");
                statements.Add($"[System.IO.File]::WriteAllText({sharedStatePath}, '{{ not-json')");
            } else if (!string.IsNullOrWhiteSpace(stateStatus)) {
                string stateExitCodeExpression = stateExitCode.HasValue
                    ? stateExitCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : "$null";
                statements.Add("$completedUtc = [DateTime]::UtcNow");
                string buildIdExpression = writeForeignBuildId
                    ? "'foreign-build-id'"
                    : "$env:HELENGINE_BUILD_INVOCATION_ID";
                statements.Add(
                    "$state = [ordered]@{ "
                    + $"buildId = {buildIdExpression}; projectPath = 'C:\\project\\project.heproj'; platform = 'ps2'; "
                    + "buildProfile = 'debug'; configuration = 'Debug'; startedUtc = $startedUtc.ToString('o'); "
                    + $"completedUtc = $completedUtc.ToString('o'); status = '{stateStatus}'; exitCode = {stateExitCodeExpression} }}");
                statements.Add("$stateJson = $state | ConvertTo-Json");
                statements.Add("$stateJson | Set-Content -LiteralPath $proofPath -Encoding UTF8");
                statements.Add($"$stateJson | Set-Content -LiteralPath {sharedStatePath} -Encoding UTF8");
                if (overwriteSharedStateWithForeignBuild) {
                    statements.Add(
                        "$foreignState = [ordered]@{ buildId = '22222222-2222-2222-2222-222222222222'; "
                        + "projectPath = 'C:\\foreign-project\\project.heproj'; platform = 'ps2'; "
                        + "buildProfile = 'debug'; configuration = 'Debug'; startedUtc = [DateTime]::UtcNow.ToString('o'); "
                        + "completedUtc = [DateTime]::UtcNow.ToString('o'); status = 'succeeded'; exitCode = 0 }");
                    statements.Add($"$foreignState | ConvertTo-Json | Set-Content -LiteralPath {sharedStatePath} -Encoding UTF8");
                }
            }

            statements.Add($"exit {childExitCode}");
            return new BuildWaiterOptions(
                outputRootPath,
                ["game.iso"],
                "powershell.exe",
                ["-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", string.Join("; ", statements)]);
        }

        /// <summary>
        /// Quotes one value as a single-quoted PowerShell literal.
        /// </summary>
        /// <param name="value">Raw value to quote.</param>
        /// <returns>PowerShell-safe single-quoted literal.</returns>
        static string ConvertToPowerShellLiteral(string value) {
            return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
        }
    }
}
