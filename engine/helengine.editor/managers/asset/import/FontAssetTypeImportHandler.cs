namespace helengine.editor {
    /// <summary>
    /// Owns the font side of the editor import pipeline: registered font importers and the default font importer selected per extension.
    /// </summary>
    sealed class FontAssetTypeImportHandler : EditorAssetTypeImportHandler {
        /// <summary>
        /// Registered font importers keyed by identifier.
        /// </summary>
        public readonly Dictionary<string, IFontImporter> ImportersById = new Dictionary<string, IFontImporter>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates the font handler for one asset import manager.
        /// </summary>
        /// <param name="owner">Manager that hosts this handler.</param>
        public FontAssetTypeImportHandler(AssetImportManager owner) : base(owner) {
        }

        /// <summary>
        /// Gets the font asset family identifier.
        /// </summary>
        public override EditorAssetImportKind Kind => EditorAssetImportKind.Font;

        /// <summary>
        /// Gets the capitalized family name used when reporting a duplicate font importer registration.
        /// </summary>
        public override string ImporterKindLabel => "Font";

        /// <summary>
        /// Gets the lowercase family name used when reporting that an importer id already belongs to font assets.
        /// </summary>
        public override string AssetKindLabel => "font";

        /// <summary>
        /// Gets the article-qualified phrase used when reporting that an extension already maps to a font importer.
        /// </summary>
        public override string ExtensionMappingLabel => "a font importer";

        /// <summary>
        /// Checks whether one importer identifier is registered as a font importer.
        /// </summary>
        /// <param name="importerId">Importer identifier to look for.</param>
        /// <returns>True when a font importer owns the identifier.</returns>
        public override bool ContainsImporterId(string importerId) {
            return ImportersById.ContainsKey(importerId);
        }

        /// <summary>
        /// Gets the identifiers of every registered font importer.
        /// </summary>
        /// <returns>Case-insensitively sorted font importer identifiers.</returns>
        public override IReadOnlyList<string> GetImporterIds() {
            List<string> importerIds = new List<string>(ImportersById.Count);
            foreach (string importerId in ImportersById.Keys) {
                importerIds.Add(importerId);
            }

            return SortImporterIds(importerIds);
        }
    }
}
