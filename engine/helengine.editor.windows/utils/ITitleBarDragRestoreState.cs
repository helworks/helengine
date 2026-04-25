namespace helengine.editor.windows {
    /// <summary>
    /// Exposes borderless-window state that must be restored before a native title-bar drag begins.
    /// </summary>
    public interface ITitleBarDragRestoreState {
        /// <summary>
        /// Restores any custom maximized state so the window can continue moving under the current cursor position.
        /// </summary>
        /// <param name="cursorScreenPosition">Current cursor position in screen coordinates.</param>
        void PrepareForTitleBarDrag(Point cursorScreenPosition);
    }
}
