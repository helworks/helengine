namespace helengine.video {
    /// <summary>
    /// Keyframes for one catalog layer property.
    /// </summary>
    public sealed class VideoAnimation {
        /// <summary>
        /// Catalog layer property, e.g. opacity or zoom.
        /// </summary>
        public string Property { get; set; } = "";

        /// <summary>
        /// Keyframes in increasing time.
        /// </summary>
        public List<VideoKeyframe> Keyframes { get; set; } = [];

    }
}
