namespace helengine.editor {
    /// <summary>
    /// Describes an authored asset recovery result and any canonicalization work required.
    /// </summary>
    public sealed class AssetReferenceResolution {
        /// <summary>
        /// Initializes one resolution result.
        /// </summary>
        /// <param name="fullPath">Resolved absolute source path. Staged resolutions leave this empty because their destination still contains the previously published bytes.</param>
        /// <param name="canonicalReference">Canonical stable reference for the source.</param>
        /// <param name="tier">Identity tier that selected the source.</param>
        /// <param name="referenceChanged">Whether the serialized reference should be rewritten.</param>
        /// <param name="metadataChanged">Whether identity metadata was adopted or repaired.</param>
        /// <param name="isStaged">Whether the canonical reference describes an unpublished transaction payload.</param>
        public AssetReferenceResolution(
            string fullPath,
            SceneAssetReference canonicalReference,
            AssetReferenceResolutionTier tier,
            bool referenceChanged,
            bool metadataChanged,
            EditorAssetResolutionCandidateScore candidateEvidence = null,
            bool isStaged = false) {
            FullPath = fullPath ?? string.Empty;
            CanonicalReference = canonicalReference ?? throw new ArgumentNullException(nameof(canonicalReference));
            Tier = tier;
            ReferenceChanged = referenceChanged;
            MetadataChanged = metadataChanged;
            CandidateEvidence = candidateEvidence;
            IsStaged = isStaged;
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

        /// <summary>Gets the immutable deterministic evidence for the selected candidate.</summary>
        public EditorAssetResolutionCandidateScore CandidateEvidence { get; }

        /// <summary>Gets a value indicating whether the canonical reference is backed by an unpublished transaction payload.</summary>
        public bool IsStaged { get; }
    }
}
