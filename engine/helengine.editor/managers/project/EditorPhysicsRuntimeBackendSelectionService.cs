namespace helengine.editor {
    /// <summary>
    /// Resolves one exclusive physics runtime backend and its generated-core preprocessor contract from selected code-generation settings.
    /// </summary>
    internal static class EditorPhysicsRuntimeBackendSelectionService {
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
        public static string Resolve(IReadOnlyDictionary<string, string> selectedCodegenOptionValues) {
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
        /// Validates a persisted explicit backend before generic profile-default resolution can replace blank settings.
        /// </summary>
        /// <param name="selectedCodegenOptionValues">Persisted code-generation settings captured directly from the build request.</param>
        public static void ValidateExplicitSelection(IReadOnlyDictionary<string, string> selectedCodegenOptionValues) {
            if (selectedCodegenOptionValues == null
                || !selectedCodegenOptionValues.ContainsKey(SettingId)) {
                return;
            }

            _ = Resolve(selectedCodegenOptionValues);
        }

        /// <summary>
        /// Builds the single generated-core symbol that corresponds to one validated canonical backend value.
        /// </summary>
        /// <param name="backend">Canonical backend value returned by <see cref="Resolve"/>.</param>
        /// <returns>A one-element symbol list that cannot identify conflicting physics runtimes.</returns>
        public static IReadOnlyList<string> BuildPreprocessorSymbols(string backend) {
            if (string.Equals(backend, BepuValue, StringComparison.Ordinal)) {
                return [BepuSymbol];
            } else if (string.Equals(backend, HelPhysicsValue, StringComparison.Ordinal)) {
                return [HelPhysicsSymbol];
            }

            throw new ArgumentException(
                $"Physics runtime backend must be canonical '{BepuValue}' or '{HelPhysicsValue}'.",
                nameof(backend));
        }

        /// <summary>
        /// Validates that the final generated-core symbol graph identifies the selected physics runtime and no competing backend.
        /// </summary>
        /// <param name="backend">Canonical backend value returned by <see cref="Resolve"/>.</param>
        /// <param name="preprocessorSymbols">Final combined symbols that will be forwarded to generated-core regeneration.</param>
        public static void ValidatePreprocessorSymbols(string backend, IReadOnlyList<string> preprocessorSymbols) {
            if (preprocessorSymbols == null) {
                throw new ArgumentNullException(nameof(preprocessorSymbols));
            }

            string selectedSymbol;
            string conflictingSymbol;
            if (string.Equals(backend, BepuValue, StringComparison.Ordinal)) {
                selectedSymbol = BepuSymbol;
                conflictingSymbol = HelPhysicsSymbol;
            } else if (string.Equals(backend, HelPhysicsValue, StringComparison.Ordinal)) {
                selectedSymbol = HelPhysicsSymbol;
                conflictingSymbol = BepuSymbol;
            } else {
                throw new ArgumentException(
                    $"Physics runtime backend must be canonical '{BepuValue}' or '{HelPhysicsValue}'.",
                    nameof(backend));
            }

            bool containsSelectedSymbol = false;
            for (int index = 0; index < preprocessorSymbols.Count; index++) {
                string symbol = preprocessorSymbols[index];
                if (string.Equals(symbol, selectedSymbol, StringComparison.Ordinal)) {
                    containsSelectedSymbol = true;
                } else if (string.Equals(symbol, conflictingSymbol, StringComparison.Ordinal)) {
                    throw new InvalidOperationException(
                        $"Generated-core symbols contain conflicting physics runtime selections '{selectedSymbol}' and '{conflictingSymbol}'.");
                }
            }

            if (!containsSelectedSymbol) {
                throw new InvalidOperationException(
                    $"Generated-core symbols do not contain selected physics runtime symbol '{selectedSymbol}'.");
            }
        }
    }
}
