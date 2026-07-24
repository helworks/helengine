using helengine.tools.buildwaiter;

namespace helengine.tools.buildwaiter.tests {
    /// <summary>
    /// Verifies the public console entry-point returns process-compatible failure codes for invalid invocations.
    /// </summary>
    public sealed class ProgramTests {
        /// <summary>
        /// Ensures incomplete command-line arguments fail without launching a child process.
        /// </summary>
        [Fact]
        public async Task RunAsync_WhenArgumentsAreInvalid_ReturnsOne() {
            int exitCode = await Program.RunAsync(["--output", "C:\\output"]);

            Assert.Equal(1, exitCode);
        }
    }
}
