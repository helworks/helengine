namespace helengine.timeline.runtime {
    /// <summary>
    /// Describes how a cooked transform track combines with the transform the slot entity had when the player bound it.
    /// The numeric values are stored in cooked files and must never be renumbered.
    /// </summary>
    public enum CookedTimelineTransformMode : byte {
        /// <summary>
        /// The track's value replaces the component.
        /// </summary>
        Absolute = 0,
        /// <summary>
        /// The track's value is added to the bound component (position, rotation degrees) or multiplies it (scale).
        /// </summary>
        Offset = 1
    }
}
