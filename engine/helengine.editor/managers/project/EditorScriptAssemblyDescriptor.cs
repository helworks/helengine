namespace helengine.editor {
    /// <summary>
    /// Describes one generated script assembly loaded by the editor along with its authored module kind.
    /// </summary>
    public sealed class EditorScriptAssemblyDescriptor {
        /// <summary>
        /// Initializes one editor-owned script assembly descriptor.
        /// </summary>
        /// <param name="moduleId">Stable code-module id that owns the assembly.</param>
        /// <param name="outputDirectoryPath">Absolute build output directory that contains the assembly and dependencies.</param>
        /// <param name="assemblyPath">Absolute path to the generated module assembly.</param>
        /// <param name="moduleKind">Declares whether the module is runtime or editor-only.</param>
        public EditorScriptAssemblyDescriptor(string moduleId, string outputDirectoryPath, string assemblyPath, EditorCodeModuleKind moduleKind) {
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
            ModuleKind = moduleKind;
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

        /// <summary>
        /// Gets whether the authored module is runtime or editor-only.
        /// </summary>
        public EditorCodeModuleKind ModuleKind { get; }
    }
}
