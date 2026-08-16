using System.Text.Json;
using helengine.tools.buildwaiter;

namespace helengine.tools.buildwaiter.tests {
    /// <summary>
    /// Verifies build-state validation accepts only a complete successful state written by the current waiter invocation.
    /// </summary>
    public sealed class BuildStateVerifierTests {
        /// <summary>
        /// Ensures a complete successful state produced after waiter startup satisfies the state contract.
        /// </summary>
        [Fact]
        public void Verify_WhenStateIsCurrentAndSucceeded_ReturnsSuccess() {
            string outputRootPath = CreateOutputRoot();
            try {
                DateTime waiterStartedUtc = DateTime.UtcNow.AddSeconds(-2);
                DateTime stateStartedUtc = DateTime.UtcNow.AddSeconds(-1);
                WriteState(outputRootPath, "build-1", stateStartedUtc, stateStartedUtc.AddMilliseconds(500), "succeeded", 0);

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(outputRootPath, waiterStartedUtc);

                Assert.True(result.Succeeded);
                Assert.Contains("succeeded", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures an absent state file reports a descriptive verification failure.
        /// </summary>
        [Fact]
        public void Verify_WhenStateFileIsMissing_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                BuildStateVerificationResult result = new BuildStateVerifier().Verify(outputRootPath, DateTime.UtcNow);

                Assert.False(result.Succeeded);
                Assert.Contains("state", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("missing", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures malformed state JSON reports a parsing failure instead of escaping an exception.
        /// </summary>
        [Fact]
        public void Verify_WhenStateJsonIsMalformed_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                File.WriteAllText(GetStatePath(outputRootPath), "{ not-json");

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(outputRootPath, DateTime.UtcNow);

                Assert.False(result.Succeeded);
                Assert.Contains("state", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("malformed", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures an exclusively locked state file reports a read failure instead of escaping an exception.
        /// </summary>
        [Fact]
        public void Verify_WhenStateFileCannotBeRead_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                DateTime waiterStartedUtc = DateTime.UtcNow.AddSeconds(-2);
                DateTime stateStartedUtc = DateTime.UtcNow.AddSeconds(-1);
                WriteState(outputRootPath, "build-1", stateStartedUtc, DateTime.UtcNow, "succeeded", 0);
                using FileStream stateLock = new FileStream(
                    GetStatePath(outputRootPath),
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None);

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(outputRootPath, waiterStartedUtc);

                Assert.False(result.Succeeded);
                Assert.Contains("state", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("could not be read", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures an in-progress state cannot satisfy build completion.
        /// </summary>
        [Fact]
        public void Verify_WhenStateStatusIsRunning_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                DateTime waiterStartedUtc = DateTime.UtcNow.AddSeconds(-2);
                DateTime stateStartedUtc = DateTime.UtcNow.AddSeconds(-1);
                WriteState(outputRootPath, "build-1", stateStartedUtc, null, "running", null);

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(outputRootPath, waiterStartedUtc);

                Assert.False(result.Succeeded);
                Assert.Contains("running", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures an explicitly failed state cannot satisfy build completion.
        /// </summary>
        [Fact]
        public void Verify_WhenStateStatusIsFailed_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                DateTime waiterStartedUtc = DateTime.UtcNow.AddSeconds(-2);
                DateTime stateStartedUtc = DateTime.UtcNow.AddSeconds(-1);
                WriteState(outputRootPath, "build-1", stateStartedUtc, DateTime.UtcNow, "failed", 7);

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(outputRootPath, waiterStartedUtc);

                Assert.False(result.Succeeded);
                Assert.Contains("failed", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a state without a usable build identifier reports the missing identity.
        /// </summary>
        [Fact]
        public void Verify_WhenBuildIdIsBlank_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                DateTime waiterStartedUtc = DateTime.UtcNow.AddSeconds(-2);
                DateTime stateStartedUtc = DateTime.UtcNow.AddSeconds(-1);
                WriteState(outputRootPath, "   ", stateStartedUtc, DateTime.UtcNow, "succeeded", 0);

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(outputRootPath, waiterStartedUtc);

                Assert.False(result.Succeeded);
                Assert.Contains("build id", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a successful state without a completion timestamp remains incomplete.
        /// </summary>
        [Fact]
        public void Verify_WhenCompletionTimeIsMissing_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                DateTime waiterStartedUtc = DateTime.UtcNow.AddSeconds(-2);
                DateTime stateStartedUtc = DateTime.UtcNow.AddSeconds(-1);
                WriteState(outputRootPath, "build-1", stateStartedUtc, null, "succeeded", 0);

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(outputRootPath, waiterStartedUtc);

                Assert.False(result.Succeeded);
                Assert.Contains("completion", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("missing", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a completion timestamp cannot precede the recorded state start.
        /// </summary>
        [Fact]
        public void Verify_WhenCompletionPrecedesStateStart_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                DateTime waiterStartedUtc = DateTime.UtcNow.AddSeconds(-3);
                DateTime stateStartedUtc = DateTime.UtcNow.AddSeconds(-1);
                WriteState(outputRootPath, "build-1", stateStartedUtc, stateStartedUtc.AddSeconds(-1), "succeeded", 0);

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(outputRootPath, waiterStartedUtc);

                Assert.False(result.Succeeded);
                Assert.Contains("completion", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("before", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures state from a build started before this waiter invocation is rejected as stale.
        /// </summary>
        [Fact]
        public void Verify_WhenStateStartPredatesWaiter_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                DateTime waiterStartedUtc = DateTime.UtcNow;
                DateTime stateStartedUtc = waiterStartedUtc.AddSeconds(-1);
                WriteState(outputRootPath, "build-1", stateStartedUtc, stateStartedUtc.AddMilliseconds(500), "succeeded", 0);

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(outputRootPath, waiterStartedUtc);

                Assert.False(result.Succeeded);
                Assert.Contains("stale", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("predates", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a succeeded state with a nonzero exit code is rejected.
        /// </summary>
        [Fact]
        public void Verify_WhenExitCodeIsNonzero_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                DateTime waiterStartedUtc = DateTime.UtcNow.AddSeconds(-2);
                DateTime stateStartedUtc = DateTime.UtcNow.AddSeconds(-1);
                WriteState(outputRootPath, "build-1", stateStartedUtc, DateTime.UtcNow, "succeeded", 9);

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(outputRootPath, waiterStartedUtc);

                Assert.False(result.Succeeded);
                Assert.Contains("exit code", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("9", result.Message, StringComparison.Ordinal);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a succeeded state without an exit code is rejected.
        /// </summary>
        [Fact]
        public void Verify_WhenExitCodeIsMissing_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                DateTime waiterStartedUtc = DateTime.UtcNow.AddSeconds(-2);
                DateTime stateStartedUtc = DateTime.UtcNow.AddSeconds(-1);
                WriteState(outputRootPath, "build-1", stateStartedUtc, DateTime.UtcNow, "succeeded", null);

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(outputRootPath, waiterStartedUtc);

                Assert.False(result.Succeeded);
                Assert.Contains("exit code", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("missing", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures an empty output root is rejected consistently with artifact verification.
        /// </summary>
        [Fact]
        public void Verify_WhenOutputRootIsBlank_ThrowsArgumentException() {
            Assert.Throws<ArgumentException>(() => new BuildStateVerifier().Verify(" ", DateTime.UtcNow));
        }

        /// <summary>
        /// Creates one disposable output root for state-verifier testing.
        /// </summary>
        /// <returns>Absolute path to the created output root.</returns>
        static string CreateOutputRoot() {
            string outputRootPath = Path.Combine(Path.GetTempPath(), "build-waiter-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputRootPath);
            return outputRootPath;
        }

        /// <summary>
        /// Returns the canonical state-file path beneath one output root.
        /// </summary>
        /// <param name="outputRootPath">Output root containing build state.</param>
        /// <returns>Path to the build-state JSON file.</returns>
        static string GetStatePath(string outputRootPath) {
            return Path.Combine(outputRootPath, ".helengine-build-state.json");
        }

        /// <summary>
        /// Writes one complete wrapper-shaped state document with selected validation values.
        /// </summary>
        /// <param name="outputRootPath">Output root that receives the state file.</param>
        /// <param name="buildId">Build identifier value.</param>
        /// <param name="startedUtc">Recorded build start.</param>
        /// <param name="completedUtc">Recorded build completion, when present.</param>
        /// <param name="status">Recorded terminal status.</param>
        /// <param name="exitCode">Recorded terminal exit code, when present.</param>
        static void WriteState(
            string outputRootPath,
            string buildId,
            DateTime startedUtc,
            DateTime? completedUtc,
            string status,
            int? exitCode) {
            string json = JsonSerializer.Serialize(new {
                buildId,
                projectPath = "C:\\project\\project.heproj",
                platform = "ps2",
                buildProfile = "debug",
                configuration = "Debug",
                startedUtc,
                completedUtc,
                status,
                exitCode
            });
            File.WriteAllText(GetStatePath(outputRootPath), json);
        }
    }
}
