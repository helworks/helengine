namespace helengine.demo_disc_scene_writer {
    /// <summary>
    /// Stores atlas placement for one rendered glyph.
    /// </summary>
    sealed class FontGlyphLayout {
        /// <summary>
        /// Initializes one glyph layout record.
        /// </summary>
        /// <param name="character">Character rendered into the atlas.</param>
        /// <param name="x">Glyph X position in pixels.</param>
        /// <param name="y">Glyph Y position in pixels.</param>
        /// <param name="width">Glyph width in pixels.</param>
        /// <param name="height">Glyph height in pixels.</param>
        public FontGlyphLayout(char character, int x, int y, int width, int height) {
            Character = character;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Gets the character rendered into the atlas.
        /// </summary>
        public char Character { get; }

        /// <summary>
        /// Gets the glyph X position in pixels.
        /// </summary>
        public int X { get; }

        /// <summary>
        /// Gets the glyph Y position in pixels.
        /// </summary>
        public int Y { get; }

        /// <summary>
        /// Gets the glyph width in pixels.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the glyph height in pixels.
        /// </summary>
        public int Height { get; }
    }
}
