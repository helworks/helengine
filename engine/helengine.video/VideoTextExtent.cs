namespace helengine.video {
    /// <summary>
    /// Size of one line of text as the text renderer lays it out, in output pixels at the requested font size.
    /// </summary>
    public sealed class VideoTextExtent {
        /// <summary>
        /// Gets the advance width of the whole line, words and spaces included.
        /// </summary>
        public double Width { get; }

        /// <summary>
        /// Gets the height of the line box the renderer reserves for the line.
        /// </summary>
        public double Height { get; }

        /// <summary>
        /// Creates one extent.
        /// </summary>
        /// <param name="width">Advance width in pixels.</param>
        /// <param name="height">Line box height in pixels.</param>
        public VideoTextExtent(double width, double height) {
            Width = width;
            Height = height;
        }
    }
}
