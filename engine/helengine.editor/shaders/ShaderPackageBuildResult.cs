namespace helengine.editor {
    /// <summary>
    /// Captures the result of building a shader package for a specific target.
    /// </summary>
    public class ShaderPackageBuildResult {
        /// <summary>
        /// Stores compilation results for the target.
        /// </summary>
        readonly ShaderCompileResult[] results;

        /// <summary>
        /// Initializes a new shader package build result.
        /// </summary>
        /// <param name="target">Target backend for this result.</param>
        /// <param name="packagePath">Package output path.</param>
        /// <param name="results">Compilation results captured during build.</param>
        /// <param name="success">True when the package build succeeded.</param>
        /// <param name="errorMessage">Error message when build fails; empty otherwise.</param>
        public ShaderPackageBuildResult(
            ShaderCompileTarget target,
            string packagePath,
            ShaderCompileResult[] results,
            bool success,
            string errorMessage) {
            if (string.IsNullOrWhiteSpace(packagePath)) {
                throw new ArgumentException("Package path must be provided.", nameof(packagePath));
            }

            if (results == null) {
                throw new ArgumentNullException(nameof(results));
            }

            if (errorMessage == null) {
                throw new ArgumentNullException(nameof(errorMessage));
            }

            Target = target;
            PackagePath = packagePath;
            this.results = results;
            Success = success;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Gets the target backend for this result.
        /// </summary>
        public ShaderCompileTarget Target { get; }

        /// <summary>
        /// Gets the package output path.
        /// </summary>
        public string PackagePath { get; }

        /// <summary>
        /// Gets the compilation results captured during the build.
        /// </summary>
        public IReadOnlyList<ShaderCompileResult> Results {
            get {
                return results;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the package build succeeded.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets the error message for a failed build.
        /// </summary>
        public string ErrorMessage { get; }
    }
}
