namespace helengine.timeline.runtime {
    /// <summary>
    /// One span of a cooked activation track during which the slot entity is enabled: ticks in
    /// [<see cref="StartTick"/>, <see cref="EndTick"/>).
    /// </summary>
    public sealed class CookedTimelineInterval {
        /// <summary>
        /// Gets or sets the first tick at which the entity is enabled.
        /// </summary>
        public int StartTick { get; set; }

        /// <summary>
        /// Gets or sets the first tick at which the entity is disabled again.
        /// </summary>
        public int EndTick { get; set; }
    }
}
