using helengine.editor.tests.testing;

namespace helengine.editor.tests.managers.rendering {
    /// <summary>
    /// Verifies the flat 2D command container preserves order, payload access, and reuse semantics.
    /// </summary>
    public sealed class RenderCommandList2DTests {
        /// <summary>
        /// Ensures mixed 2D commands preserve insertion order and expose typed payload data.
        /// </summary>
        [Fact]
        public void Add_WhenCommandsAreWritten_PreservesInsertionOrderAndPayloadAccess() {
            RenderCommandList2D list = new RenderCommandList2D(4);
            TestRuntimeTexture texturedQuadTexture = new TestRuntimeTexture();
            TestRuntimeTexture glyphTexture = new TestRuntimeTexture();

            list.AddTexturedQuad(
                texturedQuadTexture,
                new float4(10f, 20f, 30f, 40f),
                new float4(0.1f, 0.2f, 0.3f, 0.4f),
                new byte4(1, 2, 3, 4),
                0.25f);
            list.AddGlyphQuad(
                glyphTexture,
                new float4(11f, 21f, 31f, 41f),
                new float4(0.11f, 0.21f, 0.31f, 0.41f),
                new byte4(5, 6, 7, 8));
            list.AddRoundedRect(
                new float4(12f, 22f, 32f, 42f),
                9f,
                3f,
                RoundedRectCorners.TopLeft | RoundedRectCorners.BottomRight,
                new byte4(9, 10, 11, 12),
                new byte4(13, 14, 15, 16));

            Assert.Equal(3, list.Count);
            Assert.Equal(RenderCommand2DType.TexturedQuad, list.GetCommandType(0));
            Assert.Equal(RenderCommand2DType.GlyphQuad, list.GetCommandType(1));
            Assert.Equal(RenderCommand2DType.RoundedRect, list.GetCommandType(2));

            int texturedQuadPayloadIndex = list.GetTexturedQuadPayloadIndex(0);
            Assert.Same(texturedQuadTexture, list.GetTexturedQuadTexture(texturedQuadPayloadIndex));
            Assert.Equal(new float4(10f, 20f, 30f, 40f), list.GetTexturedQuadBounds(texturedQuadPayloadIndex));
            Assert.Equal(new float4(0.1f, 0.2f, 0.3f, 0.4f), list.GetTexturedQuadSourceRect(texturedQuadPayloadIndex));
            Assert.Equal(new byte4(1, 2, 3, 4), list.GetTexturedQuadColor(texturedQuadPayloadIndex));
            Assert.Equal(0.25f, list.GetTexturedQuadRotation(texturedQuadPayloadIndex));

            int glyphPayloadIndex = list.GetGlyphQuadPayloadIndex(1);
            Assert.Same(glyphTexture, list.GetGlyphQuadTexture(glyphPayloadIndex));
            Assert.Equal(new float4(11f, 21f, 31f, 41f), list.GetGlyphQuadBounds(glyphPayloadIndex));
            Assert.Equal(new float4(0.11f, 0.21f, 0.31f, 0.41f), list.GetGlyphQuadSourceRect(glyphPayloadIndex));
            Assert.Equal(new byte4(5, 6, 7, 8), list.GetGlyphQuadColor(glyphPayloadIndex));

            int roundedRectPayloadIndex = list.GetRoundedRectPayloadIndex(2);
            Assert.Equal(new float4(12f, 22f, 32f, 42f), list.GetRoundedRectBounds(roundedRectPayloadIndex));
            Assert.Equal(9f, list.GetRoundedRectRadius(roundedRectPayloadIndex));
            Assert.Equal(3f, list.GetRoundedRectBorderThickness(roundedRectPayloadIndex));
            Assert.Equal(RoundedRectCorners.TopLeft | RoundedRectCorners.BottomRight, list.GetRoundedRectCorners(roundedRectPayloadIndex));
            Assert.Equal(new byte4(9, 10, 11, 12), list.GetRoundedRectFillColor(roundedRectPayloadIndex));
            Assert.Equal(new byte4(13, 14, 15, 16), list.GetRoundedRectBorderColor(roundedRectPayloadIndex));
        }

        /// <summary>
        /// Ensures reset clears the logical command count and allows the same container to be reused for new commands.
        /// </summary>
        [Fact]
        public void Reset_WhenCalled_ClearsLogicalCommandCountForReuse() {
            RenderCommandList2D list = new RenderCommandList2D(2);

            list.AddRoundedRect(
                new float4(1f, 2f, 3f, 4f),
                5f,
                6f,
                RoundedRectCorners.All,
                new byte4(7, 8, 9, 10),
                new byte4(11, 12, 13, 14));

            list.Reset();

            Assert.Equal(0, list.Count);

            TestRuntimeTexture texturedQuadTexture = new TestRuntimeTexture();
            list.AddTexturedQuad(
                texturedQuadTexture,
                new float4(20f, 30f, 40f, 50f),
                new float4(0.2f, 0.3f, 0.4f, 0.5f),
                new byte4(15, 16, 17, 18),
                0.5f);

            Assert.Equal(1, list.Count);
            Assert.Equal(RenderCommand2DType.TexturedQuad, list.GetCommandType(0));

            int texturedQuadPayloadIndex = list.GetTexturedQuadPayloadIndex(0);
            Assert.Same(texturedQuadTexture, list.GetTexturedQuadTexture(texturedQuadPayloadIndex));
            Assert.Equal(new float4(20f, 30f, 40f, 50f), list.GetTexturedQuadBounds(texturedQuadPayloadIndex));
            Assert.Equal(new float4(0.2f, 0.3f, 0.4f, 0.5f), list.GetTexturedQuadSourceRect(texturedQuadPayloadIndex));
            Assert.Equal(new byte4(15, 16, 17, 18), list.GetTexturedQuadColor(texturedQuadPayloadIndex));
            Assert.Equal(0.5f, list.GetTexturedQuadRotation(texturedQuadPayloadIndex));
        }

        /// <summary>
        /// Ensures clip push and pop commands preserve their order and expose the stored clip rectangle payload.
        /// </summary>
        [Fact]
        public void AddClipCommands_WhenWritten_PreservesClipOrderAndPayloadAccess() {
            RenderCommandList2D list = new RenderCommandList2D(4);

            list.AddClipPush(new float4(3f, 4f, 50f, 60f));
            list.AddClipPop();

            Assert.Equal(2, list.Count);
            Assert.Equal(RenderCommand2DType.ClipPush, list.GetCommandType(0));
            Assert.Equal(RenderCommand2DType.ClipPop, list.GetCommandType(1));

            int payloadIndex = list.GetClipPushPayloadIndex(0);
            Assert.Equal(new float4(3f, 4f, 50f, 60f), list.GetClipPushRect(payloadIndex));
        }
    }
}
