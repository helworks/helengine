using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace helengine.video {
    /// <summary>
    /// Reads and derives text style snapshots. Snapshots are the caption style JSON the composition text renderer reads
    /// case-insensitively; the defaults here mirror that renderer's own defaults so layout agrees with what is drawn.
    /// </summary>
    public static class VideoTextStyles {
        /// <summary>
        /// Font size the renderer uses when a style sets none.
        /// </summary>
        public const double DefaultFontSize = 76;

        /// <summary>
        /// Vertical center the renderer uses when a style sets none.
        /// </summary>
        public const double DefaultCenterY = 0.72;

        /// <summary>
        /// Wrapping width fraction the renderer uses when a style sets none; also the graphic safe-area width.
        /// </summary>
        public const double DefaultMaxWidth = 0.84;

        /// <summary>
        /// Outline width the renderer uses when a style sets none.
        /// </summary>
        public const double DefaultOutlineWidth = 7;

        /// <summary>
        /// Shadow offset the renderer uses when a style sets none.
        /// </summary>
        public const double DefaultShadowOffset = 5;

        /// <summary>
        /// Line box height of one text line in ems, as reserved by the renderer.
        /// </summary>
        public const double LineHeight = 1.35;

        /// <summary>
        /// Reads a number property, ignoring property-name case.
        /// </summary>
        /// <param name="style">Style snapshot.</param>
        /// <param name="name">Property name.</param>
        /// <param name="fallback">Value when the property is absent or not a finite number.</param>
        /// <returns>Property value.</returns>
        public static double Number(JsonElement style, string name, double fallback) {
            if (Find(style, name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number) && double.IsFinite(number)) {
                return number;
            }
            return fallback;
        }

        /// <summary>
        /// Reads a boolean property, ignoring property-name case.
        /// </summary>
        /// <param name="style">Style snapshot.</param>
        /// <param name="name">Property name.</param>
        /// <param name="fallback">Value when the property is absent or not a boolean.</param>
        /// <returns>Property value.</returns>
        public static bool Flag(JsonElement style, string name, bool fallback) {
            if (Find(style, name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False) {
                return value.GetBoolean();
            }
            return fallback;
        }

        /// <summary>
        /// Reads a string property, ignoring property-name case.
        /// </summary>
        /// <param name="style">Style snapshot.</param>
        /// <param name="name">Property name.</param>
        /// <param name="fallback">Value when the property is absent or not a string.</param>
        /// <returns>Property value.</returns>
        public static string Text(JsonElement style, string name, string fallback) {
            if (Find(style, name, out JsonElement value) && value.ValueKind == JsonValueKind.String) {
                return value.GetString();
            }
            return fallback;
        }

        /// <summary>
        /// Derives a style snapshot with some properties replaced; existing properties are matched ignoring case so the
        /// renderer never sees two spellings of one setting.
        /// </summary>
        /// <param name="style">Source snapshot.</param>
        /// <param name="overrides">Properties to set, by renderer property name.</param>
        /// <returns>New snapshot.</returns>
        public static JsonElement With(JsonElement style, IReadOnlyDictionary<string, JsonNode> overrides) {
            JsonObject result = style.ValueKind == JsonValueKind.Object ? JsonNode.Parse(style.GetRawText()).AsObject() : new JsonObject();
            foreach (KeyValuePair<string, JsonNode> entry in overrides) {
                foreach (string existing in result.Select(property => property.Key).Where(key => string.Equals(key, entry.Key, StringComparison.OrdinalIgnoreCase)).ToList()) {
                    result.Remove(existing);
                }
                result[entry.Key] = entry.Value;
            }
            return JsonSerializer.SerializeToElement(result);
        }

        /// <summary>
        /// Formats an RGBA color with components from zero to one as the renderer's <c>#AARRGGBB</c> notation.
        /// </summary>
        /// <param name="red">Red component.</param>
        /// <param name="green">Green component.</param>
        /// <param name="blue">Blue component.</param>
        /// <param name="alpha">Alpha component.</param>
        /// <returns>Hex color.</returns>
        public static string Hex(double red, double green, double blue, double alpha) {
            return "#" + Byte(alpha) + Byte(red) + Byte(green) + Byte(blue);
        }

        /// <summary>
        /// Converts one zero-to-one component to two hex digits.
        /// </summary>
        /// <param name="component">Component value.</param>
        /// <returns>Uppercase hex byte.</returns>
        static string Byte(double component) {
            return ((int)Math.Round(Math.Clamp(component, 0, 1) * 255)).ToString("X2", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Finds a property ignoring case.
        /// </summary>
        /// <param name="style">Style snapshot.</param>
        /// <param name="name">Property name.</param>
        /// <param name="value">Found value.</param>
        /// <returns>True when the property exists.</returns>
        static bool Find(JsonElement style, string name, out JsonElement value) {
            if (style.ValueKind == JsonValueKind.Object) {
                foreach (JsonProperty property in style.EnumerateObject()) {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) {
                        value = property.Value;
                        return true;
                    }
                }
            }
            value = default;
            return false;
        }
    }
}
