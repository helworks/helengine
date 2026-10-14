namespace helengine.editor.tests.testing {
    /// <summary>
    /// Test texture importer that, the first time it imports, also edits a non-derived field of another texture's
    /// import settings, standing in for a real settings edit (here a processor resolution change) made while a cook
    /// is packaging. It otherwise imports like <see cref="TestTextureImporter"/>.
    /// </summary>
    internal sealed class SidecarEditingTextureImporter : ITextureImporter {
        /// <summary>
        /// Importer that produces the texture.
        /// </summary>
        readonly TestTextureImporter InnerImporter = new TestTextureImporter();

        /// <summary>
        /// Import-settings sidecar edited during the first import.
        /// </summary>
        readonly string EditedSidecarPath;

        /// <summary>
        /// Whether the sidecar was already edited.
        /// </summary>
        bool HasEdited;

        /// <summary>
        /// Creates an importer that edits one sidecar during its first import.
        /// </summary>
        /// <param name="editedSidecarPath">Absolute path of the texture import-settings sidecar to edit.</param>
        public SidecarEditingTextureImporter(string editedSidecarPath) {
            EditedSidecarPath = editedSidecarPath ?? throw new ArgumentNullException(nameof(editedSidecarPath));
        }

        /// <summary>
        /// Changes the sidecar's windows maximum resolution to a different value on the first call, then imports the fixed test texture.
        /// </summary>
        /// <param name="stream">Stream containing source texture data.</param>
        /// <returns>Texture asset with deterministic pixel data.</returns>
        public TextureAsset ImportTexture(Stream stream) {
            if (!HasEdited) {
                HasEdited = true;
                TextureAssetImportSettings settings;
                using (MemoryStream input = new MemoryStream(File.ReadAllBytes(EditedSidecarPath))) {
                    settings = TextureAssetImportSettingsBinarySerializer.Deserialize(input);
                }

                int currentMaxResolution = settings.Processor.Platforms.TryGetValue("windows", out TextureAssetProcessorSettings current)
                    ? current.MaxResolution
                    : 0;
                settings.Processor.Platforms["windows"] = new TextureAssetProcessorSettings { MaxResolution = currentMaxResolution + 17 };
                using MemoryStream output = new MemoryStream();
                TextureAssetImportSettingsBinarySerializer.Serialize(output, settings);
                File.WriteAllBytes(EditedSidecarPath, output.ToArray());
            }

            return InnerImporter.ImportTexture(stream);
        }
    }
}
