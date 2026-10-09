using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// A solid rectangle bound to a <c>rect</c> slot (bars, underlines, strikes, backing panels). Its size is in box
    /// units at transform scale 1; the <c>reveal</c> channel grows it from its left edge.
    /// </summary>
    public sealed class VideoTimelineRect {
        /// <summary>
        /// Default width as a fraction of the box width when neither a width nor a matched text is given.
        /// </summary>
        public const double DefaultWidth = 0.5;

        /// <summary>
        /// Default height as a fraction of the box height.
        /// </summary>
        public const double DefaultHeight = 0.05;

        /// <summary>
        /// Fill color, <c>"#RRGGBB"</c>, <c>"#RRGGBBAA"</c> or <c>[r, g, b]</c>/<c>[r, g, b, a]</c> in 0..1.
        /// </summary>
        public JsonElement Color { get; set; }

        /// <summary>
        /// Width as a fraction of the box width; 0.5 when absent (or the matched text's width when <see cref="Match"/> is set).
        /// </summary>
        public double? Width { get; set; }

        /// <summary>
        /// Height as a fraction of the box height; 0.05 when absent.
        /// </summary>
        public double? Height { get; set; }

        /// <summary>
        /// Name of a text slot whose measured width (at scale 1) this rectangle takes instead of <see cref="Width"/>, so
        /// a strike or underline always spans its word.
        /// </summary>
        public string Match { get; set; }
    }
}
