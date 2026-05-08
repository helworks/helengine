namespace helengine.editor {
    /// <summary>
    /// Publishes built-in generated engine assets such as primitive models.
    /// </summary>
    public class EngineGeneratedAssetProvider : IGeneratedAssetProvider {
        /// <summary>
        /// Stable provider identifier used by engine-generated entries.
        /// </summary>
        public const string ProviderIdValue = "engine";

        /// <summary>
        /// Virtual root directory for engine-generated assets.
        /// </summary>
        public const string EngineRootPath = "Engine";

        /// <summary>
        /// Virtual directory that groups generated model primitives.
        /// </summary>
        public const string EngineModelsPath = "Engine/Models";
        /// <summary>
        /// Virtual directory that groups generated engine materials.
        /// </summary>
        public const string EngineMaterialsPath = "Engine/Materials";

        /// <summary>
        /// Virtual entry path for the generated cube primitive.
        /// </summary>
        public const string CubeRelativePath = "Engine/Models/Cube";

        /// <summary>
        /// Virtual entry path for the generated plane primitive.
        /// </summary>
        public const string PlaneRelativePath = "Engine/Models/Plane";

        /// <summary>
        /// Virtual entry path for the generated sphere primitive.
        /// </summary>
        public const string SphereRelativePath = "Engine/Models/Sphere";

        /// <summary>
        /// Virtual entry path for the generated standard material.
        /// </summary>
        public const string StandardMaterialRelativePath = "Engine/Materials/Standard";

        /// <summary>
        /// Gets the stable provider identifier used by engine-generated entries.
        /// </summary>
        public string ProviderId => ProviderIdValue;

        /// <summary>
        /// Appends generated entries that live directly under one supported virtual path.
        /// </summary>
        /// <param name="relativePath">Virtual path whose direct children should be appended.</param>
        /// <param name="entries">Target list that receives the generated entries.</param>
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

        /// <summary>
        /// Resolves one engine-generated model entry through the shared runtime-model cache.
        /// </summary>
        /// <param name="entry">Generated entry requested by the editor.</param>
        /// <param name="runtimeModel">Resolved runtime model when the entry belongs to this provider.</param>
        /// <returns>True when the provider resolved the model; otherwise false.</returns>
        public bool TryResolveRuntimeModel(AssetBrowserEntry entry, out RuntimeModel runtimeModel) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            runtimeModel = null;
            if (!string.Equals(entry.ProviderId, ProviderId, StringComparison.Ordinal)) {
                return false;
            }
            if (entry.EntryKind != AssetEntryKind.Model) {
                return false;
            }

            runtimeModel = EngineGeneratedModelCache.GetRuntimeModel(entry.AssetId);
            return true;
        }

        /// <summary>
        /// Resolves one engine-generated material entry through the shared runtime-material cache.
        /// </summary>
        /// <param name="entry">Generated entry requested by the editor.</param>
        /// <param name="runtimeMaterial">Resolved runtime material when the entry belongs to this provider.</param>
        /// <returns>True when the provider resolved the material; otherwise false.</returns>
        public bool TryResolveRuntimeMaterial(AssetBrowserEntry entry, out RuntimeMaterial runtimeMaterial) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            runtimeMaterial = null;
            if (!string.Equals(entry.ProviderId, ProviderId, StringComparison.Ordinal)) {
                return false;
            }
            if (entry.EntryKind != AssetEntryKind.Material) {
                return false;
            }

            runtimeMaterial = EngineGeneratedMaterialCache.GetRuntimeMaterial(entry.AssetId);
            return true;
        }
    }
}
