namespace helengine.timeline {
    /// <summary>
    /// One timed span on a track. The start is absolute, or relative to a cue when <see cref="Cue"/> is set, so the clip
    /// moves with the cue. Activation tracks use this class directly; the other clip kinds derive from it.
    /// </summary>
    public class TimelineClipAsset {
        /// <summary>
        /// Gets or sets the start in seconds: from the timeline start, or the offset from <see cref="Cue"/> when a cue is set
        /// (negative offsets lead the cue).
        /// </summary>
        public double StartSeconds { get; set; }

        /// <summary>
        /// Gets or sets the cue the start is relative to; empty for an absolute start.
        /// </summary>
        public string Cue { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the clip length in seconds; required and positive for every clip kind.
        /// </summary>
        public double DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the seconds skipped at the beginning of the clip's source (audio, animation or nested timeline).
        /// </summary>
        public double ClipInSeconds { get; set; }

        /// <summary>
        /// Gets or sets the blend-in ramp length; the clip may overlap the previous clip on its track only inside this ramp.
        /// </summary>
        public double EaseInSeconds { get; set; }

        /// <summary>
        /// Gets or sets the blend-out ramp length; the next clip on the track may overlap this one only inside this ramp.
        /// </summary>
        public double EaseOutSeconds { get; set; }

        /// <summary>
        /// Resolves the clip start in timeline seconds against the cue times of its timeline.
        /// </summary>
        /// <param name="cueTimeSeconds">Time of <see cref="Cue"/>; ignored when the start is absolute.</param>
        /// <returns>Start in seconds from the timeline start.</returns>
        public double ResolveStart(double cueTimeSeconds) {
            if (string.IsNullOrEmpty(Cue)) {
                return StartSeconds;
            }
            return cueTimeSeconds + StartSeconds;
        }
    }
}
