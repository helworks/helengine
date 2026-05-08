namespace helengine.editor {
    /// <summary>
    /// Bundles one imported model asset together with any generated material assets derived from the source model.
    /// </summary>
    public sealed class ImportedModelAssetSet {
        /// <summary>
        /// Initializes one imported model result bundle.
        /// </summary>
        /// <param name="modelAsset">Imported model asset payload.</param>
        /// <param name="generatedMaterials">Generated material assets derived during import.</param>
        public ImportedModelAssetSet(ModelAsset modelAsset, ImportedModelMaterialAsset[] generatedMaterials) {
            ModelAsset = modelAsset ?? throw new ArgumentNullException(nameof(modelAsset));
            GeneratedMaterials = generatedMaterials ?? Array.Empty<ImportedModelMaterialAsset>();
        }

        /// <summary>
        /// Gets the imported model asset payload.
        /// </summary>
        public ModelAsset ModelAsset { get; }

        /// <summary>
        /// Gets the generated material assets derived during import.
        /// </summary>
        public ImportedModelMaterialAsset[] GeneratedMaterials { get; }
    }
}
