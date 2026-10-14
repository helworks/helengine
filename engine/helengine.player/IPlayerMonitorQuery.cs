namespace helengine.player {
    /// <summary>
    /// Reads the desktop facts <see cref="PlayerMonitorLocator"/> needs: the primary monitor's work area, a process's main
    /// window and the work area of the monitor showing a window. Abstracts the WinForms and process calls so the monitor
    /// choice can be verified without real monitors or a running editor.
    /// </summary>
    public interface IPlayerMonitorQuery {
        /// <summary>
        /// Returns the work area of the primary monitor.
        /// </summary>
        /// <returns>The primary work area.</returns>
        PlayerScreenArea GetPrimaryWorkArea();

        /// <summary>
        /// Returns the main window handle of a process.
        /// </summary>
        /// <param name="processId">Process id.</param>
        /// <returns>The main window handle, or <see cref="IntPtr.Zero"/> when the process has no main window.</returns>
        IntPtr GetProcessMainWindow(int processId);

        /// <summary>
        /// Returns the work area of the monitor that shows most of a window.
        /// </summary>
        /// <param name="windowHandle">Native window handle.</param>
        /// <returns>That monitor's work area.</returns>
        PlayerScreenArea GetWorkAreaOfWindow(IntPtr windowHandle);
    }
}
