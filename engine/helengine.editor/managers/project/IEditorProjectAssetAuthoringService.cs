namespace helengine.editor {
    /// <summary>
    /// Provides project-authored editor commands with host-owned asset settings and source-import operations.
    /// </summary>
    public interface IEditorProjectAssetAuthoringService {
        /// <summary>
        /// Loads typed texture settings for a source file or creates the current defaults when no settings exist.
        /// </summary>
        /// <param name="sourcePath">Absolute source texture path.</param>
        /// <returns>Resolved typed texture settings.</returns>
        TextureAssetImportSettings LoadOrCreateTextureImportSettings(string sourcePath);

        /// <summary>
        /// Saves typed texture settings next to a source file using the current editor format.
        /// </summary>
        /// <param name="sourcePath">Absolute source texture path.</param>
        /// <param name="settings">Typed settings to persist.</param>
        void SaveTextureImportSettings(string sourcePath, TextureAssetImportSettings settings);

        /// <summary>
        /// Loads typed model settings for a source file or creates the current defaults when no settings exist.
        /// </summary>
        /// <param name="sourcePath">Absolute source model path.</param>
        /// <returns>Resolved typed model settings.</returns>
        ModelAssetImportSettings LoadOrCreateModelImportSettings(string sourcePath);

        /// <summary>
        /// Loads typed audio settings for a source file or creates the current defaults when no settings exist.
        /// </summary>
        /// <param name="sourcePath">Absolute source audio path.</param>
        /// <returns>Resolved typed audio settings.</returns>
        AudioAssetImportSettings LoadOrCreateAudioImportSettings(string sourcePath);

        /// <summary>
        /// Loads sectioned settings for a source file or creates the current defaults when no settings exist.
        /// </summary>
        /// <param name="sourcePath">Absolute source text or font path.</param>
        /// <returns>Resolved sectioned settings.</returns>
        AssetImportSettings LoadOrCreateSectionedImportSettings(string sourcePath);

        /// <summary>
        /// Saves typed model settings next to a source file using the current editor format.
        /// </summary>
        /// <param name="sourcePath">Absolute source model path.</param>
        /// <param name="settings">Typed settings to persist.</param>
        void SaveModelImportSettings(string sourcePath, ModelAssetImportSettings settings);

        /// <summary>
        /// Saves typed audio settings next to a source file using the current editor format.
        /// </summary>
        /// <param name="sourcePath">Absolute source audio path.</param>
        /// <param name="settings">Typed settings to persist.</param>
        void SaveAudioImportSettings(string sourcePath, AudioAssetImportSettings settings);

        /// <summary>
        /// Saves sectioned settings next to a source file using the current editor format.
        /// </summary>
        /// <param name="sourcePath">Absolute source text or font path.</param>
        /// <param name="settings">Sectioned settings to persist.</param>
        void SaveSectionedImportSettings(string sourcePath, AssetImportSettings settings);

        /// <summary>
        /// Resolves a source model through the host-registered importer and current cache pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source model path.</param>
        /// <returns>Runtime model resolved from the current imported asset.</returns>
        RuntimeModel ResolveRuntimeModel(string sourcePath);

        /// <summary>
        /// Resolves a source font through the host-registered importer and current cache pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source font path.</param>
        /// <returns>Imported font asset.</returns>
        FontAsset ResolveFontAsset(string sourcePath);

        /// <summary>
        /// Resolves one source texture through the host-registered importer and current cache pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source texture path.</param>
        /// <returns>Imported texture asset.</returns>
        TextureAsset ResolveTextureAsset(string sourcePath);

        /// <summary>
        /// Creates the scene-reference resolver backed by the host-owned current import pipeline.
        /// </summary>
        /// <returns>Resolver for file-backed scene asset references.</returns>
        EditorSceneAssetReferenceResolver CreateSceneAssetReferenceResolver();

        /// <summary>
        /// Writes one current native asset beneath the active project's assets directory.
        /// </summary>
        /// <param name="relativePath">Assets-relative native asset path.</param>
        /// <param name="asset">Native asset payload to author.</param>
        void WriteNativeAsset(string relativePath, Asset asset);

        /// <summary>
        /// Writes one current native material settings document beneath the active project.
        /// </summary>
        /// <param name="relativePath">Assets-relative material path.</param>
        /// <param name="definition">Native material definition to author.</param>
        void WriteNativeMaterial(string relativePath, GeneratedMaterialAssetDefinition definition);

        /// <summary>
        /// Creates a canonical reference for one existing assets-relative authored file.
        /// </summary>
        /// <param name="relativePath">Path relative to the active project's assets directory.</param>
        /// <param name="expectedKind">Expected editor asset category.</param>
        /// <returns>Canonical reference carrying the embedded or imported identity and hash.</returns>
        SceneAssetReference CreateFileReference(string relativePath, AssetEntryKind expectedKind);

        /// <summary>
        /// Loads one current native asset through the host-owned asset reader.
        /// </summary>
        /// <typeparam name="TAsset">Expected native asset type.</typeparam>
        /// <param name="relativePath">Assets-relative native asset path.</param>
        /// <returns>Loaded native asset.</returns>
        TAsset LoadNativeAsset<TAsset>(string relativePath) where TAsset : Asset;

        /// <summary>
        /// Loads an imported texture by its authored asset identifier.
        /// </summary>
        /// <param name="assetId">Stable imported texture asset identifier.</param>
        /// <param name="textureAsset">Loaded texture when available.</param>
        /// <returns>True when the imported texture could be loaded.</returns>
        bool TryLoadImportedTextureAsset(string assetId, out TextureAsset textureAsset);
    }
}
