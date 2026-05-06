using helengine;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides a configurable raw input backend for deterministic editor tests.
    /// </summary>
    internal sealed class TestInputBackend : IInputBackend {
        /// <summary>
        /// Gets or sets the keyboard state returned by the backend.
        /// </summary>
        public KeyboardState KeyboardState { get; set; }

        /// <summary>
        /// Gets or sets the mouse state returned by the backend.
        /// </summary>
        public MouseState MouseState { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the simulated host window is foreground active.
        /// </summary>
        public bool IsForegroundActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the gamepad states returned by the backend.
        /// </summary>
        public InputGamepadState[] Gamepads { get; set; } = Array.Empty<InputGamepadState>();

        /// <summary>
        /// Gets or sets the number of valid gamepad entries returned by the backend.
        /// </summary>
        public int GamepadCount { get; set; }

        /// <summary>
        /// Advances the active core input system through its early frame phase.
        /// </summary>
        public void EarlyUpdate() {
            ResolveInputSystem().EarlyUpdate();
        }

        /// <summary>
        /// Completes the active core input system frame.
        /// </summary>
        public void Update() {
            ResolveInputSystem().Update();
            ResolvePointerInteractionSystem().Update();
        }

        /// <summary>
        /// Gets the pointer position reported by the active core input system.
        /// </summary>
        /// <returns>Current mouse position in window coordinates.</returns>
        public int2 GetMousePosition() {
            return ResolveInputSystem().GetMousePosition();
        }

        /// <summary>
        /// Gets the pointer delta reported by the active core input system.
        /// </summary>
        /// <returns>Current mouse delta in window coordinates.</returns>
        public int2 GetMouseDelta() {
            return ResolveInputSystem().GetMouseDelta();
        }

        /// <summary>
        /// Gets the cursor requested by the active pointer interaction system.
        /// </summary>
        public PointerCursorKind HoverCursor {
            get { return ResolvePointerInteractionSystem().HoverCursor; }
        }

        /// <summary>
        /// Captures the configured raw input frame for the current test step.
        /// </summary>
        /// <returns>Input frame supplied by the test.</returns>
        public InputFrameState CaptureFrame() {
            InputFrameState frame = new InputFrameState();
            frame.Keyboard = KeyboardState;
            frame.Mouse = CaptureMouseState();
            frame.Gamepads = Gamepads;
            frame.GamepadCount = GamepadCount;
            return frame;
        }

        /// <summary>
        /// Replaces the keyboard state returned during the next input capture.
        /// </summary>
        /// <param name="state">Keyboard state to expose.</param>
        public void SetKeyboardState(KeyboardState state) {
            KeyboardState = state;
        }

        /// <summary>
        /// Replaces the mouse state returned during the next input capture.
        /// </summary>
        /// <param name="state">Mouse state to expose.</param>
        public void SetMouseState(MouseState state) {
            MouseState = state;
        }

        /// <summary>
        /// Replaces the gamepad states returned during the next input capture.
        /// </summary>
        /// <param name="states">Gamepad states to expose.</param>
        public void SetGamepadStates(InputGamepadState[] states) {
            if (states == null) {
                throw new ArgumentNullException(nameof(states));
            }

            Gamepads = states;
            GamepadCount = states.Length;
        }

        /// <summary>
        /// Captures the current mouse state while suppressing clicks when the test window is inactive.
        /// </summary>
        /// <returns>Mouse state supplied by the test.</returns>
        MouseState CaptureMouseState() {
            MouseState state = MouseState;
            if (IsForegroundActive) {
                return state;
            }

            state.LeftButton = ButtonState.Released;
            state.MiddleButton = ButtonState.Released;
            state.RightButton = ButtonState.Released;
            state.XButton1 = ButtonState.Released;
            state.XButton2 = ButtonState.Released;
            return state;
        }

        /// <summary>
        /// Resolves the active core input system used by the current test.
        /// </summary>
        /// <returns>Current input system owned by the active core instance.</returns>
        InputSystem ResolveInputSystem() {
            if (Core.Instance == null) {
                throw new InvalidOperationException("A core instance must exist before advancing test input.");
            }

            return Core.Instance.InputSystem;
        }

        /// <summary>
        /// Resolves the active pointer interaction system used by the current test.
        /// </summary>
        /// <returns>Current pointer interaction system owned by the active core instance.</returns>
        PointerInteractionSystem ResolvePointerInteractionSystem() {
            if (Core.Instance == null) {
                throw new InvalidOperationException("A core instance must exist before reading the hover cursor.");
            }

            return Core.Instance.PointerInteractionSystem;
        }
    }
}
