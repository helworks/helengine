namespace helengine.video {
    /// <summary>
    /// Alpha or luma mask from a media file.
    /// </summary>
    public sealed class VideoMask {
        /// <summary>
        /// Mask media id.
        /// </summary>
        public string Media { get; set; } = "";

        /// <summary>
        /// alpha or luma.
        /// </summary>
        public string Channel { get; set; } = "alpha";

        /// <summary>
        /// Inverts the mask.
        /// </summary>
        public bool Inverted { get; set; } = false;

    }
}
