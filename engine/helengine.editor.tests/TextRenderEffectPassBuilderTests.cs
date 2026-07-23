using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies text effect pass ordering and pixel offsets shared by the 2D renderer backends.
    /// </summary>
    public sealed class TextRenderEffectPassBuilderTests {
        /// <summary>
        /// Ensures enabled text effects are ordered behind the main glyph and use the authored offsets and colors.
        /// </summary>
        [Fact]
        public void Build_WhenEffectsAreEnabled_ReturnsShadowOutlineAndMainInOrder() {
            TextComponent component = new TextComponent {
                Color = new byte4(255, 255, 255, 255),
                OutlineScale = 2f,
                OutlineColor = new byte4(1, 2, 3, 4),
                ShadowOffset = new float2(5f, 6f),
                ShadowColor = new byte4(7, 8, 9, 10)
            };

            List<TextRenderEffectPass> passes = TextRenderEffectPassBuilder.Build(component);

            Assert.Equal(6, passes.Count);
            Assert.Equal(new float2(5f, 6f), passes[0].Offset);
            Assert.Equal(component.ShadowColor, passes[0].Color);
            Assert.Equal(new float2(-2f, 0f), passes[1].Offset);
            Assert.Equal(new float2(2f, 0f), passes[2].Offset);
            Assert.Equal(new float2(0f, -2f), passes[3].Offset);
            Assert.Equal(new float2(0f, 2f), passes[4].Offset);
            Assert.Equal(component.OutlineColor, passes[1].Color);
            Assert.Equal(new float2(0f, 0f), passes[5].Offset);
            Assert.Equal(component.Color, passes[5].Color);
        }

        /// <summary>
        /// Ensures disabled effects leave exactly one normal glyph pass.
        /// </summary>
        [Fact]
        public void Build_WhenEffectsAreDisabled_ReturnsOnlyTheMainPass() {
            TextComponent component = new TextComponent {
                Color = new byte4(255, 255, 255, 255)
            };

            List<TextRenderEffectPass> passes = TextRenderEffectPassBuilder.Build(component);

            TextRenderEffectPass pass = Assert.Single(passes);
            Assert.Equal(new float2(0f, 0f), pass.Offset);
            Assert.Equal(component.Color, pass.Color);
        }
    }
}
