namespace helengine.editor {
    /// <summary>
    /// Stores importer metadata and platform-specific material processor settings for one material asset sidecar.
    /// </summary>
    public class MaterialAssetImportSettings {
        /// <summary>
        /// Initializes nested importer and processor settings containers.
        /// </summary>
        public MaterialAssetImportSettings() {
            Importer = new AssetImporterSettings();
            Processor = new MaterialAssetProcessorPlatformSettings();
        }

        /// <summary>
        /// Gets or sets importer metadata for the material asset.
        /// </summary>
        public AssetImporterSettings Importer { get; set; }

        /// <summary>
        /// Gets or sets per-platform material processor settings.
        /// </summary>
        public MaterialAssetProcessorPlatformSettings Processor { get; set; }
    }
}
