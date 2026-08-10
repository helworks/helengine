namespace helengine.editor {
    /// <summary>
    /// Stores material processor settings keyed by platform identifier.
    /// </summary>
    public class MaterialAssetProcessorPlatformSettings {
        /// <summary>
        /// Initializes an empty platform map for material processor settings.
        /// </summary>
        public MaterialAssetProcessorPlatformSettings() {
            Platforms = new Dictionary<string, MaterialAssetProcessorSettings>(StringComparer.OrdinalIgnoreCase);
            Environments = new Dictionary<string, Dictionary<string, MaterialAssetProcessorSettings>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets or sets the platform-specific material processor settings.
        /// </summary>
        public Dictionary<string, MaterialAssetProcessorSettings> Platforms { get; set; }

        /// <summary>Gets or sets material settings keyed by platform and nested environment id.</summary>
        public Dictionary<string, Dictionary<string, MaterialAssetProcessorSettings>> Environments { get; set; }
    }
}
