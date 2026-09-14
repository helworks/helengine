namespace helengine.editor {
    /// <summary>
    /// Executes typed asset imports through the established manager pipeline.
    /// </summary>
    public sealed class AssetImportExecutionService {
        /// <summary>
        /// Manager that owns importer selection, cache publication, and import transactions.
        /// </summary>
        readonly AssetImportManager AssetImportManager;

        /// <summary>
        /// Creates an execution service for one manager-owned import pipeline.
        /// </summary>
        /// <param name="assetImportManager">Manager that owns import execution.</param>
        public AssetImportExecutionService(AssetImportManager assetImportManager) {
            AssetImportManager = assetImportManager ?? throw new ArgumentNullException(nameof(assetImportManager));
        }

        /// <summary>
        /// Imports and publishes one texture asset.
        /// </summary>
        /// <param name="sourcePath">Authored texture source path.</param>
        /// <returns>Imported texture asset.</returns>
        public TextureAsset ImportTexture(string sourcePath) {
            return AssetImportManager.ImportTexture(sourcePath);
        }

        /// <summary>
        /// Imports and publishes one text asset.
        /// </summary>
        /// <param name="sourcePath">Authored text source path.</param>
        /// <returns>Imported text asset.</returns>
        public TextAsset ImportText(string sourcePath) {
            return AssetImportManager.ImportText(sourcePath);
        }

        /// <summary>
        /// Imports and publishes one font asset.
        /// </summary>
        /// <param name="sourcePath">Authored font source path.</param>
        /// <returns>Imported font asset.</returns>
        public FontAsset ImportFont(string sourcePath) {
            return AssetImportManager.ImportFont(sourcePath);
        }

        /// <summary>
        /// Imports and publishes one audio asset.
        /// </summary>
        /// <param name="sourcePath">Authored audio source path.</param>
        /// <returns>Imported audio asset.</returns>
        public AudioAsset ImportAudio(string sourcePath) {
            return AssetImportManager.ImportAudio(sourcePath);
        }

        /// <summary>
        /// Imports and publishes one model asset.
        /// </summary>
        /// <param name="sourcePath">Authored model source path.</param>
        /// <returns>Imported model asset.</returns>
        public ModelAsset ImportModel(string sourcePath) {
            return AssetImportManager.ImportModel(sourcePath);
        }
    }
}