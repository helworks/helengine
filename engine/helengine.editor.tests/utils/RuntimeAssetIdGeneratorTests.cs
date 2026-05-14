using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies deterministic runtime asset id generation from canonical asset keys.
    /// </summary>
    public sealed class RuntimeAssetIdGeneratorTests {
        /// <summary>
        /// Ensures identical canonical keys produce one stable non-zero runtime id.
        /// </summary>
        [Fact]
        public void Generate_whenCanonicalKeyMatches_returnsSameId() {
            ulong first = RuntimeAssetIdGenerator.Generate("cooked/Fonts/DemoDiscBody.hefont#atlas");
            ulong second = RuntimeAssetIdGenerator.Generate("cooked/Fonts/DemoDiscBody.hefont#atlas");

            Assert.Equal(first, second);
            Assert.NotEqual(0ul, first);
        }

        /// <summary>
        /// Ensures equivalent path separators normalize to the same runtime id.
        /// </summary>
        [Fact]
        public void Generate_whenPathSeparatorsDiffer_returnsSameId() {
            ulong unixStyle = RuntimeAssetIdGenerator.Generate("cooked/Fonts/DemoDiscBody.hefont#atlas");
            ulong windowsStyle = RuntimeAssetIdGenerator.Generate("cooked\\Fonts\\DemoDiscBody.hefont#atlas");

            Assert.Equal(unixStyle, windowsStyle);
        }
    }
}
