namespace helengine.editor {
    /// <summary>
    /// Stores processor settings for one target platform.
    /// </summary>
    public class AssetPlatformProcessorSettings {
        /// <summary>
        /// Initializes the processor settings that apply to model assets on this platform.
        /// </summary>
        public AssetPlatformProcessorSettings() {
            Texture = new TextureAssetProcessorSettings();
            Model = new ModelAssetProcessorSettings();
            Material = new MaterialAssetProcessorSettings();
        }

        /// <summary>
        /// Gets or sets the processor settings that affect texture asset generation.
        /// </summary>
        public TextureAssetProcessorSettings Texture { get; set; }

        /// <summary>
        /// Gets or sets the processor settings that affect model asset generation.
        /// </summary>
        public ModelAssetProcessorSettings Model { get; set; }

        /// <summary>
        /// Gets or sets the processor settings that affect material asset authoring on this platform.
        /// </summary>
        public MaterialAssetProcessorSettings Material { get; set; }
    }
}
