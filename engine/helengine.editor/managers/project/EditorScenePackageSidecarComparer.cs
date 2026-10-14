namespace helengine.editor {
    /// <summary>
    /// Decides whether a texture import-settings sidecar rewritten during packaging changed only its cook-derived
    /// fields. <see cref="AssetImporterSettings.AssetId"/> is derived from the source checksum, the importer, the platform
    /// being cooked and its processor settings, and <see cref="AssetImporterSettings.SourceChecksum"/> follows the
    /// source; neither is an authoring decision. Every other field (importer id, per-platform and per-environment
    /// processor settings such as resolution or color format) changes how the texture cooks, so a difference there
    /// means the settings were edited, not refreshed.
    /// </summary>
    internal static class EditorScenePackageSidecarComparer {
        /// <summary>
        /// Compares two serialized texture import settings with the cook-derived fields blanked.
        /// </summary>
        /// <param name="before">Sidecar bytes captured before packaging.</param>
        /// <param name="after">Sidecar bytes after packaging.</param>
        /// <returns>True when both parse as texture import settings and are equal apart from the cook-derived fields;
        /// false when they differ in any other field or either is not texture import settings.</returns>
        public static bool DiffersOnlyByCookDerivedFields(byte[] before, byte[] after) {
            if (before == null) {
                throw new ArgumentNullException(nameof(before));
            } else if (after == null) {
                throw new ArgumentNullException(nameof(after));
            }

            byte[] normalizedBefore = SerializeWithoutCookDerivedFields(before);
            byte[] normalizedAfter = SerializeWithoutCookDerivedFields(after);
            return normalizedBefore != null
                && normalizedAfter != null
                && normalizedBefore.AsSpan().SequenceEqual(normalizedAfter);
        }

        /// <summary>
        /// Parses texture import settings, blanks the cook-derived fields, and serializes them again in the
        /// serializer's canonical order.
        /// </summary>
        /// <param name="sidecarBytes">Serialized sidecar.</param>
        /// <returns>Canonical bytes without cook-derived fields, or null when the sidecar is not texture import settings.
        /// Only reached for a sidecar packaging changed, so a model, audio or font sidecar parsing failure is rare and
        /// simply keeps the entry under its pre-packaging key.</returns>
        static byte[] SerializeWithoutCookDerivedFields(byte[] sidecarBytes) {
            TextureAssetImportSettings settings;
            try {
                using MemoryStream input = new MemoryStream(sidecarBytes, writable: false);
                settings = TextureAssetImportSettingsBinarySerializer.Deserialize(input);
            } catch (Exception exception) when (exception is EndOfStreamException || exception is InvalidOperationException || exception is ArgumentException || exception is InvalidDataException) {
                return null;
            }

            settings.Importer.AssetId = string.Empty;
            settings.Importer.SourceChecksum = string.Empty;
            using MemoryStream output = new MemoryStream();
            TextureAssetImportSettingsBinarySerializer.Serialize(output, settings);
            return output.ToArray();
        }
    }
}
