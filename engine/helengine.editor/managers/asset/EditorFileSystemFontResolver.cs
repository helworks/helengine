namespace helengine.editor {
    /// <summary>
    /// Resolves authored file-backed source fonts through the shared asset import manager.
    /// </summary>
    public sealed class EditorFileSystemFontResolver {
        /// <summary>
        /// Shared asset import manager that imports and caches file-backed source fonts.
        /// </summary>
        readonly AssetImportManager AssetImportManager;

        /// <summary>
        /// Initializes a new file-system font resolver.
        /// </summary>
        /// <param name="assetImportManager">Shared asset import manager used for source font importing.</param>
        public EditorFileSystemFontResolver(AssetImportManager assetImportManager) {
            AssetImportManager = assetImportManager ?? throw new ArgumentNullException(nameof(assetImportManager));
        }

        /// <summary>
        /// Resolves one source font path to an imported font asset.
        /// </summary>
        /// <param name="sourcePath">Absolute source font path.</param>
        /// <returns>Imported font asset.</returns>
        public FontAsset ResolveFontAsset(string sourcePath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (!AssetImportManager.TryLoadFontAsset(sourcePath, out FontAsset fontAsset) || fontAsset == null) {
                throw new InvalidOperationException($"Font reference '{sourcePath}' could not be imported into a FontAsset.");
            }

            return fontAsset;
        }
    }
}
