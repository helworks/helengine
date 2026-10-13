namespace helengine.player {
    /// <summary>
    /// Combines two pads that drive the same gamepad slot (the desktop controller and the keyboard-emulated pad), so
    /// either one can be used at any moment without the other cancelling it.
    /// </summary>
    public static class PlayerGamepadMerger {
        /// <summary>
        /// Merges two pads: buttons are held when held on either pad, and each stick axis and trigger takes the value
        /// with the larger magnitude (the first pad wins ties).
        /// </summary>
        /// <param name="first">First pad (the desktop controller).</param>
        /// <param name="second">Second pad (the keyboard-emulated pad).</param>
        /// <returns>The merged pad, connected when either source is connected.</returns>
        public static InputGamepadState Merge(InputGamepadState first, InputGamepadState second) {
            return new InputGamepadState {
                Connected = first.Connected || second.Connected,
                Buttons = first.Buttons | second.Buttons,
                LeftStickX = LargerMagnitude(first.LeftStickX, second.LeftStickX),
                LeftStickY = LargerMagnitude(first.LeftStickY, second.LeftStickY),
                RightStickX = LargerMagnitude(first.RightStickX, second.RightStickX),
                RightStickY = LargerMagnitude(first.RightStickY, second.RightStickY),
                LeftTrigger = LargerMagnitude(first.LeftTrigger, second.LeftTrigger),
                RightTrigger = LargerMagnitude(first.RightTrigger, second.RightTrigger)
            };
        }

        /// <summary>
        /// Picks the axis value farther from center.
        /// </summary>
        /// <param name="first">First candidate; returned on ties.</param>
        /// <param name="second">Second candidate.</param>
        /// <returns>The candidate with the larger magnitude.</returns>
        static short LargerMagnitude(short first, short second) {
            return Math.Abs((int)second) > Math.Abs((int)first) ? second : first;
        }
    }
}
