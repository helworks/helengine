using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies text components expose a stable render-state version that only changes when render-relevant text data changes.
    /// </summary>
    public sealed class TextComponentRenderStateVersionTests {
        /// <summary>
        /// Ensures changing visible text data advances the render-state version.
        /// </summary>
        [Fact]
        public void TextRenderStateVersion_WhenVisibleTextDataChanges_Increments() {
            TextComponent component = new TextComponent();
            int initialVersion = component.TextRenderStateVersion;

            component.Text = "Hello";
            int textVersion = component.TextRenderStateVersion;
            component.Size = new int2(120, 32);
            int sizeVersion = component.TextRenderStateVersion;
            component.FontScale = 2f;
            int fontScaleVersion = component.TextRenderStateVersion;
            component.WrapText = true;
            int wrapVersion = component.TextRenderStateVersion;
            component.Color = new byte4(255, 0, 0, 255);
            int colorVersion = component.TextRenderStateVersion;
            component.SourceRect = new float4(0f, 0f, 0.5f, 1f);
            int sourceRectVersion = component.TextRenderStateVersion;
            component.Alignment = TextAlignment.Center;
            int alignmentVersion = component.TextRenderStateVersion;

            Assert.True(textVersion > initialVersion);
            Assert.True(sizeVersion > textVersion);
            Assert.True(fontScaleVersion > sizeVersion);
            Assert.True(wrapVersion > fontScaleVersion);
            Assert.True(colorVersion > wrapVersion);
            Assert.True(sourceRectVersion > colorVersion);
            Assert.True(alignmentVersion > sourceRectVersion);
        }

        /// <summary>
        /// Ensures assigning the same visible values does not advance the render-state version.
        /// </summary>
        [Fact]
        public void TextRenderStateVersion_WhenVisibleTextDataDoesNotChange_DoesNotIncrement() {
            TextComponent component = new TextComponent {
                Text = "Stable",
                Size = new int2(64, 16),
                FontScale = 1.5f,
                WrapText = true,
                Color = new byte4(255, 255, 255, 255),
                SourceRect = new float4(0f, 0f, 1f, 1f),
                Alignment = TextAlignment.Right
            };
            int stableVersion = component.TextRenderStateVersion;

            component.Text = "Stable";
            component.Size = new int2(64, 16);
            component.FontScale = 1.5f;
            component.WrapText = true;
            component.Color = new byte4(255, 255, 255, 255);
            component.SourceRect = new float4(0f, 0f, 1f, 1f);
            component.Alignment = TextAlignment.Right;

            Assert.Equal(stableVersion, component.TextRenderStateVersion);
        }
    }
}
