namespace helengine {
    /// <summary>
    /// Stores editor-time save metadata for one persisted component.
    /// </summary>
    public class EntityComponentSaveState {
        /// <summary>
        /// Stable asset references keyed by component-specific reference name.
        /// </summary>
        readonly Dictionary<string, SceneAssetReference> AssetReferencesByName;

        /// <summary>
        /// Editor-only platform override metadata keyed by target platform id.
        /// </summary>
        readonly Dictionary<string, EntityComponentPlatformOverrideState> PlatformOverridesById;

        /// <summary>
        /// Initializes a new empty component save-state container.
        /// </summary>
        public EntityComponentSaveState() {
            AssetReferencesByName = new Dictionary<string, SceneAssetReference>(StringComparer.Ordinal);
            PlatformOverridesById = new Dictionary<string, EntityComponentPlatformOverrideState>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Stores one named asset reference for the component.
        /// </summary>
        /// <param name="referenceName">Stable reference slot name.</param>
        /// <param name="reference">Stable asset reference to store.</param>
        public void SetAssetReference(string referenceName, SceneAssetReference reference) {
            if (string.IsNullOrWhiteSpace(referenceName)) {
                throw new ArgumentException("Reference name must be provided.", nameof(referenceName));
            }
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            AssetReferencesByName[referenceName] = reference;
        }

        /// <summary>
        /// Attempts to read one named asset reference from the component state.
        /// </summary>
        /// <param name="referenceName">Stable reference slot name.</param>
        /// <param name="reference">Resolved stable asset reference when found.</param>
        /// <returns>True when the named reference exists.</returns>
        public bool TryGetAssetReference(string referenceName, out SceneAssetReference reference) {
            if (string.IsNullOrWhiteSpace(referenceName)) {
                throw new ArgumentException("Reference name must be provided.", nameof(referenceName));
            }

            return AssetReferencesByName.TryGetValue(referenceName, out reference);
        }

        /// <summary>
        /// Enumerates every asset reference stored in this component save-state.
        /// </summary>
        /// <returns>Stable asset references stored for the component.</returns>
        public IEnumerable<SceneAssetReference> EnumerateAssetReferences() {
            return AssetReferencesByName.Values;
        }

        /// <summary>
        /// Enumerates every named asset reference stored in this component save-state.
        /// </summary>
        /// <returns>Named asset references stored for the component.</returns>
        public IEnumerable<KeyValuePair<string, SceneAssetReference>> EnumerateNamedAssetReferences() {
            return AssetReferencesByName;
        }

        /// <summary>
        /// Stores one named platform override payload for this component.
        /// </summary>
        /// <param name="platformId">Platform identifier that owns the override payload.</param>
        /// <param name="overrideState">Override payload metadata to store.</param>
        public void SetPlatformOverride(string platformId, EntityComponentPlatformOverrideState overrideState) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (overrideState == null) {
                throw new ArgumentNullException(nameof(overrideState));
            }

            overrideState.PlatformId = platformId;
            PlatformOverridesById[platformId] = overrideState;
        }

        /// <summary>
        /// Gets the existing platform override payload for one platform or creates a new one when needed.
        /// </summary>
        /// <param name="platformId">Platform identifier whose override payload should be returned.</param>
        /// <returns>Mutable platform override payload metadata.</returns>
        public EntityComponentPlatformOverrideState GetOrCreatePlatformOverride(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (!PlatformOverridesById.TryGetValue(platformId, out EntityComponentPlatformOverrideState overrideState)) {
                overrideState = new EntityComponentPlatformOverrideState {
                    PlatformId = platformId
                };
                PlatformOverridesById.Add(platformId, overrideState);
            }

            return overrideState;
        }

        /// <summary>
        /// Attempts to read one platform override payload from this component state.
        /// </summary>
        /// <param name="platformId">Platform identifier whose override payload should be resolved.</param>
        /// <param name="overrideState">Resolved platform override payload metadata when one exists.</param>
        /// <returns>True when one platform override payload exists for the supplied platform.</returns>
        public bool TryGetPlatformOverride(string platformId, out EntityComponentPlatformOverrideState overrideState) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return PlatformOverridesById.TryGetValue(platformId, out overrideState);
        }

        /// <summary>
        /// Returns whether one platform override payload exists for this component state.
        /// </summary>
        /// <param name="platformId">Platform identifier whose override payload should be checked.</param>
        /// <returns>True when one override exists for the supplied platform.</returns>
        public bool HasPlatformOverride(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return PlatformOverridesById.ContainsKey(platformId);
        }

        /// <summary>
        /// Enumerates every platform override payload stored in this component state.
        /// </summary>
        /// <returns>Platform override payload metadata stored for this component.</returns>
        public IEnumerable<EntityComponentPlatformOverrideState> EnumeratePlatformOverrides() {
            return PlatformOverridesById.Values;
        }
    }
}
