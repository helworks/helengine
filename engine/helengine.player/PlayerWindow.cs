namespace helengine.player {
    /// <summary>
    /// Fixed-size desktop window the Play player presents its DirectX 11 swap chain into.
    /// </summary>
    public sealed class PlayerWindow : Form {
        /// <summary>Client-area width in pixels.</summary>
        public const int ClientWidth = 640;

        /// <summary>Client-area height in pixels.</summary>
        public const int ClientHeight = 480;

        /// <summary>Set once the window has been closed by the user.</summary>
        bool IsClosedValue;

        /// <summary>
        /// Creates the player window titled after the platform and scene being played.
        /// </summary>
        /// <param name="platformName">Display name of the platform shown in square brackets in the title bar.</param>
        /// <param name="sceneId">Stable id of the scene shown in the title bar.</param>
        /// <exception cref="ArgumentException">The platform name or scene id is blank.</exception>
        public PlayerWindow(string platformName, string sceneId) {
            string title = PlayerWindowTitle.Format(platformName, sceneId);
            ClientSize = new Size(ClientWidth, ClientHeight);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = false;
            Text = title;
        }

        /// <summary>Gets whether the user has closed the window, which ends the player's frame loop.</summary>
        public bool IsClosed => IsClosedValue;

        /// <summary>
        /// Records that the window was closed so the host stops rendering into it.
        /// </summary>
        /// <param name="e">Close event arguments.</param>
        protected override void OnFormClosed(FormClosedEventArgs e) {
            IsClosedValue = true;
            base.OnFormClosed(e);
        }
    }
}
