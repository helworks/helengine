namespace helengine.editor {
    /// <summary>
    /// Stores importer and processor configuration for a single source asset file.
    /// </summary>
    public class AssetImportSettings {
        /// <summary>
        /// Initializes nested importer and processor settings containers for a source asset.
        /// </summary>
        public AssetImportSettings() {
            Importer = new AssetImporterSettings();
            Processor = new AssetProcessorSettings();
        }

        /// <summary>
        /// Gets or sets the importer-facing settings shared across every target platform.
        /// </summary>
        public AssetImporterSettings Importer { get; set; }

        /// <summary>
        /// Gets or sets the processor-facing settings keyed by target platform.
        /// </summary>
        public AssetProcessorSettings Processor { get; set; }
    }
}
