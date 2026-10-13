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

        /// <summary>Gets or sets whether source glyph advances and space width are rounded to whole cooked pixels without changing rasterization or vertical metrics.</summary>
        public bool SnapHorizontalMetricsToPixels { get; set; }
    }
}
