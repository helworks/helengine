namespace helengine.vfx {
    /// <summary>
    /// Describes one parameter an effect exposes, for CLI help text and validation.
    /// </summary>
    public class VfxEffectParameterDescriptor {
        public string Name { get; }
        public VfxParameterType Type { get; }
        public string DefaultValueText { get; }
        public string Description { get; }

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
