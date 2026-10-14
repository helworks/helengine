namespace helengine.video {
    /// <summary>
    /// Timed text drawn over a scene: plain styled text, a graphic template expansion or a timeline.
    /// </summary>
    public sealed class VideoOverlay {
        /// <summary>
        /// Overlay id, unique within its scene.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Overlay text.
        /// </summary>
        public string Text { get; set; } = "";

        /// <summary>
        /// Name of the text style in the edit text styles.
        /// </summary>
        public string Style { get; set; } = "graphic";

        /// <summary>
        /// When the overlay appears; with a graphic or a timeline it may be left empty, and the overlay then starts with its first item
        /// (graphic) or so that its earliest cue lands on its moment (timeline).
        /// </summary>
        public VideoMoment At { get; set; } = new();

        /// <summary>
        /// When it disappears; scene end when absent (a timeline also ends with its own duration).
        /// </summary>
        public VideoMoment Until { get; set; }

        /// <summary>
        /// Name of the scene arrangement region the overlay is laid out in (measured, scaled to fit and centered); absent
        /// keeps the text style placement, and for graphics the search for the part of the frame the pictures leave free.
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// human when locked against AI replanning.
        /// </summary>
        public string By { get; set; }

        /// <summary>
        /// Optional kinetic typography replacing the plain text rendering; <see cref="Text"/> stays as the fallback text.
        /// </summary>
        public VideoGraphic Graphic { get; set; }

        /// <summary>
        /// Optional motion graphics authored as a timeline, replacing the plain text rendering; an overlay has at most one
        /// of <see cref="Graphic"/> and this. <see cref="Text"/> is then an optional label.
        /// </summary>
        public VideoOverlayTimeline Timeline { get; set; }

        /// <summary>
        /// Optional reading time in seconds (0 to 10): the overlay stays visible at least this long after its last item
        /// appears (last graphic item, last timeline element, or the overlay start for plain text), even past an earlier
        /// <see cref="Until"/> or the scene end. An overlay held past its scene keeps the box laid out in its own scene and
        /// is drawn over the following scenes, at most up to the end of the video.
        /// </summary>
        public double? HoldSec { get; set; }

    }
}
