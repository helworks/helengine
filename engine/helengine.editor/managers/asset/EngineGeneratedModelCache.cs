namespace helengine.editor {
    /// <summary>
    /// Builds and caches built-in runtime models exposed by the engine generated-asset provider.
    /// </summary>
    public static class EngineGeneratedModelCache {
        /// <summary>
        /// Stable generated asset identifier for the built-in cube primitive.
        /// </summary>
        public const string CubeAssetId = "engine:model:cube";

        /// <summary>
        /// Stable generated asset identifier for the built-in plane primitive.
        /// </summary>
        public const string PlaneAssetId = "engine:model:plane";

        /// <summary>
        /// Cached runtime models keyed by stable generated asset identifier.
        /// </summary>
        static readonly Dictionary<string, RuntimeModel> RuntimeModels = new Dictionary<string, RuntimeModel>(StringComparer.Ordinal);

        /// <summary>
        /// Clears the generated model cache so tests can start from a known state.
        /// </summary>
        public static void ResetForTests() {
            RuntimeModels.Clear();
        }

        /// <summary>
        /// Gets a cached runtime model for one built-in generated asset id, building it on first use.
        /// </summary>
        /// <param name="assetId">Stable generated asset identifier.</param>
        /// <returns>Cached runtime model for the requested generated primitive.</returns>
        public static RuntimeModel GetRuntimeModel(string assetId) {
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Generated asset id must be provided.", nameof(assetId));
            }

            if (RuntimeModels.TryGetValue(assetId, out RuntimeModel runtimeModel)) {
                return runtimeModel;
            }

            Core core = Core.Instance;
            if (core == null) {
                throw new InvalidOperationException("Core must be initialized before generated engine models can be resolved.");
            }
            if (core.RenderManager3D == null) {
                throw new InvalidOperationException("A 3D render manager is required before generated engine models can be resolved.");
            }

            ModelAsset modelAsset = CreateModelAsset(assetId);
            runtimeModel = core.RenderManager3D.BuildModelFromRaw(modelAsset);
            RuntimeModels.Add(assetId, runtimeModel);
            return runtimeModel;
        }

        /// <summary>
        /// Creates the raw model asset for one supported built-in generated primitive.
        /// </summary>
        /// <param name="assetId">Stable generated asset identifier.</param>
        /// <returns>Raw model asset for the generated primitive.</returns>
        static ModelAsset CreateModelAsset(string assetId) {
            if (string.Equals(assetId, CubeAssetId, StringComparison.Ordinal)) {
                return ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);
            }

            if (string.Equals(assetId, PlaneAssetId, StringComparison.Ordinal)) {
                return ModelUtils.GeneratePlaneMesh(float3.Zero, float3.One);
            }

            throw new InvalidOperationException($"Generated engine model '{assetId}' is not registered.");
        }
    }
}
