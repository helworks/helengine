using helengine.editor;

namespace helengine.editor.windows.tests.content.textures {
    /// <summary>
    /// Verifies the editor host texture importer registrations used at startup.
    /// </summary>
    public sealed class EditorHostTextureImporterFactoryTests {
        /// <summary>
        /// Ensures the default texture importer list includes the layered lazy texture importers used by the editor host.
        /// </summary>
        [Fact]
        public void CreateDefault_WhenCalled_IncludesLazyTextureImportersForBroadCoverage() {
            IReadOnlyList<IAssetImporterRegistration> registrations = EditorHostTextureImporterFactory.CreateDefault();

            TextureImporterRegistration gdiRegistration = Assert.IsType<TextureImporterRegistration>(registrations[0]);
            TextureImporterRegistration pfimRegistration = Assert.IsType<TextureImporterRegistration>(registrations[1]);
            TextureImporterRegistration magickRegistration = Assert.IsType<TextureImporterRegistration>(registrations[2]);

            Assert.Equal("gdi", gdiRegistration.ImporterId);
            Assert.Equal(
                new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif" },
                gdiRegistration.Extensions,
                StringComparer.OrdinalIgnoreCase);
            Assert.IsType<LazyTextureImporter>(gdiRegistration.Importer);

            Assert.Equal("pfim", pfimRegistration.ImporterId);
            Assert.Equal(
                new[] { ".dds", ".tga", ".targa" },
                pfimRegistration.Extensions,
                StringComparer.OrdinalIgnoreCase);
            Assert.IsType<LazyTextureImporter>(pfimRegistration.Importer);

            Assert.Equal("magick", magickRegistration.ImporterId);
            Assert.Contains(".webp", magickRegistration.Extensions, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(".psd", magickRegistration.Extensions, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(".avif", magickRegistration.Extensions, StringComparer.OrdinalIgnoreCase);
            Assert.IsType<LazyTextureImporter>(magickRegistration.Importer);
        }
    }
}
