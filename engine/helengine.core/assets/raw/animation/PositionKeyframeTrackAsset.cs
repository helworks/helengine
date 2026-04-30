namespace helengine {
    /// <summary>
    /// Stores one absolute position animation track using <see cref="PositionKeyframeAsset"/> keyframes.
    /// </summary>
    public class PositionKeyframeTrackAsset {
        /// <summary>
        /// Gets or sets the ordered keyframes belonging to this absolute position track.
        /// </summary>
        public PositionKeyframeAsset[] Keyframes { get; set; } = Array.Empty<PositionKeyframeAsset>();
    }
}
