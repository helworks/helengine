namespace helengine {
    /// <summary>
    /// Represents a font atlas and glyph metrics used for rendering text.
    /// </summary>
    public class FontAsset : IDisposable {
        /// <summary>
        /// Initializes a new font asset with atlas texture, metrics, and glyph map.
        /// </summary>
        /// <param name="fontInfo">Basic font metrics.</param>
        /// <param name="tex">Atlas texture containing glyphs.</param>
        /// <param name="chars">Map of characters to glyph metrics.</param>
        /// <param name="lineHeight">Line height in pixels.</param>
        /// <param name="atlasWidth">Atlas width in pixels.</param>
        /// <param name="atlasHeight">Atlas height in pixels.</param>
        public FontAsset(FontInfo fontInfo, RuntimeTexture tex,
            Dictionary<char, FontChar> chars, float lineHeight, int atlasWidth, int atlasHeight) {
            LineHeight = lineHeight;
            FontInfo = fontInfo;
            Texture = tex;
            Characters = chars;
            AtlasWidth = atlasWidth;
            AtlasHeight = atlasHeight;
        }

        /// <summary>
        /// Gets the basic font metrics.
        /// </summary>
        public FontInfo FontInfo { get; protected set; }

        /// <summary>
        /// Gets the atlas texture containing glyphs.
        /// </summary>
        public RuntimeTexture Texture { get; protected set; }

        /// <summary>
        /// Gets the glyph map keyed by character.
        /// </summary>
        public Dictionary<char, FontChar> Characters { get; protected set; }

        /// <summary>
        /// Gets the line height in pixels.
        /// </summary>
        public float LineHeight { get; protected set; }

        /// <summary>
        /// Gets the atlas width in pixels.
        /// </summary>
        public int AtlasWidth { get; protected set; }

        /// <summary>
        /// Gets the atlas height in pixels.
        /// </summary>
        public int AtlasHeight { get; protected set; }

        /// <summary>
        /// Disposes of managed resources if needed.
        /// </summary>
        public void Dispose() {
        }

        /// <summary>
        /// Measures the width and height of a string using glyph advances.
        /// </summary>
        /// <param name="text">Text to measure.</param>
        /// <returns>Size in pixels.</returns>
        public float2 MeasureString(string text) {
            float x = 0f;
            float y = 0f;
            float maxX = 0f;
            float line = Math.Max(LineHeight, 1f);

            for (int i = 0; i < text.Length; i++) {
                char c = text[i];

                if (c == '\n') {
                    if (x > maxX) maxX = x;
                    y += line;
                    x = 0f;
                    continue;
                }

                if (c == ' ') {
                    x += FontInfo.SpaceWidth;
                    continue;
                }

                if (Characters.TryGetValue(c, out var ch)) {
                    float adv = ch.AdvanceWidth > 0 ? ch.AdvanceWidth : ch.SourceRect.Z;
                    x += adv;
                }
            }

            if (x > maxX) maxX = x;
            return new float2(maxX, y + line);
        }

        /// <summary>
        /// Measures a single line of text using tight glyph bounds.
        /// </summary>
        /// <param name="text">Text to measure.</param>
        /// <returns>Metrics containing width and vertical extents.</returns>
        public FontTightMetrics MeasureTight(string text) {
            float width = 0f;
            float minTop = float.MaxValue;
            float maxBottom = float.MinValue;

            for (int i = 0; i < text.Length; i++) {
                char c = text[i];
                if (c == ' ') {
                    width += FontInfo.SpaceWidth;
                    continue;
                }
                if (!Characters.TryGetValue(c, out var ch)) continue;

                float advance = ch.AdvanceWidth > 0 ? ch.AdvanceWidth : (ch.SourceRect.Z * AtlasWidth);
                width += advance;

                float glyphTop = ch.OffsetY;
                float glyphBottom = ch.OffsetY + (ch.SourceRect.W * AtlasHeight);
                if (glyphTop < minTop) minTop = glyphTop;
                if (glyphBottom > maxBottom) maxBottom = glyphBottom;
            }

            if (minTop == float.MaxValue) {
                minTop = 0f;
                maxBottom = LineHeight > 0 ? LineHeight : 1f;
            }

            return new FontTightMetrics(width, minTop, maxBottom);
        }
    }
}
