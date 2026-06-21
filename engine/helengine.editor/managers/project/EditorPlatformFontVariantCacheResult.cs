namespace helengine.editor {
    /// <summary>
    /// Describes one resolved editor-side cached platform font variant.
    /// </summary>
    public sealed class EditorPlatformFontVariantCacheResult {
        /// <summary>
        /// Initializes one cache-resolution result.
        /// </summary>
        /// <param name="cachedFontAssetPath">Absolute path to the cached platform font asset.</param>
        /// <param name="cachedAtlasTextureAssetPath">Absolute path to the cached platform atlas texture asset.</param>
        /// <param name="isCacheHit">True when both files already existed and were reused.</param>
        public EditorPlatformFontVariantCacheResult(string cachedFontAssetPath, string cachedAtlasTextureAssetPath, bool isCacheHit) {
            if (string.IsNullOrWhiteSpace(cachedFontAssetPath)) {
                throw new ArgumentException("Cached font asset path must be provided.", nameof(cachedFontAssetPath));
            } else if (string.IsNullOrWhiteSpace(cachedAtlasTextureAssetPath)) {
                throw new ArgumentException("Cached atlas texture asset path must be provided.", nameof(cachedAtlasTextureAssetPath));
            }

            CachedFontAssetPath = cachedFontAssetPath;
            CachedAtlasTextureAssetPath = cachedAtlasTextureAssetPath;
            IsCacheHit = isCacheHit;
        }

        /// <summary>
        /// Gets the absolute path to the cached platform font asset.
        /// </summary>
        public string CachedFontAssetPath { get; }

        /// <summary>
        /// Gets the absolute path to the cached platform atlas texture asset.
        /// </summary>
        public string CachedAtlasTextureAssetPath { get; }

        /// <summary>
        /// Gets whether the cached platform font variant was reused without regeneration.
        /// </summary>
        public bool IsCacheHit { get; }
    }
}
