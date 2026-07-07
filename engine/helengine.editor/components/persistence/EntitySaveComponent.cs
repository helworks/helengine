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
        /// Entity existence override payloads keyed by their owning platform id.
        /// </summary>
        readonly Dictionary<string, SceneEntityPlatformExistenceOverrideAsset> ExistenceOverridesByPlatformId;
        /// <summary>
        /// Transform override payloads keyed by their owning platform id.
        /// </summary>
        readonly Dictionary<string, SceneEntityPlatformTransformOverrideAsset> TransformOverridesByPlatformId;
        /// <summary>
        /// Component existence override payloads keyed by their owning platform id.
        /// </summary>
        readonly Dictionary<string, EntityPlatformComponentOverrideState> ComponentOverridesByPlatformId;

        /// <summary>
        /// Stable id used to reference the owning entity from serialized scene data.
        /// </summary>
        public uint EntityId { get; set; }

        /// <summary>
        /// Gets or sets the platform currently projected into the live entity transform while editing in the inspector.
        /// </summary>
        public string ActiveTransformPlatformId { get; set; }

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
            ExistenceOverridesByPlatformId = new Dictionary<string, SceneEntityPlatformExistenceOverrideAsset>(StringComparer.OrdinalIgnoreCase);
            TransformOverridesByPlatformId = new Dictionary<string, SceneEntityPlatformTransformOverrideAsset>(StringComparer.OrdinalIgnoreCase);
            ComponentOverridesByPlatformId = new Dictionary<string, EntityPlatformComponentOverrideState>(StringComparer.OrdinalIgnoreCase);
            ActiveTransformPlatformId = string.Empty;
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
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (overrideState == null) {
                throw new ArgumentNullException(nameof(overrideState));
            }

            overrideState.PlatformId = platformId;
            ExistenceOverridesByPlatformId[platformId] = overrideState;
        }

        /// <summary>
        /// Gets the existing platform entity existence override payload for one platform or creates a new one when needed.
        /// </summary>
        /// <param name="platformId">Platform identifier whose entity existence override payload should be returned.</param>
        /// <returns>Mutable platform entity existence override payload metadata.</returns>
        public SceneEntityPlatformExistenceOverrideAsset GetOrCreateExistencePlatformOverride(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (!ExistenceOverridesByPlatformId.TryGetValue(platformId, out SceneEntityPlatformExistenceOverrideAsset overrideState)) {
                overrideState = new SceneEntityPlatformExistenceOverrideAsset {
                    PlatformId = platformId,
                    Exists = true
                };
                ExistenceOverridesByPlatformId.Add(platformId, overrideState);
            }

            return overrideState;
        }

        /// <summary>
        /// Attempts to read one platform entity existence override payload from this entity save state.
        /// </summary>
        /// <param name="platformId">Platform identifier whose entity existence override payload should be resolved.</param>
        /// <param name="overrideState">Resolved platform entity existence override payload when one exists.</param>
        /// <returns>True when one platform entity existence override exists for the supplied platform.</returns>
        public bool TryGetExistencePlatformOverride(string platformId, out SceneEntityPlatformExistenceOverrideAsset overrideState) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return ExistenceOverridesByPlatformId.TryGetValue(platformId, out overrideState);
        }

        /// <summary>
        /// Removes one stored platform entity existence override payload from this entity save state.
        /// </summary>
        /// <param name="platformId">Platform identifier whose entity existence override payload should be removed.</param>
        public void RemoveExistencePlatformOverride(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            ExistenceOverridesByPlatformId.Remove(platformId);
        }

        /// <summary>
        /// Enumerates every platform entity existence override payload stored for this entity.
        /// </summary>
        /// <returns>Platform entity existence override payload metadata stored for the entity.</returns>
        public IEnumerable<SceneEntityPlatformExistenceOverrideAsset> EnumerateExistencePlatformOverrides() {
            return ExistenceOverridesByPlatformId.Values;
        }

        /// <summary>
        /// Stores one platform transform override payload for the owning entity.
        /// </summary>
        /// <param name="platformId">Platform identifier that owns the override payload.</param>
        /// <param name="overrideState">Override payload metadata to store.</param>
        public void SetTransformPlatformOverride(string platformId, SceneEntityPlatformTransformOverrideAsset overrideState) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (overrideState == null) {
                throw new ArgumentNullException(nameof(overrideState));
            }

            overrideState.PlatformId = platformId;
            TransformOverridesByPlatformId[platformId] = overrideState;
        }

        /// <summary>
        /// Gets the existing platform transform override payload for one platform or creates a new one when needed.
        /// </summary>
        /// <param name="platformId">Platform identifier whose transform override payload should be returned.</param>
        /// <returns>Mutable platform transform override payload metadata.</returns>
        public SceneEntityPlatformTransformOverrideAsset GetOrCreateTransformPlatformOverride(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (!TransformOverridesByPlatformId.TryGetValue(platformId, out SceneEntityPlatformTransformOverrideAsset overrideState)) {
                overrideState = new SceneEntityPlatformTransformOverrideAsset {
                    PlatformId = platformId
                };
                TransformOverridesByPlatformId.Add(platformId, overrideState);
            }

            return overrideState;
        }

        /// <summary>
        /// Attempts to read one platform transform override payload from this entity save state.
        /// </summary>
        /// <param name="platformId">Platform identifier whose transform override payload should be resolved.</param>
        /// <param name="overrideState">Resolved platform transform override payload when one exists.</param>
        /// <returns>True when one platform transform override exists for the supplied platform.</returns>
        public bool TryGetTransformPlatformOverride(string platformId, out SceneEntityPlatformTransformOverrideAsset overrideState) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return TransformOverridesByPlatformId.TryGetValue(platformId, out overrideState);
        }

        /// <summary>
        /// Removes one stored platform transform override payload from this entity save state.
        /// </summary>
        /// <param name="platformId">Platform identifier whose transform override payload should be removed.</param>
        public void RemoveTransformPlatformOverride(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            TransformOverridesByPlatformId.Remove(platformId);
        }

        /// <summary>
        /// Enumerates every platform transform override payload stored for this entity.
        /// </summary>
        /// <returns>Platform transform override payload metadata stored for the entity.</returns>
        public IEnumerable<SceneEntityPlatformTransformOverrideAsset> EnumerateTransformPlatformOverrides() {
            return TransformOverridesByPlatformId.Values;
        }

        /// <summary>
        /// Gets the existing platform component existence override payload for one platform or creates a new one when needed.
        /// </summary>
        /// <param name="platformId">Platform identifier whose component override payload should be returned.</param>
        /// <returns>Mutable platform component existence override payload metadata.</returns>
        public EntityPlatformComponentOverrideState GetOrCreateComponentPlatformOverride(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (!ComponentOverridesByPlatformId.TryGetValue(platformId, out EntityPlatformComponentOverrideState overrideState)) {
                overrideState = new EntityPlatformComponentOverrideState {
                    PlatformId = platformId
                };
                ComponentOverridesByPlatformId.Add(platformId, overrideState);
            }

            return overrideState;
        }

        /// <summary>
        /// Attempts to read one platform component existence override payload from this entity save state.
        /// </summary>
        /// <param name="platformId">Platform identifier whose component override payload should be resolved.</param>
        /// <param name="overrideState">Resolved platform component override payload when one exists.</param>
        /// <returns>True when one platform component override exists for the supplied platform.</returns>
        public bool TryGetComponentPlatformOverride(string platformId, out EntityPlatformComponentOverrideState overrideState) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return ComponentOverridesByPlatformId.TryGetValue(platformId, out overrideState);
        }

        /// <summary>
        /// Removes one stored platform component existence override payload from this entity save state.
        /// </summary>
        /// <param name="platformId">Platform identifier whose component override payload should be removed.</param>
        public void RemoveComponentPlatformOverride(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            ComponentOverridesByPlatformId.Remove(platformId);
        }

        /// <summary>
        /// Enumerates every platform component existence override payload stored for this entity.
        /// </summary>
        /// <returns>Platform component override payload metadata stored for the entity.</returns>
        public IEnumerable<EntityPlatformComponentOverrideState> EnumerateComponentPlatformOverrides() {
            return ComponentOverridesByPlatformId.Values;
        }
    }
}
