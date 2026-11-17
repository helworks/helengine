using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>Checks system typography opt-in, borrowed results, clipping, resolved scale, tint, position and unchanged bitmap reuse.</summary>
    public sealed class SystemTextRenderTests {
        /// <summary>Measurement and rendering submit identical optional Size windows while older six-argument calls remain the default.</summary>
        [Fact]
        public void ClippedMeasurementAndRenderingUseTheSameResolvedWindow() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            SystemTextTestBackend backend = new SystemTextTestBackend(new SystemTextTestLayout(renderer.CreateTexture(40, 24)));
            renderer.SystemTextBackend = backend;
            Entity entity = CreateEntity(core);
            TextComponent text = new TextComponent { Text = "window", SystemTextStyle = new SystemTextStyle(SystemTextRole.Body), Size = new int2(70, 25) };
            entity.AddComponent(text);
            Assert.False(text.ClipSystemTextToSize);
            text.MeasureSystemText();
            Assert.False(backend.LastWasClipped);
            int version = text.TextRenderStateVersion;
            text.ClipSystemTextToSize = true;
            Assert.NotEqual(version, text.TextRenderStateVersion);
            text.MeasureSystemText();
            Assert.True(backend.LastWasClipped);
            Assert.Equal(new float4(0, 0, 70, 25), backend.LastWindow);
            ViewportLayoutMetrics metrics = new ViewportLayoutMetrics { ScaleX = 2f, ScaleY = 2f, GlyphScale = 2f };
            ViewportLayoutApplier.ApplyComponent(text, metrics);
            text.MeasureSystemText();
            Assert.Equal(new float4(0, 0, 140, 50), backend.LastWindow);
            Assert.Equal(2f, backend.LastScale);
            using RenderList2D queue = new RenderList2D(1);
            queue.Add(text);
            using RenderCommandListBuilder2D builder = new RenderCommandListBuilder2D { SystemTextBackend = backend };
            builder.Build(queue);
            Assert.True(backend.LastWasClipped);
            Assert.Equal(new float4(0, 0, 140, 50), backend.LastWindow);
            text.Size = new int2(30, 0);
            text.MeasureSystemText();
            Assert.Equal(new float4(0, 0, 30, 0), backend.LastWindow);
            text.ClipSystemTextToSize = false;
            builder.Build(queue);
            Assert.False(backend.LastWasClipped);
        }
        /// <summary>System rasters use ordinary clips/quads and consume metrics each build instead of retaining invalidatable borrowed layouts.</summary>
        [Fact]
        public void SystemTextUsesExistingClippedTexturePathAndFreshBorrowedMetrics() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            RuntimeTexture texture = renderer.CreateTexture(40, 24);
            SystemTextTestBackend backend = new SystemTextTestBackend(new SystemTextTestLayout(texture));
            renderer.SystemTextBackend = backend;
            Entity clip = CreateEntity(core);
            clip.AddComponent(new ClipRectComponent { Size = new int2(100, 100) });
            Entity entity = CreateEntity(core);
            entity.LocalPosition = new float3(10f, 10f, 0f);
            clip.AddChild(entity);
            TextComponent text = new TextComponent { Text = "😀é", SystemTextStyle = new SystemTextStyle(SystemTextRole.Body), FontScale = 1f, Size = new int2(45, 20), WrapText = true, Color = new byte4(20, 40, 60, 128) };
            ViewportLayoutMetrics metrics = new ViewportLayoutMetrics { ScaleX = 2f, ScaleY = 2f, GlyphScale = 2f };
            ViewportLayoutApplier.ApplyComponent(text, metrics);
            entity.AddComponent(text);
            using RenderList2D queue = new RenderList2D(1);
            queue.Add(text);
            using RenderCommandListBuilder2D builder = new RenderCommandListBuilder2D { SystemTextBackend = backend };
            builder.SetTextCommandReuseMemoryBudgetBytes(8192);
            RenderCommandList2D commands = builder.Build(queue);
            Assert.Equal(3, commands.Count);
            Assert.Equal(RenderCommand2DType.ClipPush, commands.GetCommandType(0));
            Assert.Equal(RenderCommand2DType.TexturedQuad, commands.GetCommandType(1));
            Assert.Equal(RenderCommand2DType.ClipPop, commands.GetCommandType(2));
            int payload = commands.GetTexturedQuadPayloadIndex(1);
            Assert.Same(texture, commands.GetTexturedQuadTexture(payload));
            Assert.Equal(new float4(8f, 7f, 40f, 24f), commands.GetTexturedQuadBounds(payload));
            Assert.Equal(text.Color, commands.GetTexturedQuadColor(payload));
            Assert.Equal(2f, backend.LastScale);
            Assert.Equal(90, backend.LastWidth);
            entity.LocalPosition = new float3(20f, 10f, 0f);
            text.Color = new byte4(100, 120, 140, 90);
            commands = builder.Build(queue);
            payload = commands.GetTexturedQuadPayloadIndex(1);
            Assert.Equal(new float4(18f, 7f, 40f, 24f), commands.GetTexturedQuadBounds(payload));
            Assert.Equal(text.Color, commands.GetTexturedQuadColor(payload));
            Assert.Equal(2, backend.Calls);
            Assert.Equal(0, builder.CachedGlyphCount);
            entity.LocalPosition = new float3(-10f, 10f, 0f);
            text.Size = new int2(1, 1);
            commands = builder.Build(queue);
            Assert.Equal(3, commands.Count);
            Assert.Equal(3, backend.Calls);
        }
        /// <summary>Opt-in text fails clearly on unsupported hosts while legacy text components retain null system styles.</summary>
        [Fact]
        public void UnsupportedHostsFailExplicitlyAndStylesOwnValidatedSnapshots() {
            TextComponent text = new TextComponent();
            Assert.Null(text.SystemTextStyle);
            SystemTextStyle style = new SystemTextStyle(SystemTextRole.Title, SystemTextWeight.Semibold, true, 24f);
            text.SystemTextStyle = style;
            Assert.NotSame(style, text.SystemTextStyle);
            Assert.Equal(24f, text.SystemTextStyle.PointSize);
            Assert.Throws<NotSupportedException>(() => text.MeasureSystemText());
            Assert.Throws<ArgumentOutOfRangeException>(() => new SystemTextStyle(SystemTextRole.Body, pointSize: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SystemTextStyle((SystemTextRole)99));
            text.PersistedSystemTextStyle = null;
            Assert.Null(text.SystemTextStyle);
            text.Dispose();
        }
        /// <summary>Creates a headless real core using the test assembly as its existing read-only content root.</summary>
        static Core CreateCore(Batch2DTestRenderManager renderer) {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory) });
            core.Initialize(null, renderer, null, new PlatformInfo("test", "test"));
            return core;
        }
        /// <summary>Creates an initialized real entity for drawable registration.</summary>
        static Entity CreateEntity(Core core) {
            Entity entity = new Entity(core);
            entity.InitComponents(); entity.InitChildren();
            return entity;
        }
    }
}
