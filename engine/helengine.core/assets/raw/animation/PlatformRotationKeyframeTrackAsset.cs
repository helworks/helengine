namespace helengine {
    /// <summary>
    /// Stores one platform-authored rotation animation track using <see cref="RotationKeyframeAsset"/> keyframes.
    /// </summary>
    public class PlatformRotationKeyframeTrackAsset {
        /// <summary>
        /// Gets or sets the ordered keyframes authored for this platform-specific rotation track.
        /// </summary>
        public RotationKeyframeAsset[] Keyframes { get; set; } = Array.Empty<RotationKeyframeAsset>();
    }
}
