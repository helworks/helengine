namespace helengine.video {
    /// <summary>
    /// Where a layer sits: a named preset or an explicit normalized viewport.
    /// </summary>
    public sealed class VideoLayout {
        /// <summary>
        /// full_frame, inset or side_by_side.
        /// </summary>
        public string Preset { get; set; }

        /// <summary>
        /// Explicit viewport in normalized frame coordinates.
        /// </summary>
        public VideoViewport Viewport { get; set; }

    }
}
