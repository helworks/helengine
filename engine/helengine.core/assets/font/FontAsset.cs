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
            float lastX = 0;
            float Y = LineHeight;
            int letterSpacing = 2;
            float posY = 0;
            float lineSpacing = LineHeight;

            float MaxX = 0;
            float MaxY = LineHeight;

            for (int i = 0; i < text.Length; i++) {
                char c = text[i];

                if (Characters.ContainsKey(c)) {
                    var rect = Characters[c];
                    lastX += rect.SourceRect.Z + letterSpacing;
                    if (lastX > MaxX) {
                        MaxX = lastX;
                    }

                    if (posY > MaxY) {
                        MaxY = posY;
                    }
                } else {
                    // special cases
                    if (c == ' ') {
                        // space
                        lastX += FontInfo.SpaceWidth;
                    } else if (c == '\n') {
                        posY += lineSpacing;
                        lastX = 0;
                    }
                }
            }

            return new float2(MaxX, posY + lineSpacing);
        }
    }
}

