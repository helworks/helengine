namespace helengine.editor {
    /// <summary>
    /// Resolves material-reported shader asset identifiers to source files without imposing an assets-folder convention.
    /// </summary>
    public sealed class EditorProjectShaderSourceResolver {
        /// <summary>
        /// Stores the absolute project assets root scanned for authored HLSL files.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Initializes one resolver for a project assets root.
        /// </summary>
        /// <param name="assetsRootPath">Absolute project assets root containing authored shader source files.</param>
        public EditorProjectShaderSourceResolver(string assetsRootPath) {
            if (string.IsNullOrWhiteSpace(assetsRootPath)) {
                throw new ArgumentException("Project assets root is required.", nameof(assetsRootPath));
            }

            AssetsRootPath = Path.GetFullPath(assetsRootPath);
        }

        /// <summary>
        /// Resolves each requested shader asset identifier to its authored project source or registered built-in source.
        /// </summary>
        /// <param name="shaderAssetIds">Shader identifiers reported by separately cooked materials.</param>
        /// <returns>Resolved shader sources in first-request order without duplicate asset identifiers.</returns>
        public IReadOnlyList<EditorProjectShaderSource> Resolve(IReadOnlyList<string> shaderAssetIds) {
            if (shaderAssetIds == null) {
                throw new ArgumentNullException(nameof(shaderAssetIds));
            }

            Dictionary<string, string> projectPathsByAssetId = DiscoverProjectShaderPaths();
            List<EditorProjectShaderSource> sources = [];
            HashSet<string> resolvedAssetIds = new(StringComparer.Ordinal);
            for (int index = 0; index < shaderAssetIds.Count; index++) {
                string shaderAssetId = shaderAssetIds[index];
                if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                    throw new ArgumentException("Shader asset identifiers cannot contain blank entries.", nameof(shaderAssetIds));
                } else if (!resolvedAssetIds.Add(shaderAssetId)) {
                    continue;
                }

                string sourcePath = ResolveSourcePath(shaderAssetId, projectPathsByAssetId);
                sources.Add(ReadSource(shaderAssetId, sourcePath));
            }

            return sources;
        }

        /// <summary>
        /// Discovers project HLSL files by applying the existing generated shader asset-id rule beneath the complete assets root.
        /// </summary>
        /// <returns>Absolute source paths keyed by their generated shader asset identifiers.</returns>
        Dictionary<string, string> DiscoverProjectShaderPaths() {
            Dictionary<string, string> pathsByAssetId = new(StringComparer.Ordinal);
            if (!Directory.Exists(AssetsRootPath)) {
                return pathsByAssetId;
            }

            string[] sourcePaths = Directory.GetFiles(AssetsRootPath, "*.hlsl", SearchOption.AllDirectories);
            Array.Sort(sourcePaths, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < sourcePaths.Length; index++) {
                string sourcePath = Path.GetFullPath(sourcePaths[index]);
                string shaderAssetId = ShaderAssetIdUtils.BuildShaderAssetId(sourcePath, AssetsRootPath);
                if (!pathsByAssetId.TryAdd(shaderAssetId, sourcePath)) {
                    throw new InvalidOperationException($"Project shader asset id '{shaderAssetId}' resolves from more than one HLSL source file.");
                }
            }

            return pathsByAssetId;
        }

        /// <summary>
        /// Resolves one source path from authored project files before consulting the built-in shader source registry.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier to resolve.</param>
        /// <param name="projectPathsByAssetId">Discovered project source paths keyed by asset id.</param>
        /// <returns>Absolute HLSL source path.</returns>
        static string ResolveSourcePath(string shaderAssetId, IReadOnlyDictionary<string, string> projectPathsByAssetId) {
            if (projectPathsByAssetId.TryGetValue(shaderAssetId, out string projectSourcePath)) {
                return projectSourcePath;
            }

            try {
                return EditorBuiltInShaderAssetLibrary.ResolveShaderPath(ResolveBuiltInShaderFileName(shaderAssetId));
            } catch (FileNotFoundException exception) {
                throw new InvalidOperationException($"Shader asset id '{shaderAssetId}' could not be resolved to an authored or built-in HLSL source file.", exception);
            }
        }

        /// <summary>
        /// Resolves the source-file name for one engine-owned shader identity, including explicit legacy aliases retained by existing material assets.
        /// </summary>
        /// <param name="shaderAssetId">Persistent shader identity saved by a material.</param>
        /// <returns>Built-in HLSL file name that owns the shader source.</returns>
        static string ResolveBuiltInShaderFileName(string shaderAssetId) {
            if (string.Equals(shaderAssetId, "ForwardLambertShader", StringComparison.Ordinal)) {
                return "ForwardStandardShader.hlsl";
            }

            return string.Concat(shaderAssetId, ".hlsl");
        }

        /// <summary>
        /// Reads one source file and creates its stable byte-level source identity.
        /// </summary>
        /// <param name="shaderAssetId">Stable shader asset identifier associated with the source file.</param>
        /// <param name="sourcePath">Absolute source file path.</param>
        /// <returns>Resolved source entry with text and SHA-256 content identity.</returns>
        static EditorProjectShaderSource ReadSource(string shaderAssetId, string sourcePath) {
            byte[] sourceBytes = File.ReadAllBytes(sourcePath);
            return new EditorProjectShaderSource(
                shaderAssetId,
                sourcePath,
                File.ReadAllText(sourcePath),
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(sourceBytes)));
        }
    }
}
