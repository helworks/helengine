namespace helengine.player {
    /// <summary>
    /// Reads the desktop game controller the player uses as the platform pad. A missing controller (or a missing
    /// controller driver) is a normal state reported through the return value, never through an exception.
    /// </summary>
    public interface IPlayerPhysicalGamepadReader {
        /// <summary>
        /// Reads the first connected desktop controller, already mapped to the engine pad layout.
        /// </summary>
        /// <param name="gamepad">Receives the mapped controller state, or a neutral pad when no controller is connected.</param>
        /// <returns>True when a controller was connected and read.</returns>
        bool TryReadGamepad(out InputGamepadState gamepad);
    }
}
