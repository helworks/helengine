namespace helengine.editor {
    /// <summary>Orders candidates by geometric error and stable endpoint identifiers.</summary>
    readonly struct ModelReductionPriority : IComparable<ModelReductionPriority> {
        /// <summary>Accumulated plane-error cost.</summary>
        readonly double Error;
        /// <summary>Stable removed endpoint identifier used to resolve equal-cost choices.</summary>
        readonly int Source;
        /// <summary>Stable surviving endpoint identifier used to resolve equal-cost choices.</summary>
        readonly int Target;
        /// <summary>Creates a reproducible priority independent of hash-table iteration order.</summary>
        public ModelReductionPriority(double error, int source, int target) { Error = error; Source = source; Target = target; }
        /// <summary>Compares geometric cost followed by the two endpoint identifiers.</summary>
        public int CompareTo(ModelReductionPriority other) {
            int comparison = Error.CompareTo(other.Error);
            if (comparison == 0) comparison = Source.CompareTo(other.Source);
            return comparison == 0 ? Target.CompareTo(other.Target) : comparison;
        }
    }
}
