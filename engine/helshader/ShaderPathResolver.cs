namespace helshader {
    /// <summary>
    /// Resolves absolute paths for shader manifests and outputs.
    /// </summary>
    public class ShaderPathResolver {
        /// <summary>
        /// Resolves absolute paths for the provided manifest.
        /// </summary>
        /// <param name="manifestPath">Manifest file path.</param>
        /// <param name="manifest">Loaded manifest.</param>
        /// <returns>Resolved path information.</returns>
        public ShaderPathInfo Resolve(string manifestPath, ShaderManifest manifest) {
            if (string.IsNullOrWhiteSpace(manifestPath)) {
                throw new ArgumentException("Manifest path must be provided.", nameof(manifestPath));
            }

            if (manifest == null) {
                throw new ArgumentNullException(nameof(manifest));
            }

            string manifestDirectory = Path.GetDirectoryName(manifestPath);
            if (string.IsNullOrWhiteSpace(manifestDirectory)) {
                throw new InvalidOperationException("Manifest directory could not be resolved.");
            }

            string rootPath = ResolveRootPath(manifestDirectory, manifest.Root);
            ShaderManifestOutput output = manifest.Output;

            string binaryDir = ResolveOutputPath(manifestDirectory, rootPath, output.BinaryDir);
            string reflectionDir = ResolveOutputPath(manifestDirectory, rootPath, output.ReflectionDir);
            string codegenDir = ResolveOutputPath(manifestDirectory, rootPath, output.CodegenDir);
            string moduleDir = ResolveOutputPath(manifestDirectory, rootPath, output.ModuleDir);
            string mslDir = ResolveOutputPath(manifestDirectory, rootPath, output.MslDir);
            string debugDir = ResolveOutputPath(manifestDirectory, rootPath, output.DebugDir);

            return new ShaderPathInfo(rootPath, manifestDirectory, binaryDir, reflectionDir, codegenDir, moduleDir, mslDir, debugDir);
        }

        /// <summary>
        /// Resolves the absolute shader root path.
        /// </summary>
        /// <param name="manifestDirectory">Manifest directory.</param>
        /// <param name="rootValue">Root value from the manifest.</param>
        /// <returns>Absolute root path.</returns>
        string ResolveRootPath(string manifestDirectory, string rootValue) {
            if (string.IsNullOrWhiteSpace(rootValue)) {
                throw new InvalidOperationException("Manifest root must be provided.");
            }

            if (Path.IsPathRooted(rootValue)) {
                return Path.GetFullPath(rootValue);
            }

            return Path.GetFullPath(Path.Combine(manifestDirectory, rootValue));
        }

        /// <summary>
        /// Resolves an output directory relative to the root or manifest directory.
        /// </summary>
        /// <param name="manifestDirectory">Manifest directory.</param>
        /// <param name="rootPath">Resolved shader root path.</param>
        /// <param name="relativePath">Path from the manifest.</param>
        /// <returns>Absolute output path.</returns>
        string ResolveOutputPath(string manifestDirectory, string rootPath, string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new InvalidOperationException("Output directory cannot be empty.");
            }

            if (Path.IsPathRooted(relativePath)) {
                return Path.GetFullPath(relativePath);
            }

            if (relativePath.StartsWith("..", StringComparison.Ordinal)) {
                return Path.GetFullPath(Path.Combine(manifestDirectory, relativePath));
            }

            return Path.GetFullPath(Path.Combine(rootPath, relativePath));
        }
    }
}
