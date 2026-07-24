namespace helengine.tools.buildwaiter {
    /// <summary>
    /// Describes one build command and the output artifacts that must be freshly produced before the build can be reported as complete.
    /// </summary>
    public sealed class BuildWaiterOptions {
        /// <summary>
        /// Initializes one validated build-waiter invocation.
        /// </summary>
        /// <param name="outputRootPath">Final output directory expected to contain the required artifacts.</param>
        /// <param name="requiredArtifactRelativePaths">Artifact paths relative to the output directory.</param>
        /// <param name="commandFileName">Executable file name for the child build process.</param>
        /// <param name="commandArguments">Arguments passed verbatim to the child build process.</param>
        public BuildWaiterOptions(
            string outputRootPath,
            string[] requiredArtifactRelativePaths,
            string commandFileName,
            string[] commandArguments) {
            if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("Output root path must be provided.", nameof(outputRootPath));
            } else if (requiredArtifactRelativePaths == null || requiredArtifactRelativePaths.Length == 0) {
                throw new ArgumentException("At least one required artifact path must be provided.", nameof(requiredArtifactRelativePaths));
            } else if (Array.Exists(requiredArtifactRelativePaths, string.IsNullOrWhiteSpace)) {
                throw new ArgumentException("Required artifact paths cannot be empty.", nameof(requiredArtifactRelativePaths));
            } else if (string.IsNullOrWhiteSpace(commandFileName)) {
                throw new ArgumentException("Build command file name must be provided.", nameof(commandFileName));
            } else if (commandArguments == null) {
                throw new ArgumentNullException(nameof(commandArguments));
            }

            OutputRootPath = Path.GetFullPath(outputRootPath);
            RequiredArtifactRelativePaths = [.. requiredArtifactRelativePaths];
            CommandFileName = commandFileName;
            CommandArguments = [.. commandArguments];
        }

        /// <summary>
        /// Gets the absolute final output directory expected to contain published build artifacts.
        /// </summary>
        public string OutputRootPath { get; }

        /// <summary>
        /// Gets the paths that must exist beneath the output directory after the child build succeeds.
        /// </summary>
        public string[] RequiredArtifactRelativePaths { get; }

        /// <summary>
        /// Gets the executable file name used to start the child build process.
        /// </summary>
        public string CommandFileName { get; }

        /// <summary>
        /// Gets the child-process arguments supplied after the command separator.
        /// </summary>
        public string[] CommandArguments { get; }
    }
}
