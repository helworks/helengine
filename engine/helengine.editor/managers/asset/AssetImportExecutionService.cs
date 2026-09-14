namespace helengine.editor;

/// <summary>
/// Owns typed import execution calls for one project import operation.
/// </summary>
public sealed class AssetImportExecutionService {
    readonly AssetImportManager AssetImportManager;

    /// <summary>
    /// Initializes execution against the canonical project import manager.
    /// </summary>
    /// <param name="assetImportManager">Canonical import manager.</param>
    public AssetImportExecutionService(AssetImportManager assetImportManager) {
        AssetImportManager = assetImportManager ?? throw new ArgumentNullException(nameof(assetImportManager));
    }

    /// <summary>Imports one texture source.</summary>
    /// <param name="sourcePath">Source path.</param>
    /// <returns>Imported texture asset.</returns>
    public TextureAsset ImportTexture(string sourcePath) {
        return AssetImportManager.ImportTexture(sourcePath);
    }

    /// <summary>Imports one text source.</summary>
    /// <param name="sourcePath">Source path.</param>
    /// <returns>Imported text asset.</returns>
    public TextAsset ImportText(string sourcePath) {
        return AssetImportManager.ImportText(sourcePath);
    }

    /// <summary>Imports one font source.</summary>
    /// <param name="sourcePath">Source path.</param>
    /// <returns>Imported font asset.</returns>
    public FontAsset ImportFont(string sourcePath) {
        return AssetImportManager.ImportFont(sourcePath);
    }

    /// <summary>Imports one audio source.</summary>
    /// <param name="sourcePath">Source path.</param>
    /// <returns>Imported audio asset.</returns>
    public AudioAsset ImportAudio(string sourcePath) {
        return AssetImportManager.ImportAudio(sourcePath);
    }

    /// <summary>Imports one model source.</summary>
    /// <param name="sourcePath">Source path.</param>
    /// <returns>Imported model asset.</returns>
    public ModelAsset ImportModel(string sourcePath) {
        return AssetImportManager.ImportModel(sourcePath);
    }

    /// <summary>Imports textures that do not yet have a cache artifact.</summary>
    /// <returns>Source paths that were imported.</returns>
    public IReadOnlyList<string> ImportTexturesMissingCache() {
        return AssetImportManager.ImportTexturesMissingCache();
    }

    /// <summary>Imports models that do not yet have a cache artifact.</summary>
    /// <returns>Source paths that were imported.</returns>
    public IReadOnlyList<string> ImportModelsMissingCache() {
        return AssetImportManager.ImportModelsMissingCache();
    }

    /// <summary>Imports audio sources that do not yet have a cache artifact.</summary>
    /// <returns>Source paths that were imported.</returns>
    public IReadOnlyList<string> ImportAudiosMissingCache() {
        return AssetImportManager.ImportAudiosMissingCache();
    }
}
