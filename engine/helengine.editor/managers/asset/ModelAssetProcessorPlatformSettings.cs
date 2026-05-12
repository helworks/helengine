namespace helengine.editor {
    /// <summary>
    /// Stores model processor settings keyed by platform identifier.
    /// </summary>
    public class ModelAssetProcessorPlatformSettings {
        /// <summary>
        /// Initializes an empty platform map for model processor settings.
        /// </summary>
        public ModelAssetProcessorPlatformSettings() {
            Platforms = new Dictionary<string, ModelAssetProcessorSettings>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets or sets the platform-specific model processor settings.
        /// </summary>
        public Dictionary<string, ModelAssetProcessorSettings> Platforms { get; set; }
    }
}
