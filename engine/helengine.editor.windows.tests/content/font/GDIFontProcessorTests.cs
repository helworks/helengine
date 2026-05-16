using System.Drawing;
using System.Runtime.Versioning;

namespace helengine.editor.windows.tests.content.font {
    /// <summary>
    /// Verifies that GDI font atlas generation preserves grayscale edge coverage needed by low-resolution UI renderers.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class GDIFontProcessorTests {
        /// <summary>
        /// Ensures generated glyph atlases contain intermediate alpha coverage instead of only binary transparent or opaque texels.
        /// </summary>
        [Fact]
        public void ImportFont_WhenGeneratingAtlas_ProducesIntermediateAlphaCoverage() {
            using Font font = new Font(FontFamily.GenericSansSerif, 32f, FontStyle.Regular, GraphicsUnit.Pixel);

            FontAsset fontAsset = GDIFontProcessor.ImportFont(font);

            Assert.NotNull(fontAsset);
            Assert.NotNull(fontAsset.SourceTextureAsset);
            Assert.NotNull(fontAsset.SourceTextureAsset.Colors);
            Assert.Contains(fontAsset.SourceTextureAsset.Colors.Where((value, index) => ((index + 1) % 4) == 0), alpha => alpha > 0 && alpha < byte.MaxValue);
        }
    }
}
