using helengine.editor;

namespace helengine.editor.windows.gdiimporter.tests.content.textures {
    /// <summary>
    /// Verifies GDI-backed texture imports from real encoded PNG files.
    /// </summary>
    public sealed class GDITextureImporterTests {
        /// <summary>
        /// Ensures a real PNG file is imported into engine RGBA byte order.
        /// </summary>
        [Fact]
        public void ImportTexture_WhenStreamContainsPng_ReturnsTextureAssetWithRgbaBytes() {
            GDITextureImporter importer = new GDITextureImporter();
            using MemoryStream stream = new MemoryStream(GdiTextureImporterFixtureData.CreateSinglePixelPngFile());
            TextureAsset textureAsset = importer.ImportTexture(stream);

            Assert.Equal((ushort)1, textureAsset.Width);
            Assert.Equal((ushort)1, textureAsset.Height);
            Assert.Equal(new byte[] { 9, 8, 7, 6 }, textureAsset.Colors);
        }
    }
}
