namespace helengine {
    /// <summary>
    /// Stores editor-time save metadata for one persisted component.
    /// </summary>
    public class EntityComponentSaveState {
        /// <summary>
        /// Gets or sets the stable editor component key assigned to the owning component.
        /// </summary>
        public string ComponentKey { get; set; }

        /// <summary>
        /// Stable asset references keyed by component-specific reference name.
        /// </summary>
        readonly Dictionary<string, SceneAssetReference> AssetReferencesByName;

        /// <summary>
        /// Editor-only override metadata grouped by platform and nested environment id.
        /// </summary>
        readonly EditorOverrideScopeMap<EntityComponentPlatformOverrideState> PlatformOverridesByScope;

        /// <summary>
        /// Initializes a new empty component save-state container.
        /// </summary>
        public EntityComponentSaveState() {
            AssetReferencesByName = new Dictionary<string, SceneAssetReference>(StringComparer.Ordinal);
            PlatformOverridesByScope = new EditorOverrideScopeMap<EntityComponentPlatformOverrideState>();
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
        /// Replaces matching references after authored-asset recovery.
        /// </summary>
        /// <param name="replacements">Old-to-canonical reference map.</param>
        /// <returns>True when at least one reference changed.</returns>
        public bool ReplaceAssetReferences(IReadOnlyDictionary<SceneAssetReference, SceneAssetReference> replacements) {
            if (replacements == null) {
                throw new ArgumentNullException(nameof(replacements));
            }
            bool changed = false;
            List<string> names = new List<string>(AssetReferencesByName.Keys);
            for (int index = 0; index < names.Count; index++) {
                SceneAssetReference current = AssetReferencesByName[names[index]];
                if (replacements.TryGetValue(current, out SceneAssetReference replacement) && replacement != null) {
                    AssetReferencesByName[names[index]] = replacement;
                    changed = true;
                }
            }
            foreach (EntityComponentPlatformOverrideState overrideState in PlatformOverridesByScope.EnumerateValues()) {
                changed |= overrideState.ReplaceAssetReferences(replacements);
            }
            return changed;
        }

        /// <summary>
        /// Stores one named platform override payload for this component.
        /// </summary>
        /// <param name="platformId">Platform identifier that owns the override payload.</param>
        /// <param name="overrideState">Override payload metadata to store.</param>
        public void SetPlatformOverride(string platformId, EntityComponentPlatformOverrideState overrideState) {
            SetScopedPlatformOverride(new EditorOverrideScope(platformId), overrideState);
        }

        /// <summary>
        /// Stores one platform or nested environment override payload for this component.
        /// </summary>
        /// <param name="scope">Platform or platform/environment scope that owns the payload.</param>
        /// <param name="overrideState">Override payload metadata to store.</param>
        public void SetScopedPlatformOverride(EditorOverrideScope scope, EntityComponentPlatformOverrideState overrideState) {
            if (overrideState == null) {
                throw new ArgumentNullException(nameof(overrideState));
            }

            overrideState.PlatformId = scope.PlatformId;
            overrideState.EnvironmentId = scope.EnvironmentId;
            PlatformOverridesByScope.Set(scope, overrideState);
        }

        /// <summary>
        /// Gets the existing platform override payload for one platform or creates a new one when needed.
        /// </summary>
        /// <param name="platformId">Platform identifier whose override payload should be returned.</param>
        /// <returns>Mutable platform override payload metadata.</returns>
        public EntityComponentPlatformOverrideState GetOrCreatePlatformOverride(string platformId) {
            return GetOrCreateScopedPlatformOverride(new EditorOverrideScope(platformId));
        }

        /// <summary>
        /// Gets the existing platform or nested environment override payload or creates one when needed.
        /// </summary>
        /// <param name="scope">Platform or platform/environment scope whose payload should be returned.</param>
        /// <returns>Mutable override payload metadata.</returns>
        public EntityComponentPlatformOverrideState GetOrCreateScopedPlatformOverride(EditorOverrideScope scope) {
            return PlatformOverridesByScope.GetOrCreate(scope, () => new EntityComponentPlatformOverrideState {
                PlatformId = scope.PlatformId,
                EnvironmentId = scope.EnvironmentId
            });
        }

        /// <summary>
        /// Attempts to read one platform override payload from this component state.
        /// </summary>
        /// <param name="platformId">Platform identifier whose override payload should be resolved.</param>
        /// <param name="overrideState">Resolved platform override payload metadata when one exists.</param>
        /// <returns>True when one platform override payload exists for the supplied platform.</returns>
        public bool TryGetPlatformOverride(string platformId, out EntityComponentPlatformOverrideState overrideState) {
            return TryGetScopedPlatformOverride(new EditorOverrideScope(platformId), out overrideState);
        }

        /// <summary>
        /// Attempts to read one platform or nested environment override payload.
        /// </summary>
        /// <param name="scope">Platform or platform/environment scope to resolve.</param>
        /// <param name="overrideState">Resolved override payload when one exists.</param>
        /// <returns>True when one override payload exists at the supplied scope.</returns>
        public bool TryGetScopedPlatformOverride(EditorOverrideScope scope, out EntityComponentPlatformOverrideState overrideState) {
            return PlatformOverridesByScope.TryGet(scope, out overrideState);
        }

        /// <summary>
        /// Returns whether one platform override payload exists for this component state.
        /// </summary>
        /// <param name="platformId">Platform identifier whose override payload should be checked.</param>
        /// <returns>True when one override exists for the supplied platform.</returns>
        public bool HasPlatformOverride(string platformId) {
            return HasScopedPlatformOverride(new EditorOverrideScope(platformId));
        }

        /// <summary>
        /// Returns whether one platform or nested environment override exists.
        /// </summary>
        /// <param name="scope">Platform or platform/environment scope to check.</param>
        /// <returns>True when an override exists at the supplied scope.</returns>
        public bool HasScopedPlatformOverride(EditorOverrideScope scope) {
            return PlatformOverridesByScope.TryGet(scope, out _);
        }

        /// <summary>
        /// Removes one platform override payload from this component state.
        /// </summary>
        /// <param name="platformId">Platform identifier whose override payload should be removed.</param>
        public void RemovePlatformOverride(string platformId) {
            RemoveScopedPlatformOverride(new EditorOverrideScope(platformId));
        }

        /// <summary>
        /// Removes one platform or nested environment override payload.
        /// </summary>
        /// <param name="scope">Platform or platform/environment scope to remove.</param>
        public void RemoveScopedPlatformOverride(EditorOverrideScope scope) {
            PlatformOverridesByScope.Remove(scope);
        }

        /// <summary>
        /// Enumerates every platform override payload stored in this component state.
        /// </summary>
        /// <returns>Platform override payload metadata stored for this component.</returns>
        public IEnumerable<EntityComponentPlatformOverrideState> EnumeratePlatformOverrides() {
            return PlatformOverridesByScope.EnumerateValues();
        }
    }
}
