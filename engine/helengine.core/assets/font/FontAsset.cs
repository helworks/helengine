namespace helengine {
    /// <summary>
    /// Represents a font atlas and glyph metrics used for rendering text.
    /// </summary>
    public class FontAsset : IDisposable {
        /// <summary>
        /// Tracks how many font asset instances are currently alive in native runtime builds.
        /// </summary>
        static int LiveInstanceCountValue;

        /// <summary>
        /// Tracks how many font asset instances have been constructed in the current process.
        /// </summary>
        static int ConstructedInstanceCountValue;

        /// <summary>
        /// Tracks how many font asset instances have been disposed in the current process.
        /// </summary>
        static int DisposedInstanceCountValue;

        /// <summary>
        /// Tracks how many glyph dictionary entries are currently owned by live font assets.
        /// </summary>
        static int LiveCharacterCountValue;

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
            ConstructedInstanceCountValue++;
            LiveInstanceCountValue++;
            LiveCharacterCountValue += chars == null ? 0 : chars.Count;
        }

        /// <summary>
        /// Gets how many font asset instances are currently alive in native runtime builds.
        /// </summary>
        public static int LiveInstanceCount => LiveInstanceCountValue;

        /// <summary>
        /// Gets how many font asset instances have been constructed in the current process.
        /// </summary>
        public static int ConstructedInstanceCount => ConstructedInstanceCountValue;

        /// <summary>
        /// Gets how many font asset instances have been disposed in the current process.
        /// </summary>
        public static int DisposedInstanceCount => DisposedInstanceCountValue;

        /// <summary>
        /// Gets how many glyph dictionary entries are currently owned by live font assets.
        /// </summary>
        public static int LiveCharacterCount => LiveCharacterCountValue;

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
        /// Gets or sets the raw atlas texture data used to build this font asset.
        /// </summary>
        public TextureAsset SourceTextureAsset { get; set; }

        /// <summary>
        /// Gets or sets the runtime-relative cooked atlas texture path when one platform owns the final atlas texture payload.
        /// </summary>
        public string CookedAtlasTextureRelativePath { get; set; }

        /// <summary>
        /// Gets whether this font asset has already released its scene-owned references.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Replaces the source atlas payload after import-time texture processing and rescales font metrics to match the processed atlas size.
        /// </summary>
        /// <param name="processedSourceTextureAsset">Processed source atlas that should become the authoritative texture payload.</param>
        public void ApplyProcessedSourceTextureAsset(TextureAsset processedSourceTextureAsset) {
            if (processedSourceTextureAsset == null) {
                throw new ArgumentNullException(nameof(processedSourceTextureAsset));
            } else if (processedSourceTextureAsset.Width < 1 || processedSourceTextureAsset.Height < 1) {
                throw new InvalidOperationException("Processed font atlas textures must have positive dimensions.");
            }

            int originalAtlasWidth = Math.Max(AtlasWidth, 1);
            int originalAtlasHeight = Math.Max(AtlasHeight, 1);
            double scaleX = (double)processedSourceTextureAsset.Width / originalAtlasWidth;
            double scaleY = (double)processedSourceTextureAsset.Height / originalAtlasHeight;
            bool atlasWasResized = originalAtlasWidth != processedSourceTextureAsset.Width || originalAtlasHeight != processedSourceTextureAsset.Height;

            if (atlasWasResized) {
                AtlasWidth = processedSourceTextureAsset.Width;
                AtlasHeight = processedSourceTextureAsset.Height;
                LineHeight = (float)(LineHeight * scaleY);
                if (FontInfo != null) {
                    FontInfo.LineSpacing = Math.Max(1, (int)Math.Round(FontInfo.LineSpacing * scaleY));
                    FontInfo.SpaceWidth = (float)(FontInfo.SpaceWidth * scaleX);
                }

                if (Characters != null) {
                    List<char> keys = new List<char>();
                    foreach (char key in Characters.Keys) {
                        keys.Add(key);
                    }

                    for (int keyIndex = 0; keyIndex < keys.Count; keyIndex++) {
                        char key = keys[keyIndex];
                        FontChar glyph = Characters[key];
                        glyph.OffsetY = (float)(glyph.OffsetY * scaleY);
                        glyph.AdvanceWidth = (float)(glyph.AdvanceWidth * scaleX);
                        glyph.BearingX = (float)(glyph.BearingX * scaleX);
                        glyph.BearingY = (float)(glyph.BearingY * scaleY);
                        Characters[key] = glyph;
                    }
                }
            } else {
                AtlasWidth = processedSourceTextureAsset.Width;
                AtlasHeight = processedSourceTextureAsset.Height;
            }

            SourceTextureAsset = processedSourceTextureAsset;
            if (Texture != null) {
                Texture.Width = processedSourceTextureAsset.Width;
                Texture.Height = processedSourceTextureAsset.Height;
            }
        }

        /// <summary>
        /// Attaches one runtime atlas texture and its cooked source payload to this font asset, then updates metrics to match the cooked atlas dimensions.
        /// </summary>
        /// <param name="runtimeTexture">Runtime texture rebuilt from the cooked atlas payload.</param>
        /// <param name="processedSourceTextureAsset">Cooked atlas payload that should become the authoritative source texture data.</param>
        public void AttachProcessedTexture(RuntimeTexture runtimeTexture, TextureAsset processedSourceTextureAsset) {
            if (runtimeTexture == null) {
                throw new ArgumentNullException(nameof(runtimeTexture));
            } else if (processedSourceTextureAsset == null) {
                throw new ArgumentNullException(nameof(processedSourceTextureAsset));
            }

            Texture = runtimeTexture;
            ApplyProcessedSourceTextureAsset(processedSourceTextureAsset);
        }

        /// <summary>
        /// Releases scene-owned references held by this font asset.
        /// </summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            Dictionary<char, FontChar> characters = Characters;
            FontInfo fontInfo = FontInfo;
            TextureAsset sourceTextureAsset = SourceTextureAsset;
            byte[] sourceTextureColors = sourceTextureAsset == null ? null : sourceTextureAsset.Colors;
            byte[] sourceTexturePaletteColors = sourceTextureAsset == null ? null : sourceTextureAsset.PaletteColors;
            bool sourceTextureColorsUsesSharedEmptyArray = ReferenceEquals(sourceTextureColors, Array.Empty<byte>());
            bool sourceTexturePaletteColorsUsesSharedEmptyArray = ReferenceEquals(sourceTexturePaletteColors, Array.Empty<byte>());
            LiveInstanceCountValue--;
            DisposedInstanceCountValue++;
            LiveCharacterCountValue -= characters == null ? 0 : characters.Count;
            Texture = null;
            Characters = null;
            FontInfo = null;
            SourceTextureAsset = null;
            if (sourceTextureAsset != null) {
                sourceTextureAsset.Colors = null;
                sourceTextureAsset.PaletteColors = null;
            }
            if (characters != null) {
                characters.Clear();
            }
            NativeOwnership.Delete(characters);
            NativeOwnership.Delete(fontInfo);
            if (!sourceTextureColorsUsesSharedEmptyArray) {
                NativeOwnership.Delete(sourceTextureColors);
            }
            if (!sourceTexturePaletteColorsUsesSharedEmptyArray) {
                NativeOwnership.Delete(sourceTexturePaletteColors);
            }
            NativeOwnership.Delete(sourceTextureAsset);
            IsDisposed = true;
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
