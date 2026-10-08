using System.Globalization;

namespace helengine.vfx {
    /// <summary>
    /// Turns textual parameter values (from a command line or a composition document) into the constant-slot floats an
    /// effect's shaders read, applying each parameter's declared type, bounds and default. Text forms: numbers use the
    /// invariant culture, vectors and colors are comma separated (<c>r,g,b</c> or <c>r,g,b,a</c>), switches are
    /// <c>true</c>/<c>false</c>, and enums use one of their allowed names.
    /// </summary>
    public static class VfxParameterSlotResolver {
        /// <summary>
        /// Resolves every declared parameter, using defaults for omitted ones.
        /// </summary>
        /// <param name="effect">Validated effect definition.</param>
        /// <param name="values">Supplied values keyed by parameter name.</param>
        /// <returns>Exactly <see cref="VfxFrameConstants.ParamSlotCount"/> floats.</returns>
        /// <exception cref="ArgumentException">A value is unknown, malformed or outside its range.</exception>
        public static float[] Resolve(EffectAsset effect, IReadOnlyDictionary<string, string> values) {
            if (effect == null) {
                throw new ArgumentNullException(nameof(effect));
            }
            if (values == null) {
                throw new ArgumentNullException(nameof(values));
            }

            EffectParameterAsset[] parameters = effect.Parameters ?? Array.Empty<EffectParameterAsset>();
            foreach (string name in values.Keys) {
                if (!parameters.Any(parameter => parameter.Name == name)) {
                    throw new ArgumentException($"Effect '{effect.EffectId}' has no parameter named '{name}'.");
                }
            }

            float[] slots = new float[VfxFrameConstants.ParamSlotCount];
            foreach (EffectParameterAsset parameter in parameters) {
                float[] components = values.TryGetValue(parameter.Name, out string text) ? Parse(parameter, text) : Defaults(parameter);
                Array.Copy(components, 0, slots, parameter.Slot, components.Length);
            }
            return slots;
        }

        /// <summary>
        /// Returns the declared default value as slot floats.
        /// </summary>
        /// <param name="parameter">Parameter whose default is used.</param>
        /// <returns>Default components, one per occupied slot.</returns>
        public static float[] Defaults(EffectParameterAsset parameter) {
            float[] all = { parameter.DefaultValue.X, parameter.DefaultValue.Y, parameter.DefaultValue.Z, parameter.DefaultValue.W };
            return all.Take(VfxParameterLayout.SlotCount(parameter.Type)).ToArray();
        }

        /// <summary>
        /// Parses one textual value into slot floats and checks it against the parameter's bounds.
        /// </summary>
        /// <param name="parameter">Parameter declaration.</param>
        /// <param name="text">Supplied text.</param>
        /// <returns>Parsed components, one per occupied slot.</returns>
        public static float[] Parse(EffectParameterAsset parameter, string text) {
            string value = text?.Trim() ?? string.Empty;
            if (parameter.Type == EffectParameterType.Enum) {
                int index = Array.FindIndex(parameter.AllowedValues, allowed => string.Equals(allowed, value, StringComparison.OrdinalIgnoreCase));
                if (index < 0) {
                    throw new ArgumentException($"Parameter '{parameter.Name}' must be one of {string.Join(", ", parameter.AllowedValues)}, got '{value}'.");
                }
                return new float[] { index };
            } else if (parameter.Type == EffectParameterType.Bool) {
                if (value == "true" || value == "1") {
                    return new float[] { 1f };
                } else if (value == "false" || value == "0") {
                    return new float[] { 0f };
                }
                throw new ArgumentException($"Parameter '{parameter.Name}' must be true or false, got '{value}'.");
            }

            string[] parts = value.Split(',');
            int count = VfxParameterLayout.SlotCount(parameter.Type);
            bool rgbColor = parameter.Type == EffectParameterType.Color && parts.Length == 3;
            if (parts.Length != count && !rgbColor) {
                throw new ArgumentException($"Parameter '{parameter.Name}' expects {Shape(parameter.Type)}, got '{value}'.");
            }

            float[] components = new float[count];
            for (int index = 0; index < count; index++) {
                if (rgbColor && index == 3) {
                    components[index] = 1f;
                    continue;
                }
                if (!float.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || !float.IsFinite(number)) {
                    throw new ArgumentException($"Parameter '{parameter.Name}' expects {Shape(parameter.Type)}, got '{value}'.");
                }
                if (number < parameter.Minimum || number > parameter.Maximum) {
                    throw new ArgumentException($"Parameter '{parameter.Name}' must be from {parameter.Minimum.ToString(CultureInfo.InvariantCulture)} to {parameter.Maximum.ToString(CultureInfo.InvariantCulture)}, got '{value}'.");
                }
                if (VfxParameterLayout.IsWholeNumber(parameter.Type) && number != Math.Floor(number)) {
                    throw new ArgumentException($"Parameter '{parameter.Name}' must be a whole number, got '{value}'.");
                }
                components[index] = number;
            }
            return components;
        }

        /// <summary>
        /// Describes the expected text shape of a numeric parameter for error messages.
        /// </summary>
        /// <param name="type">Parameter value shape.</param>
        /// <returns>Short human-readable description.</returns>
        static string Shape(EffectParameterType type) {
            if (type == EffectParameterType.Float2) {
                return "two comma-separated numbers";
            } else if (type == EffectParameterType.Float4) {
                return "four comma-separated numbers";
            } else if (type == EffectParameterType.Color) {
                return "R,G,B or R,G,B,A components";
            } else if (type == EffectParameterType.Integer) {
                return "a whole number";
            }
            return "a number";
        }
    }
}
