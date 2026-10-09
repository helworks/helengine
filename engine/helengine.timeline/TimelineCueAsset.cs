namespace helengine.timeline {
    /// <summary>
    /// A named point in timeline time. Clips and markers anchored on a cue keep their offset from it, so a host that moves
    /// the cue (for example to the moment a word is spoken) shifts them without stretching them.
    /// </summary>
    public class TimelineCueAsset {
        /// <summary>
        /// Gets or sets the cue name clips and markers reference, such as <c>term_b</c>.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the authored cue time in seconds from the timeline start.
        /// </summary>
        public double TimeSeconds { get; set; }
    }
}
