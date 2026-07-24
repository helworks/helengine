namespace helengine.tools.buildwaiter {
    /// <summary>
    /// Parses and validates the command-line contract used to launch one verified platform build.
    /// </summary>
    public static class BuildWaiterOptionsParser {
        /// <summary>
        /// Parses one build-waiter command line into the child build process and required output-artifact contract.
        /// </summary>
        /// <param name="args">Arguments supplied to the build-waiter executable.</param>
        /// <returns>Validated options for launching and verifying one child build process.</returns>
        public static BuildWaiterOptions Parse(string[] args) {
            if (args == null) {
                throw new ArgumentNullException(nameof(args));
            }

            string outputRootPath = string.Empty;
            List<string> requiredArtifactRelativePaths = [];
            int separatorIndex = Array.IndexOf(args, "--");
            if (separatorIndex < 0) {
                throw new ArgumentException("Build command separator '--' must be provided.", nameof(args));
            } else if (separatorIndex == args.Length - 1) {
                throw new ArgumentException("A build command must follow the '--' separator.", nameof(args));
            }

            int argumentIndex = 0;
            while (argumentIndex < separatorIndex) {
                string argument = args[argumentIndex];
                if (string.Equals(argument, "--output", StringComparison.Ordinal)) {
                    if (argumentIndex + 1 >= separatorIndex) {
                        throw new ArgumentException("The '--output' option requires a directory path.", nameof(args));
                    } else if (!string.IsNullOrWhiteSpace(outputRootPath)) {
                        throw new ArgumentException("The '--output' option can be provided only once.", nameof(args));
                    }

                    outputRootPath = args[argumentIndex + 1];
                    argumentIndex += 2;
                } else if (string.Equals(argument, "--require", StringComparison.Ordinal)) {
                    if (argumentIndex + 1 >= separatorIndex) {
                        throw new ArgumentException("The '--require' option requires an artifact path.", nameof(args));
                    }

                    requiredArtifactRelativePaths.Add(args[argumentIndex + 1]);
                    argumentIndex += 2;
                } else {
                    throw new ArgumentException($"Unknown build-waiter option '{argument}'.", nameof(args));
                }
            }

            if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("The '--output' option must be provided.", nameof(args));
            } else if (requiredArtifactRelativePaths.Count == 0) {
                throw new ArgumentException("At least one '--require' option must be provided.", nameof(args));
            }

            string fullOutputRootPath = Path.GetFullPath(outputRootPath);
            for (int requiredArtifactIndex = 0; requiredArtifactIndex < requiredArtifactRelativePaths.Count; requiredArtifactIndex++) {
                ValidateRequiredArtifactPath(fullOutputRootPath, requiredArtifactRelativePaths[requiredArtifactIndex]);
            }

            string commandFileName = args[separatorIndex + 1];
            string[] commandArguments = args[(separatorIndex + 2)..];
            return new BuildWaiterOptions(fullOutputRootPath, [.. requiredArtifactRelativePaths], commandFileName, commandArguments);
        }

        /// <summary>
        /// Ensures one required artifact resolves strictly beneath the specified final output directory.
        /// </summary>
        /// <param name="fullOutputRootPath">Absolute normalized output directory path.</param>
        /// <param name="requiredArtifactRelativePath">Artifact path supplied through one require option.</param>
        static void ValidateRequiredArtifactPath(string fullOutputRootPath, string requiredArtifactRelativePath) {
            if (string.IsNullOrWhiteSpace(requiredArtifactRelativePath)) {
                throw new ArgumentException("Required artifact paths cannot be empty.", nameof(requiredArtifactRelativePath));
            } else if (Path.IsPathRooted(requiredArtifactRelativePath)) {
                throw new ArgumentException($"Required artifact path '{requiredArtifactRelativePath}' must be relative.", nameof(requiredArtifactRelativePath));
            }

            string fullArtifactPath = Path.GetFullPath(Path.Combine(fullOutputRootPath, requiredArtifactRelativePath));
            string relativeArtifactPath = Path.GetRelativePath(fullOutputRootPath, fullArtifactPath);
            if (string.Equals(relativeArtifactPath, ".", StringComparison.Ordinal)
                || relativeArtifactPath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || string.Equals(relativeArtifactPath, "..", StringComparison.Ordinal)
                || Path.IsPathRooted(relativeArtifactPath)) {
                throw new ArgumentException($"Required artifact path '{requiredArtifactRelativePath}' must remain beneath the output root.", nameof(requiredArtifactRelativePath));
            }
        }
    }
}
