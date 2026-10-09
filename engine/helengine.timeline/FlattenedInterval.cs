namespace helengine.timeline {
    /// <summary>
    /// A span of root-timeline seconds, [<see cref="StartSeconds"/>, <see cref="EndSeconds"/>).
    /// </summary>
    public sealed class FlattenedInterval {
        /// <summary>
        /// Gets or sets the start of the span.
        /// </summary>
        public double StartSeconds { get; set; }

        /// <summary>
        /// Gets or sets the end of the span.
        /// </summary>
        public double EndSeconds { get; set; }
    }
}
