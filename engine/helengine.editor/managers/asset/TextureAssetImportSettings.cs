namespace helengine.editor {
    /// <summary>
    /// Stores importer metadata and platform-specific texture processor settings for one texture source asset.
    /// </summary>
    public class TextureAssetImportSettings {
        /// <summary>
        /// Initializes nested importer and processor settings containers.
        /// </summary>
        public TextureAssetImportSettings() {
            Importer = new AssetImporterSettings();
            Processor = new TextureAssetProcessorPlatformSettings();
        }

        /// <summary>
        /// Gets or sets importer metadata for the source texture.
        /// </summary>
        public AssetImporterSettings Importer { get; set; }

        /// <summary>
        /// Gets or sets per-platform texture processor settings.
        /// </summary>
        public TextureAssetProcessorPlatformSettings Processor { get; set; }
    }
}
