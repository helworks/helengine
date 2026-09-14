namespace helengine.editor {
    /// <summary>
    /// Owns typed import-settings persistence while leaving imported runtime assets with the import manager.
    /// </summary>
    public sealed class AssetImportSettingsRepository {
        /// <summary>
        /// Manager that currently supplies the established serialization and validation behavior.
        /// </summary>
        readonly AssetImportManager AssetImportManager;

        /// <summary>
        /// Creates a settings repository for one import manager.
        /// </summary>
        /// <param name="assetImportManager">Manager supplying the current settings behavior.</param>
        public AssetImportSettingsRepository(AssetImportManager assetImportManager) {
            AssetImportManager = assetImportManager ?? throw new ArgumentNullException(nameof(assetImportManager));
        }

        /// <summary>
        /// Gets the project root used for sidecar paths.
        /// </summary>
        public string ProjectRootPath => AssetImportManager.ProjectRootPath;

        /// <summary>
        /// Gets the project assets root used for sidecar paths.
        /// </summary>
        public string AssetsRootPath => AssetImportManager.AssetsRootPath;

        /// <summary>
        /// Gets the import cache root associated with the project settings.
        /// </summary>
        public string ImportRootPath => AssetImportManager.ImportRootPath;

        /// <summary>
        /// Loads or creates common import settings for a source asset.
        /// </summary>
        /// <param name="sourcePath">Source asset path.</param>
        /// <returns>Resolved common settings.</returns>
        public AssetImportSettings LoadOrCreateImportSettings(string sourcePath) {
            return AssetImportManager.LoadOrCreateImportSettings(sourcePath);
        }

        /// <summary>
        /// Saves common import settings for a source asset.
        /// </summary>
        /// <param name="sourcePath">Source asset path.</param>
        /// <param name="settings">Settings to persist.</param>
        public void SaveImportSettings(string sourcePath, AssetImportSettings settings) {
            AssetImportManager.SaveImportSettings(sourcePath, settings);
        }

        /// <summary>
        /// Tries to load or create common import settings for a source asset.
        /// </summary>
        /// <param name="sourcePath">Source asset path.</param>
        /// <param name="settings">Resolved settings when available.</param>
        /// <returns>True when settings were resolved.</returns>
        public bool TryLoadOrCreateImportSettings(string sourcePath, out AssetImportSettings settings) {
            return AssetImportManager.TryLoadOrCreateImportSettings(sourcePath, out settings);
        }

        /// <summary>
        /// Loads or creates texture settings for a source asset.
        /// </summary>
        /// <param name="sourcePath">Source asset path.</param>
        /// <returns>Resolved texture settings.</returns>
        public TextureAssetImportSettings LoadOrCreateTextureImportSettings(string sourcePath) {
            return AssetImportManager.LoadOrCreateTextureImportSettings(sourcePath);
        }

        /// <summary>
        /// Saves texture settings for a source asset.
        /// </summary>
        /// <param name="sourcePath">Source asset path.</param>
        /// <param name="settings">Settings to persist.</param>
        public void SaveTextureImportSettings(string sourcePath, TextureAssetImportSettings settings) {
            AssetImportManager.SaveTextureImportSettings(sourcePath, settings);
        }

        /// <summary>
        /// Tries to load or create texture settings for a source asset.
        /// </summary>
        /// <param name="sourcePath">Source asset path.</param>
        /// <param name="settings">Resolved settings when available.</param>
        /// <returns>True when settings were resolved.</returns>
        public bool TryLoadOrCreateTextureImportSettings(string sourcePath, out TextureAssetImportSettings settings) {
            return AssetImportManager.TryLoadOrCreateTextureImportSettings(sourcePath, out settings);
        }

        /// <summary>
        /// Loads or creates model settings for a source asset.
        /// </summary>
        /// <param name="sourcePath">Source asset path.</param>
        /// <returns>Resolved model settings.</returns>
        public ModelAssetImportSettings LoadOrCreateModelImportSettings(string sourcePath) {
            return AssetImportManager.LoadOrCreateModelImportSettings(sourcePath);
        }

        /// <summary>
        /// Saves model settings for a source asset.
        /// </summary>
        /// <param name="sourcePath">Source asset path.</param>
        /// <param name="settings">Settings to persist.</param>
        public void SaveModelImportSettings(string sourcePath, ModelAssetImportSettings settings) {
            AssetImportManager.SaveModelImportSettings(sourcePath, settings);
        }

        /// <summary>
        /// Tries to load or create model settings for a source asset.
        /// </summary>
        /// <param name="sourcePath">Source asset path.</param>
        /// <param name="settings">Resolved settings when available.</param>
        /// <returns>True when settings were resolved.</returns>
        public bool TryLoadOrCreateModelImportSettings(string sourcePath, out ModelAssetImportSettings settings) {
            return AssetImportManager.TryLoadOrCreateModelImportSettings(sourcePath, out settings);
        }

        /// <summary>
        /// Loads or creates audio settings for a source asset.
        /// </summary>
        /// <param name="sourcePath">Source asset path.</param>
        /// <returns>Resolved audio settings.</returns>
        public AudioAssetImportSettings LoadOrCreateAudioImportSettings(string sourcePath) {
            return AssetImportManager.LoadOrCreateAudioImportSettings(sourcePath);
        }

        /// <summary>
        /// Saves audio settings for a source asset.
        /// </summary>
        /// <param name="sourcePath">Source asset path.</param>
        /// <param name="settings">Settings to persist.</param>
        public void SaveAudioImportSettings(string sourcePath, AudioAssetImportSettings settings) {
            AssetImportManager.SaveAudioImportSettings(sourcePath, settings);
        }

        /// <summary>
        /// Tries to load or create audio settings for a source asset.
        /// </summary>
        /// <param name="sourcePath">Source asset path.</param>
        /// <param name="settings">Resolved settings when available.</param>
        /// <returns>True when settings were resolved.</returns>
        public bool TryLoadOrCreateAudioImportSettings(string sourcePath, out AudioAssetImportSettings settings) {
            return AssetImportManager.TryLoadOrCreateAudioImportSettings(sourcePath, out settings);
        }
    }
}