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
            "--script-modules", "C:/s", "--target-fps", "60" };

        /// <summary>All required options are read.</summary>
        [Fact]
        public void Parse_WhenAllRequiredPresent_ReadsValues() {
            PlayerLaunchOptions options = PlayerLaunchOptions.Parse(ValidArgs);
            Assert.Equal("C:/c", options.ContentRootPath);
            Assert.Equal("cube_test", options.SceneId);
            Assert.Equal("cooked/scenes/cube_test.hasset", options.SceneCookedRelativePath);
            Assert.Equal("helengine.ps2.builder.Ps2PlayerAdapter", options.AdapterTypeName);
            Assert.Equal(60d, options.TargetFrameRate);
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
        [InlineData("--target-fps")]
        public void Parse_WhenRequiredMissing_Throws(string option) {
            List<string> args = ValidArgs.ToList();
            int index = args.IndexOf(option);
            args.RemoveRange(index, 2);
            ArgumentException exception = Assert.Throws<ArgumentException>(() => PlayerLaunchOptions.Parse(args.ToArray()));
            Assert.Contains(option, exception.Message);
        }

        /// <summary>The target frame rate is read with the invariant culture regardless of the process culture.</summary>
        [Fact]
        public void Parse_WhenTargetFpsHasFraction_ReadsInvariantValue() {
            System.Globalization.CultureInfo previousCulture = System.Globalization.CultureInfo.CurrentCulture;
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            try {
                Assert.Equal(59.94d, PlayerLaunchOptions.Parse(WithTargetFps("59.94")).TargetFrameRate);
            } finally {
                System.Globalization.CultureInfo.CurrentCulture = previousCulture;
            }
        }

        /// <summary>A target frame rate that is not a positive, finite invariant number throws and names the option.</summary>
        /// <param name="value">Invalid <c>--target-fps</c> value.</param>
        [Theory]
        [InlineData("0")]
        [InlineData("-60")]
        [InlineData("sixty")]
        [InlineData("59,94")]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        public void Parse_WhenTargetFpsInvalid_Throws(string value) {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => PlayerLaunchOptions.Parse(WithTargetFps(value)));
            Assert.Contains("--target-fps", exception.Message);
        }

        /// <summary>Unknown options are rejected instead of ignored.</summary>
        [Fact]
        public void Parse_WhenUnknownOption_Throws() {
            Assert.Throws<ArgumentException>(() => PlayerLaunchOptions.Parse(ValidArgs.Append("--bogus").ToArray()));
        }

        /// <summary>
        /// Builds the baseline argument list with the <c>--target-fps</c> value replaced.
        /// </summary>
        /// <param name="value">Replacement target frame rate text.</param>
        /// <returns>Arguments identical to <see cref="ValidArgs"/> except for the target frame rate value.</returns>
        static string[] WithTargetFps(string value) {
            string[] args = ValidArgs.ToArray();
            args[Array.IndexOf(args, "--target-fps") + 1] = value;
            return args;
        }
    }
}
