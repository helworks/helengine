namespace helengine {
    /// <summary>
    /// Describes one generated script assembly that belongs to a discovered code module.
    /// </summary>
    public sealed class ScriptAssemblyDescriptor {
        /// <summary>
        /// Initializes one generated script assembly descriptor.
        /// </summary>
        /// <param name="moduleId">Stable code-module id that owns the assembly.</param>
        /// <param name="outputDirectoryPath">Absolute build output directory that contains the assembly and its dependencies.</param>
        /// <param name="assemblyPath">Absolute path to the generated module assembly.</param>
        public ScriptAssemblyDescriptor(string moduleId, string outputDirectoryPath, string assemblyPath) {
            if (string.IsNullOrWhiteSpace(moduleId)) {
                throw new ArgumentException("Module id must be provided.", nameof(moduleId));
            }
            if (string.IsNullOrWhiteSpace(outputDirectoryPath)) {
                throw new ArgumentException("Output directory path must be provided.", nameof(outputDirectoryPath));
            }
            if (string.IsNullOrWhiteSpace(assemblyPath)) {
                throw new ArgumentException("Assembly path must be provided.", nameof(assemblyPath));
            }

            ModuleId = moduleId;
            OutputDirectoryPath = outputDirectoryPath;
            AssemblyPath = assemblyPath;
        }

        /// <summary>
        /// Gets the stable code-module id that owns the assembly.
        /// </summary>
        public string ModuleId { get; }

        /// <summary>
        /// Gets the absolute build output directory that contains the generated assembly.
        /// </summary>
        public string OutputDirectoryPath { get; }

        /// <summary>
        /// Gets the absolute path to the generated module assembly.
        /// </summary>
        public string AssemblyPath { get; }
    }
}
