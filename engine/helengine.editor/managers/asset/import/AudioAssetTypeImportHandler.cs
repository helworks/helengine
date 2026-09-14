namespace helengine.editor {
    /// <summary>
    /// Owns the audio side of the editor import pipeline: registered audio importers, the extensions they claim, and the
    /// default audio importer selected per extension.
    /// </summary>
    sealed class AudioAssetTypeImportHandler : EditorAssetTypeImportHandler {
        /// <summary>
        /// Registered audio importers keyed by identifier.
        /// </summary>
        public readonly Dictionary<string, IAudioImporter> ImportersById = new Dictionary<string, IAudioImporter>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Audio importer identifiers keyed by normalized extension, preserving registration order so overlapping importers stay selectable.
        /// </summary>
        public readonly Dictionary<string, List<string>> ImporterIdsByExtension = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates the audio handler for one asset import manager.
        /// </summary>
        /// <param name="owner">Manager that hosts this handler.</param>
        public AudioAssetTypeImportHandler(AssetImportManager owner) : base(owner) {
        }

        /// <summary>
        /// Gets the audio asset family identifier.
        /// </summary>
        public override EditorAssetImportKind Kind => EditorAssetImportKind.Audio;

        /// <summary>
        /// Gets the capitalized family name used when reporting a duplicate audio importer registration.
        /// </summary>
        public override string ImporterKindLabel => "Audio";

        /// <summary>
        /// Gets the lowercase family name used when reporting that an importer id already belongs to audio assets.
        /// </summary>
        public override string AssetKindLabel => "audio";

        /// <summary>
        /// Gets the article-qualified phrase used when reporting that an extension already maps to an audio importer.
        /// </summary>
        public override string ExtensionMappingLabel => "an audio importer";

        /// <summary>
        /// Checks whether one importer identifier is registered as an audio importer.
        /// </summary>
        /// <param name="importerId">Importer identifier to look for.</param>
        /// <returns>True when an audio importer owns the identifier.</returns>
        public override bool ContainsImporterId(string importerId) {
            return ImportersById.ContainsKey(importerId);
        }

        /// <summary>
        /// Gets the identifiers of every registered audio importer.
        /// </summary>
        /// <returns>Case-insensitively sorted audio importer identifiers.</returns>
        public override IReadOnlyList<string> GetImporterIds() {
            List<string> importerIds = new List<string>(ImportersById.Count);
            foreach (string importerId in ImportersById.Keys) {
                importerIds.Add(importerId);
            }

            return SortImporterIds(importerIds);
        }

        /// <summary>
        /// Checks whether one normalized extension is claimed by any registered audio importer.
        /// </summary>
        /// <param name="normalizedExtension">Extension already normalized to a lowercase dotted form.</param>
        /// <returns>True when an audio importer claims the extension.</returns>
        public override bool IsNormalizedExtensionSupported(string normalizedExtension) {
            return ImporterIdsByExtension.ContainsKey(normalizedExtension);
        }

        /// <summary>
        /// Returns the audio importer identifiers registered for one normalized extension.
        /// </summary>
        /// <param name="normalizedExtension">Extension already normalized to a lowercase dotted form.</param>
        /// <returns>Copy of the applicable audio importer identifiers, or null when no audio importer claims the extension.</returns>
        public override IReadOnlyList<string> TryGetImporterIdsForExtension(string normalizedExtension) {
            List<string> importerIds;
            if (!ImporterIdsByExtension.TryGetValue(normalizedExtension, out importerIds)) {
                return null;
            }

            return new List<string>(importerIds);
        }
    }
}
