using helengine.baseplatform.Player;

namespace helengine.player {
    /// <summary>
    /// Finds the work area of the monitor the Play window opens on: the monitor showing the editor's main window, or the
    /// primary monitor.
    /// </summary>
    public sealed class PlayerMonitorLocator {
        /// <summary>Reads the desktop's monitors and the editor's main window.</summary>
        readonly IPlayerMonitorQuery Query;

        /// <summary>
        /// Initializes the locator.
        /// </summary>
        /// <param name="query">Reads the desktop's monitors and the editor's main window.</param>
        /// <exception cref="ArgumentNullException">Thrown when the query is null.</exception>
        public PlayerMonitorLocator(IPlayerMonitorQuery query) {
            Query = query ?? throw new ArgumentNullException(nameof(query));
        }

        /// <summary>
        /// Resolves the work area for the <c>window-monitor</c> setting.
        /// </summary>
        /// <param name="monitor">Monitor choice.</param>
        /// <param name="editorProcessId">Process id of the editor that launched the player.</param>
        /// <returns>Work area of the chosen monitor.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the editor has no main window to locate.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for an undefined monitor choice.</exception>
        public PlayerScreenArea ResolveWorkArea(PlayerWindowMonitor monitor, int editorProcessId) {
            if (monitor == PlayerWindowMonitor.Primary) {
                return Query.GetPrimaryWorkArea();
            } else if (monitor == PlayerWindowMonitor.Editor) {
                IntPtr editorWindow = Query.GetProcessMainWindow(editorProcessId);
                if (editorWindow == IntPtr.Zero) {
                    throw new InvalidOperationException($"Editor process {editorProcessId} has no main window, so the player cannot open on the editor's monitor; choose window-monitor 'primary'.");
                }

                return Query.GetWorkAreaOfWindow(editorWindow);
            }

            throw new ArgumentOutOfRangeException(nameof(monitor), monitor, "Unknown window monitor.");
        }
    }
}
