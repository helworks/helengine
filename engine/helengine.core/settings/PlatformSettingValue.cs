namespace helengine {
    /// <summary>Stores a portable immutable setting value without boxing, reflection or platform-dependent parsing.</summary>
    public readonly struct PlatformSettingValue {
        /// <summary>Gets the value's explicit kind.</summary>
        public PlatformRuntimeSettingKind Kind { get; }
        /// <summary>Gets the boolean payload when Kind is Boolean.</summary>
        public bool Boolean { get; }
        /// <summary>Gets the numeric payload when Kind is Integer or Number.</summary>
        public double Number { get; }
        /// <summary>Gets the string payload when Kind is Choice or Text.</summary>
        public string Text { get; }

        /// <summary>Creates one fully specified value; public factories preserve the kind/payload contract.</summary>
        PlatformSettingValue(PlatformRuntimeSettingKind kind, bool boolean, double number, string text) {
            Kind = kind; Boolean = boolean; Number = number; Text = text;
        }
        /// <summary>Creates a boolean setting value.</summary>
        public static PlatformSettingValue FromBoolean(bool value) => new(PlatformRuntimeSettingKind.Boolean, value, 0, string.Empty);
        /// <summary>Creates a whole-number setting value.</summary>
        public static PlatformSettingValue FromInteger(int value) => new(PlatformRuntimeSettingKind.Integer, false, value, string.Empty);
        /// <summary>Creates a numeric setting value; the setting definition validates finiteness and bounds.</summary>
        public static PlatformSettingValue FromNumber(double value) => new(PlatformRuntimeSettingKind.Number, false, value, string.Empty);
        /// <summary>Creates a choice value whose membership is validated by its definition.</summary>
        public static PlatformSettingValue FromChoice(string value) => new(PlatformRuntimeSettingKind.Choice, false, 0, value ?? throw new ArgumentNullException(nameof(value)));
        /// <summary>Creates a platform-defined text value.</summary>
        public static PlatformSettingValue FromText(string value) => new(PlatformRuntimeSettingKind.Text, false, 0, value ?? throw new ArgumentNullException(nameof(value)));
    }
}
