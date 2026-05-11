namespace helengine {
    /// <summary>
    /// Represents glyph metrics and UV data for a single character.
    /// </summary>
    public struct FontChar {
        /// <summary>
        /// UV coordinates in the texture atlas (x, y, width, height).
        /// </summary>
        public float4 SourceRect;  // UV coordinates in texture atlas (x, y, width, height)

        /// <summary>
        /// Vertical offset from the baseline.
        /// </summary>
        public float OffsetY;      // Vertical offset from baseline

        /// <summary>
        /// Horizontal advance to the next character.
        /// </summary>
        public float AdvanceWidth; // Horizontal advance to next character

        /// <summary>
        /// Horizontal bearing (left side bearing).
        /// </summary>
        public float BearingX;     // Horizontal bearing (left side bearing)

        /// <summary>
        /// Vertical bearing (top side bearing).
        /// </summary>
        public float BearingY;     // Vertical bearing (top side bearing)

        /// <summary>
        /// Initializes a glyph with full metric details.
        /// </summary>
        /// <param name="sourceRect">UV source rectangle.</param>
        /// <param name="offsetY">Vertical offset from baseline.</param>
        /// <param name="advanceWidth">Advance width in pixels.</param>
        /// <param name="bearingX">Horizontal bearing.</param>
        /// <param name="bearingY">Vertical bearing.</param>
        public FontChar(float4 sourceRect, float offsetY, float advanceWidth, float bearingX, float bearingY) {
            SourceRect = sourceRect;
            OffsetY = offsetY;
            AdvanceWidth = advanceWidth;
            BearingX = bearingX;
            BearingY = bearingY;
        }

    }
}
