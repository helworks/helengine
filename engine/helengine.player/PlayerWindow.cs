namespace helengine.player {
    /// <summary>
    /// Fixed-size desktop window the Play player presents its DirectX 11 swap chain into. Its client size and position
    /// come from <see cref="PlayerWindowLayout"/>.
    /// </summary>
    public sealed class PlayerWindow : Form {
        /// <summary>Set once the window has been closed by the user.</summary>
        bool IsClosedValue;

        /// <summary>
        /// Creates the player window with the given title and client size. The host builds the title with
        /// <see cref="PlayerWindowTitle"/> and positions the window explicitly.
        /// </summary>
        /// <param name="title">Title bar text naming the platform, its video mode when it has one, and the scene.</param>
        /// <param name="clientWidth">Client width in pixels.</param>
        /// <param name="clientHeight">Client height in pixels.</param>
        /// <exception cref="ArgumentException">The title is blank.</exception>
        public PlayerWindow(string title, int clientWidth, int clientHeight) {
            if (string.IsNullOrWhiteSpace(title)) {
                throw new ArgumentException("Player window title must be provided.", nameof(title));
            }

            ClientSize = new Size(clientWidth, clientHeight);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.Manual;
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
