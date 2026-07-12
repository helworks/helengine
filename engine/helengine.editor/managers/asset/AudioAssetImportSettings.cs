namespace helengine.editor {
    /// <summary>
    /// Stores importer metadata and platform-specific audio processor settings for one audio source asset.
    /// </summary>
    public sealed class AudioAssetImportSettings {
        /// <summary>
        /// Initializes nested importer and processor settings containers.
        /// </summary>
        public AudioAssetImportSettings() {
            Importer = new AssetImporterSettings();
            Processor = new AudioAssetProcessorPlatformSettings();
        }

        /// <summary>
        /// Gets or sets importer metadata for the source audio.
        /// </summary>
        public AssetImporterSettings Importer { get; set; }

        /// <summary>
        /// Gets or sets per-platform audio processor settings.
        /// </summary>
        public AudioAssetProcessorPlatformSettings Processor { get; set; }
    }
}
