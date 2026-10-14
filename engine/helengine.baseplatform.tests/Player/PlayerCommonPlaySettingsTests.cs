using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.tests.Player {
    /// <summary>
    /// Verifies the engine-declared common Play settings every platform's Play session offers.
    /// </summary>
    public sealed class PlayerCommonPlaySettingsTests {
        /// <summary>The common settings are exactly the five the Play settings spec lists, with their defaults and values.</summary>
        [Fact]
        public void CreateDefinitions_DeclaresTheFiveCommonSettings() {
            PlatformSettingDefinition[] definitions = PlayerCommonPlaySettings.CreateDefinitions();

            Assert.Equal(new[] { "window-scale", "window-monitor", "wait-for-debugger", "shadows", "frame-rate" }, definitions.Select(definition => definition.SettingId).ToArray());
            Assert.Equal("fit", definitions[0].DefaultValue);
            Assert.Equal(new[] { "1x", "2x", "3x", "4x", "fit" }, definitions[0].AllowedValues);
            Assert.Equal("editor", definitions[1].DefaultValue);
            Assert.Equal(new[] { "editor", "primary" }, definitions[1].AllowedValues);
            Assert.Equal(PlatformSettingKind.Boolean, definitions[2].SettingKind);
            Assert.Equal("false", definitions[2].DefaultValue);
            Assert.Equal(PlatformSettingKind.Boolean, definitions[3].SettingKind);
            Assert.Equal("true", definitions[3].DefaultValue);
            Assert.Equal("platform", definitions[4].DefaultValue);
            Assert.Equal(new[] { "platform", "unlimited" }, definitions[4].AllowedValues);
        }

        /// <summary>Every common declaration satisfies the same rules platform declarations must satisfy.</summary>
        [Fact]
        public void CreateDefinitions_PassDeclarationRules() {
            foreach (PlatformSettingDefinition definition in PlayerCommonPlaySettings.CreateDefinitions()) {
                PlatformPlaySettingDeclarationRules.Validate(definition, "definitions");
            }
        }

        /// <summary>Each call returns fresh instances, so no caller can change another caller's declarations.</summary>
        [Fact]
        public void CreateDefinitions_ReturnsFreshArrays() {
            Assert.NotSame(PlayerCommonPlaySettings.CreateDefinitions(), PlayerCommonPlaySettings.CreateDefinitions());
        }

        /// <summary>Only the five common ids are reported as common.</summary>
        [Theory]
        [InlineData("window-scale", true)]
        [InlineData("frame-rate", true)]
        [InlineData("video-mode", false)]
        [InlineData("Window-Scale", false)]
        public void IsCommonId_MatchesOrdinally(string settingId, bool expected) {
            Assert.Equal(expected, PlayerCommonPlaySettings.IsCommonId(settingId));
        }
    }
}
