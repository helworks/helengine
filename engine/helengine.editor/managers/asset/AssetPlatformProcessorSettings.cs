namespace helengine.editor {
    /// <summary>
    /// Stores processor settings for one target platform.
    /// </summary>
    public class AssetPlatformProcessorSettings {
        /// <summary>
        /// Initializes the processor settings that apply to model assets on this platform.
        /// </summary>
        public AssetPlatformProcessorSettings() {
            Model = new ModelAssetProcessorSettings();
        }

        /// <summary>
        /// Gets or sets the processor settings that affect model asset generation.
        /// </summary>
        public ModelAssetProcessorSettings Model { get; set; }
    }
}
