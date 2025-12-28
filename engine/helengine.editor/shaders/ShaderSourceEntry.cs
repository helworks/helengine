namespace helengine.editor {
    /// <summary>
    /// Represents a shader source file discovered by the editor.
    /// </summary>
    public class ShaderSourceEntry {
        /// <summary>
        /// Initializes a new shader source entry.
        /// </summary>
        /// <param name="name">Logical shader name derived from the file path.</param>
        /// <param name="relativePath">Path relative to the shader root.</param>
        /// <param name="sourcePath">Absolute path to the shader source file.</param>
        public ShaderSourceEntry(string name, string relativePath, string sourcePath) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Shader name must be provided.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            Name = name;
            RelativePath = relativePath;
            SourcePath = sourcePath;
        }

        /// <summary>
        /// Gets the logical shader name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the shader file path relative to the shader root.
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// Gets the absolute shader source path.
        /// </summary>
        public string SourcePath { get; }
    }
}
