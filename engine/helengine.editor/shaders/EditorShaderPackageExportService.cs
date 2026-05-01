namespace helengine.editor {
    /// <summary>
    /// Exports referenced shader packages from the editor shader cache into a Windows build root.
    /// </summary>
    public sealed class EditorShaderPackageExportService {
        /// <summary>
        /// Root directory containing the compiled editor shader cache.
        /// </summary>
        readonly string ShaderCacheRootPath;

        /// <summary>
        /// Content manager used to load cached shader assets for validation before export.
        /// </summary>
        readonly ContentManager ShaderCacheContentManager;

        /// <summary>
        /// Initializes a shader-package export service rooted at the supplied cache directory.
        /// </summary>
        /// <param name="shaderCacheRootPath">Absolute or relative shader cache root path.</param>
        public EditorShaderPackageExportService(string shaderCacheRootPath) {
            if (string.IsNullOrWhiteSpace(shaderCacheRootPath)) {
                throw new ArgumentException("Shader cache root path must be provided.", nameof(shaderCacheRootPath));
            }

            ShaderCacheRootPath = Path.GetFullPath(shaderCacheRootPath);
            ShaderCacheContentManager = new ContentManager(ShaderCacheRootPath);
            EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(ShaderCacheContentManager);
        }

        /// <summary>
        /// Copies each referenced shader package from the shader cache into the final build root.
        /// </summary>
        /// <param name="shaderAssetIds">Referenced shader asset ids to export.</param>
        /// <param name="target">Shader compile target to export.</param>
        /// <param name="buildRootPath">Absolute or relative build root path.</param>
        public void Export(IReadOnlyList<string> shaderAssetIds, ShaderCompileTarget target, string buildRootPath) {
            if (shaderAssetIds == null) {
                throw new ArgumentNullException(nameof(shaderAssetIds));
            }

            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }

            if (shaderAssetIds.Count == 0) {
                return;
            }

            string fullBuildRootPath = Path.GetFullPath(buildRootPath);
            string exportDirectoryPath = Path.Combine(fullBuildRootPath, "shaders");
            Directory.CreateDirectory(exportDirectoryPath);

            string targetName = ShaderTargetNames.GetTargetName(target);
            HashSet<string> exportedShaderIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < shaderAssetIds.Count; index++) {
                string shaderAssetId = shaderAssetIds[index];
                if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                    throw new InvalidOperationException("Referenced shader asset ids must not be empty.");
                }

                if (!exportedShaderIds.Add(shaderAssetId)) {
                    continue;
                }

                string sourcePackagePath = ShaderPackagePaths.GetPackagePath(ShaderCacheRootPath, shaderAssetId, target);
                if (!File.Exists(sourcePackagePath)) {
                    throw new FileNotFoundException($"Required shader package '{sourcePackagePath}' was not found.", sourcePackagePath);
                }

                ShaderAsset shaderAsset = ShaderCacheContentManager.Load<ShaderAsset>(sourcePackagePath, EditorContentProcessorIds.ShaderAsset);
                if (!string.Equals(shaderAsset.TargetName, targetName, StringComparison.Ordinal)) {
                    throw new InvalidOperationException(
                        $"Shader package '{sourcePackagePath}' was compiled for target '{shaderAsset.TargetName}' instead of '{targetName}'.");
                }

                string destinationPath = Path.Combine(exportDirectoryPath, Path.GetFileName(sourcePackagePath));
                File.Copy(sourcePackagePath, destinationPath, true);
            }
        }
    }
}
