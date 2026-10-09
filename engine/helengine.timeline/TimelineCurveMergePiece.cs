namespace helengine.timeline {
    /// <summary>
    /// A run of consecutive breakpoint intervals that <see cref="TimelineCurveMerger"/> gives to the same segment of the
    /// same source, either following its curve or holding its end value.
    /// </summary>
    sealed class TimelineCurveMergePiece {
        /// <summary>
        /// Gets or sets the index of the winning source.
        /// </summary>
        public int Source { get; set; }

        /// <summary>
        /// Gets or sets the index of the winning segment within the source.
        /// </summary>
        public int Segment { get; set; }

        /// <summary>
        /// Gets or sets whether the run holds the segment's end value rather than following its curve.
        /// </summary>
        public bool Held { get; set; }

        /// <summary>
        /// Gets or sets whether the run is a hold the runtime performs by itself (it directly follows the same segment), so
        /// no segment is emitted for it.
        /// </summary>
        public bool Implicit { get; set; }

        /// <summary>
        /// Gets or sets the run start.
        /// </summary>
        public double StartSeconds { get; set; }

        /// <summary>
        /// Gets or sets the run end.
        /// </summary>
        public double EndSeconds { get; set; }
    }
}
