namespace helengine.editor.tests.testing {
    /// <summary>
    /// Test model importer that returns a deterministic triangle mesh for importer-manager scenarios.
    /// </summary>
    internal class TestModelImporter : IModelImporter {
        /// <summary>
        /// Tracks how many times model import was requested.
        /// </summary>
        int ImportCountValue;

        /// <summary>
        /// Gets the number of model imports performed by this importer.
        /// </summary>
        public int ImportCount => ImportCountValue;

        /// <summary>
        /// Gets or sets the generated material assets that should be returned with the imported model payload.
        /// </summary>
        public ImportedModelMaterialAsset[] GeneratedMaterials { get; set; } = Array.Empty<ImportedModelMaterialAsset>();

        /// <summary>
        /// Imports a fixed triangle model from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing source model data.</param>
        /// <returns>Imported model payload with deterministic geometry and no generated materials.</returns>
        public ImportedModelAssetSet ImportModel(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            ImportCountValue++;
            ModelAsset modelAsset = new ModelAsset {
                Positions = new[] {
                    new float3(0f, 0f, 0f),
                    new float3(1f, 0f, 0f),
                    new float3(0f, 1f, 0f)
                },
                Normals = new[] {
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f)
                },
                TexCoords = new[] {
                    new float2(0f, 0f),
                    new float2(1f, 0f),
                    new float2(0f, 1f)
                },
                Indices16 = new ushort[] { 0, 1, 2 },
                Submeshes = new[] {
                    new ModelSubmeshAsset {
                        IndexStart = 0,
                        IndexCount = 3,
                        MaterialSlotName = "Default"
                    }
                }
            };
            return new ImportedModelAssetSet(modelAsset, GeneratedMaterials);
        }
    }
}
