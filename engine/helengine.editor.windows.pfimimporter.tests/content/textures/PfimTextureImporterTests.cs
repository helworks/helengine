using helengine.editor;

namespace helengine.editor.windows.pfimimporter.tests.content.textures {
    /// <summary>
    /// Verifies Pfim-backed texture imports from real encoded TGA and DDS files.
    /// </summary>
    public sealed class PfimTextureImporterTests : IDisposable {
        /// <summary>
        /// Temporary workspace used for fixture files.
        /// </summary>
        readonly string WorkspacePath;

        /// <summary>
        /// Initializes one isolated fixture workspace for importer tests.
        /// </summary>
        public PfimTextureImporterTests() {
            WorkspacePath = Path.Combine(Path.GetTempPath(), "helengine-pfim-importer-tests", Guid.NewGuid().ToString("N"));
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
        /// Ensures a real 32-bit TGA file is imported into engine RGBA byte order.
        /// </summary>
        [Fact]
        public void ImportTexture_WhenStreamContainsTga32_ReturnsRgbaBytes() {
            PfimTextureImporter importer = new PfimTextureImporter();
            string sourcePath = Path.Combine(WorkspacePath, "pixel32.tga");
            File.WriteAllBytes(sourcePath, PfimTextureImporterFixtureData.CreateSinglePixelTga32File());

            using FileStream stream = File.OpenRead(sourcePath);
            TextureAsset textureAsset = importer.ImportTexture(stream);

            Assert.Equal((ushort)1, textureAsset.Width);
            Assert.Equal((ushort)1, textureAsset.Height);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, textureAsset.Colors);
        }

        /// <summary>
        /// Ensures a real 24-bit TGA file is imported into engine RGBA byte order with an opaque alpha channel.
        /// </summary>
        [Fact]
        public void ImportTexture_WhenStreamContainsTga24_ReturnsRgbaBytesWithOpaqueAlpha() {
            PfimTextureImporter importer = new PfimTextureImporter();
            string sourcePath = Path.Combine(WorkspacePath, "pixel24.tga");
            File.WriteAllBytes(sourcePath, PfimTextureImporterFixtureData.CreateSinglePixelTga24File());

            using FileStream stream = File.OpenRead(sourcePath);
            TextureAsset textureAsset = importer.ImportTexture(stream);

            Assert.Equal((ushort)1, textureAsset.Width);
            Assert.Equal((ushort)1, textureAsset.Height);
            Assert.Equal(new byte[] { 7, 6, 5, 255 }, textureAsset.Colors);
        }

        /// <summary>
        /// Ensures a real DDS file is imported into engine RGBA byte order.
        /// </summary>
        [Fact]
        public void ImportTexture_WhenStreamContainsDds_ReturnsRgbaBytes() {
            PfimTextureImporter importer = new PfimTextureImporter();
            string sourcePath = Path.Combine(WorkspacePath, "pixel.dds");
            File.WriteAllBytes(sourcePath, PfimTextureImporterFixtureData.CreateSinglePixelDdsFile());

            using FileStream stream = File.OpenRead(sourcePath);
            TextureAsset textureAsset = importer.ImportTexture(stream);

            Assert.Equal((ushort)1, textureAsset.Width);
            Assert.Equal((ushort)1, textureAsset.Height);
            Assert.Equal(new byte[] { 7, 8, 9, 255 }, textureAsset.Colors);
        }
    }
}
