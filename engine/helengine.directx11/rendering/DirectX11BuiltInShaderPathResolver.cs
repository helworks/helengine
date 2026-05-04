namespace helengine.directx11 {
    /// <summary>
    /// Resolves built-in editor shader source files for DirectX11 runtime compilation.
    /// </summary>
    public static class DirectX11BuiltInShaderPathResolver {
        /// <summary>
        /// Resolves the absolute path to one built-in editor shader source file.
        /// </summary>
        /// <param name="shaderFileName">Built-in shader source file name.</param>
        /// <returns>Absolute path to the built-in shader source file.</returns>
        public static string ResolveShaderPath(string shaderFileName) {
            if (string.IsNullOrWhiteSpace(shaderFileName)) {
                throw new ArgumentException("Shader file name must be provided.", nameof(shaderFileName));
            }

            string baseDirectory = AppContext.BaseDirectory;
            if (string.IsNullOrWhiteSpace(baseDirectory)) {
                throw new InvalidOperationException("Application base directory could not be resolved.");
            }

            string packagedShaderPath = Path.GetFullPath(Path.Combine(baseDirectory, "shaders", "builtin", shaderFileName));
            if (File.Exists(packagedShaderPath)) {
                return packagedShaderPath;
            }

            DirectoryInfo directory = new DirectoryInfo(baseDirectory);
            while (directory != null) {
                string sourceShaderPath = Path.Combine(directory.FullName, "engine", "helengine.editor", "shaders", "builtin", shaderFileName);
                if (File.Exists(sourceShaderPath)) {
                    return sourceShaderPath;
                }

                directory = directory.Parent;
            }

            throw new FileNotFoundException("Built-in shader source file was not found.", packagedShaderPath);
        }
    }
}
