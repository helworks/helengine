using helengine.editor;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies legacy platform build-profile identifiers normalize to canonical current ids.
    /// </summary>
    public sealed class EditorLegacyBuildProfileIdNormalizerTests {
        /// <summary>
        /// Ensures Nintendo DS local build configs rewrite the legacy single-profile id according to the selected build mode.
        /// </summary>
        [Theory]
        [InlineData(true, "debug")]
        [InlineData(false, "release")]
        public void NormalizeLocalBuildProfileId_WhenNintendoDsUsesLegacyId_RewritesToBuildModeSpecificCanonicalProfile(bool debugBuild, string expectedProfileId) {
            string normalizedProfileId = EditorLegacyBuildProfileIdNormalizer.NormalizeLocalBuildProfileId("ds", "ds-default", debugBuild);

            Assert.Equal(expectedProfileId, normalizedProfileId);
        }

        /// <summary>
        /// Ensures Nintendo DS shared profile settings rewrite the legacy single-profile id to the canonical release profile.
        /// </summary>
        [Fact]
        public void NormalizeSharedBuildProfileId_WhenNintendoDsUsesLegacyId_RewritesToCanonicalReleaseProfile() {
            string normalizedProfileId = EditorLegacyBuildProfileIdNormalizer.NormalizeSharedBuildProfileId("ds", "ds-default");

            Assert.Equal("release", normalizedProfileId);
        }

        /// <summary>
        /// Ensures unrelated platforms preserve their authored build-profile identifiers.
        /// </summary>
        [Fact]
        public void NormalizeBuildProfileId_WhenPlatformDoesNotUseLegacyNintendoDsId_PreservesOriginalValue() {
            string normalizedLocalProfileId = EditorLegacyBuildProfileIdNormalizer.NormalizeLocalBuildProfileId("3ds", "3ds-default", false);
            string normalizedSharedProfileId = EditorLegacyBuildProfileIdNormalizer.NormalizeSharedBuildProfileId("3ds", "3ds-default");

            Assert.Equal("3ds-default", normalizedLocalProfileId);
            Assert.Equal("3ds-default", normalizedSharedProfileId);
        }
    }
}
