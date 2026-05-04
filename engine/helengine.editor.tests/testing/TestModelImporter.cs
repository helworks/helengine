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
        /// Imports a fixed triangle model from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing source model data.</param>
        /// <returns>Model asset with deterministic geometry.</returns>
        public ModelAsset ImportModel(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            ImportCountValue++;
            return new ModelAsset {
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
                Indices16 = new ushort[] { 0, 1, 2 }
            };
        }
    }
}
