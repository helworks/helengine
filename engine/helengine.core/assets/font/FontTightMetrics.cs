namespace helengine {
    public readonly struct FontTightMetrics {
        public readonly float Width;      // Sum of advances (pixels)
        public readonly float MinTop;     // Min glyph top (relative to line top)
        public readonly float MaxBottom;  // Max glyph bottom (relative to line top)

        public float Height => Math.Max(1f, MaxBottom - MinTop);

        public FontTightMetrics(float width, float minTop, float maxBottom) {
            Width = width;
            MinTop = minTop;
            MaxBottom = maxBottom;
        }
    }
}

