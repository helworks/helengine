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
        /// Initializes a new empty component save-state container.
        /// </summary>
        public EntityComponentSaveState() {
            AssetReferencesByName = new Dictionary<string, SceneAssetReference>(StringComparer.Ordinal);
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
    }
}
