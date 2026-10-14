using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.tests.Definitions {
    /// <summary>
    /// Verifies that a platform Play setting which may follow the project names a graphics option every graphics profile declares.
    /// </summary>
    public sealed class PlatformDefinitionPlaySettingsTests {
        /// <summary>A Play setting allowing <c>project</c> is accepted when every graphics profile declares the same id.</summary>
        [Fact]
        public void Ctor_WhenProjectSettingMatchesEveryGraphicsProfile_Succeeds() {
            PlatformDefinition definition = Create(
                [CreateProfile("a", "video-mode"), CreateProfile("b", "video-mode")],
                CreateCapability(new PlatformSettingDefinition("video-mode", "Video Mode", PlatformSettingKind.Choice, "project", true, ["project", "m1"])));

            Assert.Equal("video-mode", Assert.Single(definition.PlayerCapability.PlaySettings).SettingId);
        }

        /// <summary>A Play setting allowing <c>project</c> fails when one graphics profile lacks the id, naming the setting and profile.</summary>
        [Fact]
        public void Ctor_WhenAGraphicsProfileLacksTheProjectSetting_Throws() {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => Create(
                [CreateProfile("a", "video-mode"), CreateProfile("b", "other")],
                CreateCapability(new PlatformSettingDefinition("video-mode", "Video Mode", PlatformSettingKind.Choice, "project", true, ["project", "m1"]))));

            Assert.Equal("playerCapability", exception.ParamName);
            Assert.Contains("video-mode", exception.Message);
            Assert.Contains("'b'", exception.Message);
        }

        /// <summary>A text Play setting defaulting to <c>project</c> follows the same rule.</summary>
        [Fact]
        public void Ctor_WhenTextSettingDefaultsToProjectWithoutGraphicsSetting_Throws() {
            Assert.Throws<ArgumentException>(() => Create(
                [CreateProfile("a", "other")],
                CreateCapability(new PlatformSettingDefinition("resolution", "Resolution", PlatformSettingKind.Text, "project", true, []))));
        }

        /// <summary>A Play setting that cannot hold <c>project</c> needs no graphics counterpart.</summary>
        [Fact]
        public void Ctor_WhenSettingCannotFollowProject_NeedsNoGraphicsSetting() {
            PlatformDefinition definition = Create(
                [CreateProfile("a", "other")],
                CreateCapability(new PlatformSettingDefinition("screen-gap", "Gap", PlatformSettingKind.Text, "0", true, [])));

            Assert.Single(definition.PlayerCapability.PlaySettings);
        }

        /// <summary>Creates a platform definition with the supplied graphics profiles and Play capability.</summary>
        static PlatformDefinition Create(PlatformGraphicsProfileDefinition[] graphicsProfiles, PlatformPlayerCapability capability) {
            return new PlatformDefinition("ps2", "PlayStation 2", [], graphicsProfiles, [], [], [], [], [], [], playerCapability: capability);
        }

        /// <summary>Creates a graphics profile declaring one choice setting with the given id.</summary>
        static PlatformGraphicsProfileDefinition CreateProfile(string profileId, string settingId) {
            return new PlatformGraphicsProfileDefinition(profileId, profileId, profileId,
                [new PlatformSettingDefinition(settingId, settingId, PlatformSettingKind.Choice, "m1", true, ["m1"])]);
        }

        /// <summary>Creates a supported Play capability with one platform Play setting.</summary>
        static PlatformPlayerCapability CreateCapability(PlatformSettingDefinition playSetting) {
            return new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ps2", "1.0.0", [playSetting]);
        }
    }
}
