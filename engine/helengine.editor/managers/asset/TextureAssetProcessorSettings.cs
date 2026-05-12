namespace helengine.editor {
    /// <summary>
    /// Stores one platform-specific texture processor configuration record for a source asset.
    /// </summary>
    public class TextureAssetProcessorSettings {
        /// <summary>
        /// Gets or sets the maximum allowed width or height in pixels for the processed texture, or zero when uncapped.
        /// </summary>
        public int MaxResolution { get; set; }
    }
}
