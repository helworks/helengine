using helengine;
using helengine.editor;

namespace helengine.editor.portableimporter.tests {
    /// <summary>Exercises real font rasterization without GDI or a graphics device.</summary>
    public sealed class PortableFontImporterTests {
        /// <summary>Checks measured advances, visible pixels and cropped glyph coordinates against a synthetic face.</summary>
        [Fact]
        public void ImportsSourceFaceAndRetainsAtlasPixels() {
            using FileStream source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "fixtures", "Synthetic.ttf"));
            using FontAsset font = new PortableFontImporter().ImportFont(source, new FontAssetProcessorSettings { PixelSize = 32 });
            Assert.NotNull(font.SourceTextureAsset);
            Assert.Equal(font.AtlasWidth * font.AtlasHeight * 4, font.SourceTextureAsset.Colors.Length);
            Assert.True(font.LineHeight > 0);
            Assert.True(font.Characters['M'].AdvanceWidth > font.Characters['i'].AdvanceWidth);
            Assert.True(font.Characters['M'].SourceRect.Z > 0);
            Assert.Contains(font.SourceTextureAsset.Colors.Where((value, index) => index % 4 == 3), value => value > 0);
            Assert.Equal(font.AtlasWidth, font.Texture.Width);
        }

        /// <summary>Checks the existing pixel-snapped horizontal metrics option without depending on the host font registry.</summary>
        [Fact]
        public void SnapsHorizontalMetricsWhenRequested() {
            using FileStream source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "fixtures", "Synthetic.ttf"));
            using FontAsset font = new PortableFontImporter().ImportFont(source, new FontAssetProcessorSettings { PixelSize = 19, SnapHorizontalMetricsToPixels = true });
            Assert.All(font.Characters.Values, glyph => Assert.Equal(MathF.Round(glyph.AdvanceWidth), glyph.AdvanceWidth));
        }

        /// <summary>Rejects corrupt source bytes rather than substituting a system font.</summary>
        [Fact]
        public void RejectsInvalidFontBytes() {
            using MemoryStream source = new MemoryStream(new byte[] { 1, 2, 3, 4 });
            Assert.ThrowsAny<Exception>(() => new PortableFontImporter().ImportFont(source, new FontAssetProcessorSettings { PixelSize = 16 }));
        }
    }
}
