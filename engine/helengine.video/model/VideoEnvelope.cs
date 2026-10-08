namespace helengine.video {
    /// <summary>
    /// A gain ramp.
    /// </summary>
    public sealed class VideoEnvelope {
        /// <summary>
        /// linear, equal_power_in or equal_power_out.
        /// </summary>
        public string Type { get; set; } = "linear";

        /// <summary>
        /// Ramp start in the owning scene or clip.
        /// </summary>
        public VideoMoment At { get; set; } = new();

        /// <summary>
        /// Ramp length in seconds.
        /// </summary>
        public double DurationSec { get; set; } = 0;

        /// <summary>
        /// Starting gain.
        /// </summary>
        public double From { get; set; } = 1;

        /// <summary>
        /// Ending gain.
        /// </summary>
        public double To { get; set; } = 1;

    }
}
