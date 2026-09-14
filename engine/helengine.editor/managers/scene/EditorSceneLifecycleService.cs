namespace helengine.editor {
    /// <summary>
    /// Owns the editor scene lifecycle state and the runtime-facing half of the lifecycle itself:
    /// which scene is open, the settings and runtime assets that scene brought with it, whether it
    /// carries unsaved changes, and which transition is waiting on the unsaved-changes guard. It also
    /// publishes the authored scene to the runtime scene manager so gameplay systems can resolve
    /// loaded-scene ids and roots, withdraws it again before the editor replaces or clears the scene,
    /// and hands the live hierarchy to the attached physics runtime. The editor session composes this
    /// service and forwards to it; the state lives here so scene rules and scene state stay together.
    /// </summary>
    public sealed class EditorSceneLifecycleService {
        /// <summary>
        /// Resolves stable project scene ids for authored scene paths.
        /// </summary>
        EditorProjectSceneCatalogService SceneCatalogService { get; }

        /// <summary>
        /// Absolute path to the current scene file, or an empty string until the scene has been saved.
        /// </summary>
        public string CurrentScenePath { get; set; }

        /// <summary>
        /// Scene-level settings tracked for the active editor scene.
        /// </summary>
        public SceneSettingsAsset CurrentSceneSettings { get; set; }

        /// <summary>
        /// Runtime assets owned by the currently loaded editor scene and released when the scene is replaced.
        /// </summary>
        public RuntimeSceneOwnedAssetSet CurrentSceneOwnedAssets { get; set; }

        /// <summary>
        /// True when the current scene contains unsaved editor changes.
        /// </summary>
        public bool IsSceneDirty { get; set; }

        /// <summary>
        /// Pending scene transition waiting on the unsaved-changes guard or save flow.
        /// </summary>
        public SceneTransitionKind PendingSceneTransition { get; set; }

        /// <summary>
        /// Absolute path that should be opened when the pending transition resumes.
        /// </summary>
        public string PendingOpenScenePath { get; set; }

        /// <summary>
        /// Initializes one scene-lifecycle service for the current project. Only the two path fields
        /// start at an empty string, because "no scene" and "no pending open path" are real states;
        /// the scene settings and owned-asset set stay unset until the session loads or resets a
        /// scene, so a session that never established its scene baseline cannot be mistaken for one
        /// holding an empty scene.
        /// </summary>
        /// <param name="sceneCatalogService">Catalog that maps authored scene paths to stable project scene ids.</param>
        public EditorSceneLifecycleService(EditorProjectSceneCatalogService sceneCatalogService) {
            if (sceneCatalogService == null) {
                throw new ArgumentNullException(nameof(sceneCatalogService));
            }

            SceneCatalogService = sceneCatalogService;
            CurrentScenePath = string.Empty;
            PendingOpenScenePath = string.Empty;
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
        /// <param name="rootEntities">Root entities that represent the current editor-authored scene.</param>
        /// <param name="sceneSettings">Scene settings restored from the authored scene file.</param>
        public void TrackCurrentScene(SceneManager sceneManager, IReadOnlyList<Entity> rootEntities, SceneSettingsAsset sceneSettings) {
            if (rootEntities == null) {
                throw new ArgumentNullException(nameof(rootEntities));
            }
            if (sceneManager == null) {
                throw new InvalidOperationException("Editor scene tracking requires one initialized runtime scene manager.");
            }
            if (string.IsNullOrWhiteSpace(CurrentScenePath)) {
                throw new InvalidOperationException("Editor scene tracking requires one current scene path.");
            }

            string sceneId = SceneCatalogService.ResolveSceneId(CurrentScenePath);
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new InvalidOperationException($"Editor scene '{CurrentScenePath}' does not resolve to one stable project scene id.");
            }

            bool dontUnload = sceneSettings != null && sceneSettings.DontUnload;
            sceneManager.TrackExternallyLoadedScene(sceneId, rootEntities, dontUnload);
        }

        /// <summary>
        /// Removes the current editor-authored scene from runtime scene-manager tracking before the
        /// editor replaces or clears the scene. A session that never tracked a scene is a no-op.
        /// </summary>
        /// <param name="sceneManager">Runtime scene manager owned by the editor core, or null before core startup.</param>
        public void UntrackCurrentScene(SceneManager sceneManager) {
            if (sceneManager == null || string.IsNullOrWhiteSpace(CurrentScenePath)) {
                return;
            }

            string sceneId = SceneCatalogService.ResolveSceneId(CurrentScenePath);
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

        /// <summary>
        /// Records the scene transition that should run once the unsaved-changes guard is resolved.
        /// </summary>
        /// <param name="transitionKind">Transition that should continue once the guard is resolved.</param>
        /// <param name="openPath">Absolute scene path opened when resuming an open-map transition, or null for none.</param>
        public void BeginSceneTransition(SceneTransitionKind transitionKind, string openPath) {
            PendingSceneTransition = transitionKind;
            if (openPath == null) {
                PendingOpenScenePath = string.Empty;
            } else {
                PendingOpenScenePath = openPath;
            }
        }

        /// <summary>
        /// Drops any recorded scene transition so a resolved or abandoned guard cannot resume later.
        /// </summary>
        public void ClearPendingSceneTransition() {
            PendingSceneTransition = SceneTransitionKind.None;
            PendingOpenScenePath = string.Empty;
        }

        /// <summary>
        /// Resolves the scene that should be reopened for this project at startup. A recorded scene that
        /// no longer exists on disk is not restorable, and neither is a session with no recorded state.
        /// </summary>
        /// <param name="sessionStateService">Per-project session state store, or null when the session keeps none.</param>
        /// <returns>Absolute path of the scene to reopen, or an empty string when nothing should be reopened.</returns>
        public string ResolveRestorableScenePath(EditorSessionStateService sessionStateService) {
            if (sessionStateService == null) {
                return string.Empty;
            }

            string lastScenePath = sessionStateService.TryGetLastScenePath();
            if (string.IsNullOrWhiteSpace(lastScenePath) || !File.Exists(lastScenePath)) {
                return string.Empty;
            }

            return lastScenePath;
        }
    }
}
