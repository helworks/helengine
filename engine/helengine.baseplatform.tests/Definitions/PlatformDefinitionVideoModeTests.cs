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
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => Create(CreateGraphicsSetting(["a", "b"], "a"), CreatePlayCapability(0d, CreatePlaySetting(["project", "a"])), CreateModes()));

            Assert.Equal("videoModeCapability", exception.ParamName);
        }

        /// <summary>A Play platform with modes may not also declare a fixed frame rate.</summary>
        [Fact]
        public void Ctor_WhenModesAndFixedFrameRate_Throws() {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => Create(CreateGraphicsSetting(["a", "b"], "a"), CreatePlayCapability(60d, CreatePlaySetting(["project", "a", "b"])), CreateModes()));

            Assert.Equal("playerCapability", exception.ParamName);
        }

        /// <summary>A Play platform without modes must declare a fixed frame rate.</summary>
        [Fact]
        public void Ctor_WhenNoModesAndNoFrameRate_Throws() {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => Create(CreateGraphicsSetting(["a", "b"], "a"), new PlatformPlayerCapability(true, "a.B", "s", 0d, PlayerInputDevices.Gamepad, "ps2", "1.0.0", []), null));

            Assert.Equal("playerCapability", exception.ParamName);
        }

        /// <summary>Every graphics profile must declare the setting that selects the video mode.</summary>
        [Fact]
        public void Ctor_WhenGraphicsProfileLacksTheModeSetting_Throws() {
            PlatformSettingDefinition otherSetting = new PlatformSettingDefinition("other", "Other", PlatformSettingKind.Choice, "a", true, ["a", "b"]);

            ArgumentException exception = Assert.Throws<ArgumentException>(() => Create(otherSetting, null, CreateModes()));

            Assert.Equal("videoModeCapability", exception.ParamName);
        }

        /// <summary>The graphics setting selecting the mode must be a choice, even when its values match the modes.</summary>
        [Fact]
        public void Ctor_WhenGraphicsModeSettingIsNotAChoice_Throws() {
            PlatformSettingDefinition textSetting = new PlatformSettingDefinition("video-mode", "Video Mode", PlatformSettingKind.Text, "a", true, ["a", "b"]);

            ArgumentException exception = Assert.Throws<ArgumentException>(() => Create(textSetting, null, CreateModes()));

            Assert.Equal("videoModeCapability", exception.ParamName);
        }

        /// <summary>The graphics setting selecting the mode must default to one of the modes.</summary>
        [Fact]
        public void Ctor_WhenGraphicsModeDefaultIsNotAMode_Throws() {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => Create(CreateGraphicsSetting(["a", "b"], "c"), null, CreateModes()));

            Assert.Equal("videoModeCapability", exception.ParamName);
        }

        /// <summary>The Play setting must default to project, even when it offers project plus every mode.</summary>
        [Fact]
        public void Ctor_WhenPlaySettingDefaultIsNotProject_Throws() {
            PlatformSettingDefinition playSetting = new PlatformSettingDefinition("video-mode", "Video Mode", PlatformSettingKind.Choice, "a", true, ["project", "a", "b"]);

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => Create(CreateGraphicsSetting(["a", "b"], "a"), CreatePlayCapability(0d, playSetting), CreateModes()));

            Assert.Equal("videoModeCapability", exception.ParamName);
        }

        /// <summary>A platform without Play may declare modes; only its graphics profiles must agree with them.</summary>
        [Fact]
        public void Ctor_WhenModesWithoutPlaySupport_IsAccepted() {
            PlatformDefinition definition = Create(CreateGraphicsSetting(["a", "b"], "a"), null, CreateModes());

            Assert.True(definition.HasVideoModes);
            Assert.False(definition.PlayerCapability.SupportsPlay);
            Assert.False(definition.PlayerCapability.FrameRateFromVideoMode);
        }

        /// <summary>
        /// A mode Play setting that can follow the project is checked first by the project-option rule, whose message names
        /// both the setting and the graphics profile that lacks it.
        /// </summary>
        [Fact]
        public void Ctor_WhenGraphicsProfileLacksTheFollowedModeSetting_NamesSettingAndProfile() {
            PlatformSettingDefinition otherSetting = new PlatformSettingDefinition("other", "Other", PlatformSettingKind.Choice, "a", true, ["a", "b"]);

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => Create(otherSetting, CreatePlayCapability(0d, CreatePlaySetting(["project", "a", "b"])), CreateModes()));

            Assert.Equal("playerCapability", exception.ParamName);
            Assert.Contains("'video-mode'", exception.Message, StringComparison.Ordinal);
            Assert.Contains("graphics profile 'g'", exception.Message, StringComparison.Ordinal);
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
