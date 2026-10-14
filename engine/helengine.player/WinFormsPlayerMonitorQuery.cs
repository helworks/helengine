using System.Diagnostics;

namespace helengine.player {
    /// <summary>
    /// Reads monitor work areas through WinForms <see cref="Screen"/> and a process's main window through
    /// <see cref="Process"/>.
    /// </summary>
    public sealed class WinFormsPlayerMonitorQuery : IPlayerMonitorQuery {
        /// <summary>
        /// Returns the work area of the primary monitor.
        /// </summary>
        /// <returns>The primary work area.</returns>
        public PlayerScreenArea GetPrimaryWorkArea() {
            return ToScreenArea(Screen.PrimaryScreen.WorkingArea);
        }

        /// <summary>
        /// Returns the main window handle of a running process.
        /// </summary>
        /// <param name="processId">Process id.</param>
        /// <returns>The main window handle, or <see cref="IntPtr.Zero"/> when the process has no main window.</returns>
        /// <exception cref="ArgumentException">Thrown by <see cref="Process.GetProcessById(int)"/> when the process is not running.</exception>
        public IntPtr GetProcessMainWindow(int processId) {
            using Process process = Process.GetProcessById(processId);
            return process.MainWindowHandle;
        }

        /// <summary>
        /// Returns the work area of the monitor that shows most of a window (the nearest monitor when it is off-screen).
        /// </summary>
        /// <param name="windowHandle">Native window handle.</param>
        /// <returns>That monitor's work area.</returns>
        public PlayerScreenArea GetWorkAreaOfWindow(IntPtr windowHandle) {
            return ToScreenArea(Screen.FromHandle(windowHandle).WorkingArea);
        }

        /// <summary>
        /// Converts a WinForms rectangle into a work area.
        /// </summary>
        /// <param name="area">Work-area rectangle in virtual-screen pixels.</param>
        /// <returns>The work area.</returns>
        static PlayerScreenArea ToScreenArea(Rectangle area) {
            return new PlayerScreenArea(area.Left, area.Top, area.Width, area.Height);
        }
    }
}
