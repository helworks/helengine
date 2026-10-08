namespace helengine {
    /// <summary>
    /// Stores one platform-authored rotation animation track using <see cref="RotationKeyframeAsset"/> keyframes.
    /// </summary>
    public class PlatformRotationKeyframeTrackAsset : IDisposable {
        /// <summary>
        /// Gets or sets the ordered keyframes authored for this platform-specific rotation track.
        /// </summary>
        public RotationKeyframeAsset[] Keyframes { get; set; } = Array.Empty<RotationKeyframeAsset>();

        /// <summary>Releases the deserialized keyframe objects and their container once the clip is no longer used.</summary>
        public void Dispose() {
            RotationKeyframeAsset[] keyframes = Keyframes;
            Keyframes = null;
            AnimationClipAsset.DeleteOwnedKeyframes(keyframes);
        }
    }
}
