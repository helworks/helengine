namespace helengine.player {
    /// <summary>
    /// Brings a window to the foreground. Abstracts the Win32 call so the player's activation step can be verified
    /// without touching the real desktop.
    /// </summary>
    public interface IPlayerForegroundWindow {
        /// <summary>
        /// Asks the system to make the window the foreground window. Refusal is reported through the return value, never
        /// thrown.
        /// </summary>
        /// <param name="windowHandle">Native handle of the window to activate.</param>
        /// <returns>True when the window was brought to the foreground.</returns>
        bool TrySetForeground(IntPtr windowHandle);
    }
}
