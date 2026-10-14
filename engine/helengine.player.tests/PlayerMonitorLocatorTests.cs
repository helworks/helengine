using helengine.baseplatform.Player;

namespace helengine.player.tests {
    /// <summary>
    /// Verifies which work area the <c>window-monitor</c> Play setting resolves to, against a stubbed desktop so no real
    /// monitor or editor window is involved.
    /// </summary>
    public sealed class PlayerMonitorLocatorTests {
        /// <summary>Process id of the stubbed editor.</summary>
        const int EditorProcessId = 4242;

        /// <summary>Native handle of the stubbed editor main window.</summary>
        static readonly IntPtr EditorWindow = new IntPtr(0x1234);

        /// <summary>Work area of the stubbed primary monitor.</summary>
        static readonly PlayerScreenArea PrimaryArea = new PlayerScreenArea(0, 0, 1920, 1040);

        /// <summary>Work area of the stubbed secondary monitor the editor is on.</summary>
        static readonly PlayerScreenArea EditorArea = new PlayerScreenArea(1920, 0, 2560, 1400);

        /// <summary>The primary choice returns the primary monitor's work area without looking for the editor.</summary>
        [Fact]
        public void ResolveWorkArea_WhenPrimary_ReturnsPrimaryWorkArea() {
            StubPlayerMonitorQuery query = new StubPlayerMonitorQuery(PrimaryArea, EditorProcessId, EditorWindow, EditorArea);

            PlayerScreenArea area = new PlayerMonitorLocator(query).ResolveWorkArea(PlayerWindowMonitor.Primary, EditorProcessId);

            Assert.Same(PrimaryArea, area);
            Assert.Equal(0, query.MainWindowLookups);
        }

        /// <summary>The editor choice returns the work area of the monitor showing the editor's main window.</summary>
        [Fact]
        public void ResolveWorkArea_WhenEditor_ReturnsEditorMonitorWorkArea() {
            StubPlayerMonitorQuery query = new StubPlayerMonitorQuery(PrimaryArea, EditorProcessId, EditorWindow, EditorArea);

            PlayerScreenArea area = new PlayerMonitorLocator(query).ResolveWorkArea(PlayerWindowMonitor.Editor, EditorProcessId);

            Assert.Same(EditorArea, area);
        }

        /// <summary>An editor without a main window fails with a message naming the process and the fix.</summary>
        [Fact]
        public void ResolveWorkArea_WhenEditorHasNoMainWindow_Throws() {
            StubPlayerMonitorQuery query = new StubPlayerMonitorQuery(PrimaryArea, EditorProcessId, IntPtr.Zero, EditorArea);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new PlayerMonitorLocator(query).ResolveWorkArea(PlayerWindowMonitor.Editor, EditorProcessId));

            Assert.Contains("4242", error.Message);
            Assert.Contains("primary", error.Message);
        }

        /// <summary>
        /// An editor that exited before the player resolved its monitor fails with the locator's own message and never
        /// falls back to the primary monitor.
        /// </summary>
        [Fact]
        public void ResolveWorkArea_WhenEditorProcessGone_ThrowsWithoutFallingBackToPrimary() {
            StubPlayerMonitorQuery query = new StubPlayerMonitorQuery(PrimaryArea, EditorProcessId, EditorWindow, EditorArea);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new PlayerMonitorLocator(query).ResolveWorkArea(PlayerWindowMonitor.Editor, 9999));

            Assert.Contains("9999", error.Message);
            Assert.Equal(0, query.PrimaryLookups);
        }

        /// <summary>An undefined monitor value is rejected instead of silently treated as the editor's monitor.</summary>
        [Fact]
        public void ResolveWorkArea_WhenMonitorUndefined_Throws() {
            StubPlayerMonitorQuery query = new StubPlayerMonitorQuery(PrimaryArea, EditorProcessId, EditorWindow, EditorArea);

            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerMonitorLocator(query).ResolveWorkArea((PlayerWindowMonitor)7, EditorProcessId));
        }

        /// <summary>The locator needs a desktop query.</summary>
        [Fact]
        public void Constructor_WhenQueryNull_Throws() {
            Assert.Throws<ArgumentNullException>(() => new PlayerMonitorLocator(null));
        }
    }
}
