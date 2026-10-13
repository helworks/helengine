namespace helengine.baseplatform.Definitions {

    /// <summary>
    /// Names the input devices a platform's real hardware offers, so the desktop Play player forwards only those
    /// devices to the game (a PS2 game sees its pad, never the desktop mouse or keyboard).
    /// </summary>
    [Flags]
    public enum PlayerInputDevices {
        /// <summary>No input device; only valid for platforms that do not support Play.</summary>
        None = 0,
        /// <summary>A keyboard; keyboard keys reach the game as keyboard input.</summary>
        Keyboard = 1,
        /// <summary>A mouse; cursor position and mouse buttons reach the game.</summary>
        Mouse = 2,
        /// <summary>An abstract pointer device (touch screen or stylus style input).</summary>
        Pointer = 4,
        /// <summary>A gamepad; the player feeds slot 0 from a desktop controller and a keyboard-emulated pad.</summary>
        Gamepad = 8
    }
}
