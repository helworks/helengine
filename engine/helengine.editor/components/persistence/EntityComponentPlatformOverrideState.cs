namespace helengine {
    /// <summary>
    /// Stores editor-only serialized component override metadata for one target platform.
    /// </summary>
    public class EntityComponentPlatformOverrideState {
        /// <summary>
        /// Stable asset references keyed by component-specific reference name for the override payload.
        /// </summary>
        readonly Dictionary<string, SceneAssetReference> AssetReferencesByName;
        /// <summary>
        /// Stable property paths that are explicitly overridden for the target platform.
        /// </summary>
        readonly HashSet<string> OverriddenPropertyPaths;

        /// <summary>
        /// Initializes a new empty platform override state container.
        /// </summary>
        public EntityComponentPlatformOverrideState() {
            AssetReferencesByName = new Dictionary<string, SceneAssetReference>(StringComparer.Ordinal);
            OverriddenPropertyPaths = new HashSet<string>(StringComparer.Ordinal);
            PlatformId = string.Empty;
            Payload = Array.Empty<byte>();
        }

        /// <summary>
        /// Gets or sets the platform identifier that owns this override payload.
        /// </summary>
        public string PlatformId { get; set; }

        /// <summary>
        /// Gets or sets the serialized component payload used by the override.
        /// </summary>
        public byte[] Payload { get; set; }

        /// <summary>
        /// Stores one named asset reference for the platform override payload.
        /// </summary>
        /// <param name="referenceName">Stable reference slot name.</param>
        /// <param name="reference">Stable asset reference to store.</param>
        public void SetAssetReference(string referenceName, SceneAssetReference reference) {
            if (string.IsNullOrWhiteSpace(referenceName)) {
                throw new ArgumentException("Reference name must be provided.", nameof(referenceName));
            } else if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            AssetReferencesByName[referenceName] = reference;
        }

        /// <summary>
        /// Removes one named asset reference from the platform override payload.
        /// </summary>
        /// <param name="referenceName">Stable reference slot name.</param>
        public void RemoveAssetReference(string referenceName) {
            if (string.IsNullOrWhiteSpace(referenceName)) {
                throw new ArgumentException("Reference name must be provided.", nameof(referenceName));
            }

            AssetReferencesByName.Remove(referenceName);
        }

        /// <summary>
        /// Attempts to read one named asset reference from the platform override payload.
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
        /// Enumerates every asset reference stored in this platform override state.
        /// </summary>
        /// <returns>Stable asset references stored for the override payload.</returns>
        public IEnumerable<SceneAssetReference> EnumerateAssetReferences() {
            return AssetReferencesByName.Values;
        }

        /// <summary>
        /// Enumerates every named asset reference stored in this platform override state.
        /// </summary>
        /// <returns>Named stable asset references stored for the override payload.</returns>
        public IEnumerable<KeyValuePair<string, SceneAssetReference>> EnumerateNamedAssetReferences() {
            return AssetReferencesByName;
        }

        /// <summary>
        /// Marks one property path as explicitly overridden for this platform payload.
        /// </summary>
        /// <param name="propertyPath">Stable property path that should be treated as overridden.</param>
        public void SetPropertyOverride(string propertyPath) {
            if (string.IsNullOrWhiteSpace(propertyPath)) {
                throw new ArgumentException("Property path must be provided.", nameof(propertyPath));
            }

            OverriddenPropertyPaths.Add(propertyPath);
        }

        /// <summary>
        /// Removes one explicit property override marker from this platform payload.
        /// </summary>
        /// <param name="propertyPath">Stable property path that should stop being overridden.</param>
        public void ClearPropertyOverride(string propertyPath) {
            if (string.IsNullOrWhiteSpace(propertyPath)) {
                throw new ArgumentException("Property path must be provided.", nameof(propertyPath));
            }

            OverriddenPropertyPaths.Remove(propertyPath);
        }

        /// <summary>
        /// Returns whether one explicit property override marker exists for the supplied path.
        /// </summary>
        /// <param name="propertyPath">Stable property path to query.</param>
        /// <returns>True when the path is explicitly overridden.</returns>
        public bool HasPropertyOverride(string propertyPath) {
            if (string.IsNullOrWhiteSpace(propertyPath)) {
                throw new ArgumentException("Property path must be provided.", nameof(propertyPath));
            }

            return OverriddenPropertyPaths.Contains(propertyPath);
        }

        /// <summary>
        /// Gets a value indicating whether the platform payload contains any explicit property overrides.
        /// </summary>
        public bool HasAnyPropertyOverrides => OverriddenPropertyPaths.Count > 0;

        /// <summary>
        /// Gets a value indicating whether the platform payload contains any named asset references.
        /// </summary>
        public bool HasAnyAssetReferences => AssetReferencesByName.Count > 0;

        /// <summary>
        /// Enumerates every explicit property path stored in this platform override state.
        /// </summary>
        /// <returns>Stable property paths stored for the override payload.</returns>
        public IEnumerable<string> EnumeratePropertyOverrides() {
            return OverriddenPropertyPaths;
        }
    }
}
