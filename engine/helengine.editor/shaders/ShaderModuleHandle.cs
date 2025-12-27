namespace helengine.editor {
    /// <summary>
    /// Represents a loaded shader module assembly and its generated definition.
    /// </summary>
    public sealed class ShaderModuleHandle : IDisposable {
        /// <summary>
        /// Load context that owns the module assembly.
        /// </summary>
        ShaderModuleLoadContext loadContext;

        /// <summary>
        /// Absolute path to the module assembly.
        /// </summary>
        readonly string assemblyPath;

        /// <summary>
        /// Generated shader module definition.
        /// </summary>
        readonly ShaderModuleDefinition definition;

        /// <summary>
        /// Tracks whether the module has been unloaded.
        /// </summary>
        bool isUnloaded;

        /// <summary>
        /// Initializes a new shader module handle.
        /// </summary>
        /// <param name="loadContext">Load context that owns the module assembly.</param>
        /// <param name="assemblyPath">Absolute path to the module assembly.</param>
        /// <param name="definition">Generated module definition.</param>
        public ShaderModuleHandle(ShaderModuleLoadContext loadContext, string assemblyPath, ShaderModuleDefinition definition) {
            if (loadContext == null) {
                throw new ArgumentNullException(nameof(loadContext));
            }

            if (string.IsNullOrWhiteSpace(assemblyPath)) {
                throw new ArgumentException("Assembly path must be provided.", nameof(assemblyPath));
            }

            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }

            this.loadContext = loadContext;
            this.assemblyPath = assemblyPath;
            this.definition = definition;
        }

        /// <summary>
        /// Gets the generated shader module definition.
        /// </summary>
        public ShaderModuleDefinition Definition {
            get {
                return definition;
            }
        }

        /// <summary>
        /// Gets the absolute path to the module assembly.
        /// </summary>
        public string AssemblyPath {
            get {
                return assemblyPath;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the module has been unloaded.
        /// </summary>
        public bool IsUnloaded {
            get {
                return isUnloaded;
            }
        }

        /// <summary>
        /// Unloads the module assembly if it is still loaded.
        /// </summary>
        public void Unload() {
            if (isUnloaded) {
                return;
            }

            isUnloaded = true;
            loadContext.Unload();
            loadContext = null;
        }

        /// <summary>
        /// Releases the module load context.
        /// </summary>
        public void Dispose() {
            Unload();
        }
    }
}
