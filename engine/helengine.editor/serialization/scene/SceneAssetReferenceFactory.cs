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
                return new SceneAssetReference {
                    SourceKind = SceneAssetReferenceSourceKind.Generated,
                    RelativePath = entry.RelativePath,
                    ProviderId = entry.ProviderId,
                    AssetId = entry.AssetId
                };
            }

            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = entry.RelativePath,
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
        }
    }
}
