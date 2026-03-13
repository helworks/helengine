using helengine;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides a configurable input manager backed by test keyboard and mouse devices.
    /// </summary>
    public class TestInputManager : InputManager {
        /// <summary>
        /// Initializes a new configurable input manager for editor tests.
        /// </summary>
        public TestInputManager() {
            Keyboard = new TestKeyboard();
            Mouse = new TestMouse();
        }

        /// <summary>
        /// Gets the configurable keyboard used by this input manager.
        /// </summary>
        public TestKeyboard TestKeyboard {
            get { return (TestKeyboard)Keyboard; }
        }

        /// <summary>
        /// Gets the configurable mouse used by this input manager.
        /// </summary>
        public TestMouse TestMouse {
            get { return (TestMouse)Mouse; }
        }

        /// <summary>
        /// Replaces the keyboard state returned during the next input capture.
        /// </summary>
        /// <param name="state">Keyboard state to expose.</param>
        public void SetKeyboardState(KeyboardState state) {
            TestKeyboard.State = state;
        }

        /// <summary>
        /// Replaces the mouse state returned during the next input capture.
        /// </summary>
        /// <param name="state">Mouse state to expose.</param>
        public void SetMouseState(MouseState state) {
            TestMouse.State = state;
        }
    }
}
