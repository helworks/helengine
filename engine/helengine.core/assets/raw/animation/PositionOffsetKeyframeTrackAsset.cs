namespace helengine {
    /// <summary>
    /// Stores one additive position-offset animation track using <see cref="PositionKeyframeAsset"/> keyframes.
    /// </summary>
    public class PositionOffsetKeyframeTrackAsset : IDisposable {
        /// <summary>
        /// Gets or sets the ordered keyframes belonging to this additive position-offset track.
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
