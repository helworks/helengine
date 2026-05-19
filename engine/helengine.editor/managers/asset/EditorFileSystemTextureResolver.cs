namespace helengine.editor {
    /// <summary>
    /// Resolves authored file-backed source textures through the shared asset import manager.
    /// </summary>
    public sealed class EditorFileSystemTextureResolver {
        /// <summary>
        /// Shared asset import manager that imports and caches file-backed source textures.
        /// </summary>
        readonly AssetImportManager AssetImportManager;

        /// <summary>
        /// Initializes a new file-system texture resolver.
        /// </summary>
        /// <param name="assetImportManager">Shared asset import manager used for source texture importing.</param>
        public EditorFileSystemTextureResolver(AssetImportManager assetImportManager) {
            AssetImportManager = assetImportManager ?? throw new ArgumentNullException(nameof(assetImportManager));
        }

        /// <summary>
        /// Resolves one source texture path to an imported texture asset.
        /// </summary>
        /// <param name="sourcePath">Absolute source texture path.</param>
        /// <returns>Imported texture asset.</returns>
        public TextureAsset ResolveTextureAsset(string sourcePath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (!AssetImportManager.TryLoadTextureAsset(sourcePath, out TextureAsset textureAsset) || textureAsset == null) {
                throw new InvalidOperationException($"Texture reference '{sourcePath}' could not be imported into a TextureAsset.");
            }

            return textureAsset;
        }
    }
}
