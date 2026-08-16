namespace helengine.tools.buildwaiter {
    /// <summary>
    /// Provides the executable entry point for the build-waiter console tool.
    /// </summary>
    public static class Program {
        /// <summary>
        /// Runs the build-waiter console command and returns its process-compatible terminal exit code.
        /// </summary>
        /// <param name="args">Arguments supplied to the build-waiter executable.</param>
        /// <returns>Zero only when the child build succeeds and publishes every required current artifact.</returns>
        public static async Task<int> Main(string[] args) {
            return await RunAsync(args);
        }

        /// <summary>
        /// Parses one console invocation, waits for its child build command, and converts its terminal result into an exit code.
        /// </summary>
        /// <param name="args">Arguments supplied to the build-waiter executable.</param>
        /// <returns>Zero on verified success; otherwise a non-zero failure code.</returns>
        public static async Task<int> RunAsync(string[] args) {
            try {
                BuildWaiterOptions options = BuildWaiterOptionsParser.Parse(args);
                BuildWaiterResult result = await new BuildWaiter(
                    new BuildArtifactVerifier(),
                    new BuildStateVerifier()).WaitAsync(options, CancellationToken.None);
                if (!result.Succeeded) {
                    Console.Error.WriteLine("[build-waiter] " + result.Message);
                    return result.ExitCode == 0 ? 1 : result.ExitCode;
                }

                Console.WriteLine("[build-waiter] complete: " + result.Message);
                return 0;
            } catch (Exception exception) {
                Console.Error.WriteLine("[build-waiter] " + exception.Message);
                return 1;
            }
        }
    }
}
