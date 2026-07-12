namespace helengine.editor {
    /// <summary>
    /// Creates the audio importer registrations used by the Windows editor host.
    /// </summary>
    public static class EditorHostAudioImporterFactory {
        /// <summary>
        /// Creates the default audio importer registrations used at editor startup.
        /// </summary>
        /// <returns>Default audio importer registrations for the Windows editor host.</returns>
        public static IReadOnlyList<IAssetImporterRegistration> CreateDefault() {
            return [
                CreateAudioRegistration(
                    "naudio-source",
                    "helengine.editor.windows.audioimporter",
                    "helengine.editor.NAudioSourceAudioImporter",
                    AudioImportFormatCatalog.SourceExtensions)
            ];
        }

        /// <summary>
        /// Creates one lazy audio importer registration for the supplied importer metadata and extension set.
        /// </summary>
        /// <param name="importerId">Stable identifier used for sidecar settings and explicit importer selection.</param>
        /// <param name="assemblyName">Simple backend assembly name loaded on first use.</param>
        /// <param name="typeName">Fully qualified importer type name resolved from the backend assembly.</param>
        /// <param name="extensions">Extensions mapped to the importer registration.</param>
        /// <returns>Configured lazy audio importer registration.</returns>
        static AudioImporterRegistration CreateAudioRegistration(string importerId, string assemblyName, string typeName, IReadOnlyList<string> extensions) {
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

            return new AudioImporterRegistration(
                importerId,
                new LazyAudioImporter(new AssemblyAudioImporterFactory(assemblyName, typeName)),
                extensionArray);
        }
    }
}
