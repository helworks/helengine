namespace helengine {
    /// <summary>
    /// Stores one platform-authored position-style animation track using <see cref="PositionKeyframeAsset"/> keyframes.
    /// </summary>
    public class PlatformPositionKeyframeTrackAsset : IDisposable {
        /// <summary>
        /// Gets or sets the ordered keyframes authored for this platform-specific track.
        /// </summary>
        public PositionKeyframeAsset[] Keyframes { get; set; } = Array.Empty<PositionKeyframeAsset>();

        /// <summary>Releases the deserialized keyframe objects and their container once the clip is no longer used.</summary>
        public void Dispose() {
            PositionKeyframeAsset[] keyframes = Keyframes;
            Keyframes = null;
            AnimationClipAsset.DeleteOwnedKeyframes(keyframes);
        }
    }
}
