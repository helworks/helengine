namespace helengine.editor {
    /// <summary>
    /// Immutable evidence describing one automatic project asset repair.
    /// </summary>
    public sealed class EditorAssetRepairRecord : IEquatable<EditorAssetRepairRecord> {
        /// <summary>
        /// Initializes one immutable repair record.
        /// </summary>
        public EditorAssetRepairRecord(
            EditorAssetRepairKind kind,
            string relativePath,
            string previousAssetId = "",
            string currentAssetId = "",
            AssetReferenceResolutionTier? resolutionTier = null,
            string evidence = "",
            string owningDocument = "",
            string diagnostic = "") {
            Kind = kind;
            RelativePath = relativePath ?? string.Empty;
            PreviousAssetId = previousAssetId ?? string.Empty;
            CurrentAssetId = currentAssetId ?? string.Empty;
            ResolutionTier = resolutionTier;
            Evidence = evidence ?? string.Empty;
            OwningDocument = owningDocument ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
        }

        /// <summary>Gets the repair operation kind.</summary>
        public EditorAssetRepairKind Kind { get; }

        /// <summary>Gets the normalized assets-relative affected path.</summary>
        public string RelativePath { get; }

        /// <summary>Gets the prior identity, when one was replaced.</summary>
        public string PreviousAssetId { get; }

        /// <summary>Gets the current identity, when one was assigned.</summary>
        public string CurrentAssetId { get; }

        /// <summary>Gets the resolution tier associated with the repair, when applicable.</summary>
        public AssetReferenceResolutionTier? ResolutionTier { get; }

        /// <summary>Gets the deterministic candidate evidence.</summary>
        public string Evidence { get; }

        /// <summary>Gets the owning scene, blueprint, or metadata document when known.</summary>
        public string OwningDocument { get; }

        /// <summary>Gets the human-readable repair diagnostic.</summary>
        public string Diagnostic { get; }

        /// <summary>Alias for callers using prior-identity terminology.</summary>
        public string PriorAssetId => PreviousAssetId;

        /// <summary>Alias for callers using affected-path terminology.</summary>
        public string AffectedRelativePath => RelativePath;

        /// <summary>Compares all immutable record fields.</summary>
        public bool Equals(EditorAssetRepairRecord other) {
            return other != null &&
                Kind == other.Kind &&
                string.Equals(RelativePath, other.RelativePath, StringComparison.Ordinal) &&
                string.Equals(PreviousAssetId, other.PreviousAssetId, StringComparison.Ordinal) &&
                string.Equals(CurrentAssetId, other.CurrentAssetId, StringComparison.Ordinal) &&
                ResolutionTier == other.ResolutionTier &&
                string.Equals(Evidence, other.Evidence, StringComparison.Ordinal) &&
                string.Equals(OwningDocument, other.OwningDocument, StringComparison.Ordinal) &&
                string.Equals(Diagnostic, other.Diagnostic, StringComparison.Ordinal);
        }

        /// <summary>Compares this immutable record to another object.</summary>
        public override bool Equals(object obj) => Equals(obj as EditorAssetRepairRecord);

        /// <summary>Returns the immutable record hash.</summary>
        public override int GetHashCode() => HashCode.Combine(Kind, RelativePath, PreviousAssetId, CurrentAssetId, ResolutionTier, Evidence, OwningDocument, Diagnostic);
    }
}
