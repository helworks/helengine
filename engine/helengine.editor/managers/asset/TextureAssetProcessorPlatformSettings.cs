namespace helengine.editor {
    /// <summary>
    /// Stores texture processor settings keyed by platform identifier.
    /// </summary>
    public class TextureAssetProcessorPlatformSettings {
        /// <summary>
        /// Initializes an empty platform map for texture processor settings.
        /// </summary>
        public TextureAssetProcessorPlatformSettings() {
            Platforms = new Dictionary<string, TextureAssetProcessorSettings>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets or sets the platform-specific texture processor settings.
        /// </summary>
        public Dictionary<string, TextureAssetProcessorSettings> Platforms { get; set; }
    }
}
