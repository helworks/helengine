namespace helengine.player.tests {
    /// <summary>
    /// Verifies that engine log entries written inside the player reach its console streams, which the editor reads.
    /// </summary>
    public sealed class PlayerConsoleLogForwarderTests {
        /// <summary>
        /// Informational and warning entries go to the output writer, warnings with the prefix, and errors go to the
        /// error writer; each entry is written exactly once.
        /// </summary>
        [Fact]
        public void LoggedEntries_AreWrittenToTheWriterMatchingTheirSeverity() {
            StringWriter output = new StringWriter();
            StringWriter error = new StringWriter();

            using (new PlayerConsoleLogForwarder(output, error)) {
                Logger.WriteLine("scene loaded");
                Logger.WriteWarning("texture downscaled");
                Logger.WriteError("script threw");
            }

            Assert.Equal(
                "scene loaded" + Environment.NewLine + PlayerConsoleLogForwarder.WarningPrefix + "texture downscaled" + Environment.NewLine,
                output.ToString());
            Assert.Equal("script threw" + Environment.NewLine, error.ToString());
        }

        /// <summary>
        /// Entries logged after the forwarder is disposed are no longer written.
        /// </summary>
        [Fact]
        public void Dispose_StopsForwarding() {
            StringWriter output = new StringWriter();
            StringWriter error = new StringWriter();
            PlayerConsoleLogForwarder forwarder = new PlayerConsoleLogForwarder(output, error);

            forwarder.Dispose();
            Logger.WriteLine("after dispose");
            Logger.WriteError("after dispose");

            Assert.Equal(string.Empty, output.ToString());
            Assert.Equal(string.Empty, error.ToString());
        }
    }
}
