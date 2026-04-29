namespace helengine.editor.windows.tests.testing {
    /// <summary>
    /// Provides a controllable window-arrangement feature state for borderless-window controller tests.
    /// </summary>
    public sealed class TestWindowArrangementFeatureState : IWindowArrangementFeatureState {
        /// <summary>
        /// Gets or sets a value indicating whether Windows window arrangement is enabled.
        /// </summary>
        public bool IsWindowArrangingEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether dragging a window to a screen edge should dock or maximize it.
        /// </summary>
        public bool IsDockMovingEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether dragging a maximized title bar should restore the window.
        /// </summary>
        public bool IsDragFromMaximizeEnabled { get; set; }
    }
}
