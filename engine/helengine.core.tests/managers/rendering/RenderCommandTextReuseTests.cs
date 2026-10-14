using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>Exercises bounded reuse against freshly rebuilt real command payloads, mutable font metrics and live clipping across camera passes.</summary>
    public sealed class RenderCommandTextReuseTests {
        /// <summary>Mixed queues retain static glyphs and overwrite moving text without float drift; a real nonglyph loading pass preserves the same buffers.</summary>
        [Fact]
        public void MixedQueueAndLoadingPassReuseTheSingleGlyphBuffer() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            using FontAsset font = CreateFont(renderer);
            Entity clipHost = CreateEntity(core, new float3());
            clipHost.AddComponent(new ClipRectComponent { Size = new int2(200, 100) });
            Entity fixedEntity = CreateEntity(core, new float3(10f, 10f, 1f));
            clipHost.AddChild(fixedEntity);
            TextComponent fixedText = CreateText(fixedEntity, font, "A A\nAA", true);
            Entity movingEntity = CreateEntity(core, new float3(10f, 50f, 2f));
            clipHost.AddChild(movingEntity);
            TextComponent movingText = CreateText(movingEntity, font, "AA", false);
            Entity spriteEntity = CreateEntity(core, new float3());
            SpriteComponent sprite = new SpriteComponent { Texture = renderer.CreateTexture(16, 16), Size = new int2(16, 16) };
            spriteEntity.AddComponent(sprite);
            using RenderList2D menu = new RenderList2D(3);
            menu.Add(sprite); menu.Add(fixedText); menu.Add(movingText);
            using RenderList2D loading = new RenderList2D(3);
            for (int index = 0; index < 3; index++) {
                Entity shapeEntity = CreateEntity(core, new float3(0f, index * 5f, 0f));
                RoundedRectComponent rect = new RoundedRectComponent { Size = new int2(100, 4), FillColor = new byte4(0, 0, 0, 0) };
                shapeEntity.AddComponent(rect);
                loading.Add(rect);
            }
            using RenderCommandListBuilder2D builder = CreateReuseBuilder();
            CommandBuilderProfileTestSink sink = new CommandBuilderProfileTestSink();
            builder.SetCpuProfileSink(sink);
            RenderCommandList2D first = builder.Build(menu);
            int glyphCount = first.GlyphCount;
            Assert.True(glyphCount > 2);
            AssertMatchesFresh(menu, first);
            sink.ClearSamples();
            Assert.Same(first, builder.Build(loading));
            Assert.Equal(3, first.Count);
            Assert.Equal(glyphCount, first.GlyphCount);
            Assert.Equal(glyphCount, builder.CachedGlyphCount);
            for (int frame = 0; frame < 30; frame++) {
                movingEntity.LocalPosition = new float3(10f + frame * 0.37f, 50f + frame * 0.11f, 2f);
                spriteEntity.LocalPosition = new float3(frame * 0.13f, 0f, 0f);
                sink.ClearSamples();
                Assert.Same(first, builder.Build(menu));
                Assert.Equal(glyphCount, first.GlyphCount);
                AssertMatchesFresh(menu, first);
                Assert.InRange(sink.Stages.Count(stage => stage == RuntimeCpuProfileStage.CommandListTextLayout), 0, 1);
                Assert.Equal(2, sink.Stages.Count(stage => stage == RuntimeCpuProfileStage.CommandListGlyphEmission));
                builder.Build(loading);
            }
            Assert.InRange(builder.TextCommandReuseMetadataCapacityBytes, 1, 8192);
            Assert.InRange(builder.TextCommandReuseTextCapacity, 1, 16);
            Assert.InRange(builder.TextCommandReuseMetricCapacity, 1, 128);
            builder.Dispose();
            Assert.False(font.IsDisposed);
            Assert.Equal(0, sink.DisposeCalls);
        }

        /// <summary>Glyph replacement, missing glyph insertion, space metrics and atlas mutations each invalidate exact snapshots without text setter calls.</summary>
        [Fact]
        public void MutableFontValuesInvalidateReuseWithoutHashes() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            using FontAsset font = CreateFont(renderer);
            Entity entity = CreateEntity(core, new float3());
            TextComponent text = CreateText(entity, font, "A ?A", true);
            using RenderList2D queue = new RenderList2D(1);
            queue.Add(text);
            using RenderCommandListBuilder2D builder = CreateReuseBuilder();
            CommandBuilderProfileTestSink sink = new CommandBuilderProfileTestSink();
            builder.SetCpuProfileSink(sink);
            builder.Build(queue);
            int originalVersion = text.TextRenderStateVersion;
            FontChar glyph = font.Characters['A'];
            glyph.AdvanceWidth = 7f;
            font.Characters['A'] = glyph;
            AssertRebuilt(queue, builder, sink);
            font.Characters['?'] = new FontChar(new float4(0.25f, 0f, 0.25f, 0.5f), 3f, 6f, 1f, 2f);
            AssertRebuilt(queue, builder, sink);
            font.FontInfo.SpaceWidth = 8f;
            AssertRebuilt(queue, builder, sink);
            font.Texture.Width = 32;
            AssertRebuilt(queue, builder, sink);
            RuntimeTexture replacement = renderer.CreateTexture(16, 16);
            font.AttachCookedRuntimeTexture(replacement);
            AssertRebuilt(queue, builder, sink);
            glyph.SourceRect.X = 0.5f;
            glyph.OffsetY = 4f;
            glyph.BearingX = 2f;
            glyph.BearingY = 3f;
            font.Characters['A'] = glyph;
            AssertRebuilt(queue, builder, sink);
            Assert.Equal(originalVersion, text.TextRenderStateVersion);
        }

        /// <summary>Enabled state, full clipping, live clip geometry and depth reordering cannot reuse stale visible ranges or clip transitions.</summary>
        [Fact]
        public void VisibilityClipsAndDepthRemainEquivalentToFullRebuild() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            using FontAsset font = CreateFont(renderer);
            Entity clipHost = CreateEntity(core, new float3());
            ClipRectComponent clip = new ClipRectComponent { Size = new int2(100, 100) };
            clipHost.AddComponent(clip);
            Entity first = CreateEntity(core, new float3(10f, 10f, 1f));
            Entity second = CreateEntity(core, new float3(20f, 20f, 2f));
            clipHost.AddChild(first); clipHost.AddChild(second);
            TextComponent firstText = CreateText(first, font, "AA", false);
            TextComponent secondText = CreateText(second, font, "A", false);
            using RenderList2D queue = new RenderList2D(2);
            queue.Add(firstText); queue.Add(secondText);
            using RenderCommandListBuilder2D builder = CreateReuseBuilder();
            builder.Build(queue);
            first.Enabled = false;
            AssertMatchesFresh(queue, builder.Build(queue));
            first.Enabled = true;
            AssertMatchesFresh(queue, builder.Build(queue));
            clip.Size = new int2(2, 2);
            AssertMatchesFresh(queue, builder.Build(queue));
            clip.Size = new int2(100, 100);
            AssertMatchesFresh(queue, builder.Build(queue));
            clip.Size = new int2(80, 80);
            AssertMatchesFresh(queue, builder.Build(queue));
            first.LocalPosition = new float3(10f, 10f, 3f);
            AssertMatchesFresh(queue, builder.Build(queue));
            firstText.Color = new byte4(1, 2, 3, 4);
            AssertMatchesFresh(queue, builder.Build(queue));
            firstText.Text = "AAAA";
            AssertMatchesFresh(queue, builder.Build(queue));
        }

        /// <summary>Capacity limits and a second text queue rebuild normally without adding another cache or changing payloads.</summary>
        [Fact]
        public void BudgetAndGlyphLimitsFallBackToOrdinaryRendering() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            using FontAsset font = CreateFont(renderer);
            Entity entity = CreateEntity(core, new float3());
            TextComponent text = CreateText(entity, font, new string('A', 1025), false);
            using RenderList2D queue = new RenderList2D(1);
            queue.Add(text);
            using RenderCommandListBuilder2D builder = CreateReuseBuilder();
            AssertMatchesFresh(queue, builder.Build(queue));
            Assert.Equal(0, builder.CachedGlyphCount);
            builder.SetTextCommandReuseMemoryBudgetBytes(1);
            Assert.Equal(0, builder.TextCommandReuseMetadataCapacityBytes);
            AssertMatchesFresh(queue, builder.Build(queue));
            Assert.Throws<ArgumentOutOfRangeException>(() => builder.SetTextCommandReuseMemoryBudgetBytes(-1));
            FontChar resized = font.Characters['A'];
            resized.AdvanceWidth = 7f;
            font.Characters['A'] = resized;
            AssertMatchesFresh(queue, builder.Build(queue));
            builder.SetTextCommandReuseMemoryBudgetBytes(8192);
            text.Text = "AA";
            builder.Build(queue);
            Entity otherEntity = CreateEntity(core, new float3(5f, 5f, 0f));
            TextComponent other = CreateText(otherEntity, font, "AAA", false);
            using RenderList2D otherQueue = new RenderList2D(1);
            otherQueue.Add(other);
            AssertMatchesFresh(otherQueue, builder.Build(otherQueue));
            Assert.Equal(3, builder.CachedGlyphCount);
            AssertMatchesFresh(queue, builder.Build(queue));
            Assert.Equal(2, builder.CachedGlyphCount);
            builder.InvalidateTextCommandReuse();
            Assert.Equal(0, builder.CachedGlyphCount);
            AssertMatchesFresh(queue, builder.Build(queue));
            queue.Clear();
            Assert.Equal(0, builder.Build(queue).Count);
            Assert.Equal(0, builder.CachedGlyphCount);
            Assert.False(font.IsDisposed);
        }

        /// <summary>More texts or distinct characters than fixed metadata capacities disable reuse without truncating commands.</summary>
        [Fact]
        public void MetadataOverflowDoesNotTruncateOrGrowCache() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            using FontAsset font = CreateFont(renderer);
            using RenderList2D queue = new RenderList2D(17);
            for (int index = 0; index < 17; index++) {
                queue.Add(CreateText(CreateEntity(core, new float3(0f, index, index)), font, "A", false));
            }
            using RenderCommandListBuilder2D builder = CreateReuseBuilder();
            AssertMatchesFresh(queue, builder.Build(queue));
            Assert.Equal(0, builder.CachedGlyphCount);
            int bytes = builder.TextCommandReuseMetadataCapacityBytes;
            queue.Clear();
            string characters = string.Concat(Enumerable.Range(1, 150).Select(value => (char)value));
            queue.Add(CreateText(CreateEntity(core, new float3()), font, characters, false));
            AssertMatchesFresh(queue, builder.Build(queue));
            Assert.Equal(0, builder.CachedGlyphCount);
            Assert.Equal(bytes, builder.TextCommandReuseMetadataCapacityBytes);
        }

        /// <summary>Builds a real headless core without global profile hooks.</summary>
        /// <param name="renderer">Test texture renderer.</param>
        /// <returns>The caller-owned core.</returns>
        static Core CreateCore(Batch2DTestRenderManager renderer) {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory) });
            core.Initialize(null, renderer, null, new PlatformInfo("test", "test"));
            return core;
        }
        /// <summary>Creates an initialized real entity at the desired depth.</summary>
        /// <param name="core">Owning core.</param>
        /// <param name="position">Current local position.</param>
        /// <returns>The initialized entity.</returns>
        static Entity CreateEntity(Core core, float3 position) {
            Entity entity = new Entity(core) { LocalPosition = position };
            entity.InitComponents(); entity.InitChildren();
            return entity;
        }
        /// <summary>Attaches text whose metrics, wrapping and effects use the ordinary engine path.</summary>
        /// <param name="entity">Live owner.</param>
        /// <param name="font">Borrowed atlas and metrics.</param>
        /// <param name="content">Raw text input.</param>
        /// <param name="effects">Whether to enable wrapping, outline and shadow.</param>
        /// <returns>The attached text component.</returns>
        static TextComponent CreateText(Entity entity, FontAsset font, string content, bool effects) {
            TextComponent text = new TextComponent { Font = font, Text = content, Size = new int2(60, 60), WrapText = effects,
                Alignment = TextAlignment.Center, OutlineScale = effects ? 1f : 0f, ShadowOffset = effects ? new float2(2f, 3f) : new float2() };
            entity.AddComponent(text);
            return text;
        }
        /// <summary>Creates a deterministic real font with one present and arbitrarily many initially absent characters.</summary>
        /// <param name="renderer">Atlas texture factory.</param>
        /// <returns>The caller-owned font.</returns>
        static FontAsset CreateFont(Batch2DTestRenderManager renderer) => new FontAsset(new FontInfo("reuse", 10, 3f), renderer.CreateTexture(16, 16),
            new Dictionary<char, FontChar> { ['A'] = new FontChar(new float4(0f, 0f, 0.25f, 0.5f), 2f, 5f, 0f, 0f) }, 10f, 16, 16);
        /// <summary>Creates one builder with the explicit eight-kibibyte metadata allowance used by the first PS2 integration.</summary>
        /// <returns>The caller-owned configured builder.</returns>
        static RenderCommandListBuilder2D CreateReuseBuilder() {
            RenderCommandListBuilder2D builder = new RenderCommandListBuilder2D();
            builder.SetTextCommandReuseMemoryBudgetBytes(8192);
            return builder;
        }
        /// <summary>Requires mutable font changes to rerun layout and verifies output against a fresh uncached build.</summary>
        /// <param name="queue">Current real queue.</param>
        /// <param name="builder">Configured reusable builder.</param>
        /// <param name="sink">Borrowed deterministic profile sink.</param>
        static void AssertRebuilt(RenderList2D queue, RenderCommandListBuilder2D builder, CommandBuilderProfileTestSink sink) {
            sink.ClearSamples();
            AssertMatchesFresh(queue, builder.Build(queue));
            Assert.Single(sink.Stages, stage => stage == RuntimeCpuProfileStage.CommandListTextLayout);
        }
        /// <summary>Compares every backend-visible numeric payload and texture identity with the ordinary full build.</summary>
        /// <param name="queue">Current live queue.</param>
        /// <param name="actual">Actual bounded-reuse output.</param>
        static void AssertMatchesFresh(RenderList2D queue, RenderCommandList2D actual) {
            using RenderCommandListBuilder2D fresh = new RenderCommandListBuilder2D();
            Assert.Equal(Capture(fresh.Build(queue)), Capture(actual));
        }
        /// <summary>Serializes command values only for testing, without relying on payload indices or cache implementation details.</summary>
        /// <param name="commands">Completed real command list.</param>
        /// <returns>Exact encoded values and texture identities.</returns>
        static byte[] Capture(RenderCommandList2D commands) {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(commands.Count);
            for (int index = 0; index < commands.Count; index++) {
                RenderCommand2DType type = commands.GetCommandType(index);
                writer.Write((byte)type);
                if (type == RenderCommand2DType.ClipPush) {
                    WriteRect(writer, commands.GetClipPushRect(commands.GetClipPushPayloadIndex(index)));
                } else if (type == RenderCommand2DType.GlyphQuad) {
                    int payload = commands.GetGlyphQuadPayloadIndex(index);
                    writer.Write(commands.GetGlyphQuadTexture(payload).GetHashCode());
                    WriteRect(writer, commands.GetGlyphQuadBounds(payload)); WriteRect(writer, commands.GetGlyphQuadSourceRect(payload));
                    WriteColor(writer, commands.GetGlyphQuadColor(payload));
                } else if (type == RenderCommand2DType.TexturedQuad) {
                    int payload = commands.GetTexturedQuadPayloadIndex(index);
                    writer.Write(commands.GetTexturedQuadTexture(payload).GetHashCode());
                    WriteRect(writer, commands.GetTexturedQuadBounds(payload)); WriteRect(writer, commands.GetTexturedQuadSourceRect(payload));
                    WriteColor(writer, commands.GetTexturedQuadColor(payload)); writer.Write(commands.GetTexturedQuadRotation(payload));
                } else if (type == RenderCommand2DType.RoundedRect) {
                    int payload = commands.GetRoundedRectPayloadIndex(index);
                    WriteRect(writer, commands.GetRoundedRectBounds(payload)); writer.Write(commands.GetRoundedRectRadius(payload));
                    writer.Write(commands.GetRoundedRectBorderThickness(payload)); writer.Write((byte)commands.GetRoundedRectCorners(payload));
                    WriteColor(writer, commands.GetRoundedRectFillColor(payload)); WriteColor(writer, commands.GetRoundedRectBorderColor(payload));
                }
            }
            return stream.ToArray();
        }
        /// <summary>Writes exact rectangle float values into the test comparison stream.</summary>
        /// <param name="writer">Comparison stream.</param>
        /// <param name="value">Actual rectangle.</param>
        static void WriteRect(BinaryWriter writer, float4 value) { writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Z); writer.Write(value.W); }
        /// <summary>Writes exact color bytes into the test comparison stream.</summary>
        /// <param name="writer">Comparison stream.</param>
        /// <param name="value">Actual color.</param>
        static void WriteColor(BinaryWriter writer, byte4 value) { writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Z); writer.Write(value.W); }
    }
}
