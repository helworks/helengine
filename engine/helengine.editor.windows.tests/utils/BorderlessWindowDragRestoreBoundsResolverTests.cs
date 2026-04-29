using Xunit;

namespace helengine.editor.windows.tests.utils {
    /// <summary>
    /// Verifies restored borderless-window bounds used when a maximized title-bar drag begins.
    /// </summary>
    public sealed class BorderlessWindowDragRestoreBoundsResolverTests {
        /// <summary>
        /// Ensures restoring from a maximized drag preserves the cursor's horizontal position relative to the window.
        /// </summary>
        [Fact]
        public void Resolve_WhenDragStartsFromMaximizedBounds_PlacesRestoredWindowUnderCursor() {
            Rectangle maximizedBounds = new Rectangle(0, 0, 1920, 1040);
            Rectangle restoreBounds = new Rectangle(160, 120, 1200, 800);
            Point cursorScreenPosition = new Point(1440, 18);
            Rectangle workingArea = new Rectangle(0, 0, 1920, 1040);

            Rectangle restoredBounds = BorderlessWindowDragRestoreBoundsResolver.Resolve(
                maximizedBounds,
                restoreBounds,
                cursorScreenPosition,
                workingArea);

            Assert.Equal(540, restoredBounds.Left);
            Assert.Equal(0, restoredBounds.Top);
            Assert.Equal(restoreBounds.Width, restoredBounds.Width);
            Assert.Equal(restoreBounds.Height, restoredBounds.Height);
        }

        /// <summary>
        /// Ensures restored bounds are clamped inside the current working area when the cursor is near the screen edge.
        /// </summary>
        [Fact]
        public void Resolve_WhenCursorIsNearRightEdge_ClampsTheRestoredWindowInsideWorkingArea() {
            Rectangle maximizedBounds = new Rectangle(0, 0, 1920, 1040);
            Rectangle restoreBounds = new Rectangle(200, 100, 1200, 800);
            Point cursorScreenPosition = new Point(1915, 20);
            Rectangle workingArea = new Rectangle(0, 0, 1920, 1040);

            Rectangle restoredBounds = BorderlessWindowDragRestoreBoundsResolver.Resolve(
                maximizedBounds,
                restoreBounds,
                cursorScreenPosition,
                workingArea);

            Assert.Equal(720, restoredBounds.Left);
            Assert.Equal(0, restoredBounds.Top);
            Assert.Equal(restoreBounds.Width, restoredBounds.Width);
            Assert.Equal(restoreBounds.Height, restoredBounds.Height);
        }
    }
}
