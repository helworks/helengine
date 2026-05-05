using helengine.editor;

namespace helengine.editor.windows.magickimporter.tests.content.textures {
    /// <summary>
    /// Verifies Magick-backed texture imports from real encoded PNG and PSD files.
    /// </summary>
    public sealed class MagickTextureImporterTests : IDisposable {
        /// <summary>
        /// Temporary workspace used for fixture files.
        /// </summary>
        readonly string WorkspacePath;

        /// <summary>
        /// Initializes one isolated fixture workspace for importer tests.
        /// </summary>
        public MagickTextureImporterTests() {
            WorkspacePath = Path.Combine(Path.GetTempPath(), "helengine-magick-importer-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(WorkspacePath);
        }

        /// <summary>
        /// Deletes the temporary fixture workspace after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(WorkspacePath)) {
                Directory.Delete(WorkspacePath, true);
            }
        }

        /// <summary>
        /// Ensures a real PNG file is imported into engine RGBA byte order.
        /// </summary>
        [Fact]
        public void ImportTexture_WhenStreamContainsPng_ReturnsTextureAssetWithRgbaBytes() {
            MagickTextureImporter importer = new MagickTextureImporter();
            string sourcePath = Path.Combine(WorkspacePath, "pixel.png");
            File.WriteAllBytes(sourcePath, MagickTextureImporterFixtureData.CreateSinglePixelPngFile());

            using FileStream stream = File.OpenRead(sourcePath);
            TextureAsset textureAsset = importer.ImportTexture(stream);

            Assert.Equal((ushort)1, textureAsset.Width);
            Assert.Equal((ushort)1, textureAsset.Height);
            Assert.Equal(new byte[] { 9, 8, 7, 6 }, textureAsset.Colors);
        }

        /// <summary>
        /// Ensures a real PSD file is imported into engine RGBA byte order.
        /// </summary>
        [Fact]
        public void ImportTexture_WhenStreamContainsPsd_ReturnsTextureAssetWithRgbaBytes() {
            MagickTextureImporter importer = new MagickTextureImporter();
            string sourcePath = Path.Combine(WorkspacePath, "pixel.psd");
            File.WriteAllBytes(sourcePath, MagickTextureImporterFixtureData.CreateSinglePixelPsdFile());

            using FileStream stream = File.OpenRead(sourcePath);
            TextureAsset textureAsset = importer.ImportTexture(stream);

            Assert.Equal((ushort)1, textureAsset.Width);
            Assert.Equal((ushort)1, textureAsset.Height);
            Assert.Equal(new byte[] { 9, 8, 7, 255 }, textureAsset.Colors);
        }
    }
}
