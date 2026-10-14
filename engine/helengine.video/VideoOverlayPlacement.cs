using helengine.media;

namespace helengine.video {
    /// <summary>
    /// A compiled overlay with the interval and screen box it occupies, used to check held overlays against later ones. An
    /// overlay compiled as part of a stacked list has its own interval and the list's layer and box.
    /// </summary>
    public sealed class VideoOverlayPlacement {
        /// <summary>
        /// Creates a placement.
        /// </summary>
        /// <param name="span">Scene span owning the overlay.</param>
        /// <param name="overlay">Overlay.</param>
        /// <param name="path">JSON path of the overlay.</param>
        /// <param name="layer">Compiled text or group layer drawing the overlay.</param>
        /// <param name="start">Absolute instant the overlay appears.</param>
        /// <param name="end">Absolute instant it disappears (reading time included).</param>
        /// <param name="bounds">Screen box in output pixels, or null when unknown.</param>
        public VideoOverlayPlacement(VideoSceneSpan span, VideoOverlay overlay, string path, VisualLayer layer, MediaTime start, MediaTime end, VideoGraphicRectangle bounds) {
            Span = span;
            Overlay = overlay;
            Path = path;
            Layer = layer;
            Start = start;
            End = end;
            Bounds = bounds;
        }

        /// <summary>
        /// Gets the scene span owning the overlay.
        /// </summary>
        public VideoSceneSpan Span { get; }

        /// <summary>
        /// Gets the overlay.
        /// </summary>
        public VideoOverlay Overlay { get; }

        /// <summary>
        /// Gets the JSON path of the overlay.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the compiled layer.
        /// </summary>
        public VisualLayer Layer { get; }

        /// <summary>
        /// Gets the absolute instant the overlay appears.
        /// </summary>
        public MediaTime Start { get; }

        /// <summary>
        /// Gets the absolute instant it disappears (reading time included).
        /// </summary>
        public MediaTime End { get; }

        /// <summary>
        /// Gets the screen box in output pixels, or null when unknown.
        /// </summary>
        public VideoGraphicRectangle Bounds { get; }
    }
}
