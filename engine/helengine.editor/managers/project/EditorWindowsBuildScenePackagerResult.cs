using helengine.baseplatform.Manifest;

namespace helengine.editor {
    /// <summary>
    /// Captures the shader packages referenced while packaging a Windows player build.
    /// </summary>
    public sealed class EditorPlatformBuildScenePackagerResult {
        /// <summary>
        /// Stores the deduplicated shader asset ids referenced by the packaged scenes.
        /// </summary>
        readonly string[] ReferencedShaderAssetIdsValue;
        /// <summary>
        /// Stores the builder-owned platform cook work items discovered while packaging the scenes.
        /// </summary>
        readonly PlatformCookWorkItem[] PlatformCookWorkItemsValue;

        /// <summary>
        /// Initializes a new scene-packaging result.
        /// </summary>
        /// <param name="referencedShaderAssetIds">Deduplicated shader asset ids referenced by the packaged scenes.</param>
        /// <param name="platformCookWorkItems">Builder-owned platform cook work items discovered while packaging the scenes.</param>
        public EditorPlatformBuildScenePackagerResult(
            IReadOnlyList<string> referencedShaderAssetIds,
            IReadOnlyList<PlatformCookWorkItem> platformCookWorkItems) {
            if (referencedShaderAssetIds == null) {
                throw new ArgumentNullException(nameof(referencedShaderAssetIds));
            } else if (platformCookWorkItems == null) {
                throw new ArgumentNullException(nameof(platformCookWorkItems));
            }

            ReferencedShaderAssetIdsValue = referencedShaderAssetIds.ToArray();
            PlatformCookWorkItemsValue = platformCookWorkItems.ToArray();
        }

        /// <summary>
        /// Gets the deduplicated shader asset ids referenced by the packaged scenes.
        /// </summary>
        public IReadOnlyList<string> ReferencedShaderAssetIds {
            get {
                return ReferencedShaderAssetIdsValue;
            }
        }

        /// <summary>
        /// Gets the builder-owned platform cook work items discovered while packaging the scenes.
        /// </summary>
        public IReadOnlyList<PlatformCookWorkItem> PlatformCookWorkItems {
            get {
                return PlatformCookWorkItemsValue;
            }
        }
    }
}
