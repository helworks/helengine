namespace helengine.timeline.runtime {
    /// <summary>
    /// One animation of a cooked animation track: between <see cref="StartTick"/> and <see cref="EndTick"/> the slot
    /// entity's <see cref="AnimationPlayerComponent"/> shows the clip at
    /// <c>ClipInTicks / rate + (tick - StartTick) / rate * Speed</c> seconds; after the end its last pose is held.
    /// </summary>
    public sealed class CookedTimelineAnimationClip {
        /// <summary>
        /// Gets or sets the tick at which the animation starts.
        /// </summary>
        public int StartTick { get; set; }

        /// <summary>
        /// Gets or sets the tick at which the animation stops advancing.
        /// </summary>
        public int EndTick { get; set; }

        /// <summary>
        /// Gets or sets the animation time, in ticks, shown at <see cref="StartTick"/>.
        /// </summary>
        public int ClipInTicks { get; set; }

        /// <summary>
        /// Gets or sets the playback speed multiplier.
        /// </summary>
        public float Speed { get; set; } = 1f;

        /// <summary>
        /// Gets or sets the animation clip asset to play; resolved once when the player binds.
        /// </summary>
        public SceneAssetReference Animation { get; set; }
    }
}
