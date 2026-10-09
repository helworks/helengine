namespace helengine.timeline {
    /// <summary>
    /// One scalar keyframe of one channel, extracted from a scalar keyframe or one axis of a vector keyframe, in clip time.
    /// </summary>
    sealed class TimelineKeyframeSample {
        /// <summary>
        /// Gets or sets the clip time.
        /// </summary>
        public double TimeSeconds { get; set; }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Gets or sets the curve used from this keyframe to the next.
        /// </summary>
        public string Curve { get; set; } = CurveCatalog.Linear;
    }
}
