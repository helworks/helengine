namespace helengine.editor.windows {
    /// <summary>
    /// Exposes the Windows window-arrangement feature flags that affect drag-to-snap and drag-from-maximize behavior.
    /// </summary>
    public interface IWindowArrangementFeatureState {
        /// <summary>
        /// Gets a value indicating whether Windows window arrangement is enabled globally.
        /// </summary>
        bool IsWindowArrangingEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether dragging a window to a screen edge should dock or maximize it.
        /// </summary>
        bool IsDockMovingEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether dragging a maximized title bar should restore the window.
        /// </summary>
        bool IsDragFromMaximizeEnabled { get; }
    }
}
