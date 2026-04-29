namespace helengine.editor.windows {
    /// <summary>
    /// Tracks custom maximize state for a borderless window and restores normal bounds for title-bar dragging.
    /// </summary>
    public sealed class BorderlessWindowStateController {
        /// <summary>
        /// Provides the current Windows arrangement feature flags used to mirror native drag behavior.
        /// </summary>
        readonly IWindowArrangementFeatureState WindowArrangementFeatureState;
        /// <summary>
        /// Tracks whether the host window is currently using the custom maximized bounds.
        /// </summary>
        bool IsMaximizedValue;
        /// <summary>
        /// Stores the most recent normal-state bounds to restore after custom maximization.
        /// </summary>
        Rectangle RestoreBoundsValue;
        /// <summary>
        /// Tracks whether a title-bar drag is currently expected to complete through the native move loop.
        /// </summary>
        bool HasPendingTitleBarDragValue;

        /// <summary>
        /// Gets a value indicating whether the host window currently allows resize-border behavior.
        /// </summary>
        public bool IsResizeBorderEnabled => !IsMaximizedValue;

        /// <summary>
        /// Initializes the controller with the Windows arrangement feature state used to decide which drag behaviors to emulate.
        /// </summary>
        /// <param name="windowArrangementFeatureState">Current Windows arrangement feature state provider.</param>
        public BorderlessWindowStateController(IWindowArrangementFeatureState windowArrangementFeatureState) {
            if (windowArrangementFeatureState == null) {
                throw new ArgumentNullException(nameof(windowArrangementFeatureState));
            }

            WindowArrangementFeatureState = windowArrangementFeatureState;
        }

        /// <summary>
        /// Toggles the host window between its stored normal bounds and the active screen working area.
        /// </summary>
        /// <param name="hostForm">Borderless host window whose bounds should be updated.</param>
        public void ToggleMaximize(Form hostForm) {
            if (hostForm == null) {
                throw new ArgumentNullException(nameof(hostForm));
            }

            HasPendingTitleBarDragValue = false;

            if (IsMaximizedValue) {
                RestoreFromMaximize(hostForm);
                return;
            }

            Maximize(hostForm, Screen.FromControl(hostForm).WorkingArea);
        }

        /// <summary>
        /// Restores the host window from custom maximize bounds before the native title-bar drag begins.
        /// </summary>
        /// <param name="hostForm">Borderless host window whose bounds should be restored.</param>
        /// <param name="cursorScreenPosition">Current cursor position in screen coordinates.</param>
        public void PrepareForTitleBarDrag(Form hostForm, Point cursorScreenPosition) {
            if (hostForm == null) {
                throw new ArgumentNullException(nameof(hostForm));
            }

            HasPendingTitleBarDragValue = IsDockMovingEnabled();

            if (!IsMaximizedValue) {
                return;
            }

            if (!IsDragFromMaximizeEnabled()) {
                return;
            }

            EnsureRestoreBoundsAreAvailable();

            Rectangle restoredBounds = BorderlessWindowDragRestoreBoundsResolver.Resolve(
                hostForm.Bounds,
                RestoreBoundsValue,
                cursorScreenPosition,
                Screen.FromPoint(cursorScreenPosition).WorkingArea);
            hostForm.Bounds = restoredBounds;
            IsMaximizedValue = false;
        }

        /// <summary>
        /// Completes a pending title-bar drag and maximizes the host when the drag ends at the top edge of the active screen.
        /// </summary>
        /// <param name="hostForm">Borderless host window whose move loop completed.</param>
        /// <param name="cursorScreenPosition">Current cursor position in screen coordinates.</param>
        public void CompleteTitleBarDrag(Form hostForm, Point cursorScreenPosition) {
            if (hostForm == null) {
                throw new ArgumentNullException(nameof(hostForm));
            }

            if (!HasPendingTitleBarDragValue) {
                return;
            }

            HasPendingTitleBarDragValue = false;

            if (IsMaximizedValue) {
                return;
            }

            if (!IsDockMovingEnabled()) {
                return;
            }

            Rectangle workingArea = Screen.FromPoint(cursorScreenPosition).WorkingArea;
            if (!BorderlessWindowTopEdgeMaximizeTrigger.ShouldMaximize(cursorScreenPosition, workingArea)) {
                return;
            }

            Maximize(hostForm, workingArea);
        }

        /// <summary>
        /// Ensures the stored restore bounds are available before restoring from custom maximize state.
        /// </summary>
        void EnsureRestoreBoundsAreAvailable() {
            EnsureBoundsAreValid(RestoreBoundsValue, "Stored restore bounds");
        }

        /// <summary>
        /// Determines whether custom drag-to-top maximize should be enabled for the current Windows settings.
        /// </summary>
        /// <returns>True when Windows allows docking by dragging to a screen edge.</returns>
        bool IsDockMovingEnabled() {
            return WindowArrangementFeatureState.IsWindowArrangingEnabled && WindowArrangementFeatureState.IsDockMovingEnabled;
        }

        /// <summary>
        /// Determines whether a maximized title-bar drag should restore the window before the native move loop begins.
        /// </summary>
        /// <returns>True when Windows allows dragging a maximized title bar to restore the window.</returns>
        bool IsDragFromMaximizeEnabled() {
            return WindowArrangementFeatureState.IsWindowArrangingEnabled && WindowArrangementFeatureState.IsDragFromMaximizeEnabled;
        }

        /// <summary>
        /// Applies the custom maximized bounds for the provided working area and stores the current bounds for restoration.
        /// </summary>
        /// <param name="hostForm">Borderless host window whose bounds should be updated.</param>
        /// <param name="workingArea">Working area that should become the maximized bounds.</param>
        void Maximize(Form hostForm, Rectangle workingArea) {
            EnsureBoundsAreValid(hostForm.Bounds, "Window bounds");
            EnsureBoundsAreValid(workingArea, "Working area");
            RestoreBoundsValue = hostForm.Bounds;
            hostForm.Bounds = new Rectangle(workingArea.Left, workingArea.Top, workingArea.Width, workingArea.Height);
            IsMaximizedValue = true;
        }

        /// <summary>
        /// Restores the stored normal bounds after custom maximization.
        /// </summary>
        /// <param name="hostForm">Borderless host window whose bounds should be restored.</param>
        void RestoreFromMaximize(Form hostForm) {
            EnsureRestoreBoundsAreAvailable();
            hostForm.Bounds = RestoreBoundsValue;
            IsMaximizedValue = false;
        }

        /// <summary>
        /// Ensures the provided bounds describe a visible rectangle.
        /// </summary>
        /// <param name="bounds">Bounds to validate.</param>
        /// <param name="description">Description of the value being validated.</param>
        void EnsureBoundsAreValid(Rectangle bounds, string description) {
            if (bounds.Width <= 0 || bounds.Height <= 0) {
                throw new InvalidOperationException($"{description} must contain a visible rectangle.");
            }
        }
    }
}
