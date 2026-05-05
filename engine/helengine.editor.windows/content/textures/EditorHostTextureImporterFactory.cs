namespace helengine.editor {
    /// <summary>
    /// Creates the texture importer registrations used by the Windows editor host.
    /// </summary>
    public static class EditorHostTextureImporterFactory {
        /// <summary>
        /// Creates the default texture importer registrations used at editor startup.
        /// </summary>
        /// <returns>Default texture importer registrations for the Windows editor host.</returns>
        public static IReadOnlyList<IAssetImporterRegistration> CreateDefault() {
            return [
                CreateTextureRegistration("gdi", "helengine.editor.windows.gdiimporter", "helengine.editor.GDITextureImporter", TextureImportFormatCatalog.GdiTextureExtensions),
                CreateTextureRegistration("pfim", "helengine.editor.windows.pfimimporter", "helengine.editor.PfimTextureImporter", TextureImportFormatCatalog.PfimTextureExtensions),
                CreateTextureRegistration("magick", "helengine.editor.windows.magickimporter", "helengine.editor.MagickTextureImporter", TextureImportFormatCatalog.MagickTextureExtensions)
            ];
        }

        /// <summary>
        /// Creates one lazy texture importer registration for the supplied importer metadata and extension set.
        /// </summary>
        /// <param name="importerId">Stable identifier used for sidecar settings and explicit importer selection.</param>
        /// <param name="assemblyName">Simple backend assembly name loaded on first use.</param>
        /// <param name="typeName">Fully qualified importer type name resolved from the backend assembly.</param>
        /// <param name="extensions">Extensions mapped to the importer registration.</param>
        /// <returns>Configured lazy texture importer registration.</returns>
        static TextureImporterRegistration CreateTextureRegistration(string importerId, string assemblyName, string typeName, IReadOnlyList<string> extensions) {
            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }

            if (string.IsNullOrWhiteSpace(assemblyName)) {
                throw new ArgumentException("Assembly name must be provided.", nameof(assemblyName));
            }

            if (string.IsNullOrWhiteSpace(typeName)) {
                throw new ArgumentException("Type name must be provided.", nameof(typeName));
            }

            if (extensions == null) {
                throw new ArgumentNullException(nameof(extensions));
            }

            string[] extensionArray = new string[extensions.Count];
            for (int index = 0; index < extensions.Count; index++) {
                extensionArray[index] = extensions[index];
            }

            return new TextureImporterRegistration(
                importerId,
                new LazyTextureImporter(new AssemblyTextureImporterFactory(assemblyName, typeName)),
                extensionArray);
        }
    }
}
