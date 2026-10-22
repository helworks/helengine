namespace helengine.editor {
    /// <summary>Registers source-compatible CPU importers without depending on a Windows editor assembly.</summary>
    public static class PortableImporterFactory {
        /// <summary>Preserves authored importer identifiers while choosing the portable raster implementation explicitly.</summary>
        /// <returns>Texture, font, text and model importer registrations.</returns>
        public static IReadOnlyList<IAssetImporterRegistration> CreateDefault() {
            return [
                // These identifiers are persisted in source sidecars; changing the host implementation must not change asset identity.
                new TextureImporterRegistration("gdi", new MagickTextureImporter(), TextureImportFormatCatalog.GdiTextureExtensions.ToArray()),
                new TextureImporterRegistration("pfim", new PfimTextureImporter(), TextureImportFormatCatalog.PfimTextureExtensions.ToArray()),
                new TextureImporterRegistration("magick", new MagickTextureImporter(), TextureImportFormatCatalog.MagickTextureExtensions.ToArray()),
                new TextureImporterRegistration("ps1-tim", new PlayStationTimTextureImporter(), TextureImportFormatCatalog.PlayStationTextureExtensions.ToArray()),
                new TextureImporterRegistration("dc-pvr", new DreamcastPvrTextureImporter(), TextureImportFormatCatalog.DreamcastTextureExtensions.ToArray()),
                new TextureImporterRegistration("ps3-gtf", new PlayStation3GtfTextureImporter(), TextureImportFormatCatalog.PlayStation3TextureExtensions.ToArray()),
                new TextureImporterRegistration("psp-gim", new PspGimTextureImporter(), TextureImportFormatCatalog.PspTextureExtensions.ToArray()),
                new TextureImporterRegistration("3ds-t3x", new Nintendo3DsT3xTextureImporter(), TextureImportFormatCatalog.Nintendo3DsTextureExtensions.ToArray()),
                new TextureImporterRegistration("ds-nsbtx", new NintendoDsNsbtxTextureImporter(), TextureImportFormatCatalog.NintendoDsTextureExtensions.ToArray()),
                new TextureImporterRegistration("switch-bntx", new SwitchBntxTextureImporter(), TextureImportFormatCatalog.SwitchTextureExtensions.ToArray()),
                new FontImporterRegistration("gdi-font", new PortableFontImporter(), [".ttf", ".otf"]),
                new TextImporterRegistration("text", new TextImporter(), [".txt"]),
                new ModelImporterRegistration("assimp", new LazyModelImporter(new AssemblyModelImporterFactory(
                    "helengine.editor.assimp", "helengine.editor.assimp.HelengineAssimpImporter")), [".fbx", ".obj", ".gltf", ".glb", ".dae", ".3ds", ".x"])
            ];
        }
    }
}
