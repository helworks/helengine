namespace helengine.editor {
    /// <summary>
    /// Represents a temporary glyph bitmap placement used during font import.
    /// </summary>
    public class TempFontItem {
        /// <summary>
        /// Gets or sets the character represented by this font item.
        /// </summary>
        public char Char { get; set; }

        /// <summary>
        /// Gets or sets the X position of the glyph within the atlas.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Gets or sets the Y position of the glyph within the atlas.
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Gets or sets the width of the glyph bitmap.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the height of the glyph bitmap.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets the bitmap image for the glyph.
        /// </summary>
        public Bitmap Bitmap { get; set; }
    }
}
