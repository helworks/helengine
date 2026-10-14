using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.tests.Definitions {
    /// <summary>
    /// Verifies a platform's video modes agree with its graphics profiles and Play declarations.
    /// </summary>
    public sealed class PlatformDefinitionVideoModeTests {
        /// <summary>A consistent declaration is accepted and exposed.</summary>
        [Fact]
        public void Ctor_WhenConsistent_ExposesVideoModes() {
            PlatformDefinition definition = Create(CreateGraphicsSetting(["a", "b"], "a"), CreatePlayCapability(0d, CreatePlaySetting(["project", "a", "b"])), CreateModes());

            Assert.True(definition.HasVideoModes);
            Assert.True(definition.PlayerCapability.FrameRateFromVideoMode);
            Assert.Equal("b", definition.VideoModeCapability.Resolve("b", "ps2").Id);
        }

        /// <summary>The graphics choice must list exactly the mode ids, in order.</summary>
        [Fact]
        public void Ctor_WhenGraphicsChoiceDiffers_Throws() {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => Create(CreateGraphicsSetting(["b", "a"], "a"), CreatePlayCapability(0d, CreatePlaySetting(["project", "a", "b"])), CreateModes()));

            Assert.Equal("videoModeCapability", exception.ParamName);
        }

        /// <summary>The Play setting must offer project plus every mode.</summary>
        [Fact]
        public void Ctor_WhenPlaySettingDiffers_Throws() {
            Assert.Throws<ArgumentException>(
                () => Create(CreateGraphicsSetting(["a", "b"], "a"), CreatePlayCapability(0d, CreatePlaySetting(["project", "a"])), CreateModes()));
        }

        /// <summary>A Play platform with modes may not also declare a fixed frame rate.</summary>
        [Fact]
        public void Ctor_WhenModesAndFixedFrameRate_Throws() {
            Assert.Throws<ArgumentException>(
                () => Create(CreateGraphicsSetting(["a", "b"], "a"), CreatePlayCapability(60d, CreatePlaySetting(["project", "a", "b"])), CreateModes()));
        }

        /// <summary>A Play platform without modes must declare a fixed frame rate.</summary>
        [Fact]
        public void Ctor_WhenNoModesAndNoFrameRate_Throws() {
            Assert.Throws<ArgumentException>(
                () => Create(CreateGraphicsSetting(["a", "b"], "a"), new PlatformPlayerCapability(true, "a.B", "s", 0d, PlayerInputDevices.Gamepad, "ps2", "1.0.0", []), null));
        }

        /// <summary>Creates the definition under test.</summary>
        static PlatformDefinition Create(PlatformSettingDefinition graphicsSetting, PlatformPlayerCapability capability, PlatformVideoModeCapability modes) {
            return new PlatformDefinition("ps2", "PlayStation 2", [],
                [new PlatformGraphicsProfileDefinition("g", "G", "G", [graphicsSetting])],
                [], [], [], [], [], [], playerCapability: capability, videoModeCapability: modes);
        }

        /// <summary>Creates the graphics choice selecting the mode.</summary>
        static PlatformSettingDefinition CreateGraphicsSetting(string[] allowed, string defaultValue) {
            return new PlatformSettingDefinition("video-mode", "Video Mode", PlatformSettingKind.Choice, defaultValue, true, allowed);
        }

        /// <summary>Creates the Play setting choosing how Play renders.</summary>
        static PlatformSettingDefinition CreatePlaySetting(string[] allowed) {
            return new PlatformSettingDefinition("video-mode", "Video Mode", PlatformSettingKind.Choice, "project", true, allowed);
        }

        /// <summary>Creates a supported Play capability.</summary>
        static PlatformPlayerCapability CreatePlayCapability(double frameRate, PlatformSettingDefinition playSetting) {
            return new PlatformPlayerCapability(true, "a.B", "s", frameRate, PlayerInputDevices.Gamepad, "ps2", "1.0.0", [playSetting]);
        }

        /// <summary>Creates two modes.</summary>
        static PlatformVideoModeCapability CreateModes() {
            return new PlatformVideoModeCapability("video-mode", [
                new PlatformVideoMode("a", 640, 448, false, 59.94d, 60d, 4, 3),
                new PlatformVideoMode("b", 320, 224, true, 59.94d, 60d, 4, 3)
            ]);
        }
    }
}
