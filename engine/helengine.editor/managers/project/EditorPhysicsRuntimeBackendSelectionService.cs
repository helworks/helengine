namespace helengine.editor {
    /// <summary>
    /// Resolves one exclusive physics runtime backend and its generated-core preprocessor contract from selected code-generation settings.
    /// </summary>
    public sealed class EditorPhysicsRuntimeBackendSelectionService {
        /// <summary>
        /// Stable code-generation setting identifier that carries the selected physics runtime backend.
        /// </summary>
        public const string SettingId = "physics-runtime-backend";

        /// <summary>
        /// Canonical setting value for the existing BEPU-backed runtime.
        /// </summary>
        public const string BepuValue = "bepu";

        /// <summary>
        /// Canonical setting value for the console-first HelPhysics runtime.
        /// </summary>
        public const string HelPhysicsValue = "helphysics";

        /// <summary>
        /// Generated-core preprocessor symbol that identifies BEPU as the exclusive physics runtime.
        /// </summary>
        public const string BepuSymbol = "HELENGINE_PHYSICS_RUNTIME_BEPU";

        /// <summary>
        /// Generated-core preprocessor symbol that identifies HelPhysics as the exclusive physics runtime.
        /// </summary>
        public const string HelPhysicsSymbol = "HELENGINE_PHYSICS_RUNTIME_HELPHYSICS";

        /// <summary>
        /// Resolves the canonical backend value, retaining BEPU only when older build settings omit the selection entirely.
        /// </summary>
        /// <param name="selectedCodegenOptionValues">Effective code-generation settings for the active platform build.</param>
        /// <returns>The canonical <c>bepu</c> or <c>helphysics</c> backend value.</returns>
        public string Resolve(IReadOnlyDictionary<string, string> selectedCodegenOptionValues) {
            if (selectedCodegenOptionValues == null) {
                throw new ArgumentNullException(nameof(selectedCodegenOptionValues));
            }
            if (!selectedCodegenOptionValues.TryGetValue(SettingId, out string selectedValue)) {
                return BepuValue;
            }
            if (string.IsNullOrWhiteSpace(selectedValue)) {
                throw new InvalidOperationException($"Code-generation setting '{SettingId}' must select a physics runtime backend.");
            }

            string normalizedValue = selectedValue.Trim();
            if (string.Equals(normalizedValue, BepuValue, StringComparison.OrdinalIgnoreCase)) {
                return BepuValue;
            } else if (string.Equals(normalizedValue, HelPhysicsValue, StringComparison.OrdinalIgnoreCase)) {
                return HelPhysicsValue;
            }

            throw new InvalidOperationException(
                $"Code-generation setting '{SettingId}' selected unsupported physics runtime backend '{selectedValue}'.");
        }

        /// <summary>
        /// Builds the single generated-core symbol that corresponds to one validated canonical backend value.
        /// </summary>
        /// <param name="backend">Canonical backend value returned by <see cref="Resolve"/>.</param>
        /// <returns>A one-element symbol list that cannot identify conflicting physics runtimes.</returns>
        public IReadOnlyList<string> BuildPreprocessorSymbols(string backend) {
            if (string.Equals(backend, BepuValue, StringComparison.Ordinal)) {
                return [BepuSymbol];
            } else if (string.Equals(backend, HelPhysicsValue, StringComparison.Ordinal)) {
                return [HelPhysicsSymbol];
            }

            throw new ArgumentException(
                $"Physics runtime backend must be canonical '{BepuValue}' or '{HelPhysicsValue}'.",
                nameof(backend));
        }
    }
}
