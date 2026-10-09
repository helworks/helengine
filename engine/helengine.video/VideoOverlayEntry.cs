namespace helengine.video {
    /// <summary>
    /// An overlay to compile together with the JSON path its diagnostics point at.
    /// </summary>
    public sealed class VideoOverlayEntry {
        /// <summary>
        /// Creates an entry.
        /// </summary>
        /// <param name="overlay">Overlay.</param>
        /// <param name="path">JSON path of the overlay in the edit.</param>
        public VideoOverlayEntry(VideoOverlay overlay, string path) {
            Overlay = overlay;
            Path = path;
        }

        /// <summary>
        /// Gets the overlay.
        /// </summary>
        public VideoOverlay Overlay { get; }

        /// <summary>
        /// Gets the JSON path of the overlay in the edit.
        /// </summary>
        public string Path { get; }
    }
}
