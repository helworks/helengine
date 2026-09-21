namespace helengine.editor {
    /// <summary>
    /// Enumerates project scenes that can be selected by the local build dialog.
    /// </summary>
    public sealed class EditorProjectSceneCatalogService : ISceneIdPathResolver {
        const string GeneratedSceneProviderId = "editor.scene-catalog";
        /// <summary>
        /// Absolute project root containing the `assets` directory.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Absolute path to the project's `assets` directory.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Initializes one scene catalog service for the supplied project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        public EditorProjectSceneCatalogService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
        }

        /// <summary>
        /// Gets the project-relative scene identifiers available beneath the `assets` directory.
        /// </summary>
        /// <returns>Sorted project-relative scene identifiers using forward slashes.</returns>
        public IReadOnlyList<string> GetSceneIds() {
            if (!Directory.Exists(AssetsRootPath)) {
                return [];
            }

            string[] scenePaths = Directory.GetFiles(AssetsRootPath, "*.helen", SearchOption.AllDirectories);
            Dictionary<string, string> sceneIds = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < scenePaths.Length; index++) {
                string sceneId = ResolveSceneId(scenePaths[index]);
                if (!string.IsNullOrWhiteSpace(sceneId)) {
                    if (sceneIds.TryGetValue(sceneId, out string existingScenePath)) {
                        throw CreateDuplicateSceneIdException(sceneId, existingScenePath, scenePaths[index]);
                    }

                    sceneIds.Add(sceneId, scenePaths[index]);
                }
            }

            List<string> orderedSceneIds = new List<string>(sceneIds.Keys);
            orderedSceneIds.Sort(StringComparer.Ordinal);
            return orderedSceneIds;
        }

        /// <summary>
        /// Resolves one absolute scene path to its stable scene identifier.
        /// </summary>
        /// <param name="scenePath">Absolute scene path to resolve.</param>
        /// <returns>Stable scene identifier, or an empty string when the path is outside `assets`.</returns>
        public string ResolveSceneId(string scenePath) {
            if (string.IsNullOrWhiteSpace(scenePath)) {
                return string.Empty;
            }

            string fullScenePath = Path.GetFullPath(scenePath);
            string fullAssetsRootPath = EnsureTrailingDirectorySeparator(AssetsRootPath);
            if (!fullScenePath.StartsWith(fullAssetsRootPath, StringComparison.OrdinalIgnoreCase)) {
                return string.Empty;
            }

            return SceneIdUtility.FromPath(fullScenePath);
        }

        /// <summary>
        /// Resolves one stable scene id to its project-relative authored scene path.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier derived from the authored scene file name.</param>
        /// <returns>Project-relative authored scene path using forward slashes.</returns>
        public string ResolveScenePath(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            if (!Directory.Exists(AssetsRootPath)) {
                throw new InvalidOperationException($"Project assets directory '{AssetsRootPath}' does not exist.");
            }

            string[] scenePaths = Directory.GetFiles(AssetsRootPath, "*.helen", SearchOption.AllDirectories);
            string resolvedScenePath = string.Empty;
            for (int index = 0; index < scenePaths.Length; index++) {
                string candidateSceneId = ResolveSceneId(scenePaths[index]);
                if (!string.Equals(candidateSceneId, sceneId, StringComparison.Ordinal)) {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(resolvedScenePath)) {
                    throw CreateDuplicateSceneIdException(sceneId, resolvedScenePath, scenePaths[index]);
                }

                resolvedScenePath = scenePaths[index];
            }

            if (string.IsNullOrWhiteSpace(resolvedScenePath)) {
                throw new InvalidOperationException($"Unable to resolve scene id '{sceneId}' to an authored scene path.");
            }

            return Path.GetRelativePath(AssetsRootPath, resolvedScenePath).Replace('\\', '/');
        }

        /// <summary>Creates the current stable reference for an authored or generated scene id.</summary>
        public SceneAssetReference CreateSceneReference(string sceneId) {
            return CreateSceneReferences(new[] { sceneId })[0];
        }

        /// <summary>
        /// Captures an ordered batch of scene references using one asset resolver, avoiding a full asset-index initialization for every scene.
        /// </summary>
        /// <param name="sceneIds">Scene identifiers in caller order; duplicates are preserved.</param>
        /// <returns>Current authored or generated references in the same order.</returns>
        public List<SceneAssetReference> CreateSceneReferences(IReadOnlyList<string> sceneIds) {
            if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            }

            List<SceneAssetReference> references = new List<SceneAssetReference>(sceneIds.Count);
            EditorAssetReferenceResolver resolver = null;
            try {
                for (int index = 0; index < sceneIds.Count; index++) {
                    string sceneId = sceneIds[index];
                    if (string.IsNullOrWhiteSpace(sceneId)) {
                        throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
                    }

                    if (GetSceneIds().Contains(sceneId, StringComparer.Ordinal)) {
                        string relativePath = ResolveScenePath(sceneId);
                        string fullPath = Path.Combine(AssetsRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                        if (resolver == null) {
                            resolver = new EditorAssetReferenceResolver(ProjectRootPath);
                        }
                        references.Add(resolver.CreateFileReference(fullPath, AssetEntryKind.Scene));
                    } else {
                        references.Add(global::helengine.SceneAssetReferenceFactory.Rehydrate(
                            SceneAssetReferenceSourceKind.Generated,
                            sceneId,
                            GeneratedSceneProviderId,
                            sceneId));
                    }
                }
            } finally {
                resolver?.Dispose();
            }
            return references;
        }

        /// <summary>Resolves a persisted stable scene reference back to the operational scene id.</summary>
        public string ResolveSceneId(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            return ResolveSceneIds(new[] { reference })[0];
        }

        /// <summary>
        /// Resolves an ordered batch of persisted scene references using one asset resolver, so a build
        /// configuration with hundreds of references initializes the asset identity index once instead of per reference.
        /// </summary>
        /// <param name="references">Scene references in caller order; duplicates are preserved.</param>
        /// <returns>Operational scene ids in the same order.</returns>
        public List<string> ResolveSceneIds(IReadOnlyList<SceneAssetReference> references) {
            if (references == null) {
                throw new ArgumentNullException(nameof(references));
            }

            List<string> sceneIds = new List<string>(references.Count);
            EditorAssetReferenceResolver resolver = null;
            try {
                for (int index = 0; index < references.Count; index++) {
                    SceneAssetReference reference = references[index];
                    if (reference == null) {
                        throw new ArgumentException("Scene reference must be provided.", nameof(references));
                    }

                    if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                        if (!string.Equals(reference.ProviderId, GeneratedSceneProviderId, StringComparison.Ordinal)) {
                            throw new InvalidOperationException($"Unsupported generated scene provider '{reference.ProviderId}'.");
                        }
                        sceneIds.Add(reference.AssetId);
                        continue;
                    }

                    if (resolver == null) {
                        resolver = new EditorAssetReferenceResolver(ProjectRootPath);
                    }
                    AssetReferenceResolution resolution = resolver.Resolve(reference, AssetEntryKind.Scene);
                    sceneIds.Add(ResolveSceneId(resolution.FullPath));
                }
            } finally {
                resolver?.Dispose();
            }

            return sceneIds;
        }

        /// <summary>
        /// Creates one duplicate-scene-id exception that includes both conflicting authored scene paths.
        /// </summary>
        /// <param name="sceneId">Duplicate stable scene identifier.</param>
        /// <param name="firstScenePath">First authored scene path that produced the duplicate id.</param>
        /// <param name="secondScenePath">Second authored scene path that produced the duplicate id.</param>
        /// <returns>Exception describing the conflicting authored scene assets.</returns>
        InvalidOperationException CreateDuplicateSceneIdException(string sceneId, string firstScenePath, string secondScenePath) {
            string firstRelativeScenePath = Path.GetRelativePath(AssetsRootPath, firstScenePath).Replace('\\', '/');
            string secondRelativeScenePath = Path.GetRelativePath(AssetsRootPath, secondScenePath).Replace('\\', '/');
            return new InvalidOperationException($"Scene id '{sceneId}' is ambiguous because both '{firstRelativeScenePath}' and '{secondRelativeScenePath}' derive the same id.");
        }

        /// <summary>
        /// Ensures one directory path ends with a directory separator before prefix comparisons occur.
        /// </summary>
        /// <param name="path">Directory path that should end with a separator.</param>
        /// <returns>Directory path with a trailing separator.</returns>
        string EnsureTrailingDirectorySeparator(string path) {
            if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)) {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}
