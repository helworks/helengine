namespace helengine.editor {
    /// <summary>
    /// Implements the public project asset-authoring capability over one host-owned import manager.
    /// </summary>
    public sealed class EditorProjectAssetAuthoringService : IEditorProjectAssetAuthoringService {
        /// <summary>
        /// Import manager owned by the editor host and hidden behind this project-facing facade.
        /// </summary>
        readonly AssetImportManager AssetImportManagerValue;

        /// <summary>
        /// Initializes one project asset-authoring capability.
        /// </summary>
        /// <param name="assetImportManager">Host-owned import manager backing the capability.</param>
        internal EditorProjectAssetAuthoringService(AssetImportManager assetImportManager) {
            AssetImportManagerValue = assetImportManager ?? throw new ArgumentNullException(nameof(assetImportManager));
        }

        /// <summary>
        /// Loads typed texture settings through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source texture path.</param>
        /// <returns>Resolved typed texture settings.</returns>
        public TextureAssetImportSettings LoadOrCreateTextureImportSettings(string sourcePath) {
            return AssetImportManagerValue.LoadOrCreateTextureImportSettings(sourcePath);
        }

        /// <summary>
        /// Saves typed texture settings through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source texture path.</param>
        /// <param name="settings">Typed settings to persist.</param>
        public void SaveTextureImportSettings(string sourcePath, TextureAssetImportSettings settings) {
            AssetImportManagerValue.SaveTextureImportSettings(sourcePath, settings);
        }

        /// <summary>
        /// Loads typed model settings through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source model path.</param>
        /// <returns>Resolved typed model settings.</returns>
        public ModelAssetImportSettings LoadOrCreateModelImportSettings(string sourcePath) {
            return AssetImportManagerValue.LoadOrCreateModelImportSettings(sourcePath);
        }

        /// <summary>
        /// Loads typed audio settings through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source audio path.</param>
        /// <returns>Resolved typed audio settings.</returns>
        public AudioAssetImportSettings LoadOrCreateAudioImportSettings(string sourcePath) {
            return AssetImportManagerValue.LoadOrCreateAudioImportSettings(sourcePath);
        }

        /// <summary>
        /// Loads sectioned settings through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source text or font path.</param>
        /// <returns>Resolved sectioned settings.</returns>
        public AssetImportSettings LoadOrCreateSectionedImportSettings(string sourcePath) {
            return AssetImportManagerValue.LoadOrCreateImportSettings(sourcePath);
        }

        /// <summary>
        /// Saves typed model settings through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source model path.</param>
        /// <param name="settings">Typed settings to persist.</param>
        public void SaveModelImportSettings(string sourcePath, ModelAssetImportSettings settings) {
            AssetImportManagerValue.SaveModelImportSettings(sourcePath, settings);
        }

        /// <summary>
        /// Saves typed audio settings through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source audio path.</param>
        /// <param name="settings">Typed settings to persist.</param>
        public void SaveAudioImportSettings(string sourcePath, AudioAssetImportSettings settings) {
            AssetImportManagerValue.SaveAudioImportSettings(sourcePath, settings);
        }

        /// <summary>
        /// Saves sectioned settings through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source text or font path.</param>
        /// <param name="settings">Sectioned settings to persist.</param>
        public void SaveSectionedImportSettings(string sourcePath, AssetImportSettings settings) {
            AssetImportManagerValue.SaveImportSettings(sourcePath, settings);
        }

        /// <summary>
        /// Resolves a source model through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source model path.</param>
        /// <returns>Runtime model resolved from the current imported asset.</returns>
        public RuntimeModel ResolveRuntimeModel(string sourcePath) {
            return new EditorFileSystemModelResolver(AssetImportManagerValue).ResolveRuntimeModel(sourcePath);
        }

        /// <summary>
        /// Resolves a source font through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source font path.</param>
        /// <returns>Imported font asset.</returns>
        public FontAsset ResolveFontAsset(string sourcePath) {
            return new EditorFileSystemFontResolver(AssetImportManagerValue).ResolveFontAsset(sourcePath);
        }

        /// <summary>
        /// Resolves a source texture through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source texture path.</param>
        /// <returns>Imported texture asset.</returns>
        public TextureAsset ResolveTextureAsset(string sourcePath) {
            return new EditorFileSystemTextureResolver(AssetImportManagerValue).ResolveTextureAsset(sourcePath);
        }

        /// <summary>
        /// Creates a scene-reference resolver using all file-backed asset resolvers owned by the host.
        /// </summary>
        /// <returns>Resolver for file-backed scene asset references.</returns>
        public EditorSceneAssetReferenceResolver CreateSceneAssetReferenceResolver() {
            return new EditorSceneAssetReferenceResolver(
                AssetImportManagerValue.ContentManager,
                ResolveProjectRootPath(),
                new EditorFileSystemModelResolver(AssetImportManagerValue),
                new EditorFileSystemFontResolver(AssetImportManagerValue),
                new EditorFileSystemTextureResolver(AssetImportManagerValue));
        }

        /// <summary>
        /// Loads an imported texture by its stable asset identifier.
        /// </summary>
        /// <param name="assetId">Stable imported texture asset identifier.</param>
        /// <param name="textureAsset">Loaded texture when available.</param>
        /// <returns>True when the imported texture could be loaded.</returns>
        public bool TryLoadImportedTextureAsset(string assetId, out TextureAsset textureAsset) {
            return AssetImportManagerValue.TryLoadImportedTextureAsset(assetId, out textureAsset);
        }

        /// <summary>
        /// Resolves the root path used by the hidden host import manager.
        /// </summary>
        /// <returns>Absolute project root path.</returns>
        string ResolveProjectRootPath() {
            string assetsRootPath = AssetImportManagerValue.AssetsRootPath;
            string projectRootPath = Path.GetDirectoryName(assetsRootPath);
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new InvalidOperationException("The host asset import manager does not expose a project root path.");
            }

            return Path.GetFullPath(projectRootPath);
        }
    }
}
