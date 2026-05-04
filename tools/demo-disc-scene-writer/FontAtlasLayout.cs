namespace helengine.demo_disc_scene_writer {
    /// <summary>
    /// Stores atlas dimensions and glyph placement for a generated font.
    /// </summary>
    sealed class FontAtlasLayout {
        /// <summary>
        /// Initializes one atlas layout record.
        /// </summary>
        /// <param name="width">Atlas width in pixels.</param>
        /// <param name="height">Atlas height in pixels.</param>
        /// <param name="glyphs">Placed glyph records.</param>
        public FontAtlasLayout(int width, int height, FontGlyphLayout[] glyphs) {
            Width = width;
            Height = height;
            Glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs));
        }

        /// <summary>
        /// Gets the atlas width in pixels.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the atlas height in pixels.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the placed glyph records.
        /// </summary>
        public FontGlyphLayout[] Glyphs { get; }
    }
}
