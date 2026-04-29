namespace helengine.editor {
    /// <summary>
    /// Provides helpers for building shader asset identifiers from source paths.
    /// </summary>
    public static class ShaderAssetIdUtils {
        /// <summary>
        /// Builds the shader asset id for a shader source path.
        /// </summary>
        /// <param name="shaderSourcePath">Absolute shader source path.</param>
        /// <returns>Shader asset id derived from the path.</returns>
        public static string BuildShaderAssetId(string shaderSourcePath) {
            if (string.IsNullOrWhiteSpace(shaderSourcePath)) {
                throw new ArgumentException("Shader path must be provided.", nameof(shaderSourcePath));
            }

            string assetsRoot = EditorProjectPaths.AssetsRoot;
            if (string.IsNullOrWhiteSpace(assetsRoot)) {
                throw new InvalidOperationException("Assets root path has not been initialized.");
            }

            string fullShaderPath = Path.GetFullPath(shaderSourcePath);
            string fullAssetsRoot = Path.GetFullPath(assetsRoot);
            if (!IsPathUnderRoot(fullShaderPath, fullAssetsRoot)) {
                throw new InvalidOperationException("Shader path must be located under the assets root.");
            }

            string relativePath = Path.GetRelativePath(fullAssetsRoot, fullShaderPath);
            string withoutExtension = Path.ChangeExtension(relativePath, null);
            if (string.IsNullOrWhiteSpace(withoutExtension)) {
                throw new InvalidOperationException("Shader id could not be resolved from the path.");
            }

            string normalized = withoutExtension.Replace(Path.DirectorySeparatorChar, '.');
            normalized = normalized.Replace(Path.AltDirectorySeparatorChar, '.');
            return normalized;
        }

        /// <summary>
        /// Determines whether a path is located under a root directory.
        /// </summary>
        /// <param name="path">Path to test.</param>
        /// <param name="root">Root directory to compare.</param>
        /// <returns>True when the path is under the root.</returns>
        static bool IsPathUnderRoot(string path, string root) {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root)) {
                return false;
            }

            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)) {
                root = root + Path.DirectorySeparatorChar;
            }

            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
    }
}
