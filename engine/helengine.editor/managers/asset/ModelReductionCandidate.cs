namespace helengine.editor {
    /// <summary>Represents a paired seam-preserving directed geometric edge collapse.</summary>
    sealed class ModelReductionCandidate {
        /// <summary>Position group removed by the collapse.</summary>
        public int Source;
        /// <summary>Existing position group retained by the collapse.</summary>
        public int Target;
        /// <summary>Source revision used when the candidate was validated.</summary>
        public int SourceRevision;
        /// <summary>Target revision used when the candidate was validated.</summary>
        public int TargetRevision;
        /// <summary>Bijective wedge mapping derived from actual edge adjacency.</summary>
        public readonly Dictionary<int, int> Wedges = new Dictionary<int, int>();
    }
}
