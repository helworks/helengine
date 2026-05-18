using Xunit;

namespace helengine.editor.tests.serialization {
    /// <summary>
    /// Locks native-specific font disposal contracts that managed tests cannot observe directly.
    /// </summary>
    public sealed class FontAssetSourceAuditTests {
        /// <summary>
        /// Ensures font disposal does not native-delete shared empty-array sentinels after runtime texture builders adopt source atlas buffers.
        /// </summary>
        [Fact]
        public void Dispose_whenSourceTextureUsesSharedEmptyArrays_guardsAgainstDeletingArrayEmptySentinels() {
            string sourcePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "helengine.core",
                "assets",
                "font",
                "FontAsset.cs"));
            string sourceText = File.ReadAllText(sourcePath);

            Assert.Contains("ReferenceEquals(sourceTextureColors, Array.Empty<byte>())", sourceText);
            Assert.Contains("ReferenceEquals(sourceTexturePaletteColors, Array.Empty<byte>())", sourceText);
            Assert.Contains("if (!sourceTextureColorsUsesSharedEmptyArray)", sourceText);
            Assert.Contains("if (!sourceTexturePaletteColorsUsesSharedEmptyArray)", sourceText);
        }
    }
}
