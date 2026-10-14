using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.tests.Player {
    /// <summary>
    /// Verifies Play capability validation and the platform-definition default.
    /// </summary>
    public sealed class PlatformPlayerCapabilityTests {
        /// <summary>A supported capability must name its adapter type.</summary>
        [Fact]
        public void Ctor_WhenSupportedWithoutAdapterType_Throws() {
            Assert.Throws<ArgumentException>(() => new PlatformPlayerCapability(true, " ", "ps2-staging", 60d, PlayerInputDevices.Gamepad, "ps2", "1.0.0", []));
        }

        /// <summary>A supported capability must name its content root.</summary>
        [Fact]
        public void Ctor_WhenSupportedWithoutContentRoot_Throws() {
            Assert.Throws<ArgumentException>(() => new PlatformPlayerCapability(true, "a.B", "", 60d, PlayerInputDevices.Gamepad, "ps2", "1.0.0", []));
        }

        /// <summary>A supported capability must declare a non-negative, finite target frame rate.</summary>
        /// <param name="targetFrameRate">Invalid frame rate passed to the constructor.</param>
        [Theory]
        [InlineData(-60d)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void Ctor_WhenSupportedWithNegativeOrNonFiniteTargetFrameRate_Throws(double targetFrameRate) {
            ArgumentException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlatformPlayerCapability(true, "a.B", "staging", targetFrameRate, PlayerInputDevices.Gamepad, "ps2", "1.0.0", []));
            Assert.Equal("targetFrameRate", exception.ParamName);
        }

        /// <summary>A supported capability may take its frame rate from the platform's video mode (declared as 0).</summary>
        [Fact]
        public void Ctor_WhenSupportedWithZeroFrameRate_TakesFrameRateFromVideoMode() {
            Assert.True(new PlatformPlayerCapability(true, "a.B", "staging", 0d, PlayerInputDevices.Gamepad, "ps2", "1.0.0", []).FrameRateFromVideoMode);
        }

        /// <summary>A supported capability exposes the target frame rate it was given.</summary>
        [Fact]
        public void Ctor_WhenSupported_ExposesTargetFrameRate() {
            Assert.Equal(59.94d, new PlatformPlayerCapability(true, "a.B", "staging", 59.94d, PlayerInputDevices.Gamepad, "ps2", "1.0.0", []).TargetFrameRate);
        }

        /// <summary>The unsupported capability does not support Play.</summary>
        [Fact]
        public void CreateUnsupported_DoesNotSupportPlay() {
            Assert.False(PlatformPlayerCapability.CreateUnsupported().SupportsPlay);
        }

        /// <summary>The unsupported capability declares no frame rate, because no player ever runs for it.</summary>
        [Fact]
        public void CreateUnsupported_DeclaresZeroTargetFrameRate() {
            Assert.Equal(0d, PlatformPlayerCapability.CreateUnsupported().TargetFrameRate);
        }

        /// <summary>A supported capability must declare at least one input device the platform has.</summary>
        [Fact]
        public void Ctor_WhenSupportedWithoutInputDevices_Throws() {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.None, "ps2", "1.0.0", []));
            Assert.Equal("inputDevices", exception.ParamName);
        }

        /// <summary>A supported capability rejects device bits that name no known device.</summary>
        [Fact]
        public void Ctor_WhenSupportedWithUndefinedInputDeviceBits_Throws() {
            ArgumentException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad | (PlayerInputDevices)64, "ps2", "1.0.0", []));
            Assert.Equal("inputDevices", exception.ParamName);
        }

        /// <summary>A supported capability exposes exactly the device set it was given.</summary>
        [Fact]
        public void Ctor_WhenSupported_ExposesInputDevices() {
            PlayerInputDevices devices = PlayerInputDevices.Keyboard | PlayerInputDevices.Gamepad;
            Assert.Equal(devices, new PlatformPlayerCapability(true, "a.B", "staging", 60d, devices, "ps2", "1.0.0", []).InputDevices);
        }

        /// <summary>A supported capability must name the platform its native runtime reports to the core.</summary>
        /// <param name="runtimePlatformName">Missing or blank runtime platform name.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Ctor_WhenSupportedWithoutRuntimePlatformName_Throws(string runtimePlatformName) {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, runtimePlatformName, "1.0.0", []));
            Assert.Equal("runtimePlatformName", exception.ParamName);
        }

        /// <summary>A supported capability must name the platform version its native runtime reports to the core.</summary>
        /// <param name="runtimePlatformVersion">Missing or blank runtime platform version.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Ctor_WhenSupportedWithoutRuntimePlatformVersion_Throws(string runtimePlatformVersion) {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ps2", runtimePlatformVersion, []));
            Assert.Equal("runtimePlatformVersion", exception.ParamName);
        }

        /// <summary>A supported capability exposes the runtime platform name and version it was given.</summary>
        [Fact]
        public void Ctor_WhenSupported_ExposesRuntimePlatformIdentity() {
            PlatformPlayerCapability capability = new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ps2", "1.0.0", []);
            Assert.Equal("ps2", capability.RuntimePlatformName);
            Assert.Equal("1.0.0", capability.RuntimePlatformVersion);
        }

        /// <summary>An unsupported capability needs no runtime platform identity, because no player ever runs for it.</summary>
        [Fact]
        public void Ctor_WhenUnsupportedWithoutRuntimePlatformIdentity_DoesNotThrow() {
            PlatformPlayerCapability capability = new PlatformPlayerCapability(false, "none", "none", 0d, PlayerInputDevices.None, null, null, []);
            Assert.False(capability.SupportsPlay);
        }

        /// <summary>The unsupported capability names no runtime platform, because no player ever runs for it.</summary>
        [Fact]
        public void CreateUnsupported_DeclaresNoRuntimePlatformIdentity() {
            PlatformPlayerCapability capability = PlatformPlayerCapability.CreateUnsupported();
            Assert.Equal("none", capability.RuntimePlatformName);
            Assert.Equal("none", capability.RuntimePlatformVersion);
        }

        /// <summary>The unsupported capability declares no input devices, because no player ever runs for it.</summary>
        [Fact]
        public void CreateUnsupported_DeclaresNoInputDevices() {
            Assert.Equal(PlayerInputDevices.None, PlatformPlayerCapability.CreateUnsupported().InputDevices);
        }

        /// <summary>A supported capability exposes the Play settings it declares.</summary>
        [Fact]
        public void Ctor_WhenSupported_ExposesPlaySettings() {
            PlatformSettingDefinition gap = new PlatformSettingDefinition("screen-gap", "Screen Gap", PlatformSettingKind.Text, "0", true, []);

            PlatformPlayerCapability capability = new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ds", "1.0.0", [gap]);

            Assert.Same(gap, Assert.Single(capability.PlaySettings));
            Assert.Same(gap, capability.FindPlaySetting("screen-gap"));
            Assert.Null(capability.FindPlaySetting("video-mode"));
        }

        /// <summary>A Play setting may not reuse a common Play setting id.</summary>
        [Fact]
        public void Ctor_WhenPlaySettingCollidesWithCommonId_Throws() {
            PlatformSettingDefinition collision = new PlatformSettingDefinition("window-scale", "Scale", PlatformSettingKind.Choice, "1x", true, ["1x"]);

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ps2", "1.0.0", [collision]));

            Assert.Equal("playSettings", exception.ParamName);
            Assert.Contains("window-scale", exception.Message);
        }

        /// <summary>Two Play settings may not share an id.</summary>
        [Fact]
        public void Ctor_WhenPlaySettingIdsRepeat_Throws() {
            PlatformSettingDefinition first = new PlatformSettingDefinition("screen-gap", "Gap", PlatformSettingKind.Text, "0", true, []);
            PlatformSettingDefinition second = new PlatformSettingDefinition("screen-gap", "Gap 2", PlatformSettingKind.Text, "0", true, []);

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ds", "1.0.0", [first, second]));

            Assert.Contains("screen-gap", exception.Message);
        }

        /// <summary>A choice Play setting's default must be one of its allowed values.</summary>
        [Fact]
        public void Ctor_WhenChoiceDefaultNotAllowed_Throws() {
            PlatformSettingDefinition layout = new PlatformSettingDefinition("screen-layout", "Layout", PlatformSettingKind.Choice, "side", true, ["stacked"]);

            Assert.Throws<ArgumentException>(
                () => new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ds", "1.0.0", [layout]));
        }

        /// <summary>A boolean Play setting's default must be the text true or false.</summary>
        [Fact]
        public void Ctor_WhenBooleanDefaultIsNotBooleanText_Throws() {
            PlatformSettingDefinition flag = new PlatformSettingDefinition("fast", "Fast", PlatformSettingKind.Boolean, "yes", true, []);

            Assert.Throws<ArgumentException>(
                () => new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ds", "1.0.0", [flag]));
        }

        /// <summary>The Play settings array is required, even when empty.</summary>
        [Fact]
        public void Ctor_WhenPlaySettingsNull_Throws() {
            Assert.Throws<ArgumentNullException>(
                () => new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ps2", "1.0.0", null));
        }

        /// <summary>A platform that does not support Play cannot declare Play settings.</summary>
        [Fact]
        public void Ctor_WhenUnsupportedDeclaresPlaySettings_Throws() {
            PlatformSettingDefinition gap = new PlatformSettingDefinition("screen-gap", "Gap", PlatformSettingKind.Text, "0", true, []);

            Assert.Throws<ArgumentException>(
                () => new PlatformPlayerCapability(false, "none", "none", 0d, PlayerInputDevices.None, "none", "none", [gap]));
        }

        /// <summary>The unsupported capability declares no Play settings.</summary>
        [Fact]
        public void CreateUnsupported_DeclaresNoPlaySettings() {
            Assert.Empty(PlatformPlayerCapability.CreateUnsupported().PlaySettings);
        }
    }
}
