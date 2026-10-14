namespace helengine.video {
    /// <summary>
    /// The part of a recording that a scene plays.
    /// </summary>
    public sealed class VideoTake {
        /// <summary>
        /// Video or audio media id.
        /// </summary>
        public string Media { get; set; } = "";

        /// <summary>
        /// Source start in seconds; equals the first segment start when <see cref="Segments"/> is present.
        /// </summary>
        public double InSec { get; set; } = 0;

        /// <summary>
        /// Source end in seconds; equals the last segment end when <see cref="Segments"/> is present.
        /// </summary>
        public double OutSec { get; set; } = 0;

        /// <summary>
        /// Playback speed between 0.5 and 2; the voice keeps its pitch.
        /// </summary>
        public double Speed { get; set; } = 1;

        /// <summary>
        /// Optional kept intervals of the same media, in order and without overlap, played back to back; empty means the
        /// single interval from <see cref="InSec"/> to <see cref="OutSec"/>.
        /// </summary>
        public List<VideoTakeSegment> Segments { get; set; }

        /// <summary>
        /// human when locked against AI replanning.
        /// </summary>
        public string By { get; set; }

    }
}
