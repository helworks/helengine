using System.Runtime.InteropServices;

namespace helengine.player {
    /// <summary>
    /// Finds a process's main window without throwing: walks the top-level windows with <c>EnumWindows</c> and returns the
    /// first visible, unowned one whose owning process (<c>GetWindowThreadProcessId</c>) matches. Unlike
    /// <see cref="System.Diagnostics.Process.GetProcessById(int)"/>, a process that has exited simply has no window, so the
    /// caller reports its own error instead of a first-chance exception from the base class library.
    /// </summary>
    public sealed class Win32ProcessMainWindowFinder {
        /// <summary><c>GetWindow</c> command that returns the owner window.</summary>
        const uint GetWindowOwner = 4;

        /// <summary>Process whose main window is searched for.</summary>
        readonly uint TargetProcessId;

        /// <summary>First matching window found so far; zero until one matches.</summary>
        IntPtr FoundWindow;

        /// <summary>
        /// Initializes a finder for one process.
        /// </summary>
        /// <param name="processId">Process whose main window is searched for.</param>
        Win32ProcessMainWindowFinder(int processId) {
            TargetProcessId = (uint)processId;
        }

        /// <summary>
        /// Callback signature of <c>EnumWindows</c>.
        /// </summary>
        /// <param name="windowHandle">Top-level window being visited.</param>
        /// <param name="parameter">Caller-supplied value; unused.</param>
        /// <returns>True to continue the enumeration.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate bool EnumWindowsCallback(IntPtr windowHandle, IntPtr parameter);

        /// <summary>
        /// Returns the main window of a process: its first visible, unowned top-level window.
        /// </summary>
        /// <param name="processId">Positive process id.</param>
        /// <returns>The window handle, or <see cref="IntPtr.Zero"/> when the process has no such window or is not running.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the process id is not positive.</exception>
        public static IntPtr Find(int processId) {
            if (processId <= 0) {
                throw new ArgumentOutOfRangeException(nameof(processId), processId, "A process id must be positive.");
            }

            Win32ProcessMainWindowFinder finder = new Win32ProcessMainWindowFinder(processId);
            EnumWindowsCallback callback = finder.Visit;
            EnumWindows(callback, IntPtr.Zero);
            GC.KeepAlive(callback);
            return finder.FoundWindow;
        }

        /// <summary>
        /// Visits one top-level window and stops the enumeration at the first visible, unowned window of the target process.
        /// </summary>
        /// <param name="windowHandle">Top-level window being visited.</param>
        /// <param name="parameter">Unused.</param>
        /// <returns>False once the main window is found, which ends the enumeration.</returns>
        bool Visit(IntPtr windowHandle, IntPtr parameter) {
            GetWindowThreadProcessId(windowHandle, out uint ownerProcessId);
            if (ownerProcessId != TargetProcessId) {
                return true;
            } else if (!IsWindowVisible(windowHandle) || GetWindow(windowHandle, GetWindowOwner) != IntPtr.Zero) {
                return true;
            }

            FoundWindow = windowHandle;
            return false;
        }

        /// <summary>Win32 <c>EnumWindows</c>: calls the callback for every top-level window until it returns false.</summary>
        /// <param name="callback">Visitor.</param>
        /// <param name="parameter">Value handed to the visitor.</param>
        /// <returns>False when the visitor stopped the enumeration or the call failed.</returns>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

        /// <summary>Win32 <c>GetWindowThreadProcessId</c>: returns the window's thread and writes its process id.</summary>
        /// <param name="windowHandle">Window.</param>
        /// <param name="processId">Receives the owning process id.</param>
        /// <returns>Owning thread id.</returns>
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

        /// <summary>Win32 <c>IsWindowVisible</c>.</summary>
        /// <param name="windowHandle">Window.</param>
        /// <returns>True when the window has the visible style.</returns>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr windowHandle);

        /// <summary>Win32 <c>GetWindow</c>.</summary>
        /// <param name="windowHandle">Window.</param>
        /// <param name="command">Relationship to query; <see cref="GetWindowOwner"/> returns the owner.</param>
        /// <returns>The related window, or zero.</returns>
        [DllImport("user32.dll")]
        static extern IntPtr GetWindow(IntPtr windowHandle, uint command);
    }
}
