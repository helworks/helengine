namespace helengine.timeline {
    /// <summary>
    /// An animation clip placement: an <see cref="AnimationClipAsset"/> played on the slot's entity from the clip start,
    /// skipping <see cref="TimelineClipAsset.ClipInSeconds"/> of the animation, at <see cref="Speed"/>.
    /// </summary>
    public class TimelineAnimationClipAsset : TimelineClipAsset {
        /// <summary>
        /// Gets or sets the animation clip asset to play, using the engine's canonical scene asset reference.
        /// </summary>
        public SceneAssetReference Animation { get; set; }

        /// <summary>
        /// Gets or sets the playback speed multiplier; one plays the animation at its authored speed.
        /// </summary>
        public double Speed { get; set; } = 1;
    }
}
