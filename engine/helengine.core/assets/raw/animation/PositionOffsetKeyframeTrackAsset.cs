namespace helengine {
    /// <summary>
    /// Stores one additive position-offset animation track using <see cref="PositionKeyframeAsset"/> keyframes.
    /// </summary>
    public class PositionOffsetKeyframeTrackAsset {
        /// <summary>
        /// Gets or sets the ordered keyframes belonging to this additive position-offset track.
        /// </summary>
        public PositionKeyframeAsset[] Keyframes { get; set; } = Array.Empty<PositionKeyframeAsset>();
    }
}
