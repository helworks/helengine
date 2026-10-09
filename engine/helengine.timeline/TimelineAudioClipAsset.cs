namespace helengine.timeline {
    /// <summary>
    /// An audio clip: an audio asset started at the clip start, skipping <see cref="TimelineClipAsset.ClipInSeconds"/> of
    /// its source and stopped at the clip end.
    /// </summary>
    public class TimelineAudioClipAsset : TimelineClipAsset {
        /// <summary>
        /// Gets or sets the audio asset to play, using the engine's canonical scene asset reference.
        /// </summary>
        public SceneAssetReference Audio { get; set; }

        /// <summary>
        /// Gets or sets the linear gain applied to the clip; one plays the asset unchanged.
        /// </summary>
        public double Gain { get; set; } = 1;
    }
}
