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
            Environments = new Dictionary<string, Dictionary<string, TextureAssetProcessorSettings>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets or sets the platform-specific texture processor settings.
        /// </summary>
        public Dictionary<string, TextureAssetProcessorSettings> Platforms { get; set; }

        /// <summary>Gets or sets texture settings keyed by platform and nested environment id.</summary>
        public Dictionary<string, Dictionary<string, TextureAssetProcessorSettings>> Environments { get; set; }
    }
}
