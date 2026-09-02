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
            string fontSourcePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "helengine.core",
                "assets",
                "font",
                "FontAsset.cs"));
            string textureSourcePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "helengine.core",
                "assets",
                "raw",
                "TextureAsset.cs"));
            string fontSourceText = File.ReadAllText(fontSourcePath)
                .Replace("\r\n", "\n", StringComparison.Ordinal);
            string textureSourceText = File.ReadAllText(textureSourcePath)
                .Replace("\r\n", "\n", StringComparison.Ordinal);

            Assert.Contains("NativeOwnership.DisposeAndDelete(SourceTextureAsset);", fontSourceText);
            Assert.Contains("[NativeOwnedMember]\n        public TextureAsset SourceTextureAsset", fontSourceText);
            Assert.Contains("public class TextureAsset : Asset, IDisposable", textureSourceText);
            Assert.Contains("NativeOwnership.Release(ref Colors);", textureSourceText);
            Assert.Contains("NativeOwnership.Release(ref PaletteColors);", textureSourceText);
        }
    }
}
