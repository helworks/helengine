using System.Text.Json;
using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Reads the colors overlay timeline bindings carry, <c>"#RRGGBB"</c>, <c>"#RRGGBBAA"</c> or <c>[r, g, b]</c> /
    /// <c>[r, g, b, a]</c> with components in 0..1, and converts them to the text renderer's notation.
    /// </summary>
    public static class VideoTimelineColors {
        /// <summary>
        /// Converts a binding color to the text renderer's <c>#AARRGGBB</c> notation.
        /// </summary>
        /// <param name="value">Binding color.</param>
        /// <param name="color">Renderer color when the value is valid; empty otherwise.</param>
        /// <returns>True when the value is a valid color.</returns>
        public static bool TryParse(JsonElement value, out string color) {
            color = "";
            if (value.ValueKind == JsonValueKind.String) {
                try {
                    MediaColor parsed = MediaColor.Parse(value.GetString());
                    color = VideoTextStyles.Hex(parsed.Red, parsed.Green, parsed.Blue, parsed.Alpha);
                    return true;
                } catch (InvalidDataException) {
                    return false;
                }
            }
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() is not (3 or 4)) {
                return false;
            }
            double[] components = new double[4];
            components[3] = 1;
            int index = 0;
            foreach (JsonElement component in value.EnumerateArray()) {
                if (component.ValueKind != JsonValueKind.Number || !component.TryGetDouble(out double number) || !double.IsFinite(number) || number < 0 || number > 1) {
                    return false;
                }
                components[index++] = number;
            }
            color = VideoTextStyles.Hex(components[0], components[1], components[2], components[3]);
            return true;
        }
    }
}
