namespace helengine {
    /// <summary>
    /// Stores one absolute scale animation track using <see cref="PositionKeyframeAsset"/> keyframes.
    /// </summary>
    public class ScaleKeyframeTrackAsset {
        /// <summary>
        /// Gets or sets the ordered keyframes belonging to this absolute scale track.
        /// </summary>
        public PositionKeyframeAsset[] Keyframes { get; set; } = Array.Empty<PositionKeyframeAsset>();
    }
}
