namespace helengine.patching {
    /// <summary>
    /// Captures the output of an engine build request.
    /// </summary>
    public sealed class EngineBuildResult {
        readonly List<string> logs;
        readonly List<string> errors;

        /// <summary>
        /// Initializes a new build result.
        /// </summary>
        /// <param name="success">True when the build succeeded.</param>
        /// <param name="buildId">Build identifier.</param>
        /// <param name="outputPath">Output folder path.</param>
        /// <param name="assemblyPath">Assembly output path.</param>
        public EngineBuildResult(bool success, string buildId, string outputPath, string assemblyPath) {
            Success = success;
            BuildId = buildId ?? string.Empty;
            OutputPath = outputPath ?? string.Empty;
            AssemblyPath = assemblyPath ?? string.Empty;
            logs = new List<string>();
            errors = new List<string>();
        }

        /// <summary>
        /// Gets a value indicating whether the build succeeded.
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        /// Gets the build identifier.
        /// </summary>
        public string BuildId { get; }

        /// <summary>
        /// Gets the output folder path.
        /// </summary>
        public string OutputPath { get; }

        /// <summary>
        /// Gets the built assembly path.
        /// </summary>
        public string AssemblyPath { get; }

        /// <summary>
        /// Gets the build log lines.
        /// </summary>
        public IReadOnlyList<string> Logs => logs;

        /// <summary>
        /// Gets the build error lines.
        /// </summary>
        public IReadOnlyList<string> Errors => errors;

        /// <summary>
        /// Adds a log line to the build output.
        /// </summary>
        /// <param name="line">Log line to append.</param>
        public void AddLog(string line) {
            if (string.IsNullOrWhiteSpace(line)) {
                return;
            }

            logs.Add(line);
        }

        /// <summary>
        /// Adds an error line to the build output.
        /// </summary>
        /// <param name="line">Error line to append.</param>
        public void AddError(string line) {
            if (string.IsNullOrWhiteSpace(line)) {
                return;
            }

            errors.Add(line);
        }

        /// <summary>
        /// Updates the success flag on this result.
        /// </summary>
        /// <param name="success">True when the build succeeded.</param>
        public void SetSuccess(bool success) {
            Success = success;
        }
    }
}
