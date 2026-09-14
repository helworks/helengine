namespace helengine.editor {
    /// <summary>
    /// Owns the model side of the editor import pipeline: registered model importers and the default model importer selected per extension.
    /// </summary>
    sealed class ModelAssetTypeImportHandler : EditorAssetTypeImportHandler {
        /// <summary>
        /// Registered model importers keyed by identifier.
        /// </summary>
        public readonly Dictionary<string, IModelImporter> ImportersById = new Dictionary<string, IModelImporter>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates the model handler for one asset import manager.
        /// </summary>
        /// <param name="owner">Manager that hosts this handler.</param>
        public ModelAssetTypeImportHandler(AssetImportManager owner) : base(owner) {
        }

        /// <summary>
        /// Gets the model asset family identifier.
        /// </summary>
        public override EditorAssetImportKind Kind => EditorAssetImportKind.Model;

        /// <summary>
        /// Gets the capitalized family name used when reporting a duplicate model importer registration.
        /// </summary>
        public override string ImporterKindLabel => "Model";

        /// <summary>
        /// Gets the lowercase family name used when reporting that an importer id already belongs to model assets.
        /// </summary>
        public override string AssetKindLabel => "model";

        /// <summary>
        /// Gets the article-qualified phrase used when reporting that an extension already maps to a model importer.
        /// </summary>
        public override string ExtensionMappingLabel => "a model importer";

        /// <summary>
        /// Checks whether one importer identifier is registered as a model importer.
        /// </summary>
        /// <param name="importerId">Importer identifier to look for.</param>
        /// <returns>True when a model importer owns the identifier.</returns>
        public override bool ContainsImporterId(string importerId) {
            return ImportersById.ContainsKey(importerId);
        }

        /// <summary>
        /// Gets the identifiers of every registered model importer.
        /// </summary>
        /// <returns>Case-insensitively sorted model importer identifiers.</returns>
        public override IReadOnlyList<string> GetImporterIds() {
            List<string> importerIds = new List<string>(ImportersById.Count);
            foreach (string importerId in ImportersById.Keys) {
                importerIds.Add(importerId);
            }

            return SortImporterIds(importerIds);
        }
    }
}
