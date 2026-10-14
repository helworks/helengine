using System.Runtime.InteropServices;

namespace helengine.editor {
    /// <summary>
    /// Grants the started player process the right to set the foreground window through the Win32
    /// <c>AllowSetForegroundWindow</c> call, reading its result and last error instead of throwing.
    /// <para>
    /// Windows only accepts the grant while the editor itself may set the foreground: it is the foreground app or it
    /// received the last input event. When the user switched to another app while Play was cooking, the grant is refused
    /// with <c>ERROR_ACCESS_DENIED</c> and the player opens behind that app and flashes in the taskbar. That is intended:
    /// the editor never steals the foreground forcibly (no <c>AttachThreadInput</c> or foreground-lock workarounds).
    /// </para>
    /// </summary>
    public sealed class EditorPlayWin32ForegroundPermission : IEditorPlayForegroundPermission {
        /// <summary>
        /// Error code reported when the grant is requested on an operating system without the Win32 foreground API.
        /// </summary>
        public const int NotSupportedErrorCode = 50;

        /// <summary>
        /// Allows the process to set the foreground window. On systems other than Windows the grant is reported as refused
        /// with <see cref="NotSupportedErrorCode"/> and no native call is made.
        /// </summary>
        /// <param name="processId">Operating-system identifier of the started player process.</param>
        /// <param name="errorCode">Win32 last error of a refused grant; 0 when the grant succeeded.</param>
        /// <returns>True when Windows accepted the grant.</returns>
        public bool TryAllow(int processId, out int errorCode) {
            if (!OperatingSystem.IsWindows()) {
                errorCode = NotSupportedErrorCode;
                return false;
            }

            if (AllowSetForegroundWindow(processId)) {
                errorCode = 0;
                return true;
            }

            errorCode = Marshal.GetLastPInvokeError();
            return false;
        }

        /// <summary>
        /// Win32 <c>AllowSetForegroundWindow</c>: enables the process to set the foreground window. Returns false, with the
        /// reason in the last error, when the calling process may not set the foreground itself.
        /// </summary>
        /// <param name="processId">Process identifier to enable.</param>
        /// <returns>True when the call succeeded.</returns>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllowSetForegroundWindow(int processId);
    }
}
