using Xunit;

namespace helengine.editor.windows.tests.utils {
    /// <summary>
    /// Verifies when a borderless title-bar drag should maximize after ending at the top of a screen working area.
    /// </summary>
    public sealed class BorderlessWindowTopEdgeMaximizeTriggerTests {
        /// <summary>
        /// Ensures a drag ending on the working-area top edge requests maximize.
        /// </summary>
        [Fact]
        public void ShouldMaximize_WhenCursorTouchesWorkingAreaTop_ReturnsTrue() {
            Point cursorScreenPosition = new Point(960, 32);
            Rectangle workingArea = new Rectangle(0, 32, 1920, 1048);

            bool shouldMaximize = BorderlessWindowTopEdgeMaximizeTrigger.ShouldMaximize(cursorScreenPosition, workingArea);

            Assert.True(shouldMaximize);
        }

        /// <summary>
        /// Ensures a drag ending below the working-area top edge keeps the window in its normal state.
        /// </summary>
        [Fact]
        public void ShouldMaximize_WhenCursorEndsBelowWorkingAreaTop_ReturnsFalse() {
            Point cursorScreenPosition = new Point(960, 33);
            Rectangle workingArea = new Rectangle(0, 32, 1920, 1048);

            bool shouldMaximize = BorderlessWindowTopEdgeMaximizeTrigger.ShouldMaximize(cursorScreenPosition, workingArea);

            Assert.False(shouldMaximize);
        }

        /// <summary>
        /// Ensures a drag ending outside the working area's horizontal span does not request maximize.
        /// </summary>
        [Fact]
        public void ShouldMaximize_WhenCursorEndsOutsideWorkingAreaWidth_ReturnsFalse() {
            Point cursorScreenPosition = new Point(1920, 32);
            Rectangle workingArea = new Rectangle(0, 32, 1920, 1048);

            bool shouldMaximize = BorderlessWindowTopEdgeMaximizeTrigger.ShouldMaximize(cursorScreenPosition, workingArea);

            Assert.False(shouldMaximize);
        }
    }
}
