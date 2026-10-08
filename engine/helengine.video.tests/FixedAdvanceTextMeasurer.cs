using System.Text.Json;

namespace helengine.video.tests {
    /// <summary>
    /// Test measurer with a fixed advance per character, so layout expectations can be computed by hand.
    /// </summary>
    sealed class FixedAdvanceTextMeasurer : IVideoTextMeasurer {
        /// <summary>
        /// Advance of every character in ems.
        /// </summary>
        public const double Advance = 0.6;

        /// <summary>
        /// Gets how many measurements were requested.
        /// </summary>
        public int Calls { get; private set; }

        /// <summary>
        /// Measures one line as character count times the fixed advance.
        /// </summary>
        /// <param name="style">Style snapshot (unused).</param>
        /// <param name="text">Text.</param>
        /// <param name="fontSize">Font size in pixels.</param>
        /// <returns>Width and the renderer line box height.</returns>
        public VideoTextExtent Measure(JsonElement style, string text, double fontSize) {
            Calls++;
            return new VideoTextExtent(text.Length * Advance * fontSize, fontSize * VideoTextStyles.LineHeight);
        }
    }
}
