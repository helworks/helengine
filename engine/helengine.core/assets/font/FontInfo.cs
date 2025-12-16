namespace helengine {
    /// <summary>
    /// Describes font metrics used for layout and rendering.
    /// </summary>
    public class FontInfo {
        /// <summary>
        /// Gets or sets the font family name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the line spacing in pixels.
        /// </summary>
        public int LineSpacing { get; set; }

        /// <summary>
        /// Gets or sets the width of a space character in pixels.
        /// </summary>
        public float SpaceWidth { get; set; }

        /// <summary>
        /// Initializes font metrics with name, line spacing, and space width.
        /// </summary>
        /// <param name="name">Font family name.</param>
        /// <param name="lineSpacing">Line spacing in pixels.</param>
        /// <param name="spaceWidth">Space character width in pixels.</param>
        public FontInfo(string name, int lineSpacing, float spaceWidth) {
            Name = name;
            LineSpacing = lineSpacing;
            SpaceWidth = spaceWidth;
        }
    }
}
