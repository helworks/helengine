namespace helengine.editor {
    /// <summary>Publishes built-in generated engine assets using one session-owned cache pair.</summary>
    public sealed class EngineGeneratedAssetProvider : IGeneratedAssetProvider {
        public const string ProviderIdValue = "engine";
        public const string EngineRootPath = "Engine";
        public const string EngineModelsPath = "Engine/Models";
        public const string EngineMaterialsPath = "Engine/Materials";
        public const string CubeRelativePath = "Engine/Models/Cube";
        public const string PlaneRelativePath = "Engine/Models/Plane";
        public const string SphereRelativePath = "Engine/Models/Sphere";
        public const string StandardMaterialRelativePath = "Engine/Materials/Standard";

        readonly EngineGeneratedModelCache ModelCache;
        readonly EngineGeneratedMaterialCache MaterialCache;

        /// <summary>Creates the engine provider over explicit session caches.</summary>
        public EngineGeneratedAssetProvider(EngineGeneratedModelCache modelCache, EngineGeneratedMaterialCache materialCache) {
            ModelCache = modelCache ?? throw new ArgumentNullException(nameof(modelCache));
            MaterialCache = materialCache ?? throw new ArgumentNullException(nameof(materialCache));
        }

        public string ProviderId => ProviderIdValue;

        /// <summary>Gets the model cache captured by this provider.</summary>
        internal EngineGeneratedModelCache BoundModelCache => ModelCache;

        /// <summary>Gets the material cache captured by this provider.</summary>
        internal EngineGeneratedMaterialCache BoundMaterialCache => MaterialCache;

        public void LoadEntries(string relativePath, List<AssetBrowserEntry> entries) {
            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }
            if (string.IsNullOrWhiteSpace(relativePath)) {
                entries.Add(AssetBrowserEntry.CreateGeneratedDirectory("Engine", EngineRootPath, ProviderId));
                return;
            }
            if (string.Equals(relativePath, EngineRootPath, StringComparison.Ordinal)) {
                entries.Add(AssetBrowserEntry.CreateGeneratedDirectory("Models", EngineModelsPath, ProviderId));
                entries.Add(AssetBrowserEntry.CreateGeneratedDirectory("Materials", EngineMaterialsPath, ProviderId));
                return;
            }
            if (string.Equals(relativePath, EngineModelsPath, StringComparison.Ordinal)) {
                entries.Add(AssetBrowserEntry.CreateGeneratedAsset("Cube", CubeRelativePath, AssetEntryKind.Model, ProviderId, EngineGeneratedModelCache.CubeAssetId));
                entries.Add(AssetBrowserEntry.CreateGeneratedAsset("Plane", PlaneRelativePath, AssetEntryKind.Model, ProviderId, EngineGeneratedModelCache.PlaneAssetId));
                entries.Add(AssetBrowserEntry.CreateGeneratedAsset("Sphere", SphereRelativePath, AssetEntryKind.Model, ProviderId, EngineGeneratedModelCache.SphereAssetId));
                return;
            }
            if (string.Equals(relativePath, EngineMaterialsPath, StringComparison.Ordinal)) {
                entries.Add(AssetBrowserEntry.CreateGeneratedAsset("Standard", StandardMaterialRelativePath, AssetEntryKind.Material, ProviderId, EngineGeneratedMaterialCache.StandardAssetId));
            }
        }

        public bool TryResolveRuntimeModel(AssetBrowserEntry entry, out RuntimeModel runtimeModel) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            runtimeModel = null;
            if (!string.Equals(entry.ProviderId, ProviderId, StringComparison.Ordinal) || entry.EntryKind != AssetEntryKind.Model) {
                return false;
            }
            runtimeModel = ModelCache.GetRuntimeModel(entry.AssetId);
            return true;
        }

        public bool TryResolveRuntimeMaterial(AssetBrowserEntry entry, out RuntimeMaterial runtimeMaterial) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            runtimeMaterial = null;
            if (!string.Equals(entry.ProviderId, ProviderId, StringComparison.Ordinal) || entry.EntryKind != AssetEntryKind.Material) {
                return false;
            }
            runtimeMaterial = MaterialCache.GetRuntimeMaterial(entry.AssetId);
            return true;
        }
    }
}
