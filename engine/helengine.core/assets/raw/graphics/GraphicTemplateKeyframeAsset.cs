namespace helengine {
    /// <summary>
    /// One keyframe of a template animation track, timed relative to the track's anchor moment.
    /// </summary>
    public class GraphicTemplateKeyframeAsset : IDisposable {
        /// <summary>
        /// Gets or sets the seconds from the anchor moment; negative values lead the anchor.
        /// </summary>
        public float OffsetSeconds { get; set; }

        /// <summary>
        /// Gets or sets the property value at this keyframe when <see cref="ValueParameter"/> is empty.
        /// </summary>
        public float Value { get; set; }

        /// <summary>
        /// Gets or sets the name of a number parameter whose value replaces <see cref="Value"/>, such as a dim opacity.
        /// </summary>
        public string ValueParameter { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the catalog curve from this keyframe toward the next one, such as <c>ease_out_back.v1</c>.
        /// </summary>
        public string Curve { get; set; } = "linear.v1";

        /// <summary>
        /// Releases nothing; a keyframe owns no nested allocations beyond its strings.
        /// </summary>
        public void Dispose() { }
    }
}
