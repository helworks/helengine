namespace helengine.timeline {
    /// <summary>
    /// One scalar value at an instant of clip time, with the catalog curve that shapes the segment toward the next keyframe.
    /// </summary>
    public class TimelineKeyframeAsset {
        /// <summary>
        /// Gets or sets the instant in seconds from the clip start (clip time, not timeline time).
        /// </summary>
        public double TimeSeconds { get; set; }

        /// <summary>
        /// Gets or sets the channel value at this instant.
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="CurveCatalog"/> id applied from this keyframe to the next; the last keyframe's curve
        /// is unused.
        /// </summary>
        public string Curve { get; set; } = CurveCatalog.Linear;
    }
}
