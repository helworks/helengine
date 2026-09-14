namespace helengine.editor {
    /// <summary>
    /// Owns the text side of the editor import pipeline: registered text importers and the default text importer selected per extension.
    /// </summary>
    sealed class TextAssetTypeImportHandler : EditorAssetTypeImportHandler {
        /// <summary>
        /// Registered text importers keyed by identifier.
        /// </summary>
        public readonly Dictionary<string, ITextImporter> ImportersById = new Dictionary<string, ITextImporter>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates the text handler for one asset import manager.
        /// </summary>
        /// <param name="owner">Manager that hosts this handler.</param>
        public TextAssetTypeImportHandler(AssetImportManager owner) : base(owner) {
        }

        /// <summary>
        /// Gets the text asset family identifier.
        /// </summary>
        public override EditorAssetImportKind Kind => EditorAssetImportKind.Text;

        /// <summary>
        /// Gets the capitalized family name used when reporting a duplicate text importer registration.
        /// </summary>
        public override string ImporterKindLabel => "Text";

        /// <summary>
        /// Gets the lowercase family name used when reporting that an importer id already belongs to text assets.
        /// </summary>
        public override string AssetKindLabel => "text";

        /// <summary>
        /// Gets the article-qualified phrase used when reporting that an extension already maps to a text importer.
        /// </summary>
        public override string ExtensionMappingLabel => "a text importer";

        /// <summary>
        /// Checks whether one importer identifier is registered as a text importer.
        /// </summary>
        /// <param name="importerId">Importer identifier to look for.</param>
        /// <returns>True when a text importer owns the identifier.</returns>
        public override bool ContainsImporterId(string importerId) {
            return ImportersById.ContainsKey(importerId);
        }

        /// <summary>
        /// Gets the identifiers of every registered text importer.
        /// </summary>
        /// <returns>Case-insensitively sorted text importer identifiers.</returns>
        public override IReadOnlyList<string> GetImporterIds() {
            List<string> importerIds = new List<string>(ImportersById.Count);
            foreach (string importerId in ImportersById.Keys) {
                importerIds.Add(importerId);
            }

            return SortImporterIds(importerIds);
        }
    }
}
