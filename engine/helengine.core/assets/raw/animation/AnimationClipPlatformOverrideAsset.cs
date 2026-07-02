namespace helengine {
    /// <summary>
    /// Stores one platform-specific override payload for an animation clip.
    /// </summary>
    public class AnimationClipPlatformOverrideAsset {
        /// <summary>
        /// Gets or sets the platform identifier that owns this override payload.
        /// </summary>
        public string PlatformId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets how this platform resolves relative to the base clip.
        /// </summary>
        public AnimationClipPlatformOverrideMode Mode { get; set; }

        /// <summary>
        /// Gets or sets the platform-authored absolute position tracks.
        /// </summary>
        public PlatformPositionKeyframeTrackAsset[] PositionTracks { get; set; } = Array.Empty<PlatformPositionKeyframeTrackAsset>();

        /// <summary>
        /// Gets or sets the platform-authored additive position-offset tracks.
        /// </summary>
        public PlatformPositionKeyframeTrackAsset[] PositionOffsetTracks { get; set; } = Array.Empty<PlatformPositionKeyframeTrackAsset>();

        /// <summary>
        /// Gets or sets the platform-authored absolute scale tracks.
        /// </summary>
        public PlatformPositionKeyframeTrackAsset[] ScaleTracks { get; set; } = Array.Empty<PlatformPositionKeyframeTrackAsset>();

        /// <summary>
        /// Gets or sets the platform-authored absolute rotation tracks.
        /// </summary>
        public PlatformRotationKeyframeTrackAsset[] RotationTracks { get; set; } = Array.Empty<PlatformRotationKeyframeTrackAsset>();
    }
}
