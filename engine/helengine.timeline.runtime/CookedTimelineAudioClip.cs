namespace helengine.timeline.runtime {
    /// <summary>
    /// One sound of a cooked audio track: started at <see cref="StartTick"/> (skipping <see cref="ClipInTicks"/> of the
    /// asset when the audio backend can seek) and stopped at <see cref="EndTick"/>.
    /// </summary>
    public sealed class CookedTimelineAudioClip {
        /// <summary>
        /// Gets or sets the tick at which the sound starts.
        /// </summary>
        public int StartTick { get; set; }

        /// <summary>
        /// Gets or sets the tick at which the player stops the sound if it is still playing.
        /// </summary>
        public int EndTick { get; set; }

        /// <summary>
        /// Gets or sets how much of the asset, in ticks, is skipped at the start.
        /// </summary>
        public int ClipInTicks { get; set; }

        /// <summary>
        /// Gets or sets the linear gain passed to the audio manager.
        /// </summary>
        public float Gain { get; set; } = 1f;

        /// <summary>
        /// Gets or sets the audio asset to play; resolved once when the player binds.
        /// </summary>
        public SceneAssetReference Audio { get; set; }
    }
}
