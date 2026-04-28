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
        /// Gets or sets a value indicating whether the owning window is currently foreground-active.
        /// </summary>
        public bool IsForegroundActive { get; set; } = true;

        /// <summary>
        /// Returns the configured mouse state for the current test frame.
        /// </summary>
        /// <returns>Mouse state supplied by the test.</returns>
        public override MouseState GetState() {
            MouseState currentState = State;
            if (!IsForegroundActive) {
                MouseState inactiveState = currentState;
                inactiveState.LeftButton = ButtonState.Released;
                inactiveState.MiddleButton = ButtonState.Released;
                inactiveState.RightButton = ButtonState.Released;
                inactiveState.XButton1 = ButtonState.Released;
                inactiveState.XButton2 = ButtonState.Released;
                return inactiveState;
            }

            return currentState;
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
