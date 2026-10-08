using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// Resolves the parameter values of one graphic: the value the edit supplies, otherwise the template default. Values
    /// were already checked against the published descriptors by the edit validator.
    /// </summary>
    public sealed class VideoGraphicParameters {
        /// <summary>
        /// Template declaring the parameters and their defaults.
        /// </summary>
        readonly GraphicTemplateAsset Template;

        /// <summary>
        /// Values supplied by the edit, by parameter name.
        /// </summary>
        readonly Dictionary<string, JsonElement> Values;

        /// <summary>
        /// Creates the resolver for one graphic.
        /// </summary>
        /// <param name="template">Template declaring the parameters.</param>
        /// <param name="values">Values supplied by the edit.</param>
        public VideoGraphicParameters(GraphicTemplateAsset template, Dictionary<string, JsonElement> values) {
            Template = template ?? throw new ArgumentNullException(nameof(template));
            Values = values ?? throw new ArgumentNullException(nameof(values));
        }

        /// <summary>
        /// Reports whether the edit supplies a value for a parameter.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <returns>True when the edit sets the parameter.</returns>
        public bool IsSet(string name) {
            return Values.ContainsKey(name);
        }

        /// <summary>
        /// Resolves a number parameter.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <returns>Edit value or template default.</returns>
        public double Number(string name) {
            if (Values.TryGetValue(name, out JsonElement value)) {
                return value.GetDouble();
            }
            return Declared(name).DefaultValue.X;
        }

        /// <summary>
        /// Resolves an enabling switch; an empty name means always enabled.
        /// </summary>
        /// <param name="name">Bool parameter name, or empty.</param>
        /// <returns>Whether the switch is on.</returns>
        public bool Flag(string name) {
            if (string.IsNullOrEmpty(name)) {
                return true;
            }
            if (Values.TryGetValue(name, out JsonElement value)) {
                return value.GetBoolean();
            }
            return Declared(name).DefaultValue.X != 0;
        }

        /// <summary>
        /// Resolves a color parameter as the text renderer's <c>#AARRGGBB</c> notation.
        /// </summary>
        /// <param name="name">Color parameter name.</param>
        /// <returns>Edit value or template default.</returns>
        public string Color(string name) {
            if (Values.TryGetValue(name, out JsonElement value)) {
                double[] components = value.EnumerateArray().Select(component => component.GetDouble()).ToArray();
                return VideoTextStyles.Hex(components[0], components[1], components[2], components.Length == 4 ? components[3] : 1);
            }
            float4 fallback = Declared(name).DefaultValue;
            return VideoTextStyles.Hex(fallback.X, fallback.Y, fallback.Z, fallback.W);
        }

        /// <summary>
        /// Finds a declared parameter.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <returns>Declaration.</returns>
        /// <exception cref="InvalidDataException">The template does not declare the parameter.</exception>
        EffectParameterAsset Declared(string name) {
            return GraphicTemplateValidator.FindParameter(Template, name) ?? throw new InvalidDataException($"Graphic template '{Template.TemplateId}' has no parameter '{name}'.");
        }
    }
}
