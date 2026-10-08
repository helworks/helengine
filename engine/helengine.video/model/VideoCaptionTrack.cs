using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// Captions derived from the words of the takes.
    /// </summary>
    public sealed class VideoCaptionTrack {
        /// <summary>
        /// Caption source; voice is the only one.
        /// </summary>
        public string Source { get; set; } = "voice";

        /// <summary>
        /// Words per caption block.
        /// </summary>
        public int WordsPerCue { get; set; } = 8;

        /// <summary>
        /// Words per line.
        /// </summary>
        public int WordsPerLine { get; set; } = 4;

        /// <summary>
        /// Upward entrance travel of each caption and overlay as a fraction of the frame height; zero disables it.
        /// </summary>
        public double LiftFraction { get; set; } = 0;

        /// <summary>
        /// Seconds the entrance travel lasts.
        /// </summary>
        public double EntranceSec { get; set; } = 0.22;

        /// <summary>
        /// Seconds the exit travel lasts.
        /// </summary>
        public double ExitSec { get; set; } = 0.1;

        /// <summary>
        /// Resolved caption text style snapshot, passed to text layers.
        /// </summary>
        public JsonElement Style { get; set; } = default;

        /// <summary>
        /// Resolved graphic text style snapshot used by overlays.
        /// </summary>
        public JsonElement GraphicStyle { get; set; } = default;

        /// <summary>
        /// Layer effects applied to every caption cue, e.g. a bubble box or a soft shadow.
        /// </summary>
        public List<VideoEffect> Effects { get; set; } = [];

        /// <summary>
        /// Per-cue text replacements.
        /// </summary>
        public List<VideoCaptionOverride> Overrides { get; set; } = [];

    }
}
