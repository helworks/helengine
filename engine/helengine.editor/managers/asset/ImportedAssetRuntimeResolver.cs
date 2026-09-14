namespace helengine.editor {
    /// <summary>
    /// Resolves typed runtime assets from the manager-owned cache and source recovery policy.
    /// </summary>
    public sealed class ImportedAssetRuntimeResolver {
        /// <summary>
        /// Manager that owns cache reads, recovery, and runtime resource attachment.
        /// </summary>
        readonly AssetImportManager AssetImportManager;

        /// <summary>
        /// Creates a runtime resolver for one manager-owned cache.
        /// </summary>
        /// <param name="assetImportManager">Manager that owns runtime resolution.</param>
        public ImportedAssetRuntimeResolver(AssetImportManager assetImportManager) {
            AssetImportManager = assetImportManager ?? throw new ArgumentNullException(nameof(assetImportManager));
        }

        /// <summary>
        /// Resolves a texture from its source path or current cache policy.
        /// </summary>
        /// <param name="sourcePath">Authored texture source path.</param>
        /// <param name="asset">Resolved texture asset.</param>
        /// <returns>True when a texture was resolved.</returns>
        public bool TryResolveTexture(string sourcePath, out TextureAsset asset) {
            return AssetImportManager.TryLoadTextureAsset(sourcePath, out asset);
        }

        /// <summary>
        /// Resolves a cached texture by its stable imported asset identifier.
        /// </summary>
        /// <param name="assetId">Imported texture asset identifier.</param>
        /// <param name="asset">Resolved texture asset.</param>
        /// <returns>True when a cached texture was resolved.</returns>
        public bool TryResolveImportedTexture(string assetId, out TextureAsset asset) {
            return AssetImportManager.TryLoadImportedTextureAsset(assetId, out asset);
        }

        /// <summary>
        /// Resolves a text asset from its source path or current cache policy.
        /// </summary>
        /// <param name="sourcePath">Authored text source path.</param>
        /// <param name="asset">Resolved text asset.</param>
        /// <returns>True when a text asset was resolved.</returns>
        public bool TryResolveText(string sourcePath, out TextAsset asset) {
            return AssetImportManager.TryLoadTextAsset(sourcePath, out asset);
        }

        /// <summary>
        /// Resolves a font asset from its source path or current cache policy.
        /// </summary>
        /// <param name="sourcePath">Authored font source path.</param>
        /// <param name="asset">Resolved font asset.</param>
        /// <returns>True when a font asset was resolved.</returns>
        public bool TryResolveFont(string sourcePath, out FontAsset asset) {
            return AssetImportManager.TryLoadFontAsset(sourcePath, out asset);
        }

        /// <summary>
        /// Resolves an audio asset from its source path or current cache policy.
        /// </summary>
        /// <param name="sourcePath">Authored audio source path.</param>
        /// <param name="asset">Resolved audio asset.</param>
        /// <returns>True when an audio asset was resolved.</returns>
        public bool TryResolveAudio(string sourcePath, out AudioAsset asset) {
            return AssetImportManager.TryLoadAudioAsset(sourcePath, out asset);
        }

        /// <summary>
        /// Resolves a model asset from its source path or current cache policy.
        /// </summary>
        /// <param name="sourcePath">Authored model source path.</param>
        /// <param name="asset">Resolved model asset.</param>
        /// <returns>True when a model asset was resolved.</returns>
        public bool TryResolveModel(string sourcePath, out ModelAsset asset) {
            return AssetImportManager.TryLoadModelAsset(sourcePath, out asset);
        }
    }
}