using helengine;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides a configurable mouse device for input-driven editor tests.
    /// </summary>
    public class TestMouse : Mouse {
        /// <summary>
        /// Gets or sets the mouse state returned to the input manager.
        /// </summary>
        public MouseState State { get; set; }

        /// <summary>
        /// Returns the configured mouse state for the current test frame.
        /// </summary>
        /// <returns>Mouse state supplied by the test.</returns>
        public override MouseState GetState() {
            return State;
        }

        /// <summary>
        /// Updates the stored cursor position in the configurable mouse state.
        /// </summary>
        /// <param name="x">Horizontal cursor position.</param>
        /// <param name="y">Vertical cursor position.</param>
        public override void SetPosition(int x, int y) {
            MouseState state = State;
            state.X = x;
            state.Y = y;
            State = state;
        }
    }
}
