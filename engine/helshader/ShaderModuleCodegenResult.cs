namespace helshader {
    /// <summary>
    /// Represents the output of a shader module code generation step.
    /// </summary>
    public class ShaderModuleCodegenResult {
        /// <summary>
        /// Initializes a new code generation result.
        /// </summary>
        /// <param name="sourcePath">Generated C# source path.</param>
        /// <param name="moduleTypeName">Fully qualified module type name.</param>
        public ShaderModuleCodegenResult(string sourcePath, string moduleTypeName) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (string.IsNullOrWhiteSpace(moduleTypeName)) {
                throw new ArgumentException("Module type name must be provided.", nameof(moduleTypeName));
            }

            SourcePath = sourcePath;
            ModuleTypeName = moduleTypeName;
        }

        /// <summary>
        /// Gets the generated source path.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the fully qualified module type name.
        /// </summary>
        public string ModuleTypeName { get; }
    }
}
