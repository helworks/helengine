using helengine.tools.buildwaiter;

namespace helengine.tools.buildwaiter.tests {
    /// <summary>
    /// Verifies the build waiter combines child-process completion with required-artifact validation.
    /// </summary>
    public sealed class BuildWaiterTests {
        /// <summary>
        /// Ensures a successful child process that writes the required artifact produces a successful wait result.
        /// </summary>
        [Fact]
        public async Task WaitAsync_WhenChildWritesRequiredArtifact_ReturnsSuccess() {
            string outputRootPath = Path.Combine(Path.GetTempPath(), "build-waiter-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputRootPath);
            try {
                string artifactPath = Path.Combine(outputRootPath, "game.iso");
                BuildWaiterOptions options = new(
                    outputRootPath,
                    ["game.iso"],
                    "cmd.exe",
                    ["/c", $"echo iso>{artifactPath}"]);

                BuildWaiterResult result = await new BuildWaiter(new BuildArtifactVerifier()).WaitAsync(options, CancellationToken.None);

                Assert.True(result.Succeeded);
                Assert.Equal(0, result.ExitCode);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a failed child process reports its process exit code without accepting prior or missing artifacts.
        /// </summary>
        [Fact]
        public async Task WaitAsync_WhenChildFails_ReturnsFailureWithChildExitCode() {
            string outputRootPath = Path.Combine(Path.GetTempPath(), "build-waiter-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputRootPath);
            try {
                BuildWaiterOptions options = new(outputRootPath, ["game.iso"], "cmd.exe", ["/c", "exit 7"]);

                BuildWaiterResult result = await new BuildWaiter(new BuildArtifactVerifier()).WaitAsync(options, CancellationToken.None);

                Assert.False(result.Succeeded);
                Assert.Equal(7, result.ExitCode);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a successful child process remains a failed build when it does not publish the required artifact.
        /// </summary>
        [Fact]
        public async Task WaitAsync_WhenChildOmitsRequiredArtifact_ReturnsFailure() {
            string outputRootPath = Path.Combine(Path.GetTempPath(), "build-waiter-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputRootPath);
            try {
                BuildWaiterOptions options = new(outputRootPath, ["game.iso"], "cmd.exe", ["/c", "exit 0"]);

                BuildWaiterResult result = await new BuildWaiter(new BuildArtifactVerifier()).WaitAsync(options, CancellationToken.None);

                Assert.False(result.Succeeded);
                Assert.Equal(1, result.ExitCode);
                Assert.Contains("missing", result.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(outputRootPath, true);
            }
        }
    }
}
