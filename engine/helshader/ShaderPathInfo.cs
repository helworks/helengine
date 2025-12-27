namespace helshader {
    /// <summary>
    /// Stores resolved paths for shader compilation outputs.
    /// </summary>
    public class ShaderPathInfo {
        /// <summary>
        /// Initializes a new resolved path container.
        /// </summary>
        /// <param name="rootPath">Resolved shader root path.</param>
        /// <param name="manifestDirectory">Directory containing the manifest.</param>
        /// <param name="binaryDir">Resolved binary output path.</param>
        /// <param name="reflectionDir">Resolved reflection output path.</param>
        /// <param name="codegenDir">Resolved code generation output path.</param>
        /// <param name="moduleDir">Resolved module output path.</param>
        /// <param name="mslDir">Resolved Metal output path.</param>
        /// <param name="debugDir">Resolved debug output path.</param>
        public ShaderPathInfo(
            string rootPath,
            string manifestDirectory,
            string binaryDir,
            string reflectionDir,
            string codegenDir,
            string moduleDir,
            string mslDir,
            string debugDir) {
            if (string.IsNullOrWhiteSpace(rootPath)) {
                throw new ArgumentException("Root path must be provided.", nameof(rootPath));
            }

            if (string.IsNullOrWhiteSpace(manifestDirectory)) {
                throw new ArgumentException("Manifest directory must be provided.", nameof(manifestDirectory));
            }

            if (string.IsNullOrWhiteSpace(binaryDir)) {
                throw new ArgumentException("Binary output path must be provided.", nameof(binaryDir));
            }

            if (string.IsNullOrWhiteSpace(reflectionDir)) {
                throw new ArgumentException("Reflection output path must be provided.", nameof(reflectionDir));
            }

            if (string.IsNullOrWhiteSpace(codegenDir)) {
                throw new ArgumentException("Codegen output path must be provided.", nameof(codegenDir));
            }

            if (string.IsNullOrWhiteSpace(moduleDir)) {
                throw new ArgumentException("Module output path must be provided.", nameof(moduleDir));
            }

            if (string.IsNullOrWhiteSpace(mslDir)) {
                throw new ArgumentException("MSL output path must be provided.", nameof(mslDir));
            }

            if (string.IsNullOrWhiteSpace(debugDir)) {
                throw new ArgumentException("Debug output path must be provided.", nameof(debugDir));
            }

            RootPath = rootPath;
            ManifestDirectory = manifestDirectory;
            BinaryDir = binaryDir;
            ReflectionDir = reflectionDir;
            CodegenDir = codegenDir;
            ModuleDir = moduleDir;
            MslDir = mslDir;
            DebugDir = debugDir;
        }

        /// <summary>
        /// Gets the resolved shader root path.
        /// </summary>
        public string RootPath { get; }

        /// <summary>
        /// Gets the directory containing the manifest file.
        /// </summary>
        public string ManifestDirectory { get; }

        /// <summary>
        /// Gets the resolved binary output directory.
        /// </summary>
        public string BinaryDir { get; }

        /// <summary>
        /// Gets the resolved reflection output directory.
        /// </summary>
        public string ReflectionDir { get; }

        /// <summary>
        /// Gets the resolved code generation output directory.
        /// </summary>
        public string CodegenDir { get; }

        /// <summary>
        /// Gets the resolved module output directory.
        /// </summary>
        public string ModuleDir { get; }

        /// <summary>
        /// Gets the resolved Metal output directory.
        /// </summary>
        public string MslDir { get; }

        /// <summary>
        /// Gets the resolved debug output directory.
        /// </summary>
        public string DebugDir { get; }
    }
}
