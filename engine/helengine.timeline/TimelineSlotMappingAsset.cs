namespace helengine.timeline {
    /// <summary>
    /// Binds one slot of a nested timeline to a slot of the timeline that contains it.
    /// </summary>
    public class TimelineSlotMappingAsset {
        /// <summary>
        /// Gets or sets the slot name declared by the nested timeline.
        /// </summary>
        public string Inner { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the slot name of the containing timeline that the inner slot is bound to.
        /// </summary>
        public string Outer { get; set; } = string.Empty;
    }
}
