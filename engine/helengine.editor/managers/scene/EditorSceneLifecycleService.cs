namespace helengine.editor {
    /// <summary>
    /// Owns the runtime-facing half of the editor scene lifecycle: publishing the currently authored
    /// scene to the runtime scene manager so gameplay systems can resolve loaded-scene ids and roots,
    /// withdrawing it again before the editor replaces or clears the scene, and handing the live
    /// hierarchy to the attached physics runtime. The editor session still drives the workflow and
    /// owns the scene state; this service owns the rules that state must satisfy.
    /// </summary>
    public sealed class EditorSceneLifecycleService {
        /// <summary>
        /// Resolves stable project scene ids for authored scene paths.
        /// </summary>
        EditorProjectSceneCatalogService SceneCatalogService { get; }

        /// <summary>
        /// Initializes one scene-lifecycle service for the current project.
        /// </summary>
        /// <param name="sceneCatalogService">Catalog that maps authored scene paths to stable project scene ids.</param>
        public EditorSceneLifecycleService(EditorProjectSceneCatalogService sceneCatalogService) {
            if (sceneCatalogService == null) {
                throw new ArgumentNullException(nameof(sceneCatalogService));
            }

            SceneCatalogService = sceneCatalogService;
        }

        /// <summary>
        /// Creates one empty scene-owned asset set used before any user scene has been loaded.
        /// </summary>
        /// <returns>Empty scene-owned asset set.</returns>
        public static RuntimeSceneOwnedAssetSet CreateEmptyOwnedAssetSet() {
            return new RuntimeSceneOwnedAssetSet(
                Array.Empty<RuntimeTexture>(),
                Array.Empty<FontAsset>(),
                Array.Empty<AudioAsset>(),
                Array.Empty<RuntimeModel>(),
                Array.Empty<RuntimeMaterial>());
        }

        /// <summary>
        /// Tracks the current editor-authored scene in the runtime scene manager so gameplay systems
        /// can resolve loaded-scene ids and roots exactly as they would in a packaged build.
        /// </summary>
        /// <param name="sceneManager">Runtime scene manager owned by the editor core.</param>
        /// <param name="currentScenePath">Absolute path of the authored scene that is now open.</param>
        /// <param name="rootEntities">Root entities that represent the current editor-authored scene.</param>
        /// <param name="sceneSettings">Scene settings restored from the authored scene file.</param>
        public void TrackCurrentScene(SceneManager sceneManager, string currentScenePath, IReadOnlyList<Entity> rootEntities, SceneSettingsAsset sceneSettings) {
            if (rootEntities == null) {
                throw new ArgumentNullException(nameof(rootEntities));
            }
            if (sceneManager == null) {
                throw new InvalidOperationException("Editor scene tracking requires one initialized runtime scene manager.");
            }
            if (string.IsNullOrWhiteSpace(currentScenePath)) {
                throw new InvalidOperationException("Editor scene tracking requires one current scene path.");
            }

            string sceneId = SceneCatalogService.ResolveSceneId(currentScenePath);
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new InvalidOperationException($"Editor scene '{currentScenePath}' does not resolve to one stable project scene id.");
            }

            bool dontUnload = sceneSettings != null && sceneSettings.DontUnload;
            sceneManager.TrackExternallyLoadedScene(sceneId, rootEntities, dontUnload);
        }

        /// <summary>
        /// Removes the current editor-authored scene from runtime scene-manager tracking before the
        /// editor replaces or clears the scene. A session that never tracked a scene is a no-op.
        /// </summary>
        /// <param name="sceneManager">Runtime scene manager owned by the editor core, or null before core startup.</param>
        /// <param name="currentScenePath">Absolute path of the authored scene that is being replaced, or null when the scene was never saved.</param>
        public void UntrackCurrentScene(SceneManager sceneManager, string currentScenePath) {
            if (sceneManager == null || string.IsNullOrWhiteSpace(currentScenePath)) {
                return;
            }

            string sceneId = SceneCatalogService.ResolveSceneId(currentScenePath);
            if (string.IsNullOrWhiteSpace(sceneId)) {
                return;
            }

            sceneManager.TryUntrackExternallyLoadedScene(sceneId);
        }

        /// <summary>
        /// Binds the current live editor scene hierarchy to the attached physics runtime.
        /// Editing without any physics runtime is supported, but a runtime that is attached and
        /// cannot own an editor scene is a configuration error and is rejected here rather than
        /// leaving the editor with a scene that silently never simulates.
        /// </summary>
        /// <param name="physicsRuntime">Physics runtime attached to the editor core, or null when none is attached.</param>
        /// <param name="rootEntities">Current scene root entities that should define the active bound physics scene.</param>
        public void BindSceneToPhysicsRuntime(IPhysicsRuntime physicsRuntime, IReadOnlyList<Entity> rootEntities) {
            if (rootEntities == null) {
                throw new ArgumentNullException(nameof(rootEntities));
            }
            if (physicsRuntime == null) {
                return;
            }

            ISceneBindablePhysicsRuntime sceneBindablePhysicsRuntime = (ISceneBindablePhysicsRuntime)physicsRuntime;
            sceneBindablePhysicsRuntime.BindScene(rootEntities);
        }
    }
}
