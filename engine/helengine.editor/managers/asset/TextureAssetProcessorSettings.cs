namespace helengine.editor {
    /// <summary>
    /// Stores one platform-specific texture processor configuration record for a source asset.
    /// </summary>
    public class TextureAssetProcessorSettings {
        /// <summary>
        /// Gets or sets the maximum allowed width or height in pixels for the processed texture, or zero when uncapped.
        /// </summary>
        public int MaxResolution { get; set; }

        /// <summary>
        /// Gets or sets the serialized texture payload format produced for this platform.
        /// </summary>
        public TextureAssetColorFormat ColorFormat { get; set; }

        /// <summary>
        /// Gets or sets the alpha precision stored by the processed texture payload.
        /// </summary>
        public TextureAssetAlphaPrecision AlphaPrecision { get; set; }
    }
}
