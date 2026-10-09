namespace helengine.timeline {
    /// <summary>
    /// Flattened activation of one root slot: the union of every activation clip that targets it (nested timelines
    /// included, cut to their windows), as sorted, disjoint intervals.
    /// </summary>
    public sealed class FlattenedActivationTrack {
        /// <summary>
        /// Gets or sets the root slot.
        /// </summary>
        public string Slot { get; set; } = string.Empty;

        /// <summary>
        /// Gets the intervals during which the slot is active.
        /// </summary>
        public List<FlattenedInterval> Intervals { get; } = new List<FlattenedInterval>();
    }
}
