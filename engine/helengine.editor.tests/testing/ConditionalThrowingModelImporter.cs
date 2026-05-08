namespace helengine.editor.tests.testing {
    /// <summary>
    /// Test model importer that throws when source content contains a configured marker and otherwise returns a deterministic triangle mesh.
    /// </summary>
    internal class ConditionalThrowingModelImporter : IModelImporter {
        /// <summary>
        /// Marker text that triggers an import failure.
        /// </summary>
        readonly string FailureMarker;

        /// <summary>
        /// Message used for the thrown failure when the marker is present.
        /// </summary>
        readonly string FailureMessage;

        /// <summary>
        /// Tracks how many import attempts were made.
        /// </summary>
        int ImportCountValue;

        /// <summary>
        /// Initializes one conditional throwing importer.
        /// </summary>
        /// <param name="failureMarker">Marker text that triggers an exception.</param>
        /// <param name="failureMessage">Exception message to throw when the marker is present.</param>
        public ConditionalThrowingModelImporter(string failureMarker, string failureMessage) {
            if (string.IsNullOrWhiteSpace(failureMarker)) {
                throw new ArgumentException("Failure marker must be provided.", nameof(failureMarker));
            }

            if (string.IsNullOrWhiteSpace(failureMessage)) {
                throw new ArgumentException("Failure message must be provided.", nameof(failureMessage));
            }

            FailureMarker = failureMarker;
            FailureMessage = failureMessage;
        }

        /// <summary>
        /// Gets the number of import attempts performed by this importer.
        /// </summary>
        public int ImportCount => ImportCountValue;

        /// <summary>
        /// Imports one model and throws when the configured marker is present in the source text.
        /// </summary>
        /// <param name="stream">Stream containing source model data.</param>
        /// <returns>Imported model payload with deterministic geometry when the import succeeds.</returns>
        public ImportedModelAssetSet ImportModel(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            ImportCountValue++;

            using StreamReader reader = new StreamReader(stream, leaveOpen: true);
            string sourceText = reader.ReadToEnd();
            stream.Position = 0;
            if (sourceText.Contains(FailureMarker, StringComparison.Ordinal)) {
                throw new InvalidOperationException(FailureMessage);
            }

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
            return new ImportedModelAssetSet(modelAsset, Array.Empty<ImportedModelMaterialAsset>());
        }
    }
}
