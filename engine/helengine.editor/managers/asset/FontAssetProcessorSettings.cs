namespace helengine.editor {
    /// <summary>
    /// Stores one platform-specific font processor configuration record for a source asset.
    /// </summary>
    public class FontAssetProcessorSettings {
        /// <summary>
        /// Default pixel size used by the historical font import path.
        /// </summary>
        public const int DefaultPixelSize = 32;

        /// <summary>Immutable character request shared by all font generation backends.</summary>
        FontCharacterSet CharacterSelection = FontCharacterSet.BasicLatin;

        /// <summary>Gets or sets the exact requested glyph coverage. New requests contain printable Basic Latin only; callers opt into accents or other scalars explicitly.</summary>
        public FontCharacterSet Characters {
            get => CharacterSelection;
            set => CharacterSelection = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Gets or sets the requested font pixel size used during source font rasterization.
        /// </summary>
        public int PixelSize { get; set; } = DefaultPixelSize;

        /// <summary>Gets or sets whether source glyph advances and space width are rounded to whole cooked pixels without changing rasterization or vertical metrics.</summary>
        public bool SnapHorizontalMetricsToPixels { get; set; }
    }
}
