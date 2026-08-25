namespace helengine.editor {
    /// <summary>
    /// Describes an authored asset recovery result and any canonicalization work required.
    /// </summary>
    public sealed class AssetReferenceResolution {
        /// <summary>
        /// Initializes one resolution result.
        /// </summary>
        /// <param name="fullPath">Resolved absolute source path.</param>
        /// <param name="canonicalReference">Canonical stable reference for the source.</param>
        /// <param name="tier">Identity tier that selected the source.</param>
        /// <param name="referenceChanged">Whether the serialized reference should be rewritten.</param>
        /// <param name="metadataChanged">Whether identity metadata was adopted or repaired.</param>
        public AssetReferenceResolution(string fullPath, SceneAssetReference canonicalReference, AssetReferenceResolutionTier tier, bool referenceChanged, bool metadataChanged) {
            FullPath = fullPath ?? string.Empty;
            CanonicalReference = canonicalReference ?? throw new ArgumentNullException(nameof(canonicalReference));
            Tier = tier;
            ReferenceChanged = referenceChanged;
            MetadataChanged = metadataChanged;
        }

        /// <summary>Gets the resolved absolute source path.</summary>
        public string FullPath { get; }

        /// <summary>Gets the canonical stable reference.</summary>
        public SceneAssetReference CanonicalReference { get; }

        /// <summary>Gets the recovery tier that selected the asset.</summary>
        public AssetReferenceResolutionTier Tier { get; }

        /// <summary>Gets a value indicating whether the saved reference should be rewritten.</summary>
        public bool ReferenceChanged { get; }

        /// <summary>Gets a value indicating whether metadata was changed during recovery.</summary>
        public bool MetadataChanged { get; }
    }
}
