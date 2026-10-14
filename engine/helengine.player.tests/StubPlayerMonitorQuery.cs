namespace helengine.player.tests {
    /// <summary>
    /// Stubbed desktop for <see cref="PlayerMonitorLocator"/> tests: one primary work area, one editor process with a
    /// fixed main window handle (or none) and one work area for that window.
    /// </summary>
    public sealed class StubPlayerMonitorQuery : IPlayerMonitorQuery {
        /// <summary>Work area returned for the primary monitor.</summary>
        readonly PlayerScreenArea PrimaryWorkArea;

        /// <summary>Only process id the stub knows a main window for.</summary>
        readonly int EditorProcessId;

        /// <summary>Main window handle of the editor process; zero when it has none.</summary>
        readonly IntPtr EditorWindow;

        /// <summary>Work area of the monitor showing the editor window.</summary>
        readonly PlayerScreenArea EditorWorkArea;

        /// <summary>
        /// Initializes the stubbed desktop.
        /// </summary>
        /// <param name="primaryWorkArea">Work area returned for the primary monitor.</param>
        /// <param name="editorProcessId">Only process id the stub knows a main window for.</param>
        /// <param name="editorWindow">Main window handle of that process; zero when it has none.</param>
        /// <param name="editorWorkArea">Work area of the monitor showing that window.</param>
        public StubPlayerMonitorQuery(PlayerScreenArea primaryWorkArea, int editorProcessId, IntPtr editorWindow, PlayerScreenArea editorWorkArea) {
            PrimaryWorkArea = primaryWorkArea;
            EditorProcessId = editorProcessId;
            EditorWindow = editorWindow;
            EditorWorkArea = editorWorkArea;
        }

        /// <summary>Gets how many times a process main window was looked up.</summary>
        public int MainWindowLookups { get; private set; }

        /// <summary>Gets how many times the primary work area was read.</summary>
        public int PrimaryLookups { get; private set; }

        /// <summary>
        /// Returns the configured primary work area.
        /// </summary>
        /// <returns>The primary work area.</returns>
        public PlayerScreenArea GetPrimaryWorkArea() {
            PrimaryLookups++;
            return PrimaryWorkArea;
        }

        /// <summary>
        /// Returns the editor window for the editor process id. Any other process id behaves like a process that is not
        /// running, which, like the real query, has no main window.
        /// </summary>
        /// <param name="processId">Process id to look up.</param>
        /// <returns>The configured handle, or <see cref="IntPtr.Zero"/> for any other process id.</returns>
        public IntPtr GetProcessMainWindow(int processId) {
            MainWindowLookups++;
            if (processId != EditorProcessId) {
                return IntPtr.Zero;
            }

            return EditorWindow;
        }

        /// <summary>
        /// Returns the editor work area for the editor window.
        /// </summary>
        /// <param name="windowHandle">Window handle to locate.</param>
        /// <returns>The configured editor work area.</returns>
        /// <exception cref="InvalidOperationException">Thrown for any other handle, which a test never expects.</exception>
        public PlayerScreenArea GetWorkAreaOfWindow(IntPtr windowHandle) {
            if (windowHandle != EditorWindow) {
                throw new InvalidOperationException("Unexpected window handle " + windowHandle + ".");
            }

            return EditorWorkArea;
        }
    }
}
