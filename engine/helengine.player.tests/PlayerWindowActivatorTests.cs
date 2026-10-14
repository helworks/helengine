namespace helengine.player.tests {
    /// <summary>
    /// Verifies the player asks for its window to become the foreground window and reports a refusal on standard output
    /// instead of failing.
    /// </summary>
    public sealed class PlayerWindowActivatorTests {
        /// <summary>Native handle standing in for the shown player window.</summary>
        static readonly IntPtr WindowHandle = new IntPtr(0x1234);

        /// <summary>An accepted activation targets the player window once and writes nothing.</summary>
        [Fact]
        public void Activate_WhenAccepted_ActivatesWindowSilently() {
            RecordingPlayerForegroundWindow foregroundWindow = new RecordingPlayerForegroundWindow(true);
            StringWriter output = new StringWriter();

            bool activated = new PlayerWindowActivator(foregroundWindow, output).Activate(WindowHandle);

            Assert.True(activated);
            Assert.Equal(new[] { WindowHandle }, foregroundWindow.RequestedHandles);
            Assert.Equal(string.Empty, output.ToString());
        }

        /// <summary>A refused activation is reported as one standard-output line and returns false without throwing.</summary>
        [Fact]
        public void Activate_WhenRefused_WritesRefusalLine() {
            RecordingPlayerForegroundWindow foregroundWindow = new RecordingPlayerForegroundWindow(false);
            StringWriter output = new StringWriter();

            bool activated = new PlayerWindowActivator(foregroundWindow, output).Activate(WindowHandle);

            Assert.False(activated);
            Assert.Equal(new[] { WindowHandle }, foregroundWindow.RequestedHandles);
            Assert.Equal(PlayerWindowActivator.ActivationRefusedMessage + Environment.NewLine, output.ToString());
        }

        /// <summary>The activator requires both the native request and the output stream.</summary>
        [Fact]
        public void Constructor_WhenArgumentIsNull_Throws() {
            Assert.Throws<ArgumentNullException>(() => new PlayerWindowActivator(null, new StringWriter()));
            Assert.Throws<ArgumentNullException>(() => new PlayerWindowActivator(new RecordingPlayerForegroundWindow(true), null));
        }
    }
}
