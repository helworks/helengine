namespace helengine.editor {
    /// <summary>
    /// Converts selected asset-browser entries into stable scene asset references.
    /// </summary>
    public class SceneAssetReferenceFactory {
        /// <summary>
        /// Creates one stable scene asset reference from an asset-browser entry.
        /// </summary>
        /// <param name="entry">Selected browser entry to convert.</param>
        /// <returns>Stable scene asset reference describing the selected asset.</returns>
        public SceneAssetReference CreateFromEntry(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (entry.IsGenerated) {
                return CreateGeneratedFromEntry(entry);
            }

            return CreateFileSystemFromEntry(entry);
        }

        /// <summary>
        /// Creates one stable file-backed scene asset reference from an asset-browser entry.
        /// </summary>
        /// <param name="entry">Selected file-backed browser entry to convert.</param>
        /// <returns>Stable file-backed scene asset reference.</returns>
        static SceneAssetReference CreateFileSystemFromEntry(AssetBrowserEntry entry) {
            if (entry.EntryKind == AssetEntryKind.Image) {
                return global::helengine.SceneAssetReferenceFactory.CreateFileSystemTexture(entry.RelativePath);
            }
            if (entry.EntryKind == AssetEntryKind.Model) {
                return global::helengine.SceneAssetReferenceFactory.CreateFileSystemModel(entry.RelativePath);
            }
            if (entry.EntryKind == AssetEntryKind.Material) {
                return global::helengine.SceneAssetReferenceFactory.CreateFileSystemMaterial(entry.RelativePath);
            }
            if (entry.EntryKind == AssetEntryKind.Font) {
                return global::helengine.SceneAssetReferenceFactory.CreateFileSystemFont(entry.RelativePath);
            }

            throw new InvalidOperationException($"Asset browser entry kind '{entry.EntryKind}' does not support scene asset references.");
        }

        /// <summary>
        /// Creates one stable generated scene asset reference from an asset-browser entry.
        /// </summary>
        /// <param name="entry">Selected generated browser entry to convert.</param>
        /// <returns>Stable generated scene asset reference.</returns>
        static SceneAssetReference CreateGeneratedFromEntry(AssetBrowserEntry entry) {
            if (string.Equals(entry.ProviderId, EngineGeneratedAssetProvider.ProviderIdValue, StringComparison.Ordinal)) {
                if (entry.EntryKind == AssetEntryKind.Model) {
                    if (string.Equals(entry.AssetId, EngineGeneratedModelCache.CubeAssetId, StringComparison.Ordinal)) {
                        return global::helengine.EngineSceneAssetReferenceFactory.CreateCubeModel();
                    }
                    if (string.Equals(entry.AssetId, EngineGeneratedModelCache.PlaneAssetId, StringComparison.Ordinal)) {
                        return global::helengine.EngineSceneAssetReferenceFactory.CreatePlaneModel();
                    }
                    if (string.Equals(entry.AssetId, EngineGeneratedModelCache.SphereAssetId, StringComparison.Ordinal)) {
                        return global::helengine.EngineSceneAssetReferenceFactory.CreateSphereModel();
                    }
                } else if (entry.EntryKind == AssetEntryKind.Material &&
                           string.Equals(entry.AssetId, EngineGeneratedMaterialCache.StandardAssetId, StringComparison.Ordinal)) {
                    return global::helengine.EngineSceneAssetReferenceFactory.CreateStandardMaterial();
                }
            } else if (string.Equals(entry.ProviderId, EditorSceneAssetReferenceFactory.ProviderIdValue, StringComparison.Ordinal) &&
                       entry.EntryKind == AssetEntryKind.Font &&
                       string.Equals(entry.AssetId, EditorSceneAssetReferenceFactory.EditorUiFontAssetId, StringComparison.Ordinal)) {
                return EditorSceneAssetReferenceFactory.CreateEditorUiFont();
            }

            throw new InvalidOperationException(
                $"Unsupported generated asset browser entry '{entry.ProviderId}:{entry.AssetId}' of kind '{entry.EntryKind}'.");
        }
    }
}
