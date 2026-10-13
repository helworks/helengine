namespace helengine.player.tests.input {
    /// <summary>
    /// Physical controller stand-in that reports a scripted pad, or no controller at all, and counts how often it was read.
    /// </summary>
    public sealed class FakePhysicalGamepadReader : IPlayerPhysicalGamepadReader {
        /// <summary>
        /// Gets or sets whether a controller is connected.
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// Gets or sets the pad reported while a controller is connected.
        /// </summary>
        public InputGamepadState Gamepad { get; set; }

        /// <summary>
        /// Gets how many times the reader was polled.
        /// </summary>
        public int ReadCount { get; private set; }

        /// <summary>
        /// Reports the scripted pad when <see cref="IsConnected"/> is set.
        /// </summary>
        /// <param name="gamepad">Receives the scripted pad, or a neutral pad when no controller is connected.</param>
        /// <returns>Whether a controller is connected.</returns>
        public bool TryReadGamepad(out InputGamepadState gamepad) {
            ReadCount++;
            gamepad = IsConnected ? Gamepad : new InputGamepadState();
            return IsConnected;
        }
    }
}
