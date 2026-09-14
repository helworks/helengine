namespace helengine.editor;

/// <summary>
/// Resolves cached runtime assets without owning import execution or project settings.
/// </summary>
public sealed class ImportedAssetRuntimeResolver {
    readonly AssetImportManager AssetImportManager;

    /// <summary>
    /// Initializes runtime resolution against the canonical import manager.
    /// </summary>
    /// <param name="assetImportManager">Canonical import manager.</param>
    public ImportedAssetRuntimeResolver(AssetImportManager assetImportManager) {
        AssetImportManager = assetImportManager ?? throw new ArgumentNullException(nameof(assetImportManager));
    }

    /// <summary>Resolves a texture source from its current cache.</summary>
    /// <param name="sourcePath">Source path.</param>
    /// <param name="asset">Resolved texture when available.</param>
    /// <returns>True when a cached asset was resolved.</returns>
    public bool TryLoadTextureAsset(string sourcePath, out TextureAsset asset) {
        return AssetImportManager.TryLoadTextureAsset(sourcePath, out asset);
    }

    /// <summary>Resolves an imported texture by stable asset identity.</summary>
    /// <param name="assetId">Stable asset identifier.</param>
    /// <param name="asset">Resolved texture when available.</param>
    /// <returns>True when a cached asset was resolved.</returns>
    public bool TryLoadImportedTextureAsset(string assetId, out TextureAsset asset) {
        return AssetImportManager.TryLoadImportedTextureAsset(assetId, out asset);
    }

    /// <summary>Resolves a text source from its current cache.</summary>
    /// <param name="sourcePath">Source path.</param>
    /// <param name="asset">Resolved text when available.</param>
    /// <returns>True when a cached asset was resolved.</returns>
    public bool TryLoadTextAsset(string sourcePath, out TextAsset asset) {
        return AssetImportManager.TryLoadTextAsset(sourcePath, out asset);
    }

    /// <summary>Resolves a font source from its current cache.</summary>
    /// <param name="sourcePath">Source path.</param>
    /// <param name="asset">Resolved font when available.</param>
    /// <returns>True when a cached asset was resolved.</returns>
    public bool TryLoadFontAsset(string sourcePath, out FontAsset asset) {
        return AssetImportManager.TryLoadFontAsset(sourcePath, out asset);
    }

    /// <summary>Resolves an audio source from its current cache.</summary>
    /// <param name="sourcePath">Source path.</param>
    /// <param name="asset">Resolved audio when available.</param>
    /// <returns>True when a cached asset was resolved.</returns>
    public bool TryLoadAudioAsset(string sourcePath, out AudioAsset asset) {
        return AssetImportManager.TryLoadAudioAsset(sourcePath, out asset);
    }

    /// <summary>Resolves a model source from its current cache.</summary>
    /// <param name="sourcePath">Source path.</param>
    /// <param name="asset">Resolved model when available.</param>
    /// <returns>True when a cached asset was resolved.</returns>
    public bool TryLoadModelAsset(string sourcePath, out ModelAsset asset) {
        return AssetImportManager.TryLoadModelAsset(sourcePath, out asset);
    }
}
