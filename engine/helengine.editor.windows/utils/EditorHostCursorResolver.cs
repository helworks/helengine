using helengine.editor;

namespace helengine.editor.windows {
    /// <summary>
    /// Resolves the native Windows cursor for the editor host from docking, resize, and hovered interactable state.
    /// </summary>
    public static class EditorHostCursorResolver {
        /// <summary>
        /// Resolves the native cursor to display for the current editor-host pointer state.
        /// </summary>
        /// <param name="dockingCursorState">Docking cursor state requested by the editor layout.</param>
        /// <param name="hoverCursor">Cursor requested by the currently hovered interactable.</param>
        /// <param name="hasResizeCursor">True when border-resize hit testing found a resize cursor.</param>
        /// <param name="resizeCursor">Resize cursor returned by border-resize hit testing.</param>
        /// <returns>The native Windows cursor the editor host should display.</returns>
        public static Cursor Resolve(DockingCursorState dockingCursorState, PointerCursorKind hoverCursor, bool hasResizeCursor, Cursor resizeCursor) {
            switch (dockingCursorState) {
                case DockingCursorState.VerticalSplit:
                    return Cursors.VSplit;
                case DockingCursorState.HorizontalSplit:
                    return Cursors.HSplit;
            }

            if (hasResizeCursor) {
                return resizeCursor;
            }

            switch (hoverCursor) {
                case PointerCursorKind.Hand:
                    return Cursors.Hand;
                case PointerCursorKind.Text:
                    return Cursors.IBeam;
                default:
                    return Cursors.Default;
            }
        }
    }
}
