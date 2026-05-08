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
        /// Initializes a new empty platform override state container.
        /// </summary>
        public EntityComponentPlatformOverrideState() {
            AssetReferencesByName = new Dictionary<string, SceneAssetReference>(StringComparer.Ordinal);
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
    }
}
