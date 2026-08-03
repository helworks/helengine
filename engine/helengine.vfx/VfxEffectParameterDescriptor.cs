namespace helengine.vfx {
    /// <summary>
    /// Describes one parameter an effect exposes, for CLI help text and validation.
    /// </summary>
    public class VfxEffectParameterDescriptor {
        /// <summary>
        /// Parameter name exactly as it must be written on the command line, e.g. <c>--param Name=value</c>.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Value shape callers must supply for this parameter.
        /// </summary>
        public VfxParameterType Type { get; }

        /// <summary>
        /// Textual default applied when the caller does not supply the parameter, written in the same
        /// syntax a caller would use on the command line.
        /// </summary>
        public string DefaultValueText { get; }

        /// <summary>
        /// Human-readable explanation of what the parameter does, shown in CLI help output.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Describes one effect parameter.
        /// </summary>
        /// <param name="name">Command-line name of the parameter.</param>
        /// <param name="type">Value shape the parameter accepts.</param>
        /// <param name="defaultValueText">Textual default used when the parameter is omitted.</param>
        /// <param name="description">Human-readable explanation shown in help output.</param>
        public VfxEffectParameterDescriptor(string name, VfxParameterType type, string defaultValueText, string description) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Parameter name must be provided.", nameof(name));
            }
            if (string.IsNullOrWhiteSpace(defaultValueText)) {
                throw new ArgumentException("Default value text must be provided.", nameof(defaultValueText));
            }

            Name = name;
            Type = type;
            DefaultValueText = defaultValueText;
            Description = description;
        }
    }
}
