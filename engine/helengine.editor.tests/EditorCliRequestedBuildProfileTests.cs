using helengine.baseplatform.Definitions;
using helengine.baseplatform.Profiles;

namespace helengine.editor.tests {
    /// <summary>Protects CLI profile transitions, inherited defaults and invocation precision from stale persisted settings.</summary>
    public sealed class EditorCliRequestedBuildProfileTests {
        /// <summary>Canonical profiles align the legacy toggle; custom profiles preserve its independent overlay setting.</summary>
        [Theory]
        [InlineData("debug", false, true)]
        [InlineData("release", true, false)]
        [InlineData("DEBUG", false, true)]
        [InlineData("profiler", true, true)]
        [InlineData("profiler", false, false)]
        public void ExplicitProfileSurvivesPersistedBuildMode(string profileId, bool previousDebug, bool expectedDebug) {
            EditorBuildPlatformConfigDocument config = new EditorBuildPlatformConfigDocument {
                SelectedBuildProfileId = "release",
                DebugBuild = previousDebug
            };
            EditorCliBuildPlatformConfigOverlayService.ApplyRequestedBuildProfile(config, profileId);
            Assert.Equal(profileId, config.SelectedBuildProfileId);
            Assert.Equal(expectedDebug, config.DebugBuild);
        }

        /// <summary>CLI profile transitions must apply the same inherited defaults as editor transitions instead of treating old defaults as explicit overrides.</summary>
        /// <param name="previousProfile">Profile whose inherited instrumentation value was saved.</param>
        /// <param name="requestedProfile">Profile requested by this invocation.</param>
        /// <param name="previousValue">Previously inherited instrumentation value.</param>
        /// <param name="expectedValue">New inherited instrumentation value.</param>
        [Theory]
        [InlineData("debug", "profiler", "false", "true")]
        [InlineData("release", "profiler", "false", "true")]
        [InlineData("profiler", "debug", "true", "false")]
        public void RequestedProfileUpdatesInheritedCodegenDefaults(string previousProfile, string requestedProfile, string previousValue, string expectedValue) {
            EditorBuildPlatformConfigDocument config = new EditorBuildPlatformConfigDocument {
                SelectedBuildProfileId = previousProfile,
                SelectedGraphicsProfileId = "graphics",
                SelectedCodegenProfileId = "default",
                SelectedCodegenOptionValues = new Dictionary<string, string> {
                    ["codegen-generated-function-profiling"] = previousValue,
                    ["custom-option"] = "authored-value"
                }
            };
            EditorCliBuildPlatformConfigOverlayService.ApplyRequestedBuildProfile(config, requestedProfile, CreateSelectionModel());
            Assert.Equal(expectedValue, config.SelectedCodegenOptionValues["codegen-generated-function-profiling"]);
            Assert.Equal("authored-value", config.SelectedCodegenOptionValues["custom-option"]);
        }

        /// <summary>A profiler invocation preserves a scope override which already differs from that profile's inherited value.</summary>
        [Fact]
        public void ExistingProfilerScopeOverrideRemainsExplicit() {
            EditorBuildPlatformConfigDocument config = new EditorBuildPlatformConfigDocument {
                SelectedBuildProfileId = "profiler",
                SelectedCodegenProfileId = "default",
                SelectedCodegenOptionValues = new Dictionary<string, string> {
                    ["codegen-generated-function-profiling"] = "false"
                }
            };
            EditorCliBuildPlatformConfigOverlayService.ApplyRequestedBuildProfile(config, "profiler", CreateSelectionModel());
            Assert.Equal("false", config.SelectedCodegenOptionValues["codegen-generated-function-profiling"]);
        }

        /// <summary>Valid invocation precision replaces stale persisted values before profile normalization validates them.</summary>
        /// <param name="duplicateCasing">Whether persisted aliases would also collide in the normalized case-insensitive option map.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void NumericInvocationRepairsStalePolicyBeforeProfileDefaults(bool duplicateCasing) {
            EditorBuildPlatformConfigDocument config = new EditorBuildPlatformConfigDocument {
                SelectedBuildProfileId = "debug",
                SelectedCodegenProfileId = "default",
                SelectedCodegenOptionValues = new Dictionary<string, string>(StringComparer.Ordinal) {
                    [PlatformCodegenSettingIds.RealStorageBits] = "80",
                    [PlatformCodegenSettingIds.RealArithmeticBits] = "80",
                    ["codegen-generated-function-profiling"] = "false"
                }
            };
            if (duplicateCasing) {
                config.SelectedCodegenOptionValues["REAL-STORAGE-BITS"] = "16";
            }
            EditorCliBuildOptions options = new EditorCliBuildOptions("project.heproj", "linux", "profiler", "output", false, "release", false, new Dictionary<string, string> {
                [PlatformCodegenSettingIds.RealStorageBits] = "32",
                [PlatformCodegenSettingIds.RealArithmeticBits] = "32"
            });
            EditorCliBuildPlatformConfigOverlayService.ApplyRequestedOptions(config, options, CreateSelectionModel());
            Assert.Equal("32", config.SelectedCodegenOptionValues[PlatformCodegenSettingIds.RealStorageBits]);
            Assert.Equal("32", config.SelectedCodegenOptionValues[PlatformCodegenSettingIds.RealArithmeticBits]);
            Assert.Equal("true", config.SelectedCodegenOptionValues["codegen-generated-function-profiling"]);
            Assert.Single(config.SelectedCodegenOptionValues.Keys, key => string.Equals(key, PlatformCodegenSettingIds.RealStorageBits, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Builds a real metadata model with the same shared codegen profile and per-build instrumentation defaults as native backends.</summary>
        /// <returns>Selection metadata consumed by production profile resolution.</returns>
        static EditorPlatformBuildSelectionModel CreateSelectionModel() {
            PlatformSettingDefinition profiling = new PlatformSettingDefinition("codegen-generated-function-profiling", "Generated scopes", PlatformSettingKind.Boolean, "false", true, []);
            PlatformDefinition definition = new PlatformDefinition("linux", "Linux", [
                new PlatformBuildProfileDefinition("debug", "Debug", "Debug", "graphics", "default", [], new Dictionary<string, string> { [profiling.SettingId] = "false" }),
                new PlatformBuildProfileDefinition("release", "Release", "Release", "graphics", "default", [], new Dictionary<string, string> { [profiling.SettingId] = "false" }),
                new PlatformBuildProfileDefinition("profiler", "Profiler", "Profiler", "graphics", "default", [], new Dictionary<string, string> { [profiling.SettingId] = "true" })
            ], [new PlatformGraphicsProfileDefinition("graphics", "Graphics", "Graphics", [])], [], [], [], [
                new PlatformCodegenProfileDefinition("default", "Default", "Default", PlatformCodegenLanguage.Cpp, PlatformSerializationEndianness.LittleEndian, [profiling])
            ], [], []);
            return EditorPlatformBuildSelectionModel.From(definition);
        }
    }
}
