namespace helengine {
    /// <summary>
    /// Describes a compiled shader program binary for a target backend.
    /// </summary>
    public class ShaderProgramBinary {
        /// <summary>
        /// Initializes a new shader program binary descriptor.
        /// </summary>
        /// <param name="programName">Name of the shader program this binary represents.</param>
        /// <param name="stage">Pipeline stage for the binary.</param>
        /// <param name="target">Target backend identifier (dx9, dx11, dx12, vulkan, metal).</param>
        /// <param name="variant">Variant name that produced this binary.</param>
        /// <param name="path">Absolute path to the compiled binary.</param>
        public ShaderProgramBinary(
            string programName,
            ShaderStage stage,
            string target,
            string variant,
            string path) {
            if (string.IsNullOrWhiteSpace(programName)) {
                throw new ArgumentException("Program name must be provided.", nameof(programName));
            }

            if (string.IsNullOrWhiteSpace(target)) {
                throw new ArgumentException("Target must be provided.", nameof(target));
            }

            if (string.IsNullOrWhiteSpace(variant)) {
                throw new ArgumentException("Variant must be provided.", nameof(variant));
            }

            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Path must be provided.", nameof(path));
            }

            ProgramName = programName;
            Stage = stage;
            Target = target;
            Variant = variant;
            Path = path;
        }

        /// <summary>
        /// Gets the shader program name this binary belongs to.
        /// </summary>
        public string ProgramName { get; }

        /// <summary>
        /// Gets the pipeline stage for this binary.
        /// </summary>
        public ShaderStage Stage { get; }

        /// <summary>
        /// Gets the target backend identifier.
        /// </summary>
        public string Target { get; }

        /// <summary>
        /// Gets the variant name for this binary.
        /// </summary>
        public string Variant { get; }

        /// <summary>
        /// Gets the absolute path to the compiled binary.
        /// </summary>
        public string Path { get; }
    }
}
