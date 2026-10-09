namespace helengine.timeline {
    /// <summary>
    /// Flattened animation of one root slot: every animation clip that targets it, sorted by start. Clips never overlap:
    /// when one starts while the previous is still running (a blend or a nested timeline), the previous is cut there,
    /// because one animation player shows one clip at a time.
    /// </summary>
    public sealed class FlattenedAnimationTrack {
        /// <summary>
        /// Gets or sets the root slot.
        /// </summary>
        public string Slot { get; set; } = string.Empty;

        /// <summary>
        /// Gets the clips in start order.
        /// </summary>
        public List<FlattenedAnimationClip> Clips { get; } = new List<FlattenedAnimationClip>();
    }
}
