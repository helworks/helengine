namespace helengine {
    /// <summary>
    /// Hidden editor-only component that stores per-component scene persistence metadata for one entity.
    /// </summary>
    public class EntitySaveComponent : Component, IEditorHiddenComponent {
        /// <summary>
        /// Save-state containers keyed by the live component instance they describe.
        /// </summary>
        readonly Dictionary<Component, EntityComponentSaveState> SaveStatesByComponent;
        /// <summary>
        /// Entity existence override payloads grouped by platform and nested environment id.
        /// </summary>
        readonly EditorOverrideScopeMap<SceneEntityPlatformExistenceOverrideAsset> ExistenceOverridesByScope;
        /// <summary>
        /// Transform override payloads grouped by platform and nested environment id.
        /// </summary>
        readonly EditorOverrideScopeMap<SceneEntityPlatformTransformOverrideAsset> TransformOverridesByScope;
        /// <summary>
        /// Component existence override payloads grouped by platform and nested environment id.
        /// </summary>
        readonly EditorOverrideScopeMap<EntityPlatformComponentOverrideState> ComponentOverridesByScope;

        /// <summary>
        /// Stable id used to reference the owning entity from serialized scene data.
        /// </summary>
        public uint EntityId { get; set; }

        /// <summary>
        /// Gets or sets the platform currently projected into the live entity transform while editing in the inspector.
        /// </summary>
        public string ActiveTransformPlatformId { get; set; }

        /// <summary>
        /// Gets or sets the nested environment currently projected into the live entity transform.
        /// </summary>
        public string ActiveTransformEnvironmentId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the common transform snapshot is available while one platform override is projected into the live entity.
        /// </summary>
        public bool HasCommonTransformSnapshot { get; set; }

        /// <summary>
        /// Gets or sets the common local-position snapshot preserved while one platform override is projected into the live entity.
        /// </summary>
        public float3 CommonLocalPositionSnapshot { get; set; }

        /// <summary>
        /// Gets or sets the common local-scale snapshot preserved while one platform override is projected into the live entity.
        /// </summary>
        public float3 CommonLocalScaleSnapshot { get; set; }

        /// <summary>
        /// Gets or sets the common local-orientation snapshot preserved while one platform override is projected into the live entity.
        /// </summary>
        public float4 CommonLocalOrientationSnapshot { get; set; }

        /// <summary>
        /// Initializes a new empty entity save-component.
        /// </summary>
        public EntitySaveComponent() {
            SaveStatesByComponent = new Dictionary<Component, EntityComponentSaveState>();
            ExistenceOverridesByScope = new EditorOverrideScopeMap<SceneEntityPlatformExistenceOverrideAsset>();
            TransformOverridesByScope = new EditorOverrideScopeMap<SceneEntityPlatformTransformOverrideAsset>();
            ComponentOverridesByScope = new EditorOverrideScopeMap<EntityPlatformComponentOverrideState>();
            ActiveTransformPlatformId = string.Empty;
            ActiveTransformEnvironmentId = string.Empty;
        }

        /// <summary>
        /// Gets the existing save state for a component or creates a new one when needed.
        /// </summary>
        /// <param name="component">Component whose save state should be returned.</param>
        /// <returns>Mutable save-state container for the component.</returns>
        public EntityComponentSaveState GetOrCreateComponentState(Component component) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            if (!SaveStatesByComponent.TryGetValue(component, out EntityComponentSaveState saveState)) {
                saveState = new EntityComponentSaveState();
                SaveStatesByComponent.Add(component, saveState);
            }

            return saveState;
        }

        /// <summary>
        /// Attempts to read the save state stored for one component.
        /// </summary>
        /// <param name="component">Component whose save state should be resolved.</param>
        /// <param name="saveState">Save-state container when one exists.</param>
        /// <returns>True when save metadata exists for the supplied component.</returns>
        public bool TryGetComponentState(Component component, out EntityComponentSaveState saveState) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            return SaveStatesByComponent.TryGetValue(component, out saveState);
        }

        /// <summary>
        /// Stores one named asset reference for one component.
        /// </summary>
        /// <param name="component">Component that owns the reference.</param>
        /// <param name="referenceName">Stable reference slot name.</param>
        /// <param name="reference">Stable asset reference to persist.</param>
        public void SetAssetReference(Component component, string referenceName, SceneAssetReference reference) {
            EntityComponentSaveState saveState = GetOrCreateComponentState(component);
            saveState.SetAssetReference(referenceName, reference);
        }

        /// <summary>
        /// Stores one platform entity existence override payload for the owning entity.
        /// </summary>
        /// <param name="platformId">Platform identifier that owns the override payload.</param>
        /// <param name="overrideState">Override payload metadata to store.</param>
        public void SetExistencePlatformOverride(string platformId, SceneEntityPlatformExistenceOverrideAsset overrideState) {
            SetExistencePlatformOverride(new EditorOverrideScope(platformId), overrideState);
        }

        /// <summary>
        /// Stores one platform or nested environment entity existence override payload.
        /// </summary>
        /// <param name="scope">Platform or platform/environment scope that owns the payload.</param>
        /// <param name="overrideState">Override payload metadata to store.</param>
        public void SetExistencePlatformOverride(EditorOverrideScope scope, SceneEntityPlatformExistenceOverrideAsset overrideState) {
            if (overrideState == null) {
                throw new ArgumentNullException(nameof(overrideState));
            }

            overrideState.PlatformId = scope.PlatformId;
            overrideState.EnvironmentId = scope.EnvironmentId;
            ExistenceOverridesByScope.Set(scope, overrideState);
        }

        /// <summary>
        /// Gets the existing platform entity existence override payload for one platform or creates a new one when needed.
        /// </summary>
        /// <param name="platformId">Platform identifier whose entity existence override payload should be returned.</param>
        /// <returns>Mutable platform entity existence override payload metadata.</returns>
        public SceneEntityPlatformExistenceOverrideAsset GetOrCreateExistencePlatformOverride(string platformId) {
            return GetOrCreateExistencePlatformOverride(new EditorOverrideScope(platformId));
        }

        /// <summary>
        /// Gets the existing platform or nested environment entity existence override or creates one when needed.
        /// </summary>
        /// <param name="scope">Platform or platform/environment scope whose payload should be returned.</param>
        /// <returns>Mutable entity existence override payload.</returns>
        public SceneEntityPlatformExistenceOverrideAsset GetOrCreateExistencePlatformOverride(EditorOverrideScope scope) {
            return ExistenceOverridesByScope.GetOrCreate(scope, () => new SceneEntityPlatformExistenceOverrideAsset {
                PlatformId = scope.PlatformId,
                EnvironmentId = scope.EnvironmentId,
                Exists = true
            });
        }

        /// <summary>
        /// Attempts to read one platform entity existence override payload from this entity save state.
        /// </summary>
        /// <param name="platformId">Platform identifier whose entity existence override payload should be resolved.</param>
        /// <param name="overrideState">Resolved platform entity existence override payload when one exists.</param>
        /// <returns>True when one platform entity existence override exists for the supplied platform.</returns>
        public bool TryGetExistencePlatformOverride(string platformId, out SceneEntityPlatformExistenceOverrideAsset overrideState) {
            return TryGetExistencePlatformOverride(new EditorOverrideScope(platformId), out overrideState);
        }

        /// <summary>
        /// Attempts to read one platform or nested environment entity existence override.
        /// </summary>
        /// <param name="scope">Platform or platform/environment scope to resolve.</param>
        /// <param name="overrideState">Resolved override payload when one exists.</param>
        /// <returns>True when one override exists at the supplied scope.</returns>
        public bool TryGetExistencePlatformOverride(EditorOverrideScope scope, out SceneEntityPlatformExistenceOverrideAsset overrideState) {
            return ExistenceOverridesByScope.TryGet(scope, out overrideState);
        }

        /// <summary>
        /// Removes one stored platform entity existence override payload from this entity save state.
        /// </summary>
        /// <param name="platformId">Platform identifier whose entity existence override payload should be removed.</param>
        public void RemoveExistencePlatformOverride(string platformId) {
            RemoveExistencePlatformOverride(new EditorOverrideScope(platformId));
        }

        /// <summary>
        /// Removes one platform or nested environment entity existence override.
        /// </summary>
        /// <param name="scope">Platform or platform/environment scope to remove.</param>
        public void RemoveExistencePlatformOverride(EditorOverrideScope scope) {
            ExistenceOverridesByScope.Remove(scope);
        }

        /// <summary>
        /// Enumerates every platform entity existence override payload stored for this entity.
        /// </summary>
        /// <returns>Platform entity existence override payload metadata stored for the entity.</returns>
        public IEnumerable<SceneEntityPlatformExistenceOverrideAsset> EnumerateExistencePlatformOverrides() {
            return ExistenceOverridesByScope.EnumerateValues();
        }

        /// <summary>
        /// Stores one platform transform override payload for the owning entity.
        /// </summary>
        /// <param name="platformId">Platform identifier that owns the override payload.</param>
        /// <param name="overrideState">Override payload metadata to store.</param>
        public void SetTransformPlatformOverride(string platformId, SceneEntityPlatformTransformOverrideAsset overrideState) {
            SetTransformPlatformOverride(new EditorOverrideScope(platformId), overrideState);
        }

        /// <summary>
        /// Stores one platform or nested environment transform override payload.
        /// </summary>
        /// <param name="scope">Platform or platform/environment scope that owns the payload.</param>
        /// <param name="overrideState">Override payload metadata to store.</param>
        public void SetTransformPlatformOverride(EditorOverrideScope scope, SceneEntityPlatformTransformOverrideAsset overrideState) {
            if (overrideState == null) {
                throw new ArgumentNullException(nameof(overrideState));
            }

            overrideState.PlatformId = scope.PlatformId;
            overrideState.EnvironmentId = scope.EnvironmentId;
            TransformOverridesByScope.Set(scope, overrideState);
        }

        /// <summary>
        /// Gets the existing platform transform override payload for one platform or creates a new one when needed.
        /// </summary>
        /// <param name="platformId">Platform identifier whose transform override payload should be returned.</param>
        /// <returns>Mutable platform transform override payload metadata.</returns>
        public SceneEntityPlatformTransformOverrideAsset GetOrCreateTransformPlatformOverride(string platformId) {
            return GetOrCreateTransformPlatformOverride(new EditorOverrideScope(platformId));
        }

        /// <summary>
        /// Gets the existing platform or nested environment transform override or creates one when needed.
        /// </summary>
        /// <param name="scope">Platform or platform/environment scope whose payload should be returned.</param>
        /// <returns>Mutable transform override payload.</returns>
        public SceneEntityPlatformTransformOverrideAsset GetOrCreateTransformPlatformOverride(EditorOverrideScope scope) {
            return TransformOverridesByScope.GetOrCreate(scope, () => new SceneEntityPlatformTransformOverrideAsset {
                PlatformId = scope.PlatformId,
                EnvironmentId = scope.EnvironmentId
            });
        }

        /// <summary>
        /// Attempts to read one platform transform override payload from this entity save state.
        /// </summary>
        /// <param name="platformId">Platform identifier whose transform override payload should be resolved.</param>
        /// <param name="overrideState">Resolved platform transform override payload when one exists.</param>
        /// <returns>True when one platform transform override exists for the supplied platform.</returns>
        public bool TryGetTransformPlatformOverride(string platformId, out SceneEntityPlatformTransformOverrideAsset overrideState) {
            return TryGetTransformPlatformOverride(new EditorOverrideScope(platformId), out overrideState);
        }

        /// <summary>
        /// Attempts to read one platform or nested environment transform override.
        /// </summary>
        /// <param name="scope">Platform or platform/environment scope to resolve.</param>
        /// <param name="overrideState">Resolved override payload when one exists.</param>
        /// <returns>True when one override exists at the supplied scope.</returns>
        public bool TryGetTransformPlatformOverride(EditorOverrideScope scope, out SceneEntityPlatformTransformOverrideAsset overrideState) {
            return TransformOverridesByScope.TryGet(scope, out overrideState);
        }

        /// <summary>
        /// Removes one stored platform transform override payload from this entity save state.
        /// </summary>
        /// <param name="platformId">Platform identifier whose transform override payload should be removed.</param>
        public void RemoveTransformPlatformOverride(string platformId) {
            RemoveTransformPlatformOverride(new EditorOverrideScope(platformId));
        }

        /// <summary>
        /// Removes one platform or nested environment transform override.
        /// </summary>
        /// <param name="scope">Platform or platform/environment scope to remove.</param>
        public void RemoveTransformPlatformOverride(EditorOverrideScope scope) {
            TransformOverridesByScope.Remove(scope);
        }

        /// <summary>
        /// Enumerates every platform transform override payload stored for this entity.
        /// </summary>
        /// <returns>Platform transform override payload metadata stored for the entity.</returns>
        public IEnumerable<SceneEntityPlatformTransformOverrideAsset> EnumerateTransformPlatformOverrides() {
            return TransformOverridesByScope.EnumerateValues();
        }

        /// <summary>
        /// Gets the existing platform component existence override payload for one platform or creates a new one when needed.
        /// </summary>
        /// <param name="platformId">Platform identifier whose component override payload should be returned.</param>
        /// <returns>Mutable platform component existence override payload metadata.</returns>
        public EntityPlatformComponentOverrideState GetOrCreateComponentPlatformOverride(string platformId) {
            return GetOrCreateComponentPlatformOverride(new EditorOverrideScope(platformId));
        }

        /// <summary>
        /// Gets the existing platform or nested environment component existence override or creates one when needed.
        /// </summary>
        /// <param name="scope">Platform or platform/environment scope whose payload should be returned.</param>
        /// <returns>Mutable component existence override payload.</returns>
        public EntityPlatformComponentOverrideState GetOrCreateComponentPlatformOverride(EditorOverrideScope scope) {
            return ComponentOverridesByScope.GetOrCreate(scope, () => new EntityPlatformComponentOverrideState {
                PlatformId = scope.PlatformId,
                EnvironmentId = scope.EnvironmentId
            });
        }

        /// <summary>
        /// Attempts to read one platform component existence override payload from this entity save state.
        /// </summary>
        /// <param name="platformId">Platform identifier whose component override payload should be resolved.</param>
        /// <param name="overrideState">Resolved platform component override payload when one exists.</param>
        /// <returns>True when one platform component override exists for the supplied platform.</returns>
        public bool TryGetComponentPlatformOverride(string platformId, out EntityPlatformComponentOverrideState overrideState) {
            return TryGetComponentPlatformOverride(new EditorOverrideScope(platformId), out overrideState);
        }

        /// <summary>
        /// Attempts to read one platform or nested environment component existence override.
        /// </summary>
        /// <param name="scope">Platform or platform/environment scope to resolve.</param>
        /// <param name="overrideState">Resolved override payload when one exists.</param>
        /// <returns>True when one override exists at the supplied scope.</returns>
        public bool TryGetComponentPlatformOverride(EditorOverrideScope scope, out EntityPlatformComponentOverrideState overrideState) {
            return ComponentOverridesByScope.TryGet(scope, out overrideState);
        }

        /// <summary>
        /// Removes one stored platform component existence override payload from this entity save state.
        /// </summary>
        /// <param name="platformId">Platform identifier whose component override payload should be removed.</param>
        public void RemoveComponentPlatformOverride(string platformId) {
            RemoveComponentPlatformOverride(new EditorOverrideScope(platformId));
        }

        /// <summary>
        /// Removes one platform or nested environment component existence override.
        /// </summary>
        /// <param name="scope">Platform or platform/environment scope to remove.</param>
        public void RemoveComponentPlatformOverride(EditorOverrideScope scope) {
            ComponentOverridesByScope.Remove(scope);
        }

        /// <summary>
        /// Enumerates every platform component existence override payload stored for this entity.
        /// </summary>
        /// <returns>Platform component override payload metadata stored for the entity.</returns>
        public IEnumerable<EntityPlatformComponentOverrideState> EnumerateComponentPlatformOverrides() {
            return ComponentOverridesByScope.EnumerateValues();
        }
    }
}
