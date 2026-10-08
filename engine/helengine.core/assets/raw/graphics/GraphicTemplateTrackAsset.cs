namespace helengine {
    /// <summary>
    /// Animates one property of a template element with keyframes timed relative to an item moment.
    /// </summary>
    public class GraphicTemplateTrackAsset : IDisposable {
        /// <summary>
        /// Gets or sets the animated property.
        /// </summary>
        public GraphicAnimatedProperty Property { get; set; } = GraphicAnimatedProperty.Opacity;

        /// <summary>
        /// Gets or sets the moment the keyframe offsets are measured from.
        /// </summary>
        public GraphicTimeAnchor Anchor { get; set; } = GraphicTimeAnchor.Item;

        /// <summary>
        /// Gets or sets the name of a boolean parameter that must be true for the track to apply; empty always applies.
        /// </summary>
        public string EnabledParameter { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the keyframes in increasing offset order.
        /// </summary>
        public GraphicTemplateKeyframeAsset[] Keyframes { get; set; } = Array.Empty<GraphicTemplateKeyframeAsset>();

        /// <summary>
        /// Releases the owned keyframes.
        /// </summary>
        public void Dispose() {
            GraphicTemplateKeyframeAsset[] keyframes = Keyframes;
            Keyframes = null;
            AnimationClipAsset.DisposeOwnedTracks(keyframes);
        }
    }
}
