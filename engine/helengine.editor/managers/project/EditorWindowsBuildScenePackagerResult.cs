using helengine.baseplatform.Manifest;
using helengine.baseplatform.Results;

namespace helengine.editor {
    /// <summary>
    /// Captures the shader packages referenced while packaging a Windows player build.
    /// </summary>
    public sealed class EditorPlatformBuildScenePackagerResult {
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

        /// <summary>Stores the paths actually selected by each scene's packaging rewrites, including material textures and font atlases.</summary>
        readonly IReadOnlyDictionary<string, string[]> SceneCookedDependenciesValue;

        /// <summary>
        /// Initializes a new scene-packaging result.
        /// </summary>
        /// <param name="referencedShaderDependencies">Complete shader dependencies reported by material cooking.</param>
        /// <param name="platformCookWorkItems">Builder-owned platform cook work items discovered while packaging the scenes.</param>
        /// <param name="cookedArtifactDeclarations">Explicit declarations for material and shader files written while packaging scenes.</param>
        /// <param name="sceneCookedDependencies">Runtime paths selected by each canonical scene identity before build-order IDs are resolved.</param>
        public EditorPlatformBuildScenePackagerResult(
            IReadOnlyList<PlatformShaderDependency> referencedShaderDependencies,
            IReadOnlyList<PlatformCookWorkItem> platformCookWorkItems,
            IReadOnlyList<PlatformCookedArtifactDeclaration> cookedArtifactDeclarations,
            IReadOnlyDictionary<string, string[]> sceneCookedDependencies = null) {
            if (referencedShaderDependencies == null) {
                throw new ArgumentNullException(nameof(referencedShaderDependencies));
            } else if (platformCookWorkItems == null) {
                throw new ArgumentNullException(nameof(platformCookWorkItems));
            } else if (cookedArtifactDeclarations == null) {
                throw new ArgumentNullException(nameof(cookedArtifactDeclarations));
            }

            ReferencedShaderDependenciesValue = referencedShaderDependencies.ToArray();
            PlatformCookWorkItemsValue = platformCookWorkItems.ToArray();
            CookedArtifactDeclarationsValue = cookedArtifactDeclarations.ToArray();
            SceneCookedDependenciesValue = sceneCookedDependencies == null
                ? new Dictionary<string, string[]>(StringComparer.Ordinal)
                : sceneCookedDependencies.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
        }

        /// <summary>Gets complete cooked path selections keyed by the canonical identities supplied to PackagePreservingIdentityPaths.</summary>
        public IReadOnlyDictionary<string, string[]> SceneCookedDependencies => SceneCookedDependenciesValue;

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
