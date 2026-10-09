namespace helengine.timeline.runtime {
    /// <summary>
    /// Identifies what a cooked track drives. The numeric values are stored in cooked files and must never be renumbered.
    /// </summary>
    public enum CookedTimelineTrackKind : byte {
        /// <summary>
        /// One component of the slot entity's local transform (see <see cref="CookedTimelineTransformChannel"/>), driven by
        /// segments.
        /// </summary>
        Transform = 0,
        /// <summary>
        /// One channel of an <see cref="ITimelineReceiver"/> on the slot entity, driven by segments.
        /// </summary>
        Value = 1,
        /// <summary>
        /// Intervals during which the slot entity is enabled.
        /// </summary>
        Activation = 2,
        /// <summary>
        /// Audio clips started through the core audio manager.
        /// </summary>
        Audio = 3,
        /// <summary>
        /// Animation clips played on the slot entity's <see cref="AnimationPlayerComponent"/>.
        /// </summary>
        Animation = 4,
        /// <summary>
        /// Named markers delivered to <see cref="ITimelineEventListener"/> components on the player's entity.
        /// </summary>
        Event = 5
    }
}
