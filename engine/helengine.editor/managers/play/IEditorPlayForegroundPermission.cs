namespace helengine.editor {
    /// <summary>
    /// Lets the editor hand its right to take the foreground to a player process it just started, so the player window
    /// can bring itself to the front when it opens. Windows' foreground lock otherwise refuses a background process that
    /// tries to activate its own window. The step only succeeds while the editor itself may set the foreground (it is the
    /// foreground app or received the last input); it never forces the foreground.
    /// </summary>
    public interface IEditorPlayForegroundPermission {
        /// <summary>
        /// Allows the process to set the foreground window. Refusal is reported through the return value, never thrown.
        /// </summary>
        /// <param name="processId">Operating-system identifier of the started player process.</param>
        /// <param name="errorCode">Win32 error code of a refused grant; 0 when the grant succeeded.</param>
        /// <returns>True when the process may now set the foreground window.</returns>
        bool TryAllow(int processId, out int errorCode);
    }
}
