namespace helengine {
    /// <summary>
    /// Stores one platform-authored position-style animation track using <see cref="PositionKeyframeAsset"/> keyframes.
    /// </summary>
    public class PlatformPositionKeyframeTrackAsset {
        /// <summary>
        /// Gets or sets the ordered keyframes authored for this platform-specific track.
        /// </summary>
        public PositionKeyframeAsset[] Keyframes { get; set; } = Array.Empty<PositionKeyframeAsset>();
    }
}
