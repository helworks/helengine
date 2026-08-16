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
        /// Ensures a state that starts at the exact waiter boundary is current rather than stale.
        /// </summary>
        [Fact]
        public void Verify_WhenStateStartEqualsWaiterStart_ReturnsSuccess() {
            string outputRootPath = CreateOutputRoot();
            try {
                DateTime waiterStartedUtc = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);
                WriteState(outputRootPath, "build-1", waiterStartedUtc, waiterStartedUtc.AddSeconds(1), "succeeded", 0);

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(outputRootPath, waiterStartedUtc);

                Assert.True(result.Succeeded);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a started timestamp without an explicit UTC suffix is rejected.
        /// </summary>
        [Fact]
        public void Verify_WhenStartedTimestampHasNoOffset_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                WriteStateWithTimestampText(
                    outputRootPath,
                    "2026-08-15T12:00:00.0000000",
                    "2026-08-15T12:00:01.0000000Z");

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(
                    outputRootPath,
                    new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

                Assert.False(result.Succeeded);
                Assert.Contains("started", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("UTC", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Z", result.Message, StringComparison.Ordinal);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a completion timestamp without an explicit UTC suffix is rejected.
        /// </summary>
        [Fact]
        public void Verify_WhenCompletedTimestampHasNoOffset_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                WriteStateWithTimestampText(
                    outputRootPath,
                    "2026-08-15T12:00:00.0000000Z",
                    "2026-08-15T12:00:01.0000000");

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(
                    outputRootPath,
                    new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

                Assert.False(result.Succeeded);
                Assert.Contains("completed", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("UTC", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Z", result.Message, StringComparison.Ordinal);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a started timestamp with a nonzero numeric offset is rejected even when it represents a current instant.
        /// </summary>
        [Fact]
        public void Verify_WhenStartedTimestampHasNonzeroOffset_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                WriteStateWithTimestampText(
                    outputRootPath,
                    "2026-08-15T12:00:00.0000000+02:00",
                    "2026-08-15T12:00:01.0000000Z");

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(
                    outputRootPath,
                    new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

                Assert.False(result.Succeeded);
                Assert.Contains("started", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("UTC", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Z", result.Message, StringComparison.Ordinal);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a completion timestamp with a nonzero numeric offset is rejected even when ordering remains valid.
        /// </summary>
        [Fact]
        public void Verify_WhenCompletedTimestampHasNonzeroOffset_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                WriteStateWithTimestampText(
                    outputRootPath,
                    "2026-08-15T12:00:00.0000000Z",
                    "2026-08-16T12:00:00.0000000-07:00");

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(
                    outputRootPath,
                    new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

                Assert.False(result.Succeeded);
                Assert.Contains("completed", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("UTC", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Z", result.Message, StringComparison.Ordinal);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a zero numeric offset is rejected because the wrapper contract emits a literal UTC suffix.
        /// </summary>
        [Fact]
        public void Verify_WhenStartedTimestampUsesZeroNumericOffset_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                WriteStateWithTimestampText(
                    outputRootPath,
                    "2026-08-15T12:00:00.0000000+00:00",
                    "2026-08-15T12:00:01.0000000Z");

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(
                    outputRootPath,
                    new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

                Assert.False(result.Succeeded);
                Assert.Contains("started", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("literal 'Z'", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures case-insensitive JSON property matching remains enabled for timestamp inspection and deserialization.
        /// </summary>
        [Fact]
        public void Verify_WhenTimestampPropertyNamesUseDifferentCase_ReturnsSuccess() {
            string outputRootPath = CreateOutputRoot();
            try {
                WriteStateWithTimestampText(
                    outputRootPath,
                    "2026-08-15T12:00:00.0000000Z",
                    "2026-08-15T12:00:01.0000000Z",
                    "STARTEDUTC",
                    "COMPLETEDUTC");

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(
                    outputRootPath,
                    new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc));

                Assert.True(result.Succeeded);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a missing started timestamp cannot pass when its default value equals the waiter boundary.
        /// </summary>
        [Fact]
        public void Verify_WhenStartedTimestampPropertyIsMissing_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                WriteRawState(
                    outputRootPath,
                    "\"completedUtc\":\"2026-08-15T12:00:01.0000000Z\"");
                DateTime waiterStartedUtc = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(outputRootPath, waiterStartedUtc);

                Assert.False(result.Succeeded);
                Assert.Contains("started", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("exactly one", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the completed timestamp property is required even though its sole value may be null.
        /// </summary>
        [Fact]
        public void Verify_WhenCompletedTimestampPropertyIsMissing_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                WriteRawState(
                    outputRootPath,
                    "\"startedUtc\":\"2026-08-15T12:00:00.0000000Z\"");

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(
                    outputRootPath,
                    new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc));

                Assert.False(result.Succeeded);
                Assert.Contains("completed", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("exactly one", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures case-variant duplicate started timestamps are rejected before deserialization regardless of ordering.
        /// </summary>
        /// <param name="timestampPropertiesJson">Raw timestamp properties containing duplicate started variants.</param>
        [Theory]
        [InlineData("\"startedUtc\":\"2026-08-15T12:00:00.0000000Z\",\"STARTEDUTC\":\"2026-08-15T12:00:01.0000000Z\",\"completedUtc\":\"2026-08-15T12:00:02.0000000Z\"")]
        [InlineData("\"STARTEDUTC\":\"2026-08-15T12:00:01.0000000Z\",\"startedUtc\":\"2026-08-15T12:00:00.0000000Z\",\"completedUtc\":\"2026-08-15T12:00:02.0000000Z\"")]
        public void Verify_WhenStartedTimestampHasCaseVariantDuplicate_ReturnsFailure(string timestampPropertiesJson) {
            string outputRootPath = CreateOutputRoot();
            try {
                WriteRawState(outputRootPath, timestampPropertiesJson);

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(
                    outputRootPath,
                    new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

                Assert.False(result.Succeeded);
                Assert.Contains("started", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("exactly one", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures case-variant duplicate completion timestamps are rejected even when one value is null, regardless of ordering.
        /// </summary>
        /// <param name="timestampPropertiesJson">Raw timestamp properties containing duplicate completion variants.</param>
        [Theory]
        [InlineData("\"startedUtc\":\"2026-08-15T12:00:00.0000000Z\",\"completedUtc\":null,\"COMPLETEDUTC\":\"2026-08-15T12:00:01.0000000Z\"")]
        [InlineData("\"startedUtc\":\"2026-08-15T12:00:00.0000000Z\",\"COMPLETEDUTC\":\"2026-08-15T12:00:01.0000000Z\",\"completedUtc\":null")]
        public void Verify_WhenCompletedTimestampHasCaseVariantDuplicate_ReturnsFailure(string timestampPropertiesJson) {
            string outputRootPath = CreateOutputRoot();
            try {
                WriteRawState(outputRootPath, timestampPropertiesJson);

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(
                    outputRootPath,
                    new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

                Assert.False(result.Succeeded);
                Assert.Contains("completed", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("exactly one", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures lowercase z does not satisfy the wrapper's uppercase literal-Z contract.
        /// </summary>
        [Fact]
        public void Verify_WhenTimestampEndsInLowercaseZ_ReturnsFailure() {
            string outputRootPath = CreateOutputRoot();
            try {
                WriteRawState(
                    outputRootPath,
                    "\"startedUtc\":\"2026-08-15T12:00:00.0000000z\",\"completedUtc\":\"2026-08-15T12:00:01.0000000Z\"");

                BuildStateVerificationResult result = new BuildStateVerifier().Verify(
                    outputRootPath,
                    new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

                Assert.False(result.Succeeded);
                Assert.Contains("started", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("uppercase", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Z", result.Message, StringComparison.Ordinal);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a local waiter timestamp is rejected as ambiguous programmer input.
        /// </summary>
        [Fact]
        public void Verify_WhenWaiterStartIsLocal_ThrowsArgumentException() {
            string outputRootPath = CreateOutputRoot();
            try {
                ArgumentException exception = Assert.Throws<ArgumentException>(() => new BuildStateVerifier().Verify(
                    outputRootPath,
                    new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Local)));

                Assert.Equal("waiterStartedUtc", exception.ParamName);
                Assert.Contains("UTC", exception.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures an unspecified waiter timestamp is rejected as ambiguous programmer input.
        /// </summary>
        [Fact]
        public void Verify_WhenWaiterStartIsUnspecified_ThrowsArgumentException() {
            string outputRootPath = CreateOutputRoot();
            try {
                ArgumentException exception = Assert.Throws<ArgumentException>(() => new BuildStateVerifier().Verify(
                    outputRootPath,
                    new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Unspecified)));

                Assert.Equal("waiterStartedUtc", exception.ParamName);
                Assert.Contains("UTC", exception.Message, StringComparison.OrdinalIgnoreCase);
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

        /// <summary>
        /// Writes wrapper-shaped state while preserving caller-selected timestamp text and property casing.
        /// </summary>
        /// <param name="outputRootPath">Output root that receives the state file.</param>
        /// <param name="startedUtcText">Raw started timestamp JSON string.</param>
        /// <param name="completedUtcText">Raw completed timestamp JSON string.</param>
        /// <param name="startedPropertyName">JSON property name used for the started timestamp.</param>
        /// <param name="completedPropertyName">JSON property name used for the completed timestamp.</param>
        static void WriteStateWithTimestampText(
            string outputRootPath,
            string startedUtcText,
            string completedUtcText,
            string startedPropertyName = "startedUtc",
            string completedPropertyName = "completedUtc") {
            Dictionary<string, object> state = new Dictionary<string, object> {
                ["buildId"] = "build-1",
                ["projectPath"] = "C:\\project\\project.heproj",
                ["platform"] = "ps2",
                ["buildProfile"] = "debug",
                ["configuration"] = "Debug",
                [startedPropertyName] = startedUtcText,
                [completedPropertyName] = completedUtcText,
                ["status"] = "succeeded",
                ["exitCode"] = 0
            };
            File.WriteAllText(GetStatePath(outputRootPath), JsonSerializer.Serialize(state));
        }

        /// <summary>
        /// Writes a complete state object with caller-supplied raw timestamp properties.
        /// </summary>
        /// <param name="outputRootPath">Output root that receives the state file.</param>
        /// <param name="timestampPropertiesJson">Comma-delimited raw timestamp JSON properties.</param>
        static void WriteRawState(string outputRootPath, string timestampPropertiesJson) {
            string json = "{"
                + "\"buildId\":\"build-1\","
                + "\"projectPath\":\"C:\\\\project\\\\project.heproj\","
                + "\"platform\":\"ps2\","
                + "\"buildProfile\":\"debug\","
                + "\"configuration\":\"Debug\","
                + timestampPropertiesJson
                + ",\"status\":\"succeeded\","
                + "\"exitCode\":0"
                + "}";
            File.WriteAllText(GetStatePath(outputRootPath), json);
        }
    }
}
