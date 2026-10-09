namespace helengine.timeline {
    /// <summary>
    /// Declares one abstract target the timeline animates. The timeline never names a concrete target; the player binds
    /// the slot by name (a game to a scene entity, the video pipeline to an element it creates).
    /// </summary>
    public class TimelineSlotAsset {
        /// <summary>
        /// Gets or sets the slot name tracks and bindings use, such as <c>term_a</c>.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets what the slot must be bound to; it limits which track kinds may target the slot.
        /// </summary>
        public TimelineSlotKind Kind { get; set; } = TimelineSlotKind.Entity;

        /// <summary>
        /// Gets or sets the explanation given to whoever binds the slot.
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }
}
