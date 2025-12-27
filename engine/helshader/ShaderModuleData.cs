namespace helshader {
    /// <summary>
    /// Stores all metadata required to generate a shader module source file.
    /// </summary>
    public class ShaderModuleData {
        /// <summary>
        /// Initializes a new shader module data container.
        /// </summary>
        /// <param name="moduleName">Module name.</param>
        /// <param name="programs">Shader program metadata.</param>
        /// <param name="binaries">Compiled binary metadata.</param>
        public ShaderModuleData(string moduleName, ShaderProgramData[] programs, ShaderBinaryData[] binaries) {
            if (string.IsNullOrWhiteSpace(moduleName)) {
                throw new ArgumentException("Module name must be provided.", nameof(moduleName));
            }

            if (programs == null) {
                throw new ArgumentNullException(nameof(programs));
            }

            if (binaries == null) {
                throw new ArgumentNullException(nameof(binaries));
            }

            ModuleName = moduleName;
            Programs = programs;
            Binaries = binaries;
        }

        /// <summary>
        /// Gets the module name.
        /// </summary>
        public string ModuleName { get; }

        /// <summary>
        /// Gets the shader program metadata list.
        /// </summary>
        public ShaderProgramData[] Programs { get; }

        /// <summary>
        /// Gets the compiled binary metadata list.
        /// </summary>
        public ShaderBinaryData[] Binaries { get; }
    }
}
