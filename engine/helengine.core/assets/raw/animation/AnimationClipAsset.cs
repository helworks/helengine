namespace helengine {
    /// <summary>
    /// Represents one keyframe-based animation clip containing multiple typed transform tracks.
    /// </summary>
    public class AnimationClipAsset : Asset {
        /// <summary>
        /// Gets or sets the authored clip duration in seconds.
        /// </summary>
        public float Duration { get; set; }

        /// <summary>
        /// Gets or sets the absolute position tracks stored by this clip.
        /// </summary>
        public PositionKeyframeTrackAsset[] PositionTracks { get; set; } = Array.Empty<PositionKeyframeTrackAsset>();

        /// <summary>
        /// Gets or sets the additive position-offset tracks stored by this clip.
        /// </summary>
        public PositionOffsetKeyframeTrackAsset[] PositionOffsetTracks { get; set; } = Array.Empty<PositionOffsetKeyframeTrackAsset>();

        /// <summary>
        /// Gets or sets the absolute scale tracks stored by this clip.
        /// </summary>
        public ScaleKeyframeTrackAsset[] ScaleTracks { get; set; } = Array.Empty<ScaleKeyframeTrackAsset>();

        /// <summary>
        /// Gets or sets the absolute rotation tracks stored by this clip.
        /// </summary>
        public RotationKeyframeTrackAsset[] RotationTracks { get; set; } = Array.Empty<RotationKeyframeTrackAsset>();
    }
}
