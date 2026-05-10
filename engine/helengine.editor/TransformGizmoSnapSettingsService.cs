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
        /// Camera-scoped snap-settings states used by independent viewport instances.
        /// </summary>
        static readonly Dictionary<CameraComponent, TransformGizmoSnapSettingsState> StatesByCamera =
            new Dictionary<CameraComponent, TransformGizmoSnapSettingsState>();

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
            StatesByCamera.Clear();

            Snap1ValuesByToolMode[EditorViewportToolMode.Translate] = DefaultTranslateSnap1;
            Snap2ValuesByToolMode[EditorViewportToolMode.Translate] = DefaultTranslateSnap2;
            Snap1ValuesByToolMode[EditorViewportToolMode.Rotate] = DefaultRotateSnap1;
            Snap2ValuesByToolMode[EditorViewportToolMode.Rotate] = DefaultRotateSnap2;
            Snap1ValuesByToolMode[EditorViewportToolMode.Scale] = DefaultScaleSnap1;
            Snap2ValuesByToolMode[EditorViewportToolMode.Scale] = DefaultScaleSnap2;
        }

        /// <summary>
        /// Restores the snap values for one viewport camera back to their default per-tool configuration.
        /// </summary>
        /// <param name="camera">Viewport camera whose snap state should be reset.</param>
        public static void ResetDefaults(CameraComponent camera) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            StatesByCamera[camera] = CreateDefaultState();
        }

        /// <summary>
        /// Removes any camera-scoped snap state associated with one viewport camera.
        /// </summary>
        /// <param name="camera">Viewport camera whose snap state should be discarded.</param>
        public static void ClearState(CameraComponent camera) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            StatesByCamera.Remove(camera);
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
        /// Reads the configured snap value for one viewport camera, tool mode, and slot.
        /// </summary>
        /// <param name="camera">Viewport camera whose snap state should be read.</param>
        /// <param name="toolMode">Tool mode whose snap value should be read.</param>
        /// <param name="snapSlot">Snap slot to read.</param>
        /// <returns>Configured snap value.</returns>
        public static double GetSnapValue(CameraComponent camera, EditorViewportToolMode toolMode, TransformGizmoSnapSlot snapSlot) {
            return GetSnapValue(GetOrCreateState(camera), toolMode, snapSlot);
        }

        /// <summary>
        /// Assigns one explicit snap value for the default shared snap state.
        /// </summary>
        /// <param name="toolMode">Tool mode whose snap value should be updated.</param>
        /// <param name="snapSlot">Snap slot to update.</param>
        /// <param name="value">Snap value to persist.</param>
        public static void SetSnapValue(EditorViewportToolMode toolMode, TransformGizmoSnapSlot snapSlot, double value) {
            TransformGizmoSnapSettingsState state = CreateDefaultStateFromGlobal();
            SetSnapValue(state, toolMode, snapSlot, value);
            ApplyStateToGlobal(state);
        }

        /// <summary>
        /// Assigns one explicit snap value for a viewport camera, tool mode, and slot.
        /// </summary>
        /// <param name="camera">Viewport camera whose snap state should be updated.</param>
        /// <param name="toolMode">Tool mode whose snap value should be updated.</param>
        /// <param name="snapSlot">Snap slot to update.</param>
        /// <param name="value">Snap value to persist.</param>
        public static void SetSnapValue(CameraComponent camera, EditorViewportToolMode toolMode, TransformGizmoSnapSlot snapSlot, double value) {
            SetSnapValue(GetOrCreateState(camera), toolMode, snapSlot, value);
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
        /// Increases the configured snap value for one viewport camera, tool mode, and slot.
        /// </summary>
        /// <param name="camera">Viewport camera whose snap value should be increased.</param>
        /// <param name="toolMode">Tool mode whose snap value should be increased.</param>
        /// <param name="snapSlot">Snap slot to increase.</param>
        public static void IncreaseSnapValue(CameraComponent camera, EditorViewportToolMode toolMode, TransformGizmoSnapSlot snapSlot) {
            AdjustSnapValue(GetOrCreateState(camera), toolMode, snapSlot, 2.0);
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
        /// Decreases the configured snap value for one viewport camera, tool mode, and slot.
        /// </summary>
        /// <param name="camera">Viewport camera whose snap value should be decreased.</param>
        /// <param name="toolMode">Tool mode whose snap value should be decreased.</param>
        /// <param name="snapSlot">Snap slot to decrease.</param>
        public static void DecreaseSnapValue(CameraComponent camera, EditorViewportToolMode toolMode, TransformGizmoSnapSlot snapSlot) {
            AdjustSnapValue(GetOrCreateState(camera), toolMode, snapSlot, 0.5);
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
        /// Resolves the active snap value for one viewport camera, tool mode, and current modifier keys.
        /// </summary>
        /// <param name="camera">Viewport camera whose snap state should be queried.</param>
        /// <param name="toolMode">Tool mode whose snap values are being queried.</param>
        /// <param name="isControlDown">True when either control key is pressed.</param>
        /// <param name="isShiftDown">True when either shift key is pressed.</param>
        /// <returns>Active snap value, or zero when no snap modifier is active.</returns>
        public static double GetActiveSnapValue(CameraComponent camera, EditorViewportToolMode toolMode, bool isControlDown, bool isShiftDown) {
            TransformGizmoSnapSlot activeSnapSlot = ResolveActiveSnapSlot(isControlDown, isShiftDown);
            if (activeSnapSlot == TransformGizmoSnapSlot.None) {
                return 0.0;
            }

            return GetSnapValue(camera, toolMode, activeSnapSlot);
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
        /// Adjusts one stored camera-scoped snap value by a multiplier and clamps it to tool-specific bounds.
        /// </summary>
        /// <param name="state">Snap-state bundle that owns the values.</param>
        /// <param name="toolMode">Tool mode whose value is being updated.</param>
        /// <param name="snapSlot">Snap slot to update.</param>
        /// <param name="multiplier">Multiplier applied to the current value.</param>
        static void AdjustSnapValue(TransformGizmoSnapSettingsState state, EditorViewportToolMode toolMode, TransformGizmoSnapSlot snapSlot, double multiplier) {
            if (state == null) {
                throw new ArgumentNullException(nameof(state));
            }

            ValidateSnapSlot(snapSlot);
            if (multiplier <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(multiplier), "Snap multiplier must be greater than zero.");
            }

            double currentValue = GetSnapValue(state, toolMode, snapSlot);
            double adjustedValue = RoundSnapValue(currentValue * multiplier);
            SetSnapValue(state, toolMode, snapSlot, adjustedValue);
        }

        /// <summary>
        /// Reads the configured snap value for one snap-state bundle, tool mode, and slot.
        /// </summary>
        /// <param name="state">Snap-state bundle that owns the values.</param>
        /// <param name="toolMode">Tool mode whose snap value should be read.</param>
        /// <param name="snapSlot">Snap slot to read.</param>
        /// <returns>Configured snap value.</returns>
        static double GetSnapValue(TransformGizmoSnapSettingsState state, EditorViewportToolMode toolMode, TransformGizmoSnapSlot snapSlot) {
            if (state == null) {
                throw new ArgumentNullException(nameof(state));
            }

            ValidateSnapSlot(snapSlot);

            if (snapSlot == TransformGizmoSnapSlot.Snap1) {
                return state.Snap1ValuesByToolMode[toolMode];
            }

            return state.Snap2ValuesByToolMode[toolMode];
        }

        /// <summary>
        /// Assigns one explicit snap value to one snap-state bundle after clamping it to tool-specific bounds.
        /// </summary>
        /// <param name="state">Snap-state bundle that owns the values.</param>
        /// <param name="toolMode">Tool mode whose snap value should be updated.</param>
        /// <param name="snapSlot">Snap slot to update.</param>
        /// <param name="value">Snap value to persist.</param>
        static void SetSnapValue(TransformGizmoSnapSettingsState state, EditorViewportToolMode toolMode, TransformGizmoSnapSlot snapSlot, double value) {
            if (state == null) {
                throw new ArgumentNullException(nameof(state));
            }

            ValidateSnapSlot(snapSlot);
            double adjustedValue = RoundSnapValue(value);
            double minimumValue = GetMinimumSnapValue(toolMode);
            double maximumValue = GetMaximumSnapValue(toolMode);
            if (adjustedValue < minimumValue) {
                adjustedValue = minimumValue;
            }
            if (adjustedValue > maximumValue) {
                adjustedValue = maximumValue;
            }

            if (snapSlot == TransformGizmoSnapSlot.Snap1) {
                state.Snap1ValuesByToolMode[toolMode] = adjustedValue;
                return;
            }

            state.Snap2ValuesByToolMode[toolMode] = adjustedValue;
        }

        /// <summary>
        /// Returns one camera-scoped snap-state bundle, creating it from defaults on first use.
        /// </summary>
        /// <param name="camera">Viewport camera whose snap state should be resolved.</param>
        /// <returns>Camera-scoped snap-state bundle.</returns>
        static TransformGizmoSnapSettingsState GetOrCreateState(CameraComponent camera) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            if (!StatesByCamera.TryGetValue(camera, out TransformGizmoSnapSettingsState state)) {
                state = CreateDefaultState();
                StatesByCamera[camera] = state;
            }

            return state;
        }

        /// <summary>
        /// Creates one fresh snap-state bundle initialized with the editor defaults.
        /// </summary>
        /// <returns>Fresh snap-state bundle.</returns>
        static TransformGizmoSnapSettingsState CreateDefaultState() {
            return new TransformGizmoSnapSettingsState(
                DefaultTranslateSnap1,
                DefaultTranslateSnap2,
                DefaultRotateSnap1,
                DefaultRotateSnap2,
                DefaultScaleSnap1,
                DefaultScaleSnap2);
        }

        /// <summary>
        /// Creates one snap-state bundle from the current shared global values.
        /// </summary>
        /// <returns>Snap-state bundle mirroring the current shared values.</returns>
        static TransformGizmoSnapSettingsState CreateDefaultStateFromGlobal() {
            return new TransformGizmoSnapSettingsState(
                Snap1ValuesByToolMode[EditorViewportToolMode.Translate],
                Snap2ValuesByToolMode[EditorViewportToolMode.Translate],
                Snap1ValuesByToolMode[EditorViewportToolMode.Rotate],
                Snap2ValuesByToolMode[EditorViewportToolMode.Rotate],
                Snap1ValuesByToolMode[EditorViewportToolMode.Scale],
                Snap2ValuesByToolMode[EditorViewportToolMode.Scale]);
        }

        /// <summary>
        /// Applies one snap-state bundle back onto the shared global snap values.
        /// </summary>
        /// <param name="state">Snap-state bundle whose values should become global defaults.</param>
        static void ApplyStateToGlobal(TransformGizmoSnapSettingsState state) {
            if (state == null) {
                throw new ArgumentNullException(nameof(state));
            }

            Snap1ValuesByToolMode[EditorViewportToolMode.Translate] = state.Snap1ValuesByToolMode[EditorViewportToolMode.Translate];
            Snap2ValuesByToolMode[EditorViewportToolMode.Translate] = state.Snap2ValuesByToolMode[EditorViewportToolMode.Translate];
            Snap1ValuesByToolMode[EditorViewportToolMode.Rotate] = state.Snap1ValuesByToolMode[EditorViewportToolMode.Rotate];
            Snap2ValuesByToolMode[EditorViewportToolMode.Rotate] = state.Snap2ValuesByToolMode[EditorViewportToolMode.Rotate];
            Snap1ValuesByToolMode[EditorViewportToolMode.Scale] = state.Snap1ValuesByToolMode[EditorViewportToolMode.Scale];
            Snap2ValuesByToolMode[EditorViewportToolMode.Scale] = state.Snap2ValuesByToolMode[EditorViewportToolMode.Scale];
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
