namespace helengine {
    public class FontAsset : IDisposable {
        public FontInfo FontInfo { get; protected set; }

        public RuntimeTexture Texture { get; protected set; }

        public Dictionary<char, FontChar> Characters { get; protected set; }

        public float LineHeight { get; protected set; }

        public FontAsset(FontInfo fontInfo, RuntimeTexture tex,
            Dictionary<char, FontChar> chars, float lineHeight) {
            this.FontInfo = fontInfo;
            this.LineHeight = fontInfo.LineHeight > 0 ? fontInfo.LineHeight : lineHeight; // Use FontInfo's LineHeight if available
            this.Texture = tex;
            this.Characters = chars;
        }

        public void Dispose() {
        }

        public float2 MeasureString(string text) {
            if (string.IsNullOrEmpty(text)) {
                return new float2(0, FontInfo.LineHeight);
            }

            float currentX = 0;
            float currentY = 0;
            float maxX = 0;
            float totalHeight = FontInfo.LineHeight;

            for (int i = 0; i < text.Length; i++) {
                char c = text[i];

                if (c == '\n') {
                    // New line
                    currentY += FontInfo.LineHeight;
                    totalHeight = currentY + FontInfo.LineHeight;
                    maxX = Math.Max(maxX, currentX);
                    currentX = 0;
                } else if (c == ' ') {
                    // Space character
                    currentX += FontInfo.SpaceWidth;
                } else if (Characters.ContainsKey(c)) {
                    // Regular character - use advance width for proper spacing
                    var fontChar = Characters[c];
                    currentX += fontChar.AdvanceWidth;
                } else {
                    // Unknown character - use average character width
                    currentX += FontInfo.SpaceWidth;
                }
            }

            maxX = Math.Max(maxX, currentX);
            return new float2(maxX, totalHeight);
        }
    }
}

