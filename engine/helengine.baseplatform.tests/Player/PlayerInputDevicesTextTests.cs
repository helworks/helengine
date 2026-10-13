using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.tests.Player {
    /// <summary>
    /// Verifies the lowercase comma-list text form of <see cref="PlayerInputDevices"/> that the editor writes to the
    /// player command line and the player reads back.
    /// </summary>
    public sealed class PlayerInputDevicesTextTests {
        /// <summary>Option name used as the error source in the parse tests.</summary>
        const string OptionName = "--input-devices";

        /// <summary>A single device is written as its lowercase name.</summary>
        [Fact]
        public void Format_WhenGamepadOnly_WritesGamepad() {
            Assert.Equal("gamepad", PlayerInputDevicesText.Format(PlayerInputDevices.Gamepad));
        }

        /// <summary>Several devices are written in declaration order, comma-separated, without spaces.</summary>
        [Fact]
        public void Format_WhenAllDevices_WritesCommaListInDeclarationOrder() {
            PlayerInputDevices devices = PlayerInputDevices.Gamepad | PlayerInputDevices.Pointer | PlayerInputDevices.Mouse | PlayerInputDevices.Keyboard;
            Assert.Equal("keyboard,mouse,pointer,gamepad", PlayerInputDevicesText.Format(devices));
        }

        /// <summary>An empty device set has no text form, because a launched player always has at least one device.</summary>
        [Fact]
        public void Format_WhenNone_Throws() {
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerInputDevicesText.Format(PlayerInputDevices.None));
        }

        /// <summary>Every formatted set parses back to the same flags.</summary>
        /// <param name="devices">Device set to round-trip.</param>
        [Theory]
        [InlineData(PlayerInputDevices.Gamepad)]
        [InlineData(PlayerInputDevices.Keyboard | PlayerInputDevices.Mouse)]
        [InlineData(PlayerInputDevices.Keyboard | PlayerInputDevices.Mouse | PlayerInputDevices.Pointer | PlayerInputDevices.Gamepad)]
        public void Parse_RoundTripsFormat(PlayerInputDevices devices) {
            Assert.Equal(devices, PlayerInputDevicesText.Parse(PlayerInputDevicesText.Format(devices), OptionName));
        }

        /// <summary>An unknown device name fails and names the option the text came from.</summary>
        [Fact]
        public void Parse_WhenNameUnknown_ThrowsNamingSource() {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => PlayerInputDevicesText.Parse("gamepad,joystick", OptionName));
            Assert.Contains(OptionName, exception.Message);
            Assert.Contains("joystick", exception.Message);
        }

        /// <summary>Names are matched exactly in lowercase, so the editor and the player cannot drift on casing.</summary>
        [Fact]
        public void Parse_WhenNameNotLowercase_Throws() {
            Assert.Throws<ArgumentException>(() => PlayerInputDevicesText.Parse("Gamepad", OptionName));
        }

        /// <summary>Empty entries and repeated devices are rejected instead of being ignored.</summary>
        /// <param name="text">Malformed device list.</param>
        [Theory]
        [InlineData("")]
        [InlineData("gamepad,")]
        [InlineData("gamepad,gamepad")]
        public void Parse_WhenListMalformed_Throws(string text) {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => PlayerInputDevicesText.Parse(text, OptionName));
            Assert.Contains(OptionName, exception.Message);
        }
    }
}
