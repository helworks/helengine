namespace helengine {
    /// <summary>Captures exact mutable metrics for one input character, including characters absent from the font.</summary>
    internal struct TextCommandReuseMetric {
        /// <summary>Borrowed font whose current dictionary is read during validation.</summary>
        public FontAsset Font;
        /// <summary>Input character examined before wrapping, so omitted and missing glyphs remain observable.</summary>
        public char Character;
        /// <summary>Whether the character existed in the font when captured.</summary>
        public bool Exists;
        /// <summary>Complete glyph metrics captured when the character existed.</summary>
        public FontChar Glyph;
    }
}
