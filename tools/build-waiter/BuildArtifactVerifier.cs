namespace helengine.tools.buildwaiter {
    /// <summary>
    /// Verifies that required output files exist, contain data, and were written by the current build invocation.
    /// </summary>
    public sealed class BuildArtifactVerifier {
        /// <summary>
        /// Validates every required artifact beneath the supplied output root against the supplied build-start time.
        /// </summary>
        /// <param name="outputRootPath">Final output directory containing the published build artifacts.</param>
        /// <param name="requiredArtifactRelativePaths">Required artifact paths relative to the output directory.</param>
        /// <param name="buildStartedUtc">UTC timestamp captured immediately before the child build process starts.</param>
        /// <returns>Success when all required artifacts are fresh and non-empty; otherwise the first validation failure.</returns>
        public BuildArtifactVerificationResult Verify(
            string outputRootPath,
            string[] requiredArtifactRelativePaths,
            DateTime buildStartedUtc) {
            if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("Output root path must be provided.", nameof(outputRootPath));
            } else if (requiredArtifactRelativePaths == null || requiredArtifactRelativePaths.Length == 0) {
                throw new ArgumentException("At least one required artifact path must be provided.", nameof(requiredArtifactRelativePaths));
            }

            string fullOutputRootPath = Path.GetFullPath(outputRootPath);
            if (!Directory.Exists(fullOutputRootPath)) {
                return new BuildArtifactVerificationResult(false, $"Build output directory '{fullOutputRootPath}' is missing.");
            }

            for (int requiredArtifactIndex = 0; requiredArtifactIndex < requiredArtifactRelativePaths.Length; requiredArtifactIndex++) {
                string requiredArtifactRelativePath = requiredArtifactRelativePaths[requiredArtifactIndex];
                string validationFailure = ValidateArtifact(fullOutputRootPath, requiredArtifactRelativePath, buildStartedUtc);
                if (!string.IsNullOrWhiteSpace(validationFailure)) {
                    return new BuildArtifactVerificationResult(false, validationFailure);
                }
            }

            return new BuildArtifactVerificationResult(true, "All required artifacts are fresh and non-empty.");
        }

        /// <summary>
        /// Validates one required artifact and returns an empty string when it satisfies all requirements.
        /// </summary>
        /// <param name="fullOutputRootPath">Absolute normalized output directory path.</param>
        /// <param name="requiredArtifactRelativePath">Artifact path relative to the output directory.</param>
        /// <param name="buildStartedUtc">UTC timestamp captured immediately before the child build starts.</param>
        /// <returns>An empty string on success; otherwise a detailed validation failure.</returns>
        static string ValidateArtifact(string fullOutputRootPath, string requiredArtifactRelativePath, DateTime buildStartedUtc) {
            if (string.IsNullOrWhiteSpace(requiredArtifactRelativePath)) {
                return "A required artifact path is empty.";
            } else if (Path.IsPathRooted(requiredArtifactRelativePath)) {
                return $"Required artifact path '{requiredArtifactRelativePath}' must be relative.";
            }

            string fullArtifactPath = Path.GetFullPath(Path.Combine(fullOutputRootPath, requiredArtifactRelativePath));
            string relativeArtifactPath = Path.GetRelativePath(fullOutputRootPath, fullArtifactPath);
            if (string.Equals(relativeArtifactPath, ".", StringComparison.Ordinal)
                || relativeArtifactPath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || string.Equals(relativeArtifactPath, "..", StringComparison.Ordinal)
                || Path.IsPathRooted(relativeArtifactPath)) {
                return $"Required artifact path '{requiredArtifactRelativePath}' escapes the build output directory.";
            } else if (!File.Exists(fullArtifactPath)) {
                return $"Required artifact '{requiredArtifactRelativePath}' is missing.";
            }

            FileInfo artifactFileInfo = new FileInfo(fullArtifactPath);
            if (artifactFileInfo.Length == 0) {
                return $"Required artifact '{requiredArtifactRelativePath}' is empty.";
            } else if (artifactFileInfo.LastWriteTimeUtc < buildStartedUtc) {
                return $"Required artifact '{requiredArtifactRelativePath}' is stale because it predates this build.";
            }

            return string.Empty;
        }
    }
}
