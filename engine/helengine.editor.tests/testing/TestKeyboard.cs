using helengine;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides a configurable keyboard device for input-driven editor tests.
    /// </summary>
    public class TestKeyboard : Keyboard {
        /// <summary>
        /// Gets or sets the keyboard state returned to the input manager.
        /// </summary>
        public KeyboardState State { get; set; }

        /// <summary>
        /// Returns the configured keyboard state for the current test frame.
        /// </summary>
        /// <returns>Keyboard state supplied by the test.</returns>
        public override KeyboardState GetState() {
            return State;
        }
    }
}
