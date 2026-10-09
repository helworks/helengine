namespace helengine.timeline {
    /// <summary>
    /// One three-component value (position, rotation in degrees, or scale) at an instant of clip time, with the catalog
    /// curve that shapes the segment toward the next keyframe.
    /// </summary>
    public class TimelineVectorKeyframeAsset {
        /// <summary>
        /// Gets or sets the instant in seconds from the clip start (clip time, not timeline time).
        /// </summary>
        public double TimeSeconds { get; set; }

        /// <summary>
        /// Gets or sets the X component.
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Gets or sets the Y component.
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// Gets or sets the Z component.
        /// </summary>
        public double Z { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="CurveCatalog"/> id applied from this keyframe to the next; the last keyframe's curve
        /// is unused.
        /// </summary>
        public string Curve { get; set; } = CurveCatalog.Linear;
    }
}
