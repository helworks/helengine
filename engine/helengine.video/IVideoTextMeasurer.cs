using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// Measures text exactly as the composition text renderer will draw it (same font loading, case conversion, word
    /// spacing and line box), so graphic templates can lay out and fit items before any frame is rendered. Hosts with a
    /// renderer supply an implementation through <see cref="VideoCompileContext.TextMeasurer"/>.
    /// </summary>
    public interface IVideoTextMeasurer {
        /// <summary>
        /// Measures one line of text.
        /// </summary>
        /// <param name="style">Text style snapshot from the edit text styles (font, weight, case).</param>
        /// <param name="text">Text as authored; the measurer applies the style's case conversion.</param>
        /// <param name="fontSize">Font size in output pixels, overriding the style's own size.</param>
        /// <returns>Line advance width and line box height in output pixels.</returns>
        VideoTextExtent Measure(JsonElement style, string text, double fontSize);
    }
}
