namespace helshader {
    /// <summary>
    /// Stores results from compiling a shader module.
    /// </summary>
    public class ShaderModuleCompilationResult {
        /// <summary>
        /// Initializes a new compilation result.
        /// </summary>
        /// <param name="success">True when compilation succeeded.</param>
        /// <param name="outputPath">Output assembly path.</param>
        /// <param name="diagnostics">Compilation diagnostics.</param>
        public ShaderModuleCompilationResult(bool success, string outputPath, string[] diagnostics) {
            if (string.IsNullOrWhiteSpace(outputPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            }

            if (diagnostics == null) {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            Success = success;
            OutputPath = outputPath;
            Diagnostics = diagnostics;
        }

        /// <summary>
        /// Gets a value indicating whether compilation succeeded.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets the output assembly path.
        /// </summary>
        public string OutputPath { get; }

        /// <summary>
        /// Gets the compilation diagnostic messages.
        /// </summary>
        public string[] Diagnostics { get; }
    }
}
