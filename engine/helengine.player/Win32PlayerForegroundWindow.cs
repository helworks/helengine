using System.Runtime.InteropServices;

namespace helengine.player {
    /// <summary>
    /// Brings the player window to the foreground with the Win32 <c>SetForegroundWindow</c> call. Windows' foreground lock
    /// accepts it only when the editor granted the player the foreground right at launch; otherwise the call returns
    /// false and Windows flashes the window's taskbar button instead.
    /// </summary>
    public sealed class Win32PlayerForegroundWindow : IPlayerForegroundWindow {
        /// <summary>
        /// Asks Windows to make the window the foreground window.
        /// </summary>
        /// <param name="windowHandle">Native handle of the window to activate.</param>
        /// <returns>True when Windows brought the window to the foreground.</returns>
        public bool TrySetForeground(IntPtr windowHandle) {
            return SetForegroundWindow(windowHandle);
        }

        /// <summary>
        /// Win32 <c>SetForegroundWindow</c>: brings the thread that created the window into the foreground and activates
        /// the window. Returns false when the foreground lock refused the request.
        /// </summary>
        /// <param name="windowHandle">Window to activate.</param>
        /// <returns>True when the window was brought to the foreground.</returns>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr windowHandle);
    }
}
