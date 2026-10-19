using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>Verifies horizontal crop bearings move glyph geometry consistently without altering logical advances, wrapping or vertical placement.</summary>
    public sealed class TextGlyphBearingTests {
        /// <summary>Positive, negative and legacy zero bearings affect every effect and main quad identically in both maintained emission paths.</summary>
        /// <param name="bearing">Horizontal origin of the cropped B glyph.</param>
        /// <param name="effects">Whether the text emits shadow and outline copies before its main glyph.</param>
        [Theory]
        [InlineData(-3f, false)]
        [InlineData(0f, false)]
        [InlineData(2f, false)]
        [InlineData(-3f, true)]
        [InlineData(0f, true)]
        [InlineData(2f, true)]
        public void BothEmittersApplyScaledBearingWithoutChangingPenAdvance(float bearing, bool effects) {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            using FontAsset font = CreateFont(renderer, bearing);
            TextComponent text = CreateText(core, font, "Be", TextAlignment.Left, 2f, effects);
            using RenderList2D queue = new RenderList2D(1);
            queue.Add(text);
            using RenderCommandListBuilder2D builder = new RenderCommandListBuilder2D();
            RenderCommandList2D commands = builder.Build(queue);
            int passes = effects ? 6 : 1;
            Assert.Equal(2 * passes, commands.Count);
            AssertBothPaths(text, commands);
            float4 first = commands.GetGlyphQuadBounds(commands.GetGlyphQuadPayloadIndex(passes - 1));
            float4 second = commands.GetGlyphQuadBounds(commands.GetGlyphQuadPayloadIndex(2 * passes - 1));
            Assert.Equal(10f + bearing * 2f, first.X);
            Assert.Equal(20f, second.X);
            Assert.Equal(8f, first.Z);
            Assert.Equal(24f, first.Y);
            Assert.Equal(22f, second.Y);
            Assert.Equal(18f, font.MeasureString("Be").X * 2f);
        }

        /// <summary>Right alignment uses the same bearing-adjusted right extent in both emitters, including a negative overhang at the pen origin.</summary>
        /// <param name="bearing">Positive, negative or zero crop bearing.</param>
        [Theory]
        [InlineData(-3f)]
        [InlineData(0f)]
        [InlineData(2f)]
        public void RightAlignmentKeepsTheLastVisibleEdgeAtTheBoxEdge(float bearing) {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            using FontAsset font = CreateFont(renderer, bearing);
            TextComponent text = CreateText(core, font, "B", TextAlignment.Right, 2f, false);
            using RenderList2D queue = new RenderList2D(1);
            queue.Add(text);
            using RenderCommandListBuilder2D builder = new RenderCommandListBuilder2D();
            RenderCommandList2D commands = builder.Build(queue);
            AssertBothPaths(text, commands);
            float4 glyph = commands.GetGlyphQuadBounds(commands.GetGlyphQuadPayloadIndex(0));
            Assert.Equal(50f, glyph.X + glyph.Z);
            Assert.Equal(Math.Max(0d, (bearing + 4d) * 2d), TextLayoutAlignmentUtils.MeasureVisibleLineWidth("B", font, 2d, 16d));
        }

        /// <summary>Multiline geometry applies bearings independently while spaces, pen advances, line heights and wrapping remain advance-based.</summary>
        [Fact]
        public void MultilineSpacesAndWrapKeepTheirLogicalMetrics() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            using FontAsset font = CreateFont(renderer, 0f);
            float2 before = font.MeasureString("B e\nBe");
            string wrapped = TextLayoutUtils.WrapText("Be Be Be", font, 10);
            FontChar glyph = font.Characters['B'];
            glyph.BearingX = 2f;
            font.Characters['B'] = glyph;
            Assert.Equal(before.X, font.MeasureString("B e\nBe").X);
            Assert.Equal(before.Y, font.MeasureString("B e\nBe").Y);
            Assert.Equal(wrapped, TextLayoutUtils.WrapText("Be Be Be", font, 10));
            TextComponent text = CreateText(core, font, "B e\nBe", TextAlignment.Left, 1f, false);
            using RenderList2D queue = new RenderList2D(1);
            queue.Add(text);
            using RenderCommandListBuilder2D builder = new RenderCommandListBuilder2D();
            RenderCommandList2D commands = builder.Build(queue);
            AssertBothPaths(text, commands);
            Assert.Equal(new[] { 12d, 18d, 12d, 15d }, Enumerable.Range(0, 4).Select(index => commands.GetGlyphQuadBounds(commands.GetGlyphQuadPayloadIndex(index)).X.ToDouble()));
            Assert.Equal(new[] { 22d, 21d, 32d, 31d }, Enumerable.Range(0, 4).Select(index => commands.GetGlyphQuadBounds(commands.GetGlyphQuadPayloadIndex(index)).Y.ToDouble()));
            Assert.Equal(12f, before.X);
            Assert.Equal(20f, before.Y);
        }

        /// <summary>A bearing mutation outside TextComponent setters invalidates warmed exact font snapshots and updates the same retained command buffers.</summary>
        [Fact]
        public void WarmedTextReuseRebuildsWhenBearingChanges() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            using FontAsset font = CreateFont(renderer, 0f);
            TextComponent text = CreateText(core, font, "Be", TextAlignment.Left, 1f, false);
            using RenderList2D queue = new RenderList2D(1);
            queue.Add(text);
            using RenderCommandListBuilder2D builder = new RenderCommandListBuilder2D();
            builder.SetTextCommandReuseMemoryBudgetBytes(8192);
            CommandBuilderProfileTestSink sink = new CommandBuilderProfileTestSink();
            builder.SetCpuProfileSink(sink);
            RenderCommandList2D commands = builder.Build(queue);
            int textVersion = text.TextRenderStateVersion;
            Assert.Equal(10f, commands.GetGlyphQuadBounds(0).X);
            sink.ClearSamples();
            builder.Build(queue);
            Assert.DoesNotContain(RuntimeCpuProfileStage.CommandListTextLayout, sink.Stages);
            FontChar glyph = font.Characters['B'];
            glyph.BearingX = 2f;
            font.Characters['B'] = glyph;
            sink.ClearSamples();
            Assert.Same(commands, builder.Build(queue));
            Assert.Single(sink.Stages, stage => stage == RuntimeCpuProfileStage.CommandListTextLayout);
            Assert.Equal(12f, commands.GetGlyphQuadBounds(0).X);
            Assert.Equal(textVersion, text.TextRenderStateVersion);
            AssertBothPaths(text, commands);
            sink.ClearSamples();
            builder.Build(queue);
            Assert.DoesNotContain(RuntimeCpuProfileStage.CommandListTextLayout, sink.Stages);
            Assert.Equal(2, builder.CachedGlyphCount);
        }

        /// <summary>Creates a real headless core whose initialized entities expose resolved world positions.</summary>
        /// <param name="renderer">Test texture renderer.</param>
        /// <returns>The caller-owned core.</returns>
        static Core CreateCore(Batch2DTestRenderManager renderer) {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory) });
            core.Initialize(null, renderer, null, new PlatformInfo("test", "test"));
            return core;
        }
        /// <summary>Creates deterministic glyph data with independent crop width, pen advance and horizontal origin.</summary>
        /// <param name="renderer">Atlas texture factory.</param>
        /// <param name="bearing">Horizontal B crop origin.</param>
        /// <returns>The caller-owned font.</returns>
        static FontAsset CreateFont(Batch2DTestRenderManager renderer, float bearing) => new FontAsset(new FontInfo("bearing", 10, 3f), renderer.CreateTexture(16, 16),
            new Dictionary<char, FontChar> {
                ['B'] = new FontChar(new float4(0f, 0f, 0.25f, 0.5f), 2f, 5f, bearing, 0f),
                ['e'] = new FontChar(new float4(0.25f, 0f, 0.25f, 0.5f), 1f, 4f, 0f, 0f)
            }, 10f, 16, 16);
        /// <summary>Attaches real text at a fixed origin without enabling native policies or altering the layout box.</summary>
        /// <param name="core">Owning core.</param>
        /// <param name="font">Borrowed glyph atlas.</param>
        /// <param name="content">Raw input string.</param>
        /// <param name="alignment">Requested logical alignment.</param>
        /// <param name="scale">Existing glyph scale used to verify bearing scaling.</param>
        /// <param name="effects">Whether to add shadow and outline copies.</param>
        /// <returns>The attached text component.</returns>
        static TextComponent CreateText(Core core, FontAsset font, string content, TextAlignment alignment, float scale, bool effects) {
            Entity entity = new Entity(core) { LocalPosition = new float3(10f, 20f, 0f) };
            entity.InitComponents(); entity.InitChildren();
            TextComponent text = new TextComponent { Font = font, Text = content, FontScale = scale, Size = new int2(40, 100), Alignment = alignment,
                OutlineScale = effects ? 1f : 0f, ShadowOffset = effects ? new float2(1f, 2f) : new float2() };
            entity.AddComponent(text);
            return text;
        }
        /// <summary>Compares flattened command bounds and atlas coordinates with the independent maintained batch emitter for every effect and main glyph.</summary>
        /// <param name="text">Current real text component.</param>
        /// <param name="commands">Completed command-builder output.</param>
        static void AssertBothPaths(TextComponent text, RenderCommandList2D commands) {
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 64);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 200f, 200f));
            Batch2DTextEmitter.EmitText(writer, text, new Batch2DRun(Batch2DVariant.Textured, text.Font.Texture, 0, 0, 200, 200));
            writer.Flush();
            Batch2DVertex[] vertices = sink.Vertices.SelectMany(chunk => chunk).ToArray();
            Assert.Equal(commands.Count * 4, vertices.Length);
            for (int index = 0; index < commands.Count; index++) {
                int payload = commands.GetGlyphQuadPayloadIndex(index);
                float4 bounds = commands.GetGlyphQuadBounds(payload);
                float4 source = commands.GetGlyphQuadSourceRect(payload);
                Assert.Equal(bounds.X, vertices[index * 4].Position.X);
                Assert.Equal(bounds.Y, vertices[index * 4].Position.Y);
                Assert.Equal((double)(bounds.X + bounds.Z),(double)(vertices[index * 4 + 2].Position.X.ToDouble()));
                Assert.Equal((double)(bounds.Y + bounds.W),(double)(vertices[index * 4 + 2].Position.Y.ToDouble()));
                Assert.Equal(source.X, vertices[index * 4].TexLocal.X);
                Assert.Equal(source.Y, vertices[index * 4].TexLocal.Y);
            }
        }
    }
}
