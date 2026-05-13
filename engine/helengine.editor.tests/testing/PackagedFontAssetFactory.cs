namespace helengine.editor.tests.testing {
    /// <summary>
    /// Creates packaged font assets with source atlas data suitable for scene-packaging tests.
    /// </summary>
    public static class PackagedFontAssetFactory {
        /// <summary>
        /// Creates one packaged font asset backed by a small exported atlas texture.
        /// </summary>
        /// <returns>Packaged font asset that can be written by the scene packager.</returns>
        public static FontAsset Create() {
            byte[] colors = new byte[64];
            for (int index = 0; index < colors.Length; index += 4) {
                byte pixelIndex = (byte)(index / 4);
                colors[index] = (byte)(pixelIndex * 16);
                colors[index + 1] = (byte)(255 - (pixelIndex * 16));
                colors[index + 2] = (byte)(pixelIndex * 8);
                colors[index + 3] = 255;
            }

            TextureAsset textureAsset = new TextureAsset {
                Width = 4,
                Height = 4,
                Colors = colors
            };

            FontAsset fontAsset = new FontAsset(
                new FontInfo("PackagedTest", 16, 4f),
                new TestRuntimeTexture {
                    Width = textureAsset.Width,
                    Height = textureAsset.Height
                },
                new Dictionary<char, FontChar> {
                    ['A'] = new FontChar(new float4(0f, 0f, 1f, 1f), 0f, 1f, 0f, 0f)
                },
                16f,
                textureAsset.Width,
                textureAsset.Height);

            fontAsset.SourceTextureAsset = textureAsset;
            return fontAsset;
        }
    }
}
