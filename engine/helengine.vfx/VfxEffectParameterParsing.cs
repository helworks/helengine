using System.Globalization;

namespace helengine.vfx {
    /// <summary>
    /// Shared raw-string parsing for effect parameters, used by every IVfxEffect.ResolveParameterSlots
    /// implementation so each effect does not reimplement the same numeric and easing parsing rules.
    /// </summary>
    public static class VfxEffectParameterParsing {
        /// <summary>
        /// Parses a scalar parameter, falling back to its documented default when the caller omitted it.
        /// </summary>
        /// <param name="values">Raw name/value pairs supplied by the caller.</param>
        /// <param name="name">Parameter name to read.</param>
        /// <param name="defaultValueText">Textual default used when the parameter is absent.</param>
        /// <returns>The parsed scalar value.</returns>
        public static float ResolveFloat(IReadOnlyDictionary<string, string> values, string name, string defaultValueText) {
            string text = values.TryGetValue(name, out string raw) ? raw : defaultValueText;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)) {
                throw new ArgumentException($"Parameter '{name}' must be a number, got '{text}'.");
            }
            return parsed;
        }

        /// <summary>
        /// Parses the easing parameter, accepting either an easing name or its numeric value and
        /// rejecting numbers that do not map to a declared easing kind.
        /// </summary>
        /// <param name="values">Raw name/value pairs supplied by the caller.</param>
        /// <param name="name">Parameter name to read.</param>
        /// <param name="defaultValueText">Textual default used when the parameter is absent.</param>
        /// <returns>The selected easing curve.</returns>
        public static VfxEasingKind ResolveEasing(IReadOnlyDictionary<string, string> values, string name, string defaultValueText) {
            string text = values.TryGetValue(name, out string raw) ? raw : defaultValueText;
            if (!Enum.TryParse(text, ignoreCase: true, out VfxEasingKind kind) || !Enum.IsDefined(typeof(VfxEasingKind), kind)) {
                throw new ArgumentException($"Parameter '{name}' must be one of Linear, EaseIn, EaseOut, EaseInOut, got '{text}'.");
            }
            return kind;
        }
    }
}
