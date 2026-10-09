namespace helengine.timeline {
    /// <summary>
    /// Identifies the fixed kinds of tracks a timeline can hold. The numeric values are stored in the binary form and must
    /// never be renumbered.
    /// </summary>
    public enum TimelineTrackKind {
        /// <summary>
        /// Keyframes for position, rotation (degrees) and scale of the slot's target.
        /// </summary>
        Transform = 0,

        /// <summary>
        /// Keyframes for one named value channel the target's receiver understands, such as <c>opacity</c>.
        /// </summary>
        Value = 1,

        /// <summary>
        /// Intervals during which the slot's target is active (visible, enabled).
        /// </summary>
        Activation = 2,

        /// <summary>
        /// Audio clips started at their clip start.
        /// </summary>
        Audio = 3,

        /// <summary>
        /// Animation clip assets played on the slot's entity.
        /// </summary>
        Animation = 4,

        /// <summary>
        /// Named markers delivered to event listeners; the only kind without a slot.
        /// </summary>
        Event = 5,

        /// <summary>
        /// Nested timelines placed as clips, with their own slot mapping.
        /// </summary>
        Timeline = 6
    }
}
