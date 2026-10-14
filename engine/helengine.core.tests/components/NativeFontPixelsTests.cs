using helengine;
using helengine.core.tests.managers.rendering;

namespace helengine.core.tests.components {
    /// <summary>Verifies native glyph pixels remain independent of canvas and entity scaling.</summary>
    public sealed class NativeFontPixelsTests {
        /// <summary>Opting in normalizes the current scale and rejects every subsequent nonunit value, while legacy text remains scalable.</summary>
        [Fact]
        public void PolicyRequiresUnitScaleAndPreservesLegacyDefault() {
            TextComponent text = new TextComponent { FontScale = 3f };
            Assert.Equal(TextNativePixelMode.Disabled, text.NativeFontPixels);
            int version = text.TextRenderStateVersion;
            text.NativeFontPixels = TextNativePixelMode.RoundNearest;
            Assert.Equal(1f, text.FontScale);
            Assert.True(text.TextRenderStateVersion > version);
            Assert.Throws<ArgumentOutOfRangeException>(() => text.FontScale = 2f);
            Assert.Throws<ArgumentOutOfRangeException>(() => text.FontScale = 0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => text.FontScale = float.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => text.FontScale = float.PositiveInfinity);
            text.FontScale = 1f;
            text.NativeFontPixels = TextNativePixelMode.Disabled;
            text.FontScale = 2f;
            Assert.Equal(2f, text.FontScale);
        }

        /// <summary>The shared persistence member classifier includes the one-byte policy and applies its persisted mode through the validating setter.</summary>
        [Fact]
        public void PersistenceIncludesNativePixelPolicy() {
            System.Reflection.PropertyInfo member = typeof(TextComponent).GetProperty(nameof(TextComponent.NativeFontPixels));
            Assert.True(ScenePersistenceValueWalker.IsSerializableMember(member));
            Assert.Equal(typeof(TextNativePixelMode), ScenePersistenceValueWalker.GetMemberValueType(member));
            Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(TextNativePixelMode)));
            Assert.True(ScenePersistenceMemberOrdering.IsAppended(member));
            System.Reflection.MemberInfo[] members = ScenePersistenceMemberOrdering.OrderMembers(
                typeof(TextComponent).GetMembers(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                    .Where(ScenePersistenceValueWalker.IsSerializableMember));
            Assert.Equal(nameof(TextComponent.NativeFontPixels), members[^1].Name);
            TextComponent restored = new TextComponent { FontScale = 3f };
            ScenePersistenceValueWalker.SetMemberValue(member, restored, TextNativePixelMode.RoundUp);
            Assert.Equal(TextNativePixelMode.RoundUp, restored.NativeFontPixels);
            Assert.Equal(1f, restored.FontScale);
        }

        /// <summary>Both canvas snapshots resize geometry while preserving native font pixels and restoring authored geometry on later applications.</summary>
        /// <param name="referenceCanvas">Whether the legacy reference canvas rather than viewport snapshot is exercised.</param>
        /// <param name="nativePixels">Whether the text opts into native pixels or retains proportional scaling.</param>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void CanvasSnapshotsKeepNativeGlyphScale(bool referenceCanvas, bool nativePixels) {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            Entity entity = new Entity(core);
            entity.InitComponents();
            TextComponent text = new TextComponent { FontScale = 2f, Size = new int2(400, 100), NativeFontPixels = nativePixels ? TextNativePixelMode.RoundNearest : TextNativePixelMode.Disabled };
            entity.AddComponent(text);
            AnchorSpace reduced = new AnchorSpace(new int2(320, 360), new float2());
            ViewportLayoutSnapshot viewport = new ViewportLayoutSnapshot(entity, false);
            ReferenceCanvasFitSnapshot canvas = new ReferenceCanvasFitSnapshot(entity, false);
            if (referenceCanvas) {
                canvas.Apply(reduced, new float2(), 1280, 720, RenderManager3D.SquarePixelAspect);
            } else {
                viewport.Apply(reduced, new float2(), 1280, 720, false, RenderManager3D.SquarePixelAspect);
            }
            Assert.Equal(nativePixels ? 1f : 0.5f, text.FontScale);
            Assert.Equal(new int2(100, 50), text.Size);
            if (referenceCanvas) {
                canvas.Apply(new AnchorSpace(new int2(1280, 720), new float2()), new float2(), 1280, 720, RenderManager3D.SquarePixelAspect);
            } else {
                viewport.Apply(new AnchorSpace(new int2(1280, 720), new float2()), new float2(), 1280, 720, false, RenderManager3D.SquarePixelAspect);
            }
            Assert.Equal(nativePixels ? 1f : 2f, text.FontScale);
            Assert.Equal(new int2(400, 100), text.Size);
        }

        /// <summary>The actual text batch emitter uses atlas pixels at unit font scale even when its entity has anisotropic geometry scaling.</summary>
        [Fact]
        public void SharedGlyphEmitterDrawsAtlasPixelsWithoutEntityScale() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            Entity parent = new Entity(core) { Position = new float3(10f, 20f, 0f), Scale = new float3(0.25f, 0.5f, 1f) };
            parent.InitComponents();
            RuntimeTexture atlas = renderer.CreateTexture(16, 16);
            using FontAsset font = new FontAsset(new FontInfo("native pixels", 10, 3f), atlas,
                new Dictionary<char, FontChar> { ['A'] = new FontChar(new float4(0f, 0f, 0.25f, 0.5f), 2f, 5f, 0f, 0f) }, 10f, 16, 16);
            TextComponent text = new TextComponent { Font = font, Text = "AA", NativeFontPixels = TextNativePixelMode.RoundNearest, Size = new int2(100, 20) };
            parent.AddComponent(text);
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 8);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 100f, 100f));
            new Batch2DDrawableEmitter(writer, renderer).EmitText(text, new Batch2DRun(Batch2DVariant.Textured, atlas, 0, 0, 100, 100));
            writer.Flush();
            Assert.Equal(8, sink.Vertices[0].Length);
            Assert.Equal(4f, sink.Vertices[0][1].Position.X - sink.Vertices[0][0].Position.X);
            Assert.Equal(8f, sink.Vertices[0][2].Position.Y - sink.Vertices[0][1].Position.Y);
            Assert.Equal(5f, sink.Vertices[0][4].Position.X - sink.Vertices[0][0].Position.X);
        }

        /// <summary>Creates the real headless text runtime with a test renderer that owns its atlas textures.</summary>
        /// <param name="renderer">Renderer installed on the core.</param>
        /// <returns>An initialized core for the caller to dispose.</returns>
        static Core CreateCore(Batch2DTestRenderManager renderer) {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory) });
            core.Initialize(null, renderer, null, new PlatformInfo("test", "test-version"));
            return core;
        }
    }
}
