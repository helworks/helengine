namespace helengine.editor {
    /// <summary>
    /// Stores one platform-specific font processor configuration record for a source asset.
    /// </summary>
    public class FontAssetProcessorSettings {
        /// <summary>
        /// Default pixel size used by the historical font import path.
        /// </summary>
        public const int DefaultPixelSize = 32;

        /// <summary>
        /// Gets or sets the requested font pixel size used during source font rasterization.
        /// </summary>
        public int PixelSize { get; set; } = DefaultPixelSize;
    }
}
