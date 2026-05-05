namespace helengine.editor {
    /// <summary>
    /// Stores one validated editor UI scale selection and resolves it to an effective runtime scale.
    /// </summary>
    public sealed class EditorUiScaleSettings {
        /// <summary>
        /// Gets the supported override percentages exposed by the editor preferences UI.
        /// </summary>
        static int[] SupportedPercents { get; } = [75, 100, 125, 150, 175, 200];

        /// <summary>
        /// Initializes one validated editor UI scale selection.
        /// </summary>
        /// <param name="mode">Indicates whether the editor follows monitor DPI or uses one explicit override.</param>
        /// <param name="overridePercent">Explicit UI scale percentage stored for override mode and preserved for dialog state.</param>
        public EditorUiScaleSettings(EditorUiScaleMode mode, int overridePercent) {
            if (!IsSupportedPercent(overridePercent)) {
                throw new ArgumentOutOfRangeException(nameof(overridePercent), "UI scale override percent must match one supported editor value.");
            }

            Mode = mode;
            OverridePercent = overridePercent;
        }

        /// <summary>
        /// Gets the rule used to resolve the effective editor UI scale.
        /// </summary>
        public EditorUiScaleMode Mode { get; }

        /// <summary>
        /// Gets the persisted explicit UI scale percentage selected by the user.
        /// </summary>
        public int OverridePercent { get; }

        /// <summary>
        /// Resolves the effective runtime scale for the supplied monitor DPI.
        /// </summary>
        /// <param name="monitorDpi">Current monitor DPI reported by the active host.</param>
        /// <returns>Effective editor UI scale as a multiplier relative to 96 DPI.</returns>
        public double ResolveEffectiveScale(int monitorDpi) {
            if (monitorDpi <= 0) {
                throw new ArgumentOutOfRangeException(nameof(monitorDpi), "Monitor DPI must be greater than zero.");
            }

            if (Mode == EditorUiScaleMode.Auto) {
                return monitorDpi / 96d;
            }

            return OverridePercent / 100d;
        }

        /// <summary>
        /// Returns true when the supplied percent matches one supported override value.
        /// </summary>
        /// <param name="percent">UI scale override percentage to validate.</param>
        /// <returns>True when the supplied percent is supported; otherwise false.</returns>
        static bool IsSupportedPercent(int percent) {
            for (int index = 0; index < SupportedPercents.Length; index++) {
                if (SupportedPercents[index] == percent) {
                    return true;
                }
            }

            return false;
        }
    }
}
