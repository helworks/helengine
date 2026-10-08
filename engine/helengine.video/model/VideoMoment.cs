namespace helengine.video {
    /// <summary>
    /// A point in scene time: exactly one of seconds from the start, seconds before the end, a fraction of the scene, or a spoken word of the take.
    /// </summary>
    public sealed class VideoMoment {
        /// <summary>
        /// Seconds from the scene start.
        /// </summary>
        public double? Sec { get; set; }

        /// <summary>
        /// Seconds before the scene end.
        /// </summary>
        public double? FromEnd { get; set; }

        /// <summary>
        /// Fraction of the scene, 0 to 1.
        /// </summary>
        public double? Fraction { get; set; }

        /// <summary>
        /// Spoken word of the take to anchor on.
        /// </summary>
        public string Word { get; set; }

        /// <summary>
        /// Which matching occurrence of the word, from 1.
        /// </summary>
        public int Occurrence { get; set; } = 1;

        /// <summary>
        /// Offset added to a word anchor.
        /// </summary>
        public double OffsetSec { get; set; } = 0;

    }
}
