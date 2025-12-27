namespace helengine {
    /// <summary>
    /// Describes the output of compiling a single shader entry point.
    /// </summary>
    public class ShaderCompileResult {
        /// <summary>
        /// Initializes a new shader compile result.
        /// </summary>
        /// <param name="request">Request that produced this result.</param>
        /// <param name="programDefinition">Reflected program metadata.</param>
        /// <param name="binary">Compiled shader bytecode.</param>
        /// <param name="diagnostics">Diagnostics emitted during compilation.</param>
        /// <param name="success">True when compilation completed without errors.</param>
        public ShaderCompileResult(
            ShaderCompileRequest request,
            ShaderProgramDefinition programDefinition,
            ShaderCompiledBinary binary,
            IReadOnlyList<ShaderCompileDiagnostic> diagnostics,
            bool success) {
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            if (programDefinition == null) {
                throw new ArgumentNullException(nameof(programDefinition));
            }

            if (binary == null) {
                throw new ArgumentNullException(nameof(binary));
            }

            if (diagnostics == null) {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            Request = request;
            ProgramDefinition = programDefinition;
            Binary = binary;
            Diagnostics = diagnostics;
            Success = success;
        }

        /// <summary>
        /// Gets the compile request associated with this result.
        /// </summary>
        public ShaderCompileRequest Request { get; }

        /// <summary>
        /// Gets the reflected program definition.
        /// </summary>
        public ShaderProgramDefinition ProgramDefinition { get; }

        /// <summary>
        /// Gets the compiled shader bytecode.
        /// </summary>
        public ShaderCompiledBinary Binary { get; }

        /// <summary>
        /// Gets the diagnostics emitted during compilation.
        /// </summary>
        public IReadOnlyList<ShaderCompileDiagnostic> Diagnostics { get; }

        /// <summary>
        /// Gets a value indicating whether compilation succeeded.
        /// </summary>
        public bool Success { get; }
    }
}
