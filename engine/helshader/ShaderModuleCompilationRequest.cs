namespace helshader {
    /// <summary>
    /// Describes a request to compile a generated shader module.
    /// </summary>
    public class ShaderModuleCompilationRequest {
        /// <summary>
        /// Initializes a new compilation request.
        /// </summary>
        /// <param name="sourcePath">C# source path.</param>
        /// <param name="outputPath">Output assembly path.</param>
        public ShaderModuleCompilationRequest(string sourcePath, string outputPath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (string.IsNullOrWhiteSpace(outputPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            }

            SourcePath = sourcePath;
            OutputPath = outputPath;
        }

        /// <summary>
        /// Gets the C# source path.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the output assembly path.
        /// </summary>
        public string OutputPath { get; }
    }
}
