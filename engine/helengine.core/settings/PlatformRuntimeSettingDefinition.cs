namespace helengine {
    /// <summary>Declares one engine or platform option's type, editor category, creation default and runtime mutability.</summary>
    public sealed class PlatformRuntimeSettingDefinition : IDisposable {
        /// <summary>Owns the validated choice values.</summary>
        [NativeOwnedMember] string[] ChoicesValue;
        /// <summary>Creates a setting contract; defaults seed new complete presets and never supply inherited runtime values.</summary>
        public PlatformRuntimeSettingDefinition(string id, string displayName, string category, PlatformSettingValue defaultValue,
            bool runtimeMutable, double minimum, double maximum, [NativeNoEscape] string[] choices) {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(category)) {
                throw new ArgumentException("Setting id, display name and category are required.");
            } else if (choices == null) {
                throw new ArgumentNullException(nameof(choices));
            }
            if (double.IsNaN(minimum) || double.IsNaN(maximum) || double.IsInfinity(minimum) || double.IsInfinity(maximum) || minimum > maximum) {
                throw new ArgumentException("Setting bounds must be finite and ordered.");
            }
            Id = id; DisplayName = displayName; Category = category; DefaultValue = defaultValue;
            RuntimeMutable = runtimeMutable; Minimum = minimum; Maximum = maximum; ChoicesValue = CopyChoices(choices);
            if (defaultValue.Kind == PlatformRuntimeSettingKind.Choice && ChoicesValue.Length == 0) {
                throw new ArgumentException("Choice settings require allowed values.");
            }
            for (int index = 0; index < ChoicesValue.Length; index++) {
                if (string.IsNullOrWhiteSpace(ChoicesValue[index])) { throw new ArgumentException("Setting choices cannot be blank."); }
                for (int previous = 0; previous < index; previous++) {
                    if (ChoicesValue[previous] == ChoicesValue[index]) { throw new ArgumentException("Setting choices must be unique."); }
                }
            }
            Validate(defaultValue);
        }
        /// <summary>Gets the stable identifier used by presets and the runtime API.</summary>
        public string Id { get; }
        /// <summary>Gets the editor-facing label.</summary>
        public string DisplayName { get; }
        /// <summary>Gets the editor category containing this option.</summary>
        public string Category { get; }
        /// <summary>Gets the value copied into newly created presets.</summary>
        public PlatformSettingValue DefaultValue { get; }
        /// <summary>Gets the accepted portable value kind.</summary>
        public PlatformRuntimeSettingKind Kind => DefaultValue.Kind;
        /// <summary>Gets whether the game may change this option after startup.</summary>
        public bool RuntimeMutable { get; }
        /// <summary>Gets the inclusive lower numeric bound.</summary>
        public double Minimum { get; }
        /// <summary>Gets the inclusive upper numeric bound.</summary>
        public double Maximum { get; }
        /// <summary>Gets a copy of the allowed choices so callers cannot mutate the contract.</summary>
        public string[] GetChoices() => CopyChoices(ChoicesValue);
        /// <summary>Rejects mismatched types, non-finite numbers and values outside the declared domain.</summary>
        public void Validate(PlatformSettingValue value) {
            if (value.Kind != Kind) { throw new InvalidOperationException("Setting '" + Id + "' has an incompatible value kind."); }
            if (Kind == PlatformRuntimeSettingKind.Integer || Kind == PlatformRuntimeSettingKind.Number) {
                if (double.IsNaN(value.Number) || double.IsInfinity(value.Number) || value.Number < Minimum || value.Number > Maximum
                    || (Kind == PlatformRuntimeSettingKind.Integer && Math.Floor(value.Number) != value.Number)) {
                    throw new InvalidOperationException("Setting '" + Id + "' is outside its numeric bounds.");
                }
            } else if (Kind == PlatformRuntimeSettingKind.Choice) {
                bool found = false;
                for (int index = 0; index < ChoicesValue.Length; index++) { if (ChoicesValue[index] == value.Text) { found = true; } }
                if (!found) { throw new InvalidOperationException("Setting '" + Id + "' has an unsupported choice."); }
            }
        }
        /// <summary>Copies allowed choices with portable array operations supported by managed and generated runtimes.</summary>
        static string[] CopyChoices([NativeNoEscape] string[] choices) {
            string[] copy = new string[choices.Length];
            for (int index = 0; index < choices.Length; index++) { copy[index] = choices[index]; }
            return copy;
        }
        /// <summary>Creates an independently owned definition for a runtime catalog.</summary>
        public PlatformRuntimeSettingDefinition Clone() => new(Id, DisplayName, Category, DefaultValue, RuntimeMutable, Minimum, Maximum, ChoicesValue);
        /// <summary>Releases the catalog-owned choices.</summary>
        public void Dispose() { NativeOwnership.Release(ref ChoicesValue); }
    }
}
