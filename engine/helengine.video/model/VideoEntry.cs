using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// How a scene enters: a cut, an appear ramp or a catalog transition from the previous scene.
    /// </summary>
    public sealed class VideoEntry {
        /// <summary>
        /// cut, appear or a catalog transition effect id.
        /// </summary>
        public string Effect { get; set; } = "cut";

        /// <summary>
        /// Catalog effect version for transitions.
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Transition or appear duration in seconds.
        /// </summary>
        public double DurationSec { get; set; } = 0;

        /// <summary>
        /// Transition parameters as declared by the catalog.
        /// </summary>
        public Dictionary<string, JsonElement> Parameters { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Seconds of equal-power voice fade on both sides of the cut.
        /// </summary>
        public double AudioFadeSec { get; set; } = 0;

        /// <summary>
        /// human when locked against AI replanning.
        /// </summary>
        public string By { get; set; }

    }
}
