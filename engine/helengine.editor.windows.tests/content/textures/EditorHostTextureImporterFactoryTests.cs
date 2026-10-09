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
            TextureImporterRegistration timRegistration = Assert.IsType<TextureImporterRegistration>(registrations[3]);
            Assert.Equal("ps1-tim", timRegistration.ImporterId);
            Assert.Equal(new[] { ".tim" }, timRegistration.Extensions);
            Assert.IsType<PlayStationTimTextureImporter>(timRegistration.Importer);
            TextureImporterRegistration pvrRegistration = Assert.IsType<TextureImporterRegistration>(registrations[4]);
            Assert.Equal("dc-pvr", pvrRegistration.ImporterId);
            Assert.Equal(new[] { ".pvr" }, pvrRegistration.Extensions);
            Assert.IsType<DreamcastPvrTextureImporter>(pvrRegistration.Importer);
            TextureImporterRegistration gtfRegistration = Assert.IsType<TextureImporterRegistration>(registrations[5]);
            Assert.Equal("ps3-gtf", gtfRegistration.ImporterId);
            Assert.Equal(new[] { ".gtf" }, gtfRegistration.Extensions);
            Assert.IsType<PlayStation3GtfTextureImporter>(gtfRegistration.Importer);
            Assert.Contains(".gtf", TextureImportFormatCatalog.AllTextureExtensions);
            TextureImporterRegistration gimRegistration = Assert.IsType<TextureImporterRegistration>(registrations[6]);
            Assert.Equal("psp-gim", gimRegistration.ImporterId);
            Assert.Equal(new[] { ".gim" }, gimRegistration.Extensions);
            Assert.IsType<PspGimTextureImporter>(gimRegistration.Importer);
            Assert.Contains(".gim", TextureImportFormatCatalog.AllTextureExtensions);
            TextureImporterRegistration t3xRegistration = Assert.IsType<TextureImporterRegistration>(registrations[7]);
            Assert.Equal("3ds-t3x", t3xRegistration.ImporterId);
            Assert.Equal(new[] { ".t3x" }, t3xRegistration.Extensions);
            Assert.IsType<Nintendo3DsT3xTextureImporter>(t3xRegistration.Importer);
            Assert.Contains(".t3x", TextureImportFormatCatalog.AllTextureExtensions);
            TextureImporterRegistration nsbtxRegistration = Assert.IsType<TextureImporterRegistration>(registrations[8]);
            Assert.Equal("ds-nsbtx", nsbtxRegistration.ImporterId);
            Assert.Equal(new[] { ".nsbtx" }, nsbtxRegistration.Extensions);
            Assert.IsType<NintendoDsNsbtxTextureImporter>(nsbtxRegistration.Importer);
            Assert.Contains(".nsbtx", TextureImportFormatCatalog.AllTextureExtensions);
            TextureImporterRegistration bntxRegistration = Assert.IsType<TextureImporterRegistration>(registrations[9]);
            Assert.Equal("switch-bntx", bntxRegistration.ImporterId);
            Assert.Equal(new[] { ".bntx" }, bntxRegistration.Extensions);
            Assert.IsType<SwitchBntxTextureImporter>(bntxRegistration.Importer);
            Assert.Contains(".bntx", TextureImportFormatCatalog.AllTextureExtensions);
        }
    }
}
