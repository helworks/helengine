using helengine.baseplatform.Definitions;

namespace helengine.player.tests {
    /// <summary>
    /// Verifies command-line parsing for the Play player.
    /// </summary>
    public sealed class PlayerLaunchOptionsTests {
        /// <summary>
        /// Complete argument list carrying every required option once, used as the baseline for each test.
        /// </summary>
        static readonly string[] ValidArgs = {
            "--content-root", "C:/c", "--scene-id", "cube_test", "--platform-name", "PlayStation 2", "--scene-path", "cooked/scenes/cube_test.hasset",
            "--adapter-assembly", "C:/p/helengine.ps2.builder.dll", "--adapter-type", "helengine.ps2.builder.Ps2PlayerAdapter",
            "--script-modules", "C:/s", "--target-fps", "60", "--input-devices", "gamepad",
            "--runtime-platform-name", "ps2", "--runtime-platform-version", "1.0.0" };

        /// <summary>All required options are read.</summary>
        [Fact]
        public void Parse_WhenAllRequiredPresent_ReadsValues() {
            PlayerLaunchOptions options = PlayerLaunchOptions.Parse(ValidArgs);
            Assert.Equal("C:/c", options.ContentRootPath);
            Assert.Equal("cube_test", options.SceneId);
            Assert.Equal("PlayStation 2", options.PlatformName);
            Assert.Equal("cooked/scenes/cube_test.hasset", options.SceneCookedRelativePath);
            Assert.Equal("helengine.ps2.builder.Ps2PlayerAdapter", options.AdapterTypeName);
            Assert.Equal(60d, options.TargetFrameRate);
            Assert.Equal(PlayerInputDevices.Gamepad, options.InputDevices);
            Assert.Equal("ps2", options.RuntimePlatformName);
            Assert.Equal("1.0.0", options.RuntimePlatformVersion);
            Assert.False(options.WaitForDebugger);
            Assert.True(options.ShadowsEnabled);
            Assert.Empty(options.RuntimeSettingsPath);
        }

        /// <summary>
        /// The platform identity the player hands to the core is the runtime platform name and version the editor passed,
        /// not a player-specific identity, so game code sees the same platform as on the console.
        /// </summary>
        [Fact]
        public void CreateRuntimePlatformInfo_ReportsRuntimePlatformNameAndVersionToTheCore() {
            PlatformInfo platformInfo = PlayerLaunchOptions.Parse(ValidArgs).CreateRuntimePlatformInfo();
            Assert.Equal("ps2", platformInfo.Name);
            Assert.Equal("1.0.0", platformInfo.Version);
        }

        /// <summary>A repeated runtime platform option is rejected like every other value option.</summary>
        [Fact]
        public void Parse_WhenRuntimePlatformNameRepeated_Throws() {
            Assert.Throws<ArgumentException>(() => PlayerLaunchOptions.Parse([.. ValidArgs, "--runtime-platform-name", "player"]));
        }

        /// <summary>The debugger flag is a switch.</summary>
        [Fact]
        public void Parse_WhenWaitForDebuggerPresent_SetsFlag() {
            Assert.True(PlayerLaunchOptions.Parse(ValidArgs.Append("--wait-for-debugger").ToArray()).WaitForDebugger);
        }

        /// <summary>The platform's global shadow switch survives the editor-to-player command line.</summary>
        /// <param name="value">Serialized shadow switch.</param>
        /// <param name="enabled">Expected renderer policy.</param>
        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        public void Parse_WhenShadowsOptionPresent_ReadsPolicy(string value, bool enabled) {
            Assert.Equal(enabled, PlayerLaunchOptions.Parse([.. ValidArgs, "--shadows-enabled", value]).ShadowsEnabled);
        }

        /// <summary>The complete runtime preset catalog path reaches the player without becoming a required legacy argument.</summary>
        [Fact]
        public void Parse_WhenRuntimeCatalogPresent_ReadsPath() {
            Assert.Equal("C:/settings/catalog.bin", PlayerLaunchOptions.Parse([.. ValidArgs, "--runtime-settings", "C:/settings/catalog.bin"]).RuntimeSettingsPath);
        }

        /// <summary>Invalid, repeated and valueless shadow switches are rejected before the renderer starts.</summary>
        [Fact]
        public void Parse_WhenShadowOptionMalformed_Throws() {
            Assert.Throws<ArgumentException>(() => PlayerLaunchOptions.Parse([.. ValidArgs, "--shadows-enabled", "off"]));
            Assert.Throws<ArgumentException>(() => PlayerLaunchOptions.Parse([.. ValidArgs, "--shadows-enabled"]));
            Assert.Throws<ArgumentException>(() => PlayerLaunchOptions.Parse([.. ValidArgs, "--shadows-enabled", "false", "--shadows-enabled", "true"]));
        }

        /// <summary>Missing required options throw and name the option.</summary>
        [Theory]
        [InlineData("--content-root")]
        [InlineData("--scene-id")]
        [InlineData("--platform-name")]
        [InlineData("--scene-path")]
        [InlineData("--adapter-assembly")]
        [InlineData("--adapter-type")]
        [InlineData("--script-modules")]
        [InlineData("--target-fps")]
        [InlineData("--input-devices")]
        [InlineData("--runtime-platform-name")]
        [InlineData("--runtime-platform-version")]
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

        /// <summary>A comma list of several devices is read into the matching flags.</summary>
        [Fact]
        public void Parse_WhenInputDevicesList_ReadsAllDevices() {
            PlayerLaunchOptions options = PlayerLaunchOptions.Parse(WithInputDevices("keyboard,mouse,pointer,gamepad"));
            Assert.Equal(
                PlayerInputDevices.Keyboard | PlayerInputDevices.Mouse | PlayerInputDevices.Pointer | PlayerInputDevices.Gamepad,
                options.InputDevices);
        }

        /// <summary>An unknown, repeated or mis-cased device name throws and names the option.</summary>
        /// <param name="value">Invalid <c>--input-devices</c> value.</param>
        [Theory]
        [InlineData("joystick")]
        [InlineData("gamepad,gamepad")]
        [InlineData("Gamepad")]
        [InlineData("gamepad,")]
        public void Parse_WhenInputDevicesInvalid_Throws(string value) {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => PlayerLaunchOptions.Parse(WithInputDevices(value)));
            Assert.Contains("--input-devices", exception.Message);
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

        /// <summary>
        /// Builds the baseline argument list with the <c>--input-devices</c> value replaced.
        /// </summary>
        /// <param name="value">Replacement input device list text.</param>
        /// <returns>Arguments identical to <see cref="ValidArgs"/> except for the input device list.</returns>
        static string[] WithInputDevices(string value) {
            string[] args = ValidArgs.ToArray();
            args[Array.IndexOf(args, "--input-devices") + 1] = value;
            return args;
        }
    }
}
