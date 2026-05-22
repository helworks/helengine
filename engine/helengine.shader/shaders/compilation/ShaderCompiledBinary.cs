namespace helengine {
    /// <summary>
    /// Contains the compiled bytecode for a shader entry point.
    /// </summary>
    public class ShaderCompiledBinary {
        /// <summary>
        /// Initializes a new compiled shader binary container.
        /// </summary>
        /// <param name="target">Backend target that produced the bytecode.</param>
        /// <param name="stage">Pipeline stage for the bytecode.</param>
        /// <param name="entryPoint">Entry point function name.</param>
        /// <param name="variant">Variant name for the compilation.</param>
        /// <param name="bytecode">Compiled bytecode payload.</param>
        public ShaderCompiledBinary(
            ShaderCompileTarget target,
            ShaderStage stage,
            string entryPoint,
            string variant,
            byte[] bytecode) {
            if (string.IsNullOrWhiteSpace(entryPoint)) {
                throw new ArgumentException("Entry point must be provided.", nameof(entryPoint));
            }

            if (string.IsNullOrWhiteSpace(variant)) {
                throw new ArgumentException("Variant name must be provided.", nameof(variant));
            }

            if (bytecode == null) {
                throw new ArgumentNullException(nameof(bytecode));
            }

            if (bytecode.Length == 0) {
                throw new ArgumentException("Bytecode payload must be provided.", nameof(bytecode));
            }

            Target = target;
            Stage = stage;
            EntryPoint = entryPoint;
            Variant = variant;
            Bytecode = bytecode;
        }

        /// <summary>
        /// Gets the backend target that produced the bytecode.
        /// </summary>
        public ShaderCompileTarget Target { get; }

        /// <summary>
        /// Gets the pipeline stage for the bytecode.
        /// </summary>
        public ShaderStage Stage { get; }

        /// <summary>
        /// Gets the entry point function name.
        /// </summary>
        public string EntryPoint { get; }

        /// <summary>
        /// Gets the variant name for the compilation.
        /// </summary>
        public string Variant { get; }

        /// <summary>
        /// Gets the compiled bytecode payload.
        /// </summary>
        public byte[] Bytecode { get; }
    }
}
