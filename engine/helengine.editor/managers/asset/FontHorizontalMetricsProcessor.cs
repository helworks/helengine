namespace helengine.editor {
    /// <summary>Applies an optional integer-pixel advance policy to imported fonts without changing glyph images, atlas layout or vertical metrics.</summary>
    public static class FontHorizontalMetricsProcessor {
        /// <summary>Rounds horizontal advances away from midpoint zero only when explicitly requested by the platform font settings.</summary>
        /// <param name="font">Imported font whose existing raster and atlas remain untouched.</param>
        /// <param name="settings">Explicit platform font policy; the default retains all legacy metrics.</param>
        public static void Apply(FontAsset font, FontAssetProcessorSettings settings) {
            if (font == null) { throw new ArgumentNullException(nameof(font)); }
            if (settings == null) { throw new ArgumentNullException(nameof(settings)); }
            if (!settings.SnapHorizontalMetricsToPixels) { return; }
            if (font.FontInfo == null || font.Characters == null) { throw new InvalidOperationException("Font horizontal processing requires source font metrics and glyphs."); }
            font.FontInfo.SpaceWidth = (float)Math.Round(font.FontInfo.SpaceWidth, MidpointRounding.AwayFromZero);
            foreach (char character in font.Characters.Keys.ToArray()) {
                FontChar glyph = font.Characters[character];
                glyph.AdvanceWidth = (float)Math.Round(glyph.AdvanceWidth, MidpointRounding.AwayFromZero);
                font.Characters[character] = glyph;
            }
        }
    }
}
