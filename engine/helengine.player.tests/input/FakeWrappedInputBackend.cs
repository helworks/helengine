namespace helengine.player.tests.input {
    /// <summary>
    /// Desktop input backend stand-in that returns a scripted frame and focus state, so the player input filter can be
    /// tested without a window or real devices.
    /// </summary>
    public sealed class FakeWrappedInputBackend : IInputBackend, IInputForegroundState {
        /// <summary>
        /// Gets or sets the frame returned by every <see cref="CaptureFrame"/> call.
        /// </summary>
        public InputFrameState Frame { get; set; }

        /// <summary>
        /// Gets or sets whether the fake host window owns foreground focus.
        /// </summary>
        public bool IsForegroundActive { get; set; }

        /// <summary>
        /// Gets or sets whether input should keep flowing while the host window is in the background.
        /// </summary>
        public bool ReceiveInputInBackground { get; set; }

        /// <summary>
        /// Returns the scripted frame.
        /// </summary>
        /// <returns>The value of <see cref="Frame"/>.</returns>
        public InputFrameState CaptureFrame() {
            return Frame;
        }
    }
}
