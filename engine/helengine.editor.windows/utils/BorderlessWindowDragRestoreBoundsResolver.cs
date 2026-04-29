namespace helengine.editor.windows {
    /// <summary>
    /// Resolves restored bounds for a borderless window when a title-bar drag starts from a custom maximized state.
    /// </summary>
    public static class BorderlessWindowDragRestoreBoundsResolver {
        /// <summary>
        /// Calculates restored bounds that keep the cursor over the same relative point in the restored window.
        /// </summary>
        /// <param name="maximizedBounds">Current maximized bounds of the borderless window.</param>
        /// <param name="restoreBounds">Stored normal-state bounds to restore.</param>
        /// <param name="cursorScreenPosition">Current cursor position in screen coordinates.</param>
        /// <param name="workingArea">Working area of the screen that should contain the restored window.</param>
        /// <returns>Restored bounds clamped inside the provided working area.</returns>
        public static Rectangle Resolve(
            Rectangle maximizedBounds,
            Rectangle restoreBounds,
            Point cursorScreenPosition,
            Rectangle workingArea) {
            ValidateBounds(maximizedBounds, nameof(maximizedBounds));
            ValidateBounds(restoreBounds, nameof(restoreBounds));
            ValidateBounds(workingArea, nameof(workingArea));

            double horizontalRatio = ResolveHorizontalRatio(maximizedBounds, cursorScreenPosition);
            int horizontalOffset = ResolveHorizontalOffset(restoreBounds, horizontalRatio);
            int verticalOffset = ResolveVerticalOffset(maximizedBounds, restoreBounds, cursorScreenPosition);
            Rectangle restoredBounds = new Rectangle(
                cursorScreenPosition.X - horizontalOffset,
                cursorScreenPosition.Y - verticalOffset,
                restoreBounds.Width,
                restoreBounds.Height);
            return ClampToWorkingArea(restoredBounds, workingArea);
        }

        /// <summary>
        /// Validates that the provided bounds can describe a visible rectangle.
        /// </summary>
        /// <param name="bounds">Bounds to validate.</param>
        /// <param name="parameterName">Name of the parameter being validated.</param>
        static void ValidateBounds(Rectangle bounds, string parameterName) {
            if (bounds.Width <= 0 || bounds.Height <= 0) {
                throw new ArgumentException("Bounds must have a positive width and height.", parameterName);
            }
        }

        /// <summary>
        /// Resolves the cursor's horizontal ratio across the maximized window width.
        /// </summary>
        /// <param name="maximizedBounds">Current maximized bounds.</param>
        /// <param name="cursorScreenPosition">Current cursor position.</param>
        /// <returns>Horizontal ratio clamped between zero and one.</returns>
        static double ResolveHorizontalRatio(Rectangle maximizedBounds, Point cursorScreenPosition) {
            if (maximizedBounds.Width <= 1) {
                return 0d;
            }

            double ratio = (double)(cursorScreenPosition.X - maximizedBounds.Left) / maximizedBounds.Width;
            return Math.Clamp(ratio, 0d, 1d);
        }

        /// <summary>
        /// Resolves the restored-window horizontal cursor offset from the left edge.
        /// </summary>
        /// <param name="restoreBounds">Bounds that will be restored.</param>
        /// <param name="horizontalRatio">Horizontal ratio across the maximized window.</param>
        /// <returns>Horizontal cursor offset inside the restored window.</returns>
        static int ResolveHorizontalOffset(Rectangle restoreBounds, double horizontalRatio) {
            int maximumHorizontalOffset = Math.Max(restoreBounds.Width - 1, 0);
            int resolvedOffset = (int)Math.Round(restoreBounds.Width * horizontalRatio, MidpointRounding.AwayFromZero);
            return Math.Clamp(resolvedOffset, 0, maximumHorizontalOffset);
        }

        /// <summary>
        /// Resolves the restored-window vertical cursor offset from the top edge.
        /// </summary>
        /// <param name="maximizedBounds">Current maximized bounds.</param>
        /// <param name="restoreBounds">Bounds that will be restored.</param>
        /// <param name="cursorScreenPosition">Current cursor position.</param>
        /// <returns>Vertical cursor offset inside the restored window.</returns>
        static int ResolveVerticalOffset(Rectangle maximizedBounds, Rectangle restoreBounds, Point cursorScreenPosition) {
            int maximumVerticalOffset = Math.Max(restoreBounds.Height - 1, 0);
            int resolvedOffset = cursorScreenPosition.Y - maximizedBounds.Top;
            return Math.Clamp(resolvedOffset, 0, maximumVerticalOffset);
        }

        /// <summary>
        /// Clamps restored bounds so the window remains inside the current working area.
        /// </summary>
        /// <param name="restoredBounds">Restored bounds before clamping.</param>
        /// <param name="workingArea">Screen working area that should contain the window.</param>
        /// <returns>Bounds adjusted to remain visible inside the working area.</returns>
        static Rectangle ClampToWorkingArea(Rectangle restoredBounds, Rectangle workingArea) {
            int maximumLeft = Math.Max(workingArea.Left, workingArea.Right - restoredBounds.Width);
            int maximumTop = Math.Max(workingArea.Top, workingArea.Bottom - restoredBounds.Height);
            int clampedLeft = Math.Clamp(restoredBounds.Left, workingArea.Left, maximumLeft);
            int clampedTop = Math.Clamp(restoredBounds.Top, workingArea.Top, maximumTop);
            return new Rectangle(clampedLeft, clampedTop, restoredBounds.Width, restoredBounds.Height);
        }
    }
}
