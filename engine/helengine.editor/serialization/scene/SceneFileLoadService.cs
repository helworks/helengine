namespace helengine.editor {
    /// <summary>
    /// Reads one `.helen` file from disk and materializes editor entities from it.
    /// </summary>
    public class SceneFileLoadService : IDisposable {
        /// <summary>
        /// Absolute path to the project root.
        /// </summary>
        readonly string ProjectRootPath;
        /// <summary>
        /// Scene asset resolver used to rebuild runtime-backed asset references.
        /// </summary>
        readonly ISceneAssetReferenceResolver ReferenceResolver;
        /// <summary>Session-owned generated material cache used by editor-only scene visuals.</summary>
        readonly EngineGeneratedMaterialCache GeneratedMaterialCache;
        readonly ObjectManager ObjectManager;
        readonly EditorSessionRendererResources RendererResources;
        /// <summary>
        /// Scene-load service that reconstructs entities from scene assets.
        /// </summary>
        readonly SceneLoadService SceneLoadService;

        /// <summary>
        /// Initializes a new scene-file load service.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the assets folder.</param>
        /// <param name="persistenceRegistry">Registry used to deserialize persisted components.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime-backed assets.</param>
        public SceneFileLoadService(
            string projectRootPath,
            ComponentPersistenceRegistry persistenceRegistry,
            ISceneAssetReferenceResolver referenceResolver,
            EngineGeneratedMaterialCache generatedMaterialCache,
            EditorSessionRendererResources rendererResources) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (persistenceRegistry == null) {
                throw new ArgumentNullException(nameof(persistenceRegistry));
            }
            if (referenceResolver == null) {
                throw new ArgumentNullException(nameof(referenceResolver));
            }
            if (generatedMaterialCache == null) {
                throw new ArgumentNullException(nameof(generatedMaterialCache));
            }
            if (rendererResources == null) {
                throw new ArgumentNullException(nameof(rendererResources));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            ReferenceResolver = referenceResolver;
            GeneratedMaterialCache = generatedMaterialCache;
            RendererResources = rendererResources;
            ObjectManager = rendererResources.ObjectManager ?? throw new InvalidOperationException("Scene loading resources must provide an object manager.");
            SceneLoadService = new SceneLoadService(ProjectRootPath, persistenceRegistry, referenceResolver, generatedMaterialCache, rendererResources);
        }

        /// <summary>
        /// Releases resolver state owned by the scene loader.
        /// </summary>
        public void Dispose() {
            SceneLoadService.Dispose();
        }

        /// <summary>
        /// Expands one live blueprint instance root into inherited children.
        /// </summary>
        /// <param name="instanceRoot">Scene-owned blueprint instance root to expand.</param>
        public void ExpandBlueprintInstanceRoot(EditorEntity instanceRoot) {
            SceneLoadService.ExpandBlueprintInstanceRoot(instanceRoot);
        }

        /// <summary>
        /// Loads one `.helen` scene file from disk.
        /// </summary>
        /// <param name="fullPath">Absolute path to the scene file.</param>
        /// <returns>Loaded editor scene document.</returns>
        public LoadedEditorSceneDocument Load(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Scene path must be provided.", nameof(fullPath));
            }

            string normalizedPath = Path.GetFullPath(fullPath);
            HashSet<Entity> existingEntities = new HashSet<Entity>(ObjectManager.Entities);
            string previousAssetPath = EngineBinaryReadContext.CurrentAssetPath;
            try {
                if (!normalizedPath.StartsWith(ProjectRootPath, StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidOperationException("Scene path must be inside the current project.");
                }

                EngineBinaryReadContext.CurrentAssetPath = normalizedPath;
                using MemoryStream stream = new MemoryStream(
                    EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, normalizedPath),
                    writable: false);
                Asset deserializedAsset = AssetSerializer.Deserialize(stream);
                if (deserializedAsset is not SceneAsset sceneAsset) {
                    throw new InvalidOperationException("Scene file did not deserialize into a SceneAsset.");
                }

                return LoadSceneAsset(sceneAsset, normalizedPath, existingEntities);
            } catch (Exception ex) {
                CleanupFailedLoad(existingEntities);
                throw new InvalidOperationException($"Scene load failed: {ex.Message}", ex);
            } finally {
                EngineBinaryReadContext.CurrentAssetPath = previousAssetPath;
            }
        }

        /// <summary>
        /// Loads one already materialized scene asset into editor entities using the same asset-resolution pipeline as on-disk scene files.
        /// </summary>
        /// <param name="sceneAsset">Scene asset payload that should be materialized.</param>
        /// <param name="assetPath">Synthetic or on-disk path used as the active read context while the scene materializes.</param>
        /// <returns>Loaded editor scene document.</returns>
        public LoadedEditorSceneDocument Load(SceneAsset sceneAsset, string assetPath) {
            if (sceneAsset == null) {
                throw new ArgumentNullException(nameof(sceneAsset));
            }
            if (string.IsNullOrWhiteSpace(assetPath)) {
                throw new ArgumentException("Asset path must be provided.", nameof(assetPath));
            }

            string normalizedPath = Path.GetFullPath(assetPath);
            if (!normalizedPath.StartsWith(ProjectRootPath, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("History scene materialization must stay inside the current project.");
            }

            HashSet<Entity> existingEntities = new HashSet<Entity>(ObjectManager.Entities);
            string previousAssetPath = EngineBinaryReadContext.CurrentAssetPath;
            try {
                return LoadSceneAsset(sceneAsset, normalizedPath, existingEntities);
            } catch (Exception ex) {
                CleanupFailedLoad(existingEntities);
                throw new InvalidOperationException($"Scene load failed: {ex.Message}", ex);
            } finally {
                EngineBinaryReadContext.CurrentAssetPath = previousAssetPath;
            }
        }

        /// <summary>
        /// Applies the enabled state to each loaded root entity.
        /// </summary>
        /// <param name="roots">Loaded root entities to update.</param>
        /// <param name="enabled">Enabled state applied to every root.</param>
        void SetRootsEnabled(IReadOnlyList<EditorEntity> roots, bool enabled) {
            if (roots == null) {
                throw new ArgumentNullException(nameof(roots));
            }

            for (int i = 0; i < roots.Count; i++) {
                if (roots[i] == null) {
                    throw new InvalidOperationException("Loaded scene contained a null root entity.");
                }

                roots[i].Enabled = enabled;
            }
        }

        /// <summary>
        /// Removes entities created during a failed scene load attempt.
        /// </summary>
        /// <param name="existingEntities">Entities that existed before load started.</param>
        void CleanupFailedLoad(HashSet<Entity> existingEntities) {
            if (existingEntities == null) {
                throw new ArgumentNullException(nameof(existingEntities));
            }

            List<EditorEntity> newRootEntities = new List<EditorEntity>();
            List<Entity> liveEntities = new List<Entity>(ObjectManager.Entities);
            for (int i = 0; i < liveEntities.Count; i++) {
                if (liveEntities[i] is not EditorEntity editorEntity) {
                    continue;
                }
                if (existingEntities.Contains(editorEntity)) {
                    continue;
                }
                if (editorEntity.InternalEntity) {
                    continue;
                }
                if (editorEntity.Parent != null) {
                    continue;
                }

                newRootEntities.Add(editorEntity);
            }

            for (int index = newRootEntities.Count - 1; index >= 0; index--) {
                NativeOwnership.DisposeAndDelete(newRootEntities[index]);
            }
        }

        /// <summary>
        /// Creates one empty scene-owned asset set for resolvers that do not track materialized runtime assets.
        /// </summary>
        /// <returns>Empty scene-owned asset set.</returns>
        static RuntimeSceneOwnedAssetSet CreateEmptyOwnedAssetSet() {
            return new RuntimeSceneOwnedAssetSet(
                Array.Empty<RuntimeTexture>(),
                Array.Empty<FontAsset>(),
                Array.Empty<AudioAsset>(),
                Array.Empty<RuntimeModel>(),
                Array.Empty<RuntimeMaterial>());
        }

        /// <summary>
        /// Materializes one scene asset payload into editor entities while tracking any runtime assets owned by the loaded scene.
        /// </summary>
        /// <param name="sceneAsset">Scene asset payload that should be materialized.</param>
        /// <param name="assetPath">Active asset path used for the read context while the scene loads.</param>
        /// <param name="existingEntities">Snapshot of entities that existed before load started.</param>
        /// <returns>Loaded editor scene document.</returns>
        LoadedEditorSceneDocument LoadSceneAsset(SceneAsset sceneAsset, string assetPath, HashSet<Entity> existingEntities) {
            if (sceneAsset == null) {
                throw new ArgumentNullException(nameof(sceneAsset));
            }
            if (string.IsNullOrWhiteSpace(assetPath)) {
                throw new ArgumentException("Asset path must be provided.", nameof(assetPath));
            }
            if (existingEntities == null) {
                throw new ArgumentNullException(nameof(existingEntities));
            }

            IEditorOwnedAssetTrackingSceneAssetReferenceResolver ownedAssetTrackingResolver = ReferenceResolver as IEditorOwnedAssetTrackingSceneAssetReferenceResolver;
            IEditorAssetReferenceHealingResolver healingResolver = ReferenceResolver as IEditorAssetReferenceHealingResolver;
            bool ownedAssetTrackingStarted = false;
            bool healingStarted = false;
            try {
                ownedAssetTrackingResolver?.BeginOwnedAssetTracking();
                ownedAssetTrackingStarted = ownedAssetTrackingResolver != null;
                healingResolver?.BeginReferenceHealing();
                healingStarted = healingResolver != null;
                EngineBinaryReadContext.CurrentAssetPath = assetPath;
                IReadOnlyList<EditorEntity> loadedRoots = SceneLoadService.Load(sceneAsset);
                EditorEntity[] rootEntityArray = loadedRoots.ToArray();
                SetRootsEnabled(rootEntityArray, false);
                IReadOnlyDictionary<SceneAssetReference, SceneAssetReference> replacements = healingStarted
                    ? healingResolver.CompleteReferenceHealing()
                    : new Dictionary<SceneAssetReference, SceneAssetReference>();
                healingStarted = false;
                bool referencesHealed = replacements.Count > 0 && new SceneAssetReferenceHealingService().Apply(rootEntityArray, replacements);
                RuntimeSceneOwnedAssetSet ownedAssets = ownedAssetTrackingResolver != null
                    ? ownedAssetTrackingResolver.CompleteOwnedAssetTracking()
                    : CreateEmptyOwnedAssetSet();
                return new LoadedEditorSceneDocument {
                    RootEntities = rootEntityArray,
                    SceneSettings = sceneAsset.SceneSettings,
                    OwnedAssets = ownedAssets,
                    ReferencesHealed = referencesHealed
                };
            } catch {
                if (healingStarted) {
                    healingResolver.CancelReferenceHealing();
                }
                if (ownedAssetTrackingStarted) {
                    RuntimeSceneOwnedAssetSet ownedAssets = ownedAssetTrackingResolver.CancelOwnedAssetTracking();
                    EditorSceneOwnedAssetReleaseService.ReleaseOwnedAssets(ownedAssets, RendererResources);
                }

                throw;
            }
        }
    }
}
