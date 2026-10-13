namespace helengine.player.tests {
    /// <summary>
    /// Verifies command-line parsing for the Play player.
    /// </summary>
    public sealed class PlayerLaunchOptionsTests {
        /// <summary>
        /// Complete argument list carrying every required option once, used as the baseline for each test.
        /// </summary>
        static readonly string[] ValidArgs = {
            "--content-root", "C:/c", "--scene-id", "cube_test", "--scene-path", "cooked/scenes/cube_test.hasset",
            "--adapter-assembly", "C:/p/helengine.ps2.builder.dll", "--adapter-type", "helengine.ps2.builder.Ps2PlayerAdapter",
            "--script-modules", "C:/s" };

        /// <summary>All required options are read.</summary>
        [Fact]
        public void Parse_WhenAllRequiredPresent_ReadsValues() {
            PlayerLaunchOptions options = PlayerLaunchOptions.Parse(ValidArgs);
            Assert.Equal("C:/c", options.ContentRootPath);
            Assert.Equal("cube_test", options.SceneId);
            Assert.Equal("cooked/scenes/cube_test.hasset", options.SceneCookedRelativePath);
            Assert.Equal("helengine.ps2.builder.Ps2PlayerAdapter", options.AdapterTypeName);
            Assert.False(options.WaitForDebugger);
        }

        /// <summary>The debugger flag is a switch.</summary>
        [Fact]
        public void Parse_WhenWaitForDebuggerPresent_SetsFlag() {
            Assert.True(PlayerLaunchOptions.Parse(ValidArgs.Append("--wait-for-debugger").ToArray()).WaitForDebugger);
        }

        /// <summary>Missing required options throw and name the option.</summary>
        [Theory]
        [InlineData("--content-root")]
        [InlineData("--scene-id")]
        [InlineData("--scene-path")]
        [InlineData("--adapter-assembly")]
        [InlineData("--adapter-type")]
        [InlineData("--script-modules")]
        public void Parse_WhenRequiredMissing_Throws(string option) {
            List<string> args = ValidArgs.ToList();
            int index = args.IndexOf(option);
            args.RemoveRange(index, 2);
            ArgumentException exception = Assert.Throws<ArgumentException>(() => PlayerLaunchOptions.Parse(args.ToArray()));
            Assert.Contains(option, exception.Message);
        }

        /// <summary>Unknown options are rejected instead of ignored.</summary>
        [Fact]
        public void Parse_WhenUnknownOption_Throws() {
            Assert.Throws<ArgumentException>(() => PlayerLaunchOptions.Parse(ValidArgs.Append("--bogus").ToArray()));
        }
    }
}
