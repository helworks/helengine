using System.Text.Json;
using System.Text.Json.Nodes;

namespace helengine.video {
    /// <summary>
    /// Places a plain text overlay inside its arrangement region: the text is wrapped the way the caption renderer wraps it
    /// (at word boundaries, at most the style's words per line, within the region width), shrunk until every line fits
    /// the region box and centered in it. The text never grows beyond the style font size.
    /// </summary>
    public static class VideoOverlayRegionLayout {
        /// <summary>
        /// Most shrinking passes.
        /// </summary>
        const int FitPasses = 20;

        /// <summary>
        /// Largest scale step of one shrinking pass, so the wrap is re-evaluated before the text shrinks too far.
        /// </summary>
        const double MaximumStep = 0.95;

        /// <summary>
        /// Words per line the caption renderer uses when the style sets none.
        /// </summary>
        const int DefaultWordsPerLine = 3;

        /// <summary>
        /// Derives the overlay style that draws the text fitted and centered in the region box.
        /// </summary>
        /// <param name="style">Overlay text style snapshot.</param>
        /// <param name="text">Overlay text.</param>
        /// <param name="area">Region box in output pixels (see <see cref="VideoGraphicSafeArea.ForRegion"/>).</param>
        /// <param name="measurer">Text measurer.</param>
        /// <returns>Style snapshot with the fitted font size, the region center and the region width.</returns>
        public static JsonElement Fit(JsonElement style, string text, VideoGraphicSafeArea area, IVideoTextMeasurer measurer) {
            string[] words = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            int wordsPerLine = Math.Max(1, (int)VideoTextStyles.Number(style, "WordsPerLine", DefaultWordsPerLine));
            double baseSize = VideoTextStyles.Number(style, "FontSize", VideoTextStyles.DefaultFontSize);
            double ink = VideoTextStyles.Number(style, "OutlineWidth", VideoTextStyles.DefaultOutlineWidth) / 2 + VideoTextStyles.Number(style, "ShadowOffset", VideoTextStyles.DefaultShadowOffset);
            double size = baseSize;
            for (int pass = 0; pass < FitPasses && words.Length > 0; pass++) {
                double inkAtSize = ink * size / baseSize;
                List<double> lines = Wrap(style, words, size, area.Width, wordsPerLine, measurer);
                double width = lines.Max() + 2 * inkAtSize, height = lines.Count * size * VideoTextStyles.LineHeight + 2 * inkAtSize;
                if (width <= area.Width && height <= area.Height) {
                    break;
                }
                size *= Math.Min(MaximumStep, Math.Min(area.Width / width, area.Height / height));
            }
            Dictionary<string, JsonNode> overrides = new Dictionary<string, JsonNode>(StringComparer.Ordinal) {
                ["FontSize"] = Math.Round(size, 3),
                ["CenterX"] = Math.Round(area.CenterX / area.FrameWidth, 6),
                ["CenterY"] = Math.Round((area.Top + area.Bottom) / 2 / area.FrameHeight, 6),
                ["MaxWidth"] = Math.Round(area.Width / area.FrameWidth, 6)
            };
            return VideoTextStyles.With(style, overrides);
        }

        /// <summary>
        /// Wraps words greedily into lines like the caption renderer: a new line starts when the current one holds the
        /// words-per-line limit or the next word would pass the width.
        /// </summary>
        /// <param name="style">Style used for measuring.</param>
        /// <param name="words">Words in order.</param>
        /// <param name="size">Font size.</param>
        /// <param name="maximumWidth">Widest a line may be.</param>
        /// <param name="wordsPerLine">Most words on one line.</param>
        /// <param name="measurer">Text measurer.</param>
        /// <returns>Width of every line.</returns>
        static List<double> Wrap(JsonElement style, string[] words, double size, double maximumWidth, int wordsPerLine, IVideoTextMeasurer measurer) {
            double space = measurer.Measure(style, "a a", size).Width - measurer.Measure(style, "aa", size).Width;
            List<double> lines = new List<double>();
            double line = 0;
            int count = 0;
            foreach (string word in words) {
                double advance = measurer.Measure(style, word, size).Width;
                if (count > 0 && (count >= wordsPerLine || line + space + advance > maximumWidth)) {
                    lines.Add(line);
                    line = 0;
                    count = 0;
                }
                line += (count == 0 ? 0 : space) + advance;
                count++;
            }
            lines.Add(line);
            return lines;
        }
    }
}
