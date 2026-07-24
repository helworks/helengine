using helengine.tools.buildwaiter;

namespace helengine.tools.buildwaiter.tests {
    /// <summary>
    /// Verifies final artifact validation accepts only non-empty files produced by the current build invocation.
    /// </summary>
    public sealed class BuildArtifactVerifierTests {
        /// <summary>
        /// Ensures a required non-empty artifact written after build start satisfies the verification contract.
        /// </summary>
        [Fact]
        public void Verify_WhenArtifactsAreFreshAndNonEmpty_ReturnsSuccess() {
            string outputRootPath = Path.Combine(Path.GetTempPath(), "build-waiter-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputRootPath);
            try {
                DateTime buildStartedUtc = DateTime.UtcNow.AddSeconds(-1);
                string gameIsoPath = Path.Combine(outputRootPath, "game.iso");
                File.WriteAllText(gameIsoPath, "iso");
                File.SetLastWriteTimeUtc(gameIsoPath, DateTime.UtcNow);

                BuildArtifactVerificationResult result = new BuildArtifactVerifier().Verify(
                    outputRootPath,
                    ["game.iso"],
                    buildStartedUtc);

                Assert.True(result.Succeeded);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a pre-existing artifact cannot be mistaken for output produced by the current build.
        /// </summary>
        [Fact]
        public void Verify_WhenArtifactPredatesBuildStart_ReturnsFailure() {
            string outputRootPath = Path.Combine(Path.GetTempPath(), "build-waiter-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputRootPath);
            try {
                string gameIsoPath = Path.Combine(outputRootPath, "game.iso");
                File.WriteAllText(gameIsoPath, "old iso");
                File.SetLastWriteTimeUtc(gameIsoPath, DateTime.UtcNow.AddMinutes(-1));

                BuildArtifactVerificationResult result = new BuildArtifactVerifier().Verify(
                    outputRootPath,
                    ["game.iso"],
                    DateTime.UtcNow);

                Assert.False(result.Succeeded);
                Assert.Contains("stale", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures absent artifacts report a verification failure instead of a successful build.
        /// </summary>
        [Fact]
        public void Verify_WhenArtifactIsMissing_ReturnsFailure() {
            string outputRootPath = Path.Combine(Path.GetTempPath(), "build-waiter-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputRootPath);
            try {
                BuildArtifactVerificationResult result = new BuildArtifactVerifier().Verify(
                    outputRootPath,
                    ["game.iso"],
                    DateTime.UtcNow);

                Assert.False(result.Succeeded);
                Assert.Contains("missing", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures empty files cannot satisfy the required artifact contract.
        /// </summary>
        [Fact]
        public void Verify_WhenArtifactIsEmpty_ReturnsFailure() {
            string outputRootPath = Path.Combine(Path.GetTempPath(), "build-waiter-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputRootPath);
            try {
                string gameIsoPath = Path.Combine(outputRootPath, "game.iso");
                File.WriteAllText(gameIsoPath, string.Empty);
                File.SetLastWriteTimeUtc(gameIsoPath, DateTime.UtcNow);

                BuildArtifactVerificationResult result = new BuildArtifactVerifier().Verify(
                    outputRootPath,
                    ["game.iso"],
                    DateTime.UtcNow.AddSeconds(-1));

                Assert.False(result.Succeeded);
                Assert.Contains("empty", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }
    }
}
