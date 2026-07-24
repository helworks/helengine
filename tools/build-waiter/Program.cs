namespace helengine.tools.buildwaiter {
    /// <summary>
    /// Provides the executable entry point for the build-waiter console tool.
    /// </summary>
    public static class Program {
        /// <summary>
        /// Returns a temporary failure code until the production command runner is introduced with the process-waiting implementation.
        /// </summary>
        /// <param name="args">Arguments supplied to the build-waiter executable.</param>
        /// <returns>One because this initial parser slice cannot run child builds yet.</returns>
        public static int Main(string[] args) {
            return 1;
        }
    }
}
