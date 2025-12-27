using helengine;

namespace helshader {
    /// <summary>
    /// Stores compiled shader binary metadata used for code generation.
    /// </summary>
    public class ShaderBinaryData {
        /// <summary>
        /// Initializes a new shader binary data container.
        /// </summary>
        /// <param name="programName">Program name.</param>
        /// <param name="stage">Shader stage.</param>
        /// <param name="target">Target backend.</param>
        /// <param name="variant">Variant name.</param>
        /// <param name="relativePath">Relative binary path from the module root.</param>
        public ShaderBinaryData(
            string programName,
            ShaderStage stage,
            string target,
            string variant,
            string relativePath) {
            if (string.IsNullOrWhiteSpace(programName)) {
                throw new ArgumentException("Program name must be provided.", nameof(programName));
            }

            if (string.IsNullOrWhiteSpace(target)) {
                throw new ArgumentException("Target must be provided.", nameof(target));
            }

            if (string.IsNullOrWhiteSpace(variant)) {
                throw new ArgumentException("Variant must be provided.", nameof(variant));
            }

            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            ProgramName = programName;
            Stage = stage;
            Target = target;
            Variant = variant;
            RelativePath = relativePath;
        }

        /// <summary>
        /// Gets the program name.
        /// </summary>
        public string ProgramName { get; }

        /// <summary>
        /// Gets the shader stage.
        /// </summary>
        public ShaderStage Stage { get; }

        /// <summary>
        /// Gets the target backend.
        /// </summary>
        public string Target { get; }

        /// <summary>
        /// Gets the variant name.
        /// </summary>
        public string Variant { get; }

        /// <summary>
        /// Gets the relative binary path from the module root.
        /// </summary>
        public string RelativePath { get; }
    }
}
