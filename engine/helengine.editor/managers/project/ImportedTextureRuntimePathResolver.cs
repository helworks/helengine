namespace helengine.editor {
    /// <summary>
    /// Resolves packaged runtime paths for imported texture assets while honoring platform-specific filesystem constraints.
    /// </summary>
    static class ImportedTextureRuntimePathResolver {
        /// <summary>
        /// Stable cooked imported-texture directory shared by packaged runtimes.
        /// </summary>
        const string ImportedTextureDirectoryName = "cooked/imported";

        /// <summary>
        /// Stable Nintendo DS runtime extension used for packaged imported textures.
        /// </summary>
        const string NintendoDsImportedTextureExtension = ".hetex";

        /// <summary>
        /// Builds one packaged runtime path for the supplied imported texture asset.
        /// </summary>
        /// <param name="targetPlatformId">Stable target platform identifier.</param>
        /// <param name="assetId">Imported texture asset identifier stored in editor cache metadata.</param>
        /// <returns>Canonical runtime-relative cooked texture path.</returns>
        public static string BuildCookedRelativePath(string targetPlatformId, string assetId) {
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Imported texture asset id must be provided.", nameof(assetId));
            }

            if (string.Equals(targetPlatformId, "ds", StringComparison.OrdinalIgnoreCase)) {
                ulong runtimeAssetId = RuntimeAssetIdGenerator.Generate(assetId);
                return string.Concat(ImportedTextureDirectoryName, "/", runtimeAssetId.ToString("x16"), NintendoDsImportedTextureExtension);
            }

            return CanonicalPackagedAssetPath.Normalize(ImportedTextureDirectoryName + "/" + assetId);
        }

        /// <summary>
        /// Determines whether one cooked runtime path matches the packaged location that would be generated for the supplied imported texture asset.
        /// </summary>
        /// <param name="targetPlatformId">Stable target platform identifier.</param>
        /// <param name="cookedRelativePath">Cooked runtime path to compare.</param>
        /// <param name="assetId">Imported texture asset identifier stored in editor cache metadata.</param>
        /// <returns>True when the cooked path matches the platform-specific imported texture runtime path.</returns>
        public static bool PathMatchesAssetId(string targetPlatformId, string cookedRelativePath, string assetId) {
            if (string.IsNullOrWhiteSpace(cookedRelativePath) || string.IsNullOrWhiteSpace(assetId)) {
                return false;
            }

            return string.Equals(
                CanonicalPackagedAssetPath.Normalize(cookedRelativePath),
                BuildCookedRelativePath(targetPlatformId, assetId),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
