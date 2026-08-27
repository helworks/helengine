namespace helengine.editor {
    /// <summary>
    /// Provides the deterministic winner-first ordering for one identity candidate set.
    /// </summary>
    public sealed class EditorAssetResolutionCandidateScore : IComparable<EditorAssetResolutionCandidateScore>, IComparable {
        /// <summary>
        /// Initializes one immutable candidate score.
        /// </summary>
        public EditorAssetResolutionCandidateScore(
            bool isCurrentId,
            bool matchesSavedPath,
            bool matchesSavedHash,
            bool isRecordedOwner,
            string relativePath) {
            IsCurrentId = isCurrentId;
            MatchesSavedPath = matchesSavedPath;
            MatchesSavedHash = matchesSavedHash;
            IsRecordedOwner = isRecordedOwner;
            RelativePath = relativePath ?? string.Empty;
        }

        /// <summary>Gets whether the candidate matched the saved current identity.</summary>
        public bool IsCurrentId { get; }

        /// <summary>Gets whether the candidate matched the normalized saved path.</summary>
        public bool MatchesSavedPath { get; }

        /// <summary>Gets whether the candidate matched the saved content hash.</summary>
        public bool MatchesSavedHash { get; }

        /// <summary>Gets whether this candidate is the recorded owner for the identity.</summary>
        public bool IsRecordedOwner { get; }

        /// <summary>Gets the normalized assets-relative candidate path.</summary>
        public string RelativePath { get; }

        /// <summary>
        /// Compares candidates in winner-first order: evidence booleans descend and paths sort ordinally.
        /// </summary>
        public int CompareTo(EditorAssetResolutionCandidateScore other) {
            if (other == null) {
                return -1;
            }

            int comparison = ComparePreferred(IsCurrentId, other.IsCurrentId);
            if (comparison != 0) return comparison;
            comparison = ComparePreferred(MatchesSavedPath, other.MatchesSavedPath);
            if (comparison != 0) return comparison;
            comparison = ComparePreferred(MatchesSavedHash, other.MatchesSavedHash);
            if (comparison != 0) return comparison;
            comparison = ComparePreferred(IsRecordedOwner, other.IsRecordedOwner);
            if (comparison != 0) return comparison;
            return string.Compare(RelativePath, other.RelativePath, StringComparison.Ordinal);
        }

        /// <summary>
        /// Compares this score through the non-generic comparison contract.
        /// </summary>
        /// <param name="obj">Another candidate score.</param>
        /// <returns>Winner-first comparison result.</returns>
        public int CompareTo(object obj) {
            if (obj == null) {
                return -1;
            }
            if (obj is not EditorAssetResolutionCandidateScore other) {
                throw new ArgumentException("Candidate scores can only be compared with another candidate score.", nameof(obj));
            }
            return CompareTo(other);
        }

        /// <summary>Returns concise evidence text suitable for repair diagnostics.</summary>
        public string ToEvidenceString() {
            return $"current-id={IsCurrentId}; saved-path={MatchesSavedPath}; saved-hash={MatchesSavedHash}; recorded-owner={IsRecordedOwner}; path='{RelativePath}'";
        }

        static int ComparePreferred(bool left, bool right) {
            if (left == right) return 0;
            return left ? -1 : 1;
        }
    }
}
