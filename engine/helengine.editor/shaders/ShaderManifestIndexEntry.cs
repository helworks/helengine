namespace helengine.editor {
    /// <summary>
    /// Represents a single shader entry extracted from the manifest index.
    /// </summary>
    public class ShaderManifestIndexEntry {
        /// <summary>
        /// Initializes a new manifest index entry.
        /// </summary>
        /// <param name="name">Logical shader name.</param>
        /// <param name="relativeFilePath">Shader file path relative to the manifest root.</param>
        /// <param name="sourcePath">Absolute shader source path.</param>
        /// <param name="moduleAssemblyPath">Absolute path to the generated module DLL.</param>
        public ShaderManifestIndexEntry(string name, string relativeFilePath, string sourcePath, string moduleAssemblyPath) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Shader name must be provided.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(relativeFilePath)) {
                throw new ArgumentException("Relative file path must be provided.", nameof(relativeFilePath));
            }

            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (string.IsNullOrWhiteSpace(moduleAssemblyPath)) {
                throw new ArgumentException("Module assembly path must be provided.", nameof(moduleAssemblyPath));
            }

            Name = name;
            RelativeFilePath = relativeFilePath;
            SourcePath = sourcePath;
            ModuleAssemblyPath = moduleAssemblyPath;
        }

        /// <summary>
        /// Gets the logical shader name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the shader file path relative to the manifest root.
        /// </summary>
        public string RelativeFilePath { get; }

        /// <summary>
        /// Gets the absolute shader source path.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the absolute path to the generated module DLL.
        /// </summary>
        public string ModuleAssemblyPath { get; }
    }
}
