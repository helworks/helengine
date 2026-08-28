namespace helengine.editor {
    /// <summary>
    /// Provides helpers for building shader asset identifiers from source paths.
    /// </summary>
    public static class ShaderAssetIdUtils {
        /// <summary>
        /// Builds the shader asset id for a source path using an explicit project assets root for command-line build workflows.
        /// </summary>
        /// <param name="shaderSourcePath">Absolute shader source path.</param>
        /// <param name="assetsRootPath">Absolute project assets root that contains the shader source.</param>
        /// <returns>Shader asset id derived from the source path relative to the supplied assets root.</returns>
        public static string BuildShaderAssetId(string shaderSourcePath, string assetsRootPath) {
            if (string.IsNullOrWhiteSpace(shaderSourcePath)) {
                throw new ArgumentException("Shader path must be provided.", nameof(shaderSourcePath));
            } else if (string.IsNullOrWhiteSpace(assetsRootPath)) {
                throw new ArgumentException("Assets root path must be provided.", nameof(assetsRootPath));
            }

            string fullShaderPath = Path.GetFullPath(shaderSourcePath);
            string fullAssetsRoot = Path.GetFullPath(assetsRootPath);
            if (!IsPathUnderRoot(fullShaderPath, fullAssetsRoot)) {
                throw new InvalidOperationException("Shader path must be located under the supplied assets root.");
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
