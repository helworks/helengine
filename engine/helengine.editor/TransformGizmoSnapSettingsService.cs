namespace helengine.editor {
    /// <summary>
    /// Stores configurable snap values per transform-gizmo tool mode.
    /// </summary>
    public static class TransformGizmoSnapSettingsService {
        /// <summary>
        /// Default translation snap value used by the first snap slot.
        /// </summary>
        const double DefaultTranslateSnap1 = 0.25;
        /// <summary>
        /// Default translation snap value used by the second snap slot.
        /// </summary>
        const double DefaultTranslateSnap2 = 1.0;
        /// <summary>
        /// Default rotation snap value used by the first snap slot.
        /// </summary>
        const double DefaultRotateSnap1 = 5.0;
        /// <summary>
        /// Default rotation snap value used by the second snap slot.
        /// </summary>
        const double DefaultRotateSnap2 = 15.0;
        /// <summary>
        /// Default scale snap value used by the first snap slot.
        /// </summary>
        const double DefaultScaleSnap1 = 0.1;
        /// <summary>
        /// Default scale snap value used by the second snap slot.
        /// </summary>
        const double DefaultScaleSnap2 = 0.25;
        /// <summary>
        /// Number of decimal places preserved when adjusting snap values.
        /// </summary>
        const int SnapValuePrecisionDigits = 6;

        /// <summary>
        /// First snap-slot value stored per tool mode.
        /// </summary>
        static readonly Dictionary<EditorViewportToolMode, double> Snap1ValuesByToolMode =
            new Dictionary<EditorViewportToolMode, double>();
        /// <summary>
        /// Second snap-slot value stored per tool mode.
        /// </summary>
        static readonly Dictionary<EditorViewportToolMode, double> Snap2ValuesByToolMode =
            new Dictionary<EditorViewportToolMode, double>();

        /// <summary>
        /// Initializes the snap store with default values for each tool mode.
        /// </summary>
        static TransformGizmoSnapSettingsService() {
            ResetDefaults();
        }

        /// <summary>
        /// Restores all snap values back to their default per-tool configuration.
        /// </summary>
        public static void ResetDefaults() {
            Snap1ValuesByToolMode.Clear();
            Snap2ValuesByToolMode.Clear();

            Snap1ValuesByToolMode[EditorViewportToolMode.Translate] = DefaultTranslateSnap1;
            Snap2ValuesByToolMode[EditorViewportToolMode.Translate] = DefaultTranslateSnap2;
            Snap1ValuesByToolMode[EditorViewportToolMode.Rotate] = DefaultRotateSnap1;
            Snap2ValuesByToolMode[EditorViewportToolMode.Rotate] = DefaultRotateSnap2;
            Snap1ValuesByToolMode[EditorViewportToolMode.Scale] = DefaultScaleSnap1;
            Snap2ValuesByToolMode[EditorViewportToolMode.Scale] = DefaultScaleSnap2;
        }

        /// <summary>
        /// Reads the configured snap value for a tool mode and slot.
        /// </summary>
        /// <param name="toolMode">Tool mode whose snap value should be read.</param>
        /// <param name="snapSlot">Snap slot to read.</param>
        /// <returns>Configured snap value.</returns>
        public static double GetSnapValue(EditorViewportToolMode toolMode, TransformGizmoSnapSlot snapSlot) {
            ValidateSnapSlot(snapSlot);

            if (snapSlot == TransformGizmoSnapSlot.Snap1) {
                return Snap1ValuesByToolMode[toolMode];
            }

            return Snap2ValuesByToolMode[toolMode];
        }

        /// <summary>
        /// Increases the configured snap value for a tool mode and slot.
        /// </summary>
        /// <param name="toolMode">Tool mode whose snap value should be increased.</param>
        /// <param name="snapSlot">Snap slot to increase.</param>
        public static void IncreaseSnapValue(EditorViewportToolMode toolMode, TransformGizmoSnapSlot snapSlot) {
            AdjustSnapValue(toolMode, snapSlot, 2.0);
        }

        /// <summary>
        /// Decreases the configured snap value for a tool mode and slot.
        /// </summary>
        /// <param name="toolMode">Tool mode whose snap value should be decreased.</param>
        /// <param name="snapSlot">Snap slot to decrease.</param>
        public static void DecreaseSnapValue(EditorViewportToolMode toolMode, TransformGizmoSnapSlot snapSlot) {
            AdjustSnapValue(toolMode, snapSlot, 0.5);
        }

        /// <summary>
        /// Resolves which snap slot should be active for the current modifier keys.
        /// </summary>
        /// <param name="isControlDown">True when either control key is pressed.</param>
        /// <param name="isShiftDown">True when either shift key is pressed.</param>
        /// <returns>Active snap slot, or <see cref="TransformGizmoSnapSlot.None"/> when snapping is inactive.</returns>
        public static TransformGizmoSnapSlot ResolveActiveSnapSlot(bool isControlDown, bool isShiftDown) {
            if (isShiftDown) {
                return TransformGizmoSnapSlot.Snap2;
            }

            if (isControlDown) {
                return TransformGizmoSnapSlot.Snap1;
            }

            return TransformGizmoSnapSlot.None;
        }

        /// <summary>
        /// Resolves the active snap value for a tool mode and current modifier keys.
        /// </summary>
        /// <param name="toolMode">Tool mode whose snap values are being queried.</param>
        /// <param name="isControlDown">True when either control key is pressed.</param>
        /// <param name="isShiftDown">True when either shift key is pressed.</param>
        /// <returns>Active snap value, or zero when no snap modifier is active.</returns>
        public static double GetActiveSnapValue(EditorViewportToolMode toolMode, bool isControlDown, bool isShiftDown) {
            TransformGizmoSnapSlot activeSnapSlot = ResolveActiveSnapSlot(isControlDown, isShiftDown);
            if (activeSnapSlot == TransformGizmoSnapSlot.None) {
                return 0.0;
            }

            return GetSnapValue(toolMode, activeSnapSlot);
        }

        /// <summary>
        /// Adjusts one stored snap value by a multiplier and clamps it to tool-specific bounds.
        /// </summary>
        /// <param name="toolMode">Tool mode whose value is being updated.</param>
        /// <param name="snapSlot">Snap slot to update.</param>
        /// <param name="multiplier">Multiplier applied to the current value.</param>
        static void AdjustSnapValue(EditorViewportToolMode toolMode, TransformGizmoSnapSlot snapSlot, double multiplier) {
            ValidateSnapSlot(snapSlot);
            if (multiplier <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(multiplier), "Snap multiplier must be greater than zero.");
            }

            double currentValue = GetSnapValue(toolMode, snapSlot);
            double adjustedValue = RoundSnapValue(currentValue * multiplier);
            double minimumValue = GetMinimumSnapValue(toolMode);
            double maximumValue = GetMaximumSnapValue(toolMode);
            if (adjustedValue < minimumValue) {
                adjustedValue = minimumValue;
            }
            if (adjustedValue > maximumValue) {
                adjustedValue = maximumValue;
            }

            if (snapSlot == TransformGizmoSnapSlot.Snap1) {
                Snap1ValuesByToolMode[toolMode] = adjustedValue;
                return;
            }

            Snap2ValuesByToolMode[toolMode] = adjustedValue;
        }

        /// <summary>
        /// Validates that a real configurable snap slot was supplied.
        /// </summary>
        /// <param name="snapSlot">Snap slot to validate.</param>
        static void ValidateSnapSlot(TransformGizmoSnapSlot snapSlot) {
            if (snapSlot != TransformGizmoSnapSlot.Snap1 && snapSlot != TransformGizmoSnapSlot.Snap2) {
                throw new ArgumentOutOfRangeException(nameof(snapSlot), "A configurable snap slot must be specified.");
            }
        }

        /// <summary>
        /// Reads the smallest supported snap value for a tool mode.
        /// </summary>
        /// <param name="toolMode">Tool mode to inspect.</param>
        /// <returns>Smallest allowed snap value.</returns>
        static double GetMinimumSnapValue(EditorViewportToolMode toolMode) {
            switch (toolMode) {
                case EditorViewportToolMode.Translate:
                    return 0.03125;
                case EditorViewportToolMode.Rotate:
                    return 1.0;
                case EditorViewportToolMode.Scale:
                    return 0.01;
                default:
                    throw new ArgumentOutOfRangeException(nameof(toolMode), "Tool mode is not supported for snap configuration.");
            }
        }

        /// <summary>
        /// Reads the largest supported snap value for a tool mode.
        /// </summary>
        /// <param name="toolMode">Tool mode to inspect.</param>
        /// <returns>Largest allowed snap value.</returns>
        static double GetMaximumSnapValue(EditorViewportToolMode toolMode) {
            switch (toolMode) {
                case EditorViewportToolMode.Translate:
                    return 128.0;
                case EditorViewportToolMode.Rotate:
                    return 180.0;
                case EditorViewportToolMode.Scale:
                    return 8.0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(toolMode), "Tool mode is not supported for snap configuration.");
            }
        }

        /// <summary>
        /// Rounds a snap value to a stable decimal representation after arithmetic adjustment.
        /// </summary>
        /// <param name="value">Value to round.</param>
        /// <returns>Rounded snap value.</returns>
        static double RoundSnapValue(double value) {
            return Math.Round(value, SnapValuePrecisionDigits, MidpointRounding.AwayFromZero);
        }
    }
}
