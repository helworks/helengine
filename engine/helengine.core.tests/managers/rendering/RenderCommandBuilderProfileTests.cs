using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>Checks coarse timing leaves real command payloads, clipping and reuse unchanged and never reads the clock inside glyph loops.</summary>
    public sealed class RenderCommandBuilderProfileTests {
        /// <summary>A mixed clipped queue produces identical byte-level payloads while every coarse phase receives disjoint deterministic samples.</summary>
        [Fact]
        public void MixedQueueKeepsCommandPayloadsAndReportsEveryPhase() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            using FontAsset font = CreateFont(renderer);
            Entity clipHost = CreateEntity(core, new float3());
            clipHost.AddComponent(new ClipRectComponent { Size = new int2(40, 100) });
            Entity spriteEntity = CreateEntity(core, new float3(0f, 0f, 0f));
            clipHost.AddChild(spriteEntity);
            SpriteComponent sprite = new SpriteComponent { Texture = renderer.CreateTexture(16, 16), Size = new int2(16, 16), Color = new byte4(1, 2, 3, 4) };
            spriteEntity.AddComponent(sprite);
            Entity textEntity = CreateEntity(core, new float3(5f, 20f, 1f));
            clipHost.AddChild(textEntity);
            TextComponent text = new TextComponent {
                Font = font, Text = "A A\nA?A", WrapText = true, Size = new int2(16, 60),
                Alignment = TextAlignment.Center, OutlineScale = 1f, ShadowOffset = new float2(2f, 3f)
            };
            textEntity.AddComponent(text);
            Entity shapeEntity = CreateEntity(core, new float3(50f, 60f, 2f));
            RoundedRectComponent shape = new RoundedRectComponent { Size = new int2(30, 20), Radius = 2f, BorderThickness = 1f };
            shapeEntity.AddComponent(shape);
            Entity disabledEntity = CreateEntity(core, new float3(60f, 0f, 3f));
            disabledEntity.Enabled = false;
            SpriteComponent disabled = new SpriteComponent { Texture = sprite.Texture, Size = new int2(16, 16) };
            disabledEntity.AddComponent(disabled);
            Entity clippedEntity = CreateEntity(core, new float3(100f, 0f, 4f));
            clipHost.AddChild(clippedEntity);
            SpriteComponent clipped = new SpriteComponent { Texture = sprite.Texture, Size = new int2(16, 16) };
            clippedEntity.AddComponent(clipped);
            using RenderList2D queue = new RenderList2D(5);
            queue.Add(sprite); queue.Add(text); queue.Add(shape); queue.Add(disabled); queue.Add(clipped);
            using RenderCommandListBuilder2D baseline = new RenderCommandListBuilder2D();
            byte[] expected = CaptureCommands(baseline.Build(queue));
            CommandBuilderProfileTestSink sink = new CommandBuilderProfileTestSink();
            using RenderCommandListBuilder2D measured = new RenderCommandListBuilder2D();
            measured.SetCpuProfileSink(sink);
            RenderCommandList2D commands = measured.Build(queue);
            Assert.Equal(expected, CaptureCommands(commands));
            Assert.Contains(RenderCommand2DType.ClipPush, Enumerable.Range(0, commands.Count).Select(commands.GetCommandType));
            Assert.Contains(RenderCommand2DType.ClipPop, Enumerable.Range(0, commands.Count).Select(commands.GetCommandType));
            Assert.Equal(5, sink.Stages.Count(stage => stage == RuntimeCpuProfileStage.CommandListClipping));
            Assert.Equal(2, sink.Stages.Count(stage => stage == RuntimeCpuProfileStage.CommandListSetup));
            foreach (RuntimeCpuProfileStage stage in new[] { RuntimeCpuProfileStage.CommandListSprites, RuntimeCpuProfileStage.CommandListTextLayout,
                RuntimeCpuProfileStage.CommandListTextEffectsSetup, RuntimeCpuProfileStage.CommandListGlyphEmission,
                RuntimeCpuProfileStage.CommandListRoundedRects, RuntimeCpuProfileStage.CommandListTrailingClips }) {
                Assert.Single(sink.Stages, sample => sample == stage);
            }
            Assert.Equal(26, sink.ClockReads);
            Assert.All(sink.Durations, duration => Assert.Equal(10UL, duration));
            Assert.Equal(0, sink.ClockFaults);
            sink.ClearSamples();
            measured.SetCpuProfileSink(null);
            Assert.Same(commands, measured.Build(queue));
            Assert.Equal(expected, CaptureCommands(commands));
            Assert.Equal(0, sink.ClockReads);
            Assert.Empty(sink.Stages);
            measured.Dispose();
            Assert.Equal(0, sink.DisposeCalls);
        }

        /// <summary>Cache hits sample only setup and keep the same version; changed text resumes all text phases without altering cache invalidation.</summary>
        [Fact]
        public void StaticTextCacheSamplesOnlySetupOnReuse() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            using FontAsset font = CreateFont(renderer);
            Entity entity = CreateEntity(core, new float3());
            TextComponent text = new TextComponent { Font = font, Text = "AA", Size = new int2(100, 20) };
            entity.AddComponent(text);
            using RenderList2D queue = new RenderList2D(1);
            queue.Add(text);
            using RenderCommandListBuilder2D builder = new RenderCommandListBuilder2D();
            CommandBuilderProfileTestSink sink = new CommandBuilderProfileTestSink();
            builder.SetCpuProfileSink(sink);
            RenderCommandList2D first = builder.Build(queue);
            int version = first.Version;
            byte[] expected = CaptureCommands(first);
            Assert.Equal(14, sink.ClockReads);
            sink.ClearSamples();
            Assert.Same(first, builder.Build(queue));
            Assert.Equal(version, first.Version);
            Assert.Equal(expected, CaptureCommands(first));
            Assert.Equal(new[] { RuntimeCpuProfileStage.CommandListSetup }, sink.Stages);
            Assert.Equal(2, sink.ClockReads);
            text.Text = "A";
            sink.ClearSamples();
            Assert.Same(first, builder.Build(queue));
            Assert.NotEqual(version, first.Version);
            Assert.Single(sink.Stages, stage => stage == RuntimeCpuProfileStage.CommandListGlyphEmission);
            Assert.Equal(14, sink.ClockReads);
        }

        /// <summary>Changing glyph and effect counts cannot add clock reads because the whole emission loop is timed once.</summary>
        /// <param name="glyphCount">Number of visible glyphs to emit.</param>
        /// <param name="effects">Whether each glyph also emits outline and shadow commands.</param>
        [Theory]
        [InlineData(1, false)]
        [InlineData(300, false)]
        [InlineData(1, true)]
        [InlineData(300, true)]
        public void ClockReadsDoNotScaleWithGlyphOrEffectCount(int glyphCount, bool effects) {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            using FontAsset font = CreateFont(renderer);
            Entity entity = CreateEntity(core, new float3());
            TextComponent text = new TextComponent {
                Font = font, Text = new string('A', glyphCount), Size = new int2(2000, 20),
                OutlineScale = effects ? 1f : 0f, ShadowOffset = effects ? new float2(1f, 1f) : new float2()
            };
            entity.AddComponent(text);
            using RenderList2D queue = new RenderList2D(1);
            queue.Add(text);
            using RenderCommandListBuilder2D builder = new RenderCommandListBuilder2D();
            CommandBuilderProfileTestSink sink = new CommandBuilderProfileTestSink();
            builder.SetCpuProfileSink(sink);
            RenderCommandList2D commands = builder.Build(queue);
            Assert.Equal(glyphCount * (effects ? 6 : 1), commands.Count);
            Assert.Equal(14, sink.ClockReads);
            Assert.Single(sink.Stages, stage => stage == RuntimeCpuProfileStage.CommandListGlyphEmission);
        }

        /// <summary>Empty text closes its layout segment without emission, and a backwards clock is reported without unsigned duration wraparound.</summary>
        [Fact]
        public void EmptyTextAndClockFaultKeepValidSamplingBoundaries() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            using FontAsset font = CreateFont(renderer);
            Entity entity = CreateEntity(core, new float3());
            TextComponent text = new TextComponent { Font = font, Text = string.Empty };
            entity.AddComponent(text);
            using RenderList2D queue = new RenderList2D(1);
            queue.Add(text);
            using RenderCommandListBuilder2D builder = new RenderCommandListBuilder2D();
            CommandBuilderProfileTestSink sink = new CommandBuilderProfileTestSink { BackwardsSecondRead = true };
            builder.SetCpuProfileSink(sink);
            Assert.Equal(0, builder.Build(queue).Count);
            Assert.Equal(1, sink.ClockFaults);
            Assert.Equal(10, sink.ClockReads);
            Assert.Single(sink.Stages, stage => stage == RuntimeCpuProfileStage.CommandListTextLayout);
            Assert.DoesNotContain(RuntimeCpuProfileStage.CommandListGlyphEmission, sink.Stages);
            Assert.All(sink.Durations, duration => Assert.Equal(10UL, duration));
        }

        /// <summary>Creates a headless core without installing a global CPU sink.</summary>
        /// <param name="renderer">Renderer used solely to create test textures.</param>
        /// <returns>The initialized core owned by the caller.</returns>
        static Core CreateCore(Batch2DTestRenderManager renderer) {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory) });
            core.Initialize(null, renderer, null, new PlatformInfo("test", "test"));
            return core;
        }
        /// <summary>Creates an initialized entity whose real hierarchy and clipping participate in command building.</summary>
        /// <param name="core">Owning headless core.</param>
        /// <param name="position">Initial local position, including queue depth.</param>
        /// <returns>The initialized scene entity.</returns>
        static Entity CreateEntity(Core core, float3 position) {
            Entity entity = new Entity(core) { LocalPosition = position };
            entity.InitComponents(); entity.InitChildren();
            return entity;
        }
        /// <summary>Creates one real font with deterministic atlas metrics.</summary>
        /// <param name="renderer">Renderer supplying the borrowed atlas texture.</param>
        /// <returns>The font owned by the test.</returns>
        static FontAsset CreateFont(Batch2DTestRenderManager renderer) {
            return new FontAsset(new FontInfo("profile", 10, 3f), renderer.CreateTexture(16, 16),
                new Dictionary<char, FontChar> { ['A'] = new FontChar(new float4(0f, 0f, 0.25f, 0.5f), 2f, 5f, 0f, 0f) }, 10f, 16, 16);
        }
        /// <summary>Captures every actual command payload as bytes so optional timing cannot change backend-visible data or texture identity.</summary>
        /// <param name="commands">Completed real command list.</param>
        /// <returns>Deterministically encoded numeric payloads and reference identities.</returns>
        static byte[] CaptureCommands(RenderCommandList2D commands) {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(commands.Count);
            for (int index = 0; index < commands.Count; index++) {
                RenderCommand2DType type = commands.GetCommandType(index);
                writer.Write((byte)type);
                if (type == RenderCommand2DType.ClipPush) {
                    WriteRect(writer, commands.GetClipPushRect(commands.GetClipPushPayloadIndex(index)));
                } else if (type == RenderCommand2DType.TexturedQuad) {
                    int payload = commands.GetTexturedQuadPayloadIndex(index);
                    writer.Write(commands.GetTexturedQuadTexture(payload).GetHashCode());
                    WriteRect(writer, commands.GetTexturedQuadBounds(payload)); WriteRect(writer, commands.GetTexturedQuadSourceRect(payload));
                    WriteColor(writer, commands.GetTexturedQuadColor(payload)); writer.Write(commands.GetTexturedQuadRotation(payload));
                    SpriteQuad2D quad = commands.GetTexturedQuadGeometry(payload);
                    writer.Write(quad.TopLeft.X); writer.Write(quad.TopLeft.Y);
                    writer.Write(quad.TopRight.X); writer.Write(quad.TopRight.Y);
                    writer.Write(quad.BottomRight.X); writer.Write(quad.BottomRight.Y);
                    writer.Write(quad.BottomLeft.X); writer.Write(quad.BottomLeft.Y);
                } else if (type == RenderCommand2DType.GlyphQuad) {
                    int payload = commands.GetGlyphQuadPayloadIndex(index);
                    writer.Write(commands.GetGlyphQuadTexture(payload).GetHashCode());
                    WriteRect(writer, commands.GetGlyphQuadBounds(payload)); WriteRect(writer, commands.GetGlyphQuadSourceRect(payload));
                    WriteColor(writer, commands.GetGlyphQuadColor(payload));
                } else if (type == RenderCommand2DType.RoundedRect) {
                    int payload = commands.GetRoundedRectPayloadIndex(index);
                    WriteRect(writer, commands.GetRoundedRectBounds(payload)); writer.Write(commands.GetRoundedRectRadius(payload));
                    writer.Write(commands.GetRoundedRectBorderThickness(payload)); writer.Write((byte)commands.GetRoundedRectCorners(payload));
                    WriteColor(writer, commands.GetRoundedRectFillColor(payload)); WriteColor(writer, commands.GetRoundedRectBorderColor(payload));
                }
            }
            return stream.ToArray();
        }
        /// <summary>Writes the four exact float bit patterns stored in one real rectangle payload.</summary>
        /// <param name="writer">Payload capture stream.</param>
        /// <param name="rect">Actual backend-visible rectangle.</param>
        static void WriteRect(BinaryWriter writer, float4 rect) { writer.Write(rect.X); writer.Write(rect.Y); writer.Write(rect.Z); writer.Write(rect.W); }
        /// <summary>Writes the four exact tint bytes stored in one real command payload.</summary>
        /// <param name="writer">Payload capture stream.</param>
        /// <param name="color">Actual backend-visible tint.</param>
        static void WriteColor(BinaryWriter writer, byte4 color) { writer.Write(color.X); writer.Write(color.Y); writer.Write(color.Z); writer.Write(color.W); }
    }
}
