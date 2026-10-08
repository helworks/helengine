using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// Deterministic text measurement used when the host supplies no renderer-backed <see cref="IVideoTextMeasurer"/>:
    /// widths come from per-character advance factors typical of bold sans-serif display fonts. Layout stays stable
    /// across machines, but real fonts differ, so the compiler reports <c>text_measure_estimated</c> when it is used.
    /// </summary>
    public sealed class VideoTextEstimator : IVideoTextMeasurer {
        /// <summary>
        /// Measures one line from character classes.
        /// </summary>
        /// <param name="style">Style snapshot; only its case conversion is read.</param>
        /// <param name="text">Text as authored.</param>
        /// <param name="fontSize">Font size in output pixels.</param>
        /// <returns>Estimated advance width and the renderer's line box height.</returns>
        public VideoTextExtent Measure(JsonElement style, string text, double fontSize) {
            string shown = VideoTextStyles.Flag(style, "Uppercase", true) ? (text ?? "").ToUpperInvariant() : text ?? "";
            double ems = 0;
            foreach (char character in shown) {
                ems += Advance(character);
            }
            return new VideoTextExtent(ems * fontSize, fontSize * VideoTextStyles.LineHeight);
        }

        /// <summary>
        /// Typical advance of one character in ems.
        /// </summary>
        /// <param name="character">Character.</param>
        /// <returns>Advance in ems.</returns>
        static double Advance(char character) {
            if (char.IsWhiteSpace(character)) {
                return 0.28;
            } else if (character is 'I' or 'i' or 'l' or 'j' or '!' or '.' or ',' or ':' or ';' or '\'' or '|') {
                return 0.3;
            } else if (character is 'M' or 'W' or 'm' or 'w') {
                return 0.9;
            } else if (char.IsUpper(character) || char.IsDigit(character)) {
                return 0.68;
            } else if (char.IsLower(character)) {
                return 0.56;
            }
            return 0.6;
        }
    }
}
