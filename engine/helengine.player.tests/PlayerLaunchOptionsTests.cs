using helengine.baseplatform.Definitions;
using helengine.baseplatform.Player;

namespace helengine.player.tests {
    /// <summary>
    /// Verifies command-line parsing for the Play player. Each test writes its own resolved Play settings file under a
    /// workspace-owned scratch folder, because <c>--play-settings</c> is required and is read while parsing.
    /// </summary>
    public sealed class PlayerLaunchOptionsTests : IDisposable {
        /// <summary>Workspace-owned folder that holds every test's scratch Play settings folder.</summary>
        const string ScratchRoot = @"C:\dev\helworks\builds\helengine\tests\player-launch-options";

        /// <summary>This test's own scratch folder, deleted when the test finishes.</summary>
        readonly string ScratchDirectory;

        /// <summary>Resolved Play settings file this test's argument list names through <c>--play-settings</c>.</summary>
        readonly string PlaySettingsPath;

        /// <summary>
        /// Writes this test's Play settings file: fit window on the editor's monitor, no debugger wait, shadows on, paced
        /// at 60 frames per second, no platform values.
        /// </summary>
        public PlayerLaunchOptionsTests() {
            ScratchDirectory = Path.Combine(ScratchRoot, Guid.NewGuid().ToString("N"));
            PlaySettingsPath = Path.Combine(ScratchDirectory, "play-settings.json");
            PlayerPlaySettings settings = new PlayerPlaySettings(
                PlayerWindowScale.Fit,
                PlayerWindowMonitor.Editor,
                4242,
                false,
                true,
                false,
                60d,
                new Dictionary<string, string>());
            PlayerPlaySettingsFile.Write(PlaySettingsPath, settings);
        }

        /// <summary>All required options are read.</summary>
        [Fact]
        public void Parse_WhenAllRequiredPresent_ReadsValues() {
            PlayerLaunchOptions options = PlayerLaunchOptions.Parse(CreateArguments());
            Assert.Equal("C:/c", options.ContentRootPath);
            Assert.Equal("cube_test", options.SceneId);
            Assert.Equal("PlayStation 2", options.PlatformName);
            Assert.Equal("cooked/scenes/cube_test.hasset", options.SceneCookedRelativePath);
            Assert.Equal("helengine.ps2.builder.Ps2PlayerAdapter", options.AdapterTypeName);
            Assert.Equal(PlayerInputDevices.Gamepad, options.InputDevices);
            Assert.Equal("ps2", options.RuntimePlatformName);
            Assert.Equal("1.0.0", options.RuntimePlatformVersion);
            Assert.Empty(options.RuntimeSettingsPath);
        }

        /// <summary>The play settings file is read into the options.</summary>
        [Fact]
        public void Parse_ReadsPlaySettingsFile() {
            PlayerLaunchOptions options = PlayerLaunchOptions.Parse(CreateArguments());

            Assert.Equal(PlayerWindowScale.Fit, options.PlaySettings.WindowScale);
            Assert.Equal(60d, options.PlaySettings.TargetFrameRate);
        }

        /// <summary>
        /// The removed flags are now unknown options. Each row passes its flag in the exact form the old parser accepted
        /// (a value option with a valid value, or a bare switch), so the row only passes because the flag is gone.
        /// </summary>
        /// <param name="removedArguments">Flag folded into the Play settings file, followed by its old value when it took one.</param>
        [Theory]
        [InlineData("--target-fps", "60")]
        [InlineData("--shadows-enabled", "false")]
        [InlineData("--wait-for-debugger")]
        public void Parse_RejectsFoldedFlags(params string[] removedArguments) {
            List<string> arguments = CreateArguments().ToList();
            arguments.AddRange(removedArguments);

            ArgumentException exception = Assert.Throws<ArgumentException>(() => PlayerLaunchOptions.Parse(arguments.ToArray()));
            Assert.Contains($"Unknown player option '{removedArguments[0]}'", exception.Message);
        }

        /// <summary>The play settings option is required.</summary>
        [Fact]
        public void Parse_WhenPlaySettingsMissing_Throws() {
            List<string> arguments = CreateArguments().ToList();
            int index = arguments.IndexOf("--play-settings");
            arguments.RemoveRange(index, 2);

            ArgumentException exception = Assert.Throws<ArgumentException>(() => PlayerLaunchOptions.Parse(arguments.ToArray()));
            Assert.Contains("--play-settings", exception.Message);
        }

        /// <summary>
        /// The platform identity the player hands to the core is the runtime platform name and version the editor passed,
        /// not a player-specific identity, so game code sees the same platform as on the console.
        /// </summary>
        [Fact]
        public void CreateRuntimePlatformInfo_ReportsRuntimePlatformNameAndVersionToTheCore() {
            PlatformInfo platformInfo = PlayerLaunchOptions.Parse(CreateArguments()).CreateRuntimePlatformInfo();
            Assert.Equal("ps2", platformInfo.Name);
            Assert.Equal("1.0.0", platformInfo.Version);
        }

        /// <summary>A repeated runtime platform option is rejected like every other value option.</summary>
        [Fact]
        public void Parse_WhenRuntimePlatformNameRepeated_Throws() {
            Assert.Throws<ArgumentException>(() => PlayerLaunchOptions.Parse([.. CreateArguments(), "--runtime-platform-name", "player"]));
        }

        /// <summary>The complete runtime preset catalog path reaches the player without becoming a required legacy argument.</summary>
        [Fact]
        public void Parse_WhenRuntimeCatalogPresent_ReadsPath() {
            Assert.Equal("C:/settings/catalog.bin", PlayerLaunchOptions.Parse([.. CreateArguments(), "--runtime-settings", "C:/settings/catalog.bin"]).RuntimeSettingsPath);
        }

        /// <summary>Missing required options throw and name the option.</summary>
        /// <param name="option">Required option removed from the argument list.</param>
        [Theory]
        [InlineData("--content-root")]
        [InlineData("--scene-id")]
        [InlineData("--platform-name")]
        [InlineData("--scene-path")]
        [InlineData("--adapter-assembly")]
        [InlineData("--adapter-type")]
        [InlineData("--script-modules")]
        [InlineData("--input-devices")]
        [InlineData("--runtime-platform-name")]
        [InlineData("--runtime-platform-version")]
        public void Parse_WhenRequiredMissing_Throws(string option) {
            List<string> args = CreateArguments().ToList();
            int index = args.IndexOf(option);
            args.RemoveRange(index, 2);
            ArgumentException exception = Assert.Throws<ArgumentException>(() => PlayerLaunchOptions.Parse(args.ToArray()));
            Assert.Contains(option, exception.Message);
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
            Assert.Throws<ArgumentException>(() => PlayerLaunchOptions.Parse(CreateArguments().Append("--bogus").ToArray()));
        }

        /// <summary>
        /// Deletes this test's scratch folder and the Play settings file in it.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ScratchDirectory)) {
                Directory.Delete(ScratchDirectory, true);
            }
        }

        /// <summary>
        /// Builds the complete argument list carrying every required option once, including this test's
        /// <c>--play-settings</c> file; it is the baseline for each test.
        /// </summary>
        /// <returns>A new argument array the caller may change.</returns>
        string[] CreateArguments() {
            return new[] {
                "--content-root", "C:/c", "--scene-id", "cube_test", "--platform-name", "PlayStation 2", "--scene-path", "cooked/scenes/cube_test.hasset",
                "--adapter-assembly", "C:/p/helengine.ps2.builder.dll", "--adapter-type", "helengine.ps2.builder.Ps2PlayerAdapter",
                "--script-modules", "C:/s", "--input-devices", "gamepad",
                "--runtime-platform-name", "ps2", "--runtime-platform-version", "1.0.0",
                "--play-settings", PlaySettingsPath };
        }

        /// <summary>
        /// Builds the baseline argument list with the <c>--input-devices</c> value replaced.
        /// </summary>
        /// <param name="value">Replacement input device list text.</param>
        /// <returns>Arguments identical to <see cref="CreateArguments"/> except for the input device list.</returns>
        string[] WithInputDevices(string value) {
            string[] args = CreateArguments();
            args[Array.IndexOf(args, "--input-devices") + 1] = value;
            return args;
        }
    }
}
