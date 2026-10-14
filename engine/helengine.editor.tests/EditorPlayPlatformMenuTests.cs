using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the items of the viewport Play platform selector.
    /// </summary>
    public sealed class EditorPlayPlatformMenuTests {
        /// <summary>The settings item follows the platforms.</summary>
        [Fact]
        public void BuildItems_AppendsPlaySettingsAfterPlatforms() {
            Assert.Equal(new[] { "PlayStation 2", "Windows", "Play Settings..." }, EditorPlayPlatformMenu.BuildItems(new[] { "PlayStation 2", "Windows" }));
        }

        /// <summary>With no platform there is nothing to configure, so no settings item.</summary>
        [Fact]
        public void BuildItems_WithoutPlatforms_IsEmpty() {
            Assert.Empty(EditorPlayPlatformMenu.BuildItems(Array.Empty<string>()));
        }

        /// <summary>Only the index after the platforms is the settings item.</summary>
        [Theory]
        [InlineData(0, 2, false)]
        [InlineData(1, 2, false)]
        [InlineData(2, 2, true)]
        public void IsPlaySettingsItem_MatchesIndexAfterPlatforms(int index, int platformCount, bool expected) {
            Assert.Equal(expected, EditorPlayPlatformMenu.IsPlaySettingsItem(index, platformCount));
        }
    }
}
