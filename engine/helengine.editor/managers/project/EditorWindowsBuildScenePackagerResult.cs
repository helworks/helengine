using helengine.baseplatform.Manifest;
using helengine.baseplatform.Results;

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
        /// Stores material-selected shader dependencies including optional program-pair lookup keys.
        /// </summary>
        readonly PlatformShaderDependency[] ReferencedShaderDependenciesValue;
        /// <summary>
        /// Stores the builder-owned platform cook work items discovered while packaging the scenes.
        /// </summary>
        readonly PlatformCookWorkItem[] PlatformCookWorkItemsValue;
        /// <summary>
        /// Stores explicit material and shader declarations for files written while packaging scenes.
        /// </summary>
        readonly PlatformCookedArtifactDeclaration[] CookedArtifactDeclarationsValue;

        /// <summary>
        /// Initializes a new scene-packaging result.
        /// </summary>
        /// <param name="referencedShaderAssetIds">Deduplicated shader asset ids referenced by the packaged scenes.</param>
        /// <param name="platformCookWorkItems">Builder-owned platform cook work items discovered while packaging the scenes.</param>
        /// <param name="cookedArtifactDeclarations">Explicit declarations for material and shader files written while packaging scenes.</param>
        /// <param name="referencedShaderDependencies">Complete shader dependencies reported by material cooking, when available.</param>
        public EditorPlatformBuildScenePackagerResult(
            IReadOnlyList<string> referencedShaderAssetIds,
            IReadOnlyList<PlatformCookWorkItem> platformCookWorkItems,
            IReadOnlyList<PlatformCookedArtifactDeclaration> cookedArtifactDeclarations,
            IReadOnlyList<PlatformShaderDependency> referencedShaderDependencies = null) {
            if (referencedShaderAssetIds == null) {
                throw new ArgumentNullException(nameof(referencedShaderAssetIds));
            } else if (platformCookWorkItems == null) {
                throw new ArgumentNullException(nameof(platformCookWorkItems));
            } else if (cookedArtifactDeclarations == null) {
                throw new ArgumentNullException(nameof(cookedArtifactDeclarations));
            }

            IReadOnlyList<PlatformShaderDependency> effectiveDependencies = referencedShaderDependencies;
            if (effectiveDependencies == null) {
                PlatformShaderDependency[] idOnlyDependencies = new PlatformShaderDependency[referencedShaderAssetIds.Count];
                for (int index = 0; index < idOnlyDependencies.Length; index++) {
                    idOnlyDependencies[index] = new PlatformShaderDependency(referencedShaderAssetIds[index], string.Empty, string.Empty, string.Empty);
                }

                effectiveDependencies = idOnlyDependencies;
            }

            ReferencedShaderAssetIdsValue = referencedShaderAssetIds.ToArray();
            ReferencedShaderDependenciesValue = effectiveDependencies.ToArray();
            PlatformCookWorkItemsValue = platformCookWorkItems.ToArray();
            CookedArtifactDeclarationsValue = cookedArtifactDeclarations.ToArray();
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
        /// Gets complete material-reported shader dependencies without reading material bytes during shader staging.
        /// </summary>
        public IReadOnlyList<PlatformShaderDependency> ReferencedShaderDependencies {
            get {
                return ReferencedShaderDependenciesValue;
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

        /// <summary>
        /// Gets explicit declarations for material and shader files written while packaging scenes.
        /// </summary>
        public IReadOnlyList<PlatformCookedArtifactDeclaration> CookedArtifactDeclarations {
            get {
                return CookedArtifactDeclarationsValue;
            }
        }
    }
}
