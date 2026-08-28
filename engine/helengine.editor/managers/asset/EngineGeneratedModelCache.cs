namespace helengine.editor {
    /// <summary>Builds and caches built-in runtime models for one editor session.</summary>
    public sealed class EngineGeneratedModelCache : IDisposable {
        public const string CubeAssetId = ModelUtils.GeneratedCubeModelId;
        public const string PlaneAssetId = ModelUtils.GeneratedPlaneModelId;
        public const string SphereAssetId = ModelUtils.GeneratedSphereModelId;

        readonly Core Core;
        readonly RenderManager3D RenderManager3D;
        readonly Dictionary<string, RuntimeModel> RuntimeModels = new Dictionary<string, RuntimeModel>(StringComparer.Ordinal);
        bool IsDisposed;

        /// <summary>Creates a generated-model cache bound to one explicit core.</summary>
        public EngineGeneratedModelCache(Core core) {
            Core = core ?? throw new ArgumentNullException(nameof(core));
            RenderManager3D = core.RenderManager3D ?? throw new InvalidOperationException("The owning core must be initialized with a 3D renderer before creating generated model assets.");
        }

        /// <summary>Gets or creates one generated runtime model owned by this cache.</summary>
        public RuntimeModel GetRuntimeModel(string assetId) {
            EnsureNotDisposed();
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Generated asset id must be provided.", nameof(assetId));
            }
            if (RuntimeModels.TryGetValue(assetId, out RuntimeModel runtimeModel)) {
                return runtimeModel;
            }

            runtimeModel = RenderManager3D.BuildModelFromRaw(CreateModelAsset(assetId));
            RuntimeModels.Add(assetId, runtimeModel);
            return runtimeModel;
        }

        static ModelAsset CreateModelAsset(string assetId) {
            if (string.Equals(assetId, CubeAssetId, StringComparison.Ordinal)) {
                return ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);
            }
            if (string.Equals(assetId, PlaneAssetId, StringComparison.Ordinal)) {
                return ModelUtils.GeneratePlaneMesh(float3.Zero, float3.One);
            }
            if (string.Equals(assetId, SphereAssetId, StringComparison.Ordinal)) {
                return ModelUtils.GenerateSphereMesh(float3.Zero, float3.One);
            }
            throw new InvalidOperationException($"Generated engine model '{assetId}' is not registered.");
        }

        void EnsureNotDisposed() {
            if (IsDisposed) {
                throw new ObjectDisposedException(nameof(EngineGeneratedModelCache));
            }
        }

        /// <summary>Releases cached runtime models owned by this session cache.</summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }
            foreach (RuntimeModel runtimeModel in RuntimeModels.Values) {
                runtimeModel?.Dispose();
            }
            RuntimeModels.Clear();
            IsDisposed = true;
        }
    }
}
