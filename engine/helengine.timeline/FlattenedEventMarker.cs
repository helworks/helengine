namespace helengine.timeline {
    /// <summary>
    /// One event of the flattened timeline in root-timeline seconds, with its cue already resolved.
    /// </summary>
    public sealed class FlattenedEventMarker {
        /// <summary>
        /// Gets or sets when the event fires.
        /// </summary>
        public double TimeSeconds { get; set; }

        /// <summary>
        /// Gets or sets the event name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the event value (empty when none).
        /// </summary>
        public string Value { get; set; } = string.Empty;
    }
}
