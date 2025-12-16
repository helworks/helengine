namespace helengine {
    /// <summary>
    /// Holds tight glyph bounds measurements for a single line of text.
    /// </summary>
    public readonly struct FontTightMetrics {
        /// <summary>
        /// Gets the total advance width in pixels.
        /// </summary>
        public readonly float Width;      // Sum of advances (pixels)

        /// <summary>
        /// Gets the minimum glyph top relative to the line top.
        /// </summary>
        public readonly float MinTop;     // Min glyph top (relative to line top)

        /// <summary>
        /// Gets the maximum glyph bottom relative to the line top.
        /// </summary>
        public readonly float MaxBottom;  // Max glyph bottom (relative to line top)

        /// <summary>
        /// Gets the total height derived from MinTop and MaxBottom.
        /// </summary>
        public float Height => Math.Max(1f, MaxBottom - MinTop);

        /// <summary>
        /// Initializes a new instance with width and vertical bounds.
        /// </summary>
        /// <param name="width">Total advance width.</param>
        /// <param name="minTop">Minimum glyph top.</param>
        /// <param name="maxBottom">Maximum glyph bottom.</param>
        public FontTightMetrics(float width, float minTop, float maxBottom) {
            Width = width;
            MinTop = minTop;
            MaxBottom = maxBottom;
        }
    }
}
