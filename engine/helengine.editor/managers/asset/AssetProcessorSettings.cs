namespace helengine.editor {
    /// <summary>
    /// Stores processor settings keyed by target platform.
    /// </summary>
    public class AssetProcessorSettings {
        /// <summary>
        /// Initializes an empty processor-settings platform map.
        /// </summary>
        public AssetProcessorSettings() {
            Platforms = new Dictionary<string, AssetPlatformProcessorSettings>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets or sets the platform-specific processor settings map.
        /// </summary>
        public Dictionary<string, AssetPlatformProcessorSettings> Platforms { get; set; }
    }
}
