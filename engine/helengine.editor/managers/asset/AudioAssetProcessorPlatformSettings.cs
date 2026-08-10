namespace helengine.editor {
    /// <summary>
    /// Stores audio processor settings keyed by platform identifier.
    /// </summary>
    public sealed class AudioAssetProcessorPlatformSettings {
        /// <summary>
        /// Initializes an empty platform map for audio processor settings.
        /// </summary>
        public AudioAssetProcessorPlatformSettings() {
            Platforms = new Dictionary<string, AudioAssetProcessorSettings>(StringComparer.OrdinalIgnoreCase);
            Environments = new Dictionary<string, Dictionary<string, AudioAssetProcessorSettings>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets or sets the platform-specific audio processor settings.
        /// </summary>
        public Dictionary<string, AudioAssetProcessorSettings> Platforms { get; set; }

        /// <summary>Gets or sets audio settings keyed by platform and nested environment id.</summary>
        public Dictionary<string, Dictionary<string, AudioAssetProcessorSettings>> Environments { get; set; }
    }
}
