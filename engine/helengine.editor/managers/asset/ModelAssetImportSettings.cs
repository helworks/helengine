namespace helengine.editor {
    /// <summary>
    /// Stores importer metadata and platform-specific model processor settings for one model source asset.
    /// </summary>
    public class ModelAssetImportSettings {
        /// <summary>
        /// Initializes nested importer and processor settings containers.
        /// </summary>
        public ModelAssetImportSettings() {
            Importer = new AssetImporterSettings();
            Processor = new ModelAssetProcessorPlatformSettings();
        }

        /// <summary>
        /// Gets or sets importer metadata for the source model.
        /// </summary>
        public AssetImporterSettings Importer { get; set; }

        /// <summary>
        /// Gets or sets per-platform model processor settings.
        /// </summary>
        public ModelAssetProcessorPlatformSettings Processor { get; set; }
    }
}
