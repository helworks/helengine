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
        /// Virtual entry path for the generated cube primitive.
        /// </summary>
        public const string CubeRelativePath = "Engine/Models/Cube";

        /// <summary>
        /// Virtual entry path for the generated plane primitive.
        /// </summary>
        public const string PlaneRelativePath = "Engine/Models/Plane";

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
                return;
            }

            if (string.Equals(relativePath, EngineModelsPath, StringComparison.Ordinal)) {
                entries.Add(AssetBrowserEntry.CreateGeneratedAsset("Cube", CubeRelativePath, AssetEntryKind.Model, ProviderId, EngineGeneratedModelCache.CubeAssetId));
                entries.Add(AssetBrowserEntry.CreateGeneratedAsset("Plane", PlaneRelativePath, AssetEntryKind.Model, ProviderId, EngineGeneratedModelCache.PlaneAssetId));
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
    }
}
