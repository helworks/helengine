namespace helengine.timeline {
    /// <summary>
    /// What one clip contributes to one channel of its track, in the local time of the timeline instance: its span, its
    /// blend ramps and its segments (null when the clip has no keyframes for the channel).
    /// </summary>
    sealed class TimelineClipChannel {
        /// <summary>
        /// Gets or sets the clip start.
        /// </summary>
        public double StartSeconds { get; set; }

        /// <summary>
        /// Gets or sets the clip end.
        /// </summary>
        public double EndSeconds { get; set; }

        /// <summary>
        /// Gets or sets the clip's segments for the channel, or null when it does not drive the channel.
        /// </summary>
        public List<FlattenedCurveSegment> Segments { get; set; }
    }
}
