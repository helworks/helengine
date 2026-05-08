namespace helengine.editor.tests.testing {
    /// <summary>
    /// Produces a deterministic model payload for lazy importer tests.
    /// </summary>
    internal sealed class ConstantModelImporter : IModelImporter {
        /// <summary>
        /// Shared deterministic model asset returned for every import request.
        /// </summary>
        readonly ImportedModelAssetSet Asset = new ImportedModelAssetSet(
            new ModelAsset {
                Submeshes = new[] {
                    new ModelSubmeshAsset {
                        IndexStart = 0,
                        IndexCount = 0,
                        MaterialSlotName = "Default"
                    }
                }
            },
            Array.Empty<ImportedModelMaterialAsset>());

        /// <summary>
        /// Imports a deterministic model asset regardless of the source stream contents.
        /// </summary>
        /// <param name="stream">Stream containing source bytes.</param>
        /// <returns>Deterministic model payload.</returns>
        public ImportedModelAssetSet ImportModel(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return Asset;
        }
    }
}
