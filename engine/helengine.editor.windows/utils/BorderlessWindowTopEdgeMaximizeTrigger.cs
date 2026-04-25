namespace helengine.editor.windows {
    /// <summary>
    /// Determines whether a borderless title-bar drag ended in the screen region that should maximize the window.
    /// </summary>
    public static class BorderlessWindowTopEdgeMaximizeTrigger {
        /// <summary>
        /// Determines whether the provided drag-end cursor position should maximize the window for the active working area.
        /// </summary>
        /// <param name="cursorScreenPosition">Cursor position in screen coordinates when the title-bar drag ends.</param>
        /// <param name="workingArea">Working area of the screen under the cursor.</param>
        /// <returns>True when the drag ended at or above the working-area top edge and inside its horizontal bounds.</returns>
        public static bool ShouldMaximize(Point cursorScreenPosition, Rectangle workingArea) {
            bool isWithinHorizontalBounds = cursorScreenPosition.X >= workingArea.Left && cursorScreenPosition.X < workingArea.Right;
            bool isAtOrAboveTopEdge = cursorScreenPosition.Y <= workingArea.Top;
            return isWithinHorizontalBounds && isAtOrAboveTopEdge;
        }
    }
}
