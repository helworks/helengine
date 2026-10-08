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
