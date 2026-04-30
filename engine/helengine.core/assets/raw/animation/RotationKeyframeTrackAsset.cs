namespace helengine {
    /// <summary>
    /// Stores one absolute rotation animation track using <see cref="RotationKeyframeAsset"/> keyframes.
    /// </summary>
    public class RotationKeyframeTrackAsset {
        /// <summary>
        /// Gets or sets the ordered keyframes belonging to this absolute rotation track.
        /// </summary>
        public RotationKeyframeAsset[] Keyframes { get; set; } = Array.Empty<RotationKeyframeAsset>();
    }
}
