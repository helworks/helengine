namespace helengine {
    public class FontAsset : IDisposable {
        public FontInfo FontInfo { get; protected set; }

        public RuntimeTexture Texture { get; protected set; }

        public Dictionary<char, FontChar> Characters { get; protected set; }

        public float LineHeight { get; protected set; }

        public FontAsset(FontInfo fontInfo, RuntimeTexture tex,
            Dictionary<char, FontChar> chars, float lineHeight) {
            this.LineHeight = lineHeight;
            this.FontInfo = fontInfo;
            this.Texture = tex;
            this.Characters = chars;
        }

        public void Dispose() {
        }

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
                    float adv = ch.AdvanceWidth > 0 ? ch.AdvanceWidth : (ch.SourceRect.Z);
                    x += adv;
                }
            }

            if (x > maxX) maxX = x;
            return new float2(maxX, y + line);
        }
    }
}

