using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.tests.Player {
    /// <summary>
    /// Verifies Play capability validation and the platform-definition default.
    /// </summary>
    public sealed class PlatformPlayerCapabilityTests {
        /// <summary>A supported capability must name its adapter type.</summary>
        [Fact]
        public void Ctor_WhenSupportedWithoutAdapterType_Throws() {
            Assert.Throws<ArgumentException>(() => new PlatformPlayerCapability(true, " ", "ps2-staging", 60d, PlayerInputDevices.Gamepad, "ps2", "1.0.0"));
        }

        /// <summary>A supported capability must name its content root.</summary>
        [Fact]
        public void Ctor_WhenSupportedWithoutContentRoot_Throws() {
            Assert.Throws<ArgumentException>(() => new PlatformPlayerCapability(true, "a.B", "", 60d, PlayerInputDevices.Gamepad, "ps2", "1.0.0"));
        }

        /// <summary>A supported capability must declare a positive, finite target frame rate.</summary>
        /// <param name="targetFrameRate">Invalid frame rate passed to the constructor.</param>
        [Theory]
        [InlineData(0d)]
        [InlineData(-60d)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void Ctor_WhenSupportedWithoutPositiveTargetFrameRate_Throws(double targetFrameRate) {
            ArgumentException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlatformPlayerCapability(true, "a.B", "staging", targetFrameRate, PlayerInputDevices.Gamepad, "ps2", "1.0.0"));
            Assert.Equal("targetFrameRate", exception.ParamName);
        }

        /// <summary>A supported capability exposes the target frame rate it was given.</summary>
        [Fact]
        public void Ctor_WhenSupported_ExposesTargetFrameRate() {
            Assert.Equal(59.94d, new PlatformPlayerCapability(true, "a.B", "staging", 59.94d, PlayerInputDevices.Gamepad, "ps2", "1.0.0").TargetFrameRate);
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
                () => new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.None, "ps2", "1.0.0"));
            Assert.Equal("inputDevices", exception.ParamName);
        }

        /// <summary>A supported capability rejects device bits that name no known device.</summary>
        [Fact]
        public void Ctor_WhenSupportedWithUndefinedInputDeviceBits_Throws() {
            ArgumentException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad | (PlayerInputDevices)64, "ps2", "1.0.0"));
            Assert.Equal("inputDevices", exception.ParamName);
        }

        /// <summary>A supported capability exposes exactly the device set it was given.</summary>
        [Fact]
        public void Ctor_WhenSupported_ExposesInputDevices() {
            PlayerInputDevices devices = PlayerInputDevices.Keyboard | PlayerInputDevices.Gamepad;
            Assert.Equal(devices, new PlatformPlayerCapability(true, "a.B", "staging", 60d, devices, "ps2", "1.0.0").InputDevices);
        }

        /// <summary>A supported capability must name the platform its native runtime reports to the core.</summary>
        /// <param name="runtimePlatformName">Missing or blank runtime platform name.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Ctor_WhenSupportedWithoutRuntimePlatformName_Throws(string runtimePlatformName) {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, runtimePlatformName, "1.0.0"));
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
                () => new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ps2", runtimePlatformVersion));
            Assert.Equal("runtimePlatformVersion", exception.ParamName);
        }

        /// <summary>A supported capability exposes the runtime platform name and version it was given.</summary>
        [Fact]
        public void Ctor_WhenSupported_ExposesRuntimePlatformIdentity() {
            PlatformPlayerCapability capability = new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ps2", "1.0.0");
            Assert.Equal("ps2", capability.RuntimePlatformName);
            Assert.Equal("1.0.0", capability.RuntimePlatformVersion);
        }

        /// <summary>An unsupported capability needs no runtime platform identity, because no player ever runs for it.</summary>
        [Fact]
        public void Ctor_WhenUnsupportedWithoutRuntimePlatformIdentity_DoesNotThrow() {
            PlatformPlayerCapability capability = new PlatformPlayerCapability(false, "none", "none", 0d, PlayerInputDevices.None, null, null);
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
    }
}
