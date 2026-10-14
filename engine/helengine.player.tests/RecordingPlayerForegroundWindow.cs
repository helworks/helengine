namespace helengine.player.tests {
    /// <summary>
    /// Test double for the native foreground request that records every window it was asked to activate and answers with
    /// a fixed outcome, so activation tests never touch the real desktop.
    /// </summary>
    public sealed class RecordingPlayerForegroundWindow : IPlayerForegroundWindow {
        /// <summary>
        /// Window handles the activator asked to bring to the foreground, in call order.
        /// </summary>
        public readonly List<IntPtr> RequestedHandles = new List<IntPtr>();

        /// <summary>
        /// Whether each request succeeds.
        /// </summary>
        readonly bool Accept;

        /// <summary>
        /// Initializes the recorder with the outcome every request reports.
        /// </summary>
        /// <param name="accept">True when Windows should appear to accept the activation.</param>
        public RecordingPlayerForegroundWindow(bool accept) {
            Accept = accept;
        }

        /// <summary>
        /// Records the handle and reports the configured outcome.
        /// </summary>
        /// <param name="windowHandle">Window the activator asked to activate.</param>
        /// <returns>The configured outcome.</returns>
        public bool TrySetForeground(IntPtr windowHandle) {
            RequestedHandles.Add(windowHandle);
            return Accept;
        }
    }
}
