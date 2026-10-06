using System.Runtime.Versioning;
using System.Xml.Linq;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>Verifies in-memory SVG masks, native pixel dimensions, and renderer ownership across size and state changes.</summary>
    [SupportedOSPlatform("windows")]
    public sealed class EditorWindowControlIconTests {
        /// <summary>Ensures each vector glyph produces visible coverage with transparent padding at its final DPI size.</summary>
        [Theory]
        [InlineData(EditorWindowControlIconKind.Minimize, 12)]
        [InlineData(EditorWindowControlIconKind.Maximize, 18)]
        [InlineData(EditorWindowControlIconKind.Restore, 24)]
        [InlineData(EditorWindowControlIconKind.Close, 12)]
        public void CreateTextureAsset_ProducesTransparentWhiteMaskAtRequestedSize(EditorWindowControlIconKind kind, int size) {
            XDocument source = XDocument.Parse(EditorWindowControlIconBuilder.BuildSvg(kind, size));
            Assert.Equal("http://www.w3.org/2000/svg", source.Root.Name.NamespaceName);
            Assert.Equal(size.ToString(), source.Root.Attribute("width").Value);
            Assert.DoesNotContain(source.Descendants(), element => element.Name.LocalName == "text" || element.Name.LocalName == "image");
            TextureAsset asset = EditorWindowControlIconBuilder.CreateTextureAsset(kind, size);
            Assert.Equal(size, asset.Width);
            Assert.Equal(size, asset.Height);
            Assert.Equal(size * size * 4, asset.Colors.Length);
            Assert.Contains(asset.Colors.Where((value, index) => index % 4 == 3), alpha => alpha > 0);
            Assert.Contains(asset.Colors.Where((value, index) => index % 4 == 3), alpha => alpha == 0);
            Assert.Equal(0, asset.Colors[3]);
            for (int index = 0; index < asset.Colors.Length; index += 4) {
                Assert.Equal(byte.MaxValue, asset.Colors[index]);
                Assert.Equal(byte.MaxValue, asset.Colors[index + 1]);
                Assert.Equal(byte.MaxValue, asset.Colors[index + 2]);
            }
        }

        /// <summary>Checks texture reuse, replacement on DPI/state change, and exactly-once cleanup during disposal.</summary>
        [Fact]
        public void ApplyGlyph_ReusesUnchangedTextureAndReleasesReplacedTextures() {
            TestRenderManager2D renderer = new TestRenderManager2D();
            EditorWindowControlIconComponent icon = new EditorWindowControlIconComponent(renderer, EditorWindowControlIconKind.Maximize, 12);
            RuntimeTexture original = icon.Sprite.Texture;
            icon.ApplyGlyph(EditorWindowControlIconKind.Maximize, 12);
            Assert.Same(original, icon.Sprite.Texture);
            icon.ApplyGlyph(EditorWindowControlIconKind.Maximize, 18);
            Assert.Equal(new int2(18, 18), icon.Sprite.Size);
            Assert.Contains(original, renderer.ReleasedTextures);
            RuntimeTexture resized = icon.Sprite.Texture;
            icon.ApplyGlyph(EditorWindowControlIconKind.Restore, 18);
            Assert.Equal(EditorWindowControlIconKind.Restore, icon.Kind);
            Assert.Contains(resized, renderer.ReleasedTextures);
            RuntimeTexture restored = icon.Sprite.Texture;
            icon.Dispose();
            icon.Dispose();
            Assert.Null(icon.Sprite.Texture);
            Assert.Equal(1, renderer.ReleasedTextures.Count(texture => ReferenceEquals(texture, restored)));
        }
    }
}
