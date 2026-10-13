namespace helengine.player {
    /// <summary>
    /// Binds one keyboard key to one engine pad button of the keyboard-emulated pad.
    /// </summary>
    public sealed class PlayerKeyboardGamepadButtonBinding {
        /// <summary>
        /// Initializes one key-to-button binding.
        /// </summary>
        /// <param name="key">Key that presses the button while held.</param>
        /// <param name="button">Engine pad button pressed by the key.</param>
        public PlayerKeyboardGamepadButtonBinding(Keys key, InputGamepadButton button) {
            Key = key;
            Button = button;
        }

        /// <summary>
        /// Gets the key that presses <see cref="Button"/> while held.
        /// </summary>
        public Keys Key { get; }

        /// <summary>
        /// Gets the engine pad button the key presses.
        /// </summary>
        public InputGamepadButton Button { get; }
    }
}
