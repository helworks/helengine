using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// The edit element one timeline slot shows: exactly one of a text (for <c>text</c> slots), an image or video of the
    /// edit (for <c>media</c> slots) or a solid rectangle (for <c>rect</c> slots).
    /// </summary>
    public sealed class VideoTimelineBinding {
        /// <summary>
        /// Default height of a bound text, as a fraction of the box height.
        /// </summary>
        public const double DefaultTextSize = 0.18;

        /// <summary>
        /// Default height of a bound image or video, as a fraction of the box height.
        /// </summary>
        public const double DefaultMediaSize = 0.5;

        /// <summary>
        /// Text drawn in the overlay text style, on one line.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Id of an image or video in the edit media.
        /// </summary>
        public string Media { get; set; }

        /// <summary>
        /// Solid rectangle.
        /// </summary>
        public VideoTimelineRect Rect { get; set; }

        /// <summary>
        /// Size at transform scale 1, as a fraction of the box height: the font size of a text (default 0.18) or the
        /// height of an image or video (default 0.5). The whole timeline may still shrink to fit the box.
        /// </summary>
        public double? Size { get; set; }

        /// <summary>
        /// Text color, <c>"#RRGGBB"</c>, <c>"#RRGGBBAA"</c> or <c>[r, g, b]</c>/<c>[r, g, b, a]</c> in 0..1; the style
        /// text color when absent.
        /// </summary>
        public JsonElement? Color { get; set; }
    }
}
