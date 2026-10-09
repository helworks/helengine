namespace helengine.timeline {
    /// <summary>
    /// A named instant delivered to event listeners when playback crosses it, such as a camera shake or a sound cue.
    /// </summary>
    public class TimelineEventMarkerAsset {
        /// <summary>
        /// Gets or sets the instant in seconds: from the timeline start, or the offset from <see cref="Cue"/> when a cue is
        /// set.
        /// </summary>
        public double TimeSeconds { get; set; }

        /// <summary>
        /// Gets or sets the cue the time is relative to; empty for an absolute time.
        /// </summary>
        public string Cue { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the event name listeners switch on.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional payload string passed with the event; empty when the event carries no value.
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Resolves the marker time in timeline seconds against the cue times of its timeline.
        /// </summary>
        /// <param name="cueTimeSeconds">Time of <see cref="Cue"/>; ignored when the time is absolute.</param>
        /// <returns>Time in seconds from the timeline start.</returns>
        public double ResolveTime(double cueTimeSeconds) {
            if (string.IsNullOrEmpty(Cue)) {
                return TimeSeconds;
            }
            return cueTimeSeconds + TimeSeconds;
        }
    }
}
