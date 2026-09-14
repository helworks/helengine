namespace helengine.editor {
    /// <summary>
    /// Owns the texture side of the editor import pipeline: registered texture importers, the extensions they claim, and the
    /// default texture importer selected per extension.
    /// </summary>
    sealed class TextureAssetTypeImportHandler : EditorAssetTypeImportHandler {
        /// <summary>
        /// Registered texture importers keyed by identifier.
        /// </summary>
        public readonly Dictionary<string, ITextureImporter> ImportersById = new Dictionary<string, ITextureImporter>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Texture importer identifiers keyed by normalized extension, preserving registration order so overlapping importers stay selectable.
        /// </summary>
        public readonly Dictionary<string, List<string>> ImporterIdsByExtension = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates the texture handler for one asset import manager.
        /// </summary>
        /// <param name="owner">Manager that hosts this handler.</param>
        public TextureAssetTypeImportHandler(AssetImportManager owner) : base(owner) {
        }

        /// <summary>
        /// Gets the texture asset family identifier.
        /// </summary>
        public override EditorAssetImportKind Kind => EditorAssetImportKind.Texture;

        /// <summary>
        /// Gets the capitalized family name used when reporting a duplicate texture importer registration.
        /// </summary>
        public override string ImporterKindLabel => "Texture";

        /// <summary>
        /// Gets the lowercase family name used when reporting that an importer id already belongs to texture assets.
        /// </summary>
        public override string AssetKindLabel => "texture";

        /// <summary>
        /// Gets the article-qualified phrase used when reporting that an extension already maps to a texture importer.
        /// </summary>
        public override string ExtensionMappingLabel => "a texture importer";

        /// <summary>
        /// Checks whether one importer identifier is registered as a texture importer.
        /// </summary>
        /// <param name="importerId">Importer identifier to look for.</param>
        /// <returns>True when a texture importer owns the identifier.</returns>
        public override bool ContainsImporterId(string importerId) {
            return ImportersById.ContainsKey(importerId);
        }

        /// <summary>
        /// Gets the identifiers of every registered texture importer.
        /// </summary>
        /// <returns>Case-insensitively sorted texture importer identifiers.</returns>
        public override IReadOnlyList<string> GetImporterIds() {
            List<string> importerIds = new List<string>(ImportersById.Count);
            foreach (string importerId in ImportersById.Keys) {
                importerIds.Add(importerId);
            }

            return SortImporterIds(importerIds);
        }

        /// <summary>
        /// Checks whether one normalized extension is claimed by any registered texture importer.
        /// </summary>
        /// <param name="normalizedExtension">Extension already normalized to a lowercase dotted form.</param>
        /// <returns>True when a texture importer claims the extension.</returns>
        public override bool IsNormalizedExtensionSupported(string normalizedExtension) {
            return ImporterIdsByExtension.ContainsKey(normalizedExtension);
        }

        /// <summary>
        /// Returns the texture importer identifiers registered for one normalized extension.
        /// </summary>
        /// <param name="normalizedExtension">Extension already normalized to a lowercase dotted form.</param>
        /// <returns>Copy of the applicable texture importer identifiers, or null when no texture importer claims the extension.</returns>
        public override IReadOnlyList<string> TryGetImporterIdsForExtension(string normalizedExtension) {
            List<string> importerIds;
            if (!ImporterIdsByExtension.TryGetValue(normalizedExtension, out importerIds)) {
                return null;
            }

            return new List<string>(importerIds);
        }
    }
}
