using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>
    /// Verifies text layout, glyph expansion, and effect submission order for shared 2D batches.
    /// </summary>
    public sealed class Batch2DTextEmitterTests {
        /// <summary>
        /// Confirms literal font metrics, wrapping, per-line alignment, missing glyphs, and glyph-major effects.
        /// </summary>
        [Fact]
        public void EmitText_WhenLayoutHasWrapAndEffects_ExpandsGlyphsInDx11Order() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            Entity parent = new Entity(core) { Position = new float3(10f, 20f, 3f) };
            RuntimeTexture atlas = renderer.CreateTexture(16, 16);
            FontAsset font = new FontAsset(new FontInfo("fixture", 10, 3f), atlas,
                new Dictionary<char, FontChar> {
                    ['A'] = new FontChar(new float4(0f, 0f, 0.25f, 0.5f), 2f, 5f, 0f, 0f),
                    ['B'] = new FontChar(new float4(0.25f, 0f, 0.25f, 0.5f), 1f, 4f, 0f, 0f)
                }, 10f, 16, 16);
            TextComponent drawable = new TextComponent {
                Font = font,
                Text = "A A\nA?B",
                FontScale = 2f,
                Size = new int2(16, 40),
                WrapText = true,
                Alignment = TextAlignment.Center,
                ShadowOffset = new float2(1f, 2f),
                ShadowColor = new byte4(0, 0, 0, 128),
                OutlineScale = 1f,
                OutlineColor = new byte4(0, 255, 0, 128),
                Color = new byte4(255, 128, 0, 64)
            };
            parent.InitComponents();
            parent.AddComponent(drawable);
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 64);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 100f, 100f));

            RuntimeTexture incomingTexture = renderer.CreateTexture(1, 1);
            new Batch2DDrawableEmitter(writer, renderer).EmitText(drawable,
                new Batch2DRun(Batch2DVariant.Textured, incomingTexture, 0, 0, 100, 100));
            writer.Flush();

            Assert.Equal(96, sink.Vertices.Sum(vertices => vertices.Length));
            Assert.Same(atlas, sink.Runs[0].Texture);
            Assert.Equal(0f, sink.Vertices[0][0].TexLocal.X);
            Assert.Equal(0f, sink.Vertices[0][0].TexLocal.Y);
            Assert.Equal(0.25f, sink.Vertices[0][2].TexLocal.X);
            Assert.Equal(0.5f, sink.Vertices[0][2].TexLocal.Y);
            Assert.Equal(0f, sink.Vertices[0][0].Position.Z);
            Assert.Equal(15f, sink.Vertices[0][0].Position.X, 4);
            Assert.Equal(26f, sink.Vertices[0][0].Position.Y, 4);
            Assert.Equal(13f, sink.Vertices[0][4].Position.X, 4);
            Assert.Equal(24f, sink.Vertices[0][4].Position.Y, 4);
            Assert.Equal(15f, sink.Vertices[0][8].Position.X, 4);
            Assert.Equal(24f, sink.Vertices[0][8].Position.Y, 4);
            Assert.Equal(14f, sink.Vertices[0][12].Position.X, 4);
            Assert.Equal(23f, sink.Vertices[0][12].Position.Y, 4);
            Assert.Equal(14f, sink.Vertices[0][16].Position.X, 4);
            Assert.Equal(25f, sink.Vertices[0][16].Position.Y, 4);
            Assert.Equal(0f, sink.Vertices[0][4].Color.X, 5);
            Assert.Equal(1f, sink.Vertices[0][4].Color.Y, 5);
            Assert.Equal(0f, sink.Vertices[0][4].Color.Z, 5);
            Assert.Equal(128f / 255f, sink.Vertices[0][4].Color.W, 5);
            Assert.Equal(0f, sink.Vertices[0][8].Color.X, 5);
            Assert.Equal(1f, sink.Vertices[0][8].Color.Y, 5);
            Assert.Equal(0f, sink.Vertices[0][8].Color.Z, 5);
            Assert.Equal(128f / 255f, sink.Vertices[0][8].Color.W, 5);
            Assert.Equal(0f, sink.Vertices[0][12].Color.X, 5);
            Assert.Equal(1f, sink.Vertices[0][12].Color.Y, 5);
            Assert.Equal(0f, sink.Vertices[0][12].Color.Z, 5);
            Assert.Equal(128f / 255f, sink.Vertices[0][12].Color.W, 5);
            Assert.Equal(0f, sink.Vertices[0][16].Color.X, 5);
            Assert.Equal(1f, sink.Vertices[0][16].Color.Y, 5);
            Assert.Equal(0f, sink.Vertices[0][16].Color.Z, 5);
            Assert.Equal(128f / 255f, sink.Vertices[0][16].Color.W, 5);
            Assert.Equal(0f, sink.Vertices[0][0].Color.X);
            Assert.Equal(128f / 255f, sink.Vertices[0][0].Color.W, 5);
            Assert.Equal(14f, sink.Vertices[0][20].Position.X, 4);
            Assert.Equal(24f, sink.Vertices[0][20].Position.Y, 4);
            Assert.Equal(22f, sink.Vertices[0][21].Position.X, 4);
            Assert.Equal(24f, sink.Vertices[0][21].Position.Y, 4);
            Assert.Equal(22f, sink.Vertices[0][22].Position.X, 4);
            Assert.Equal(40f, sink.Vertices[0][22].Position.Y, 4);
            Assert.Equal(14f, sink.Vertices[0][23].Position.X, 4);
            Assert.Equal(40f, sink.Vertices[0][23].Position.Y, 4);
            Assert.Equal(255f / 255f, sink.Vertices[0][20].Color.X, 5);
            Assert.Equal(64f / 255f, sink.Vertices[0][20].Color.W, 5);
            Assert.Equal(15f, sink.Vertices[0][24].Position.X, 4);
            Assert.Equal(46f, sink.Vertices[0][24].Position.Y, 4);
            Assert.Equal(14f, sink.Vertices[0][44].Position.X, 4);
            Assert.Equal(44f, sink.Vertices[0][44].Position.Y, 4);
            Assert.Equal(15f, sink.Vertices[0][48].Position.X, 4);
            Assert.Equal(66f, sink.Vertices[0][48].Position.Y, 4);
            Assert.Equal(14f, sink.Vertices[0][68].Position.X, 4);
            Assert.Equal(64f, sink.Vertices[0][68].Position.Y, 4);
            Assert.Equal(15f, sink.Vertices[0][72].Position.X, 4);
            Assert.Equal(84f, sink.Vertices[0][72].Position.Y, 4);
            Assert.Equal(14f, sink.Vertices[0][92].Position.X, 4);
            Assert.Equal(82f, sink.Vertices[0][92].Position.Y, 4);
        }

        /// <summary>
        /// Confirms every empty and visible line keeps its precomputed alignment offset, including a trailing line.
        /// </summary>
        [Fact]
        public void EmitText_WhenLinesHaveLeadingConsecutiveAndTrailingBreaks_UsesMatchingLineOffsets() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            Entity parent = new Entity(core) { Position = new float3(10f, 20f, 3f) };
            RuntimeTexture atlas = renderer.CreateTexture(16, 16);
            FontAsset font = new FontAsset(new FontInfo("fixture", 10, 3f), atlas,
                new Dictionary<char, FontChar> {
                    ['A'] = new FontChar(new float4(0f, 0f, 0.25f, 0.5f), 2f, 5f, 0f, 0f)
                }, 10f, 16, 16);
            TextComponent drawable = new TextComponent {
                Font = font,
                Text = "\nA\n\nA\n",
                FontScale = 2f,
                Size = new int2(16, 16),
                WrapText = false,
                Alignment = TextAlignment.Center
            };
            parent.InitComponents();
            parent.AddComponent(drawable);
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 8);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 100f, 100f));

            new Batch2DDrawableEmitter(writer, renderer).EmitText(drawable,
                new Batch2DRun(Batch2DVariant.Textured, atlas, 0, 0, 100, 100));
            writer.Flush();

            Assert.Equal(8, sink.Vertices.Sum(vertices => vertices.Length));
            Assert.Equal(12, sink.Indices.Sum(indices => indices.Length));
            Assert.Equal(14f, sink.Vertices[0][0].Position.X, 4);
            Assert.Equal(44f, sink.Vertices[0][0].Position.Y, 4);
            Assert.Equal(14f, sink.Vertices[0][4].Position.X, 4);
            Assert.Equal(84f, sink.Vertices[0][4].Position.Y, 4);
        }

        /// <summary>
        /// Confirms existing line-offset semantics position identical text at each alignment edge.
        /// </summary>
        [Theory]
        [InlineData(TextAlignment.Left, 10f)]
        [InlineData(TextAlignment.Center, 14f)]
        [InlineData(TextAlignment.Right, 18f)]
        public void EmitText_WhenAlignmentChanges_UsesSharedVisibleLineOffsets(TextAlignment alignment, float expectedX) {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            Entity parent = new Entity(core) { Position = new float3(10f, 20f, 9f) };
            RuntimeTexture atlas = renderer.CreateTexture(16, 16);
            FontAsset font = new FontAsset(new FontInfo("fixture", 10, 3f), atlas,
                new Dictionary<char, FontChar> {
                    ['A'] = new FontChar(new float4(0f, 0f, 0.25f, 0.5f), 2f, 5f, 0f, 0f)
                }, 10f, 16, 16);
            TextComponent drawable = new TextComponent {
                Font = font,
                Text = "A",
                FontScale = 2f,
                Size = new int2(16, 20),
                Alignment = alignment
            };
            parent.InitComponents();
            parent.AddComponent(drawable);
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 8);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 100f, 100f));

            new Batch2DDrawableEmitter(writer, renderer).EmitText(drawable,
                new Batch2DRun(Batch2DVariant.Textured, atlas, 0, 0, 100, 100));
            writer.Flush();

            Assert.Equal(expectedX, sink.Vertices[0][0].Position.X, 4);
        }

        /// <summary>
        /// Confirms font atlas metrics and scaled advance position adjacent glyphs in order.
        /// </summary>
        [Fact]
        public void EmitText_WhenGlyphsHaveMetrics_ScalesBoundsAndAdvance() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            Entity parent = new Entity(core) { Position = new float3(10f, 20f, 0f) };
            RuntimeTexture atlas = renderer.CreateTexture(16, 16);
            FontAsset font = new FontAsset(new FontInfo("fixture", 10, 3f), atlas,
                new Dictionary<char, FontChar> {
                    ['A'] = new FontChar(new float4(0f, 0f, 0.25f, 0.5f), 2f, 5f, 0f, 0f),
                    ['B'] = new FontChar(new float4(0.25f, 0f, 0.25f, 0.5f), 1f, 4f, 0f, 0f)
                }, 10f, 16, 16);
            TextComponent drawable = new TextComponent {
                Font = font,
                Text = "AB",
                FontScale = 2f,
                Size = new int2(100, 20),
                Alignment = TextAlignment.Left
            };
            parent.InitComponents();
            parent.AddComponent(drawable);
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 8);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 100f, 100f));

            new Batch2DDrawableEmitter(writer, renderer).EmitText(drawable,
                new Batch2DRun(Batch2DVariant.Textured, atlas, 0, 0, 100, 100));
            writer.Flush();

            Assert.Equal(8f, sink.Vertices[0][1].Position.X - sink.Vertices[0][0].Position.X, 4);
            Assert.Equal(16f, sink.Vertices[0][2].Position.Y - sink.Vertices[0][1].Position.Y, 4);
            Assert.Equal(8, sink.Vertices.Sum(vertices => vertices.Length));
            Assert.Equal(10f, sink.Vertices[0][4].Position.X - sink.Vertices[0][0].Position.X, 4);
            Assert.Equal(0.25f, sink.Vertices[0][4].TexLocal.X);
        }

        /// <summary>
        /// Fails explicitly when nonempty text has no font to provide its atlas and glyph metrics.
        /// </summary>
        [Fact]
        public void EmitText_WhenFontIsMissing_ThrowsInvalidOperationException() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            Entity parent = new Entity(core);
            TextComponent drawable = new TextComponent { Text = "A" };
            parent.InitComponents();
            parent.AddComponent(drawable);
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 4);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 20f, 20f));

            Assert.Throws<InvalidOperationException>(() => new Batch2DDrawableEmitter(writer, renderer)
                .EmitText(drawable, new Batch2DRun(Batch2DVariant.Textured, null, 0, 0, 20, 20)));
        }

        /// <summary>
        /// Fails explicitly when nonempty text references an atlas that has already been disposed.
        /// </summary>
        [Fact]
        public void EmitText_WhenAtlasIsDisposed_ThrowsInvalidOperationException() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            Entity parent = new Entity(core);
            RuntimeTexture atlas = renderer.CreateTexture(16, 16);
            atlas.Dispose();
            FontAsset font = new FontAsset(new FontInfo("fixture", 10, 3f), atlas,
                new Dictionary<char, FontChar> {
                    ['A'] = new FontChar(new float4(0f, 0f, 0.25f, 0.5f), 2f, 5f, 0f, 0f)
                }, 10f, 16, 16);
            TextComponent drawable = new TextComponent { Text = "A", Font = font };
            parent.InitComponents();
            parent.AddComponent(drawable);
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 4);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 20f, 20f));

            Assert.Throws<InvalidOperationException>(() => new Batch2DDrawableEmitter(writer, renderer)
                .EmitText(drawable, new Batch2DRun(Batch2DVariant.Textured, atlas, 0, 0, 20, 20)));
        }

        /// <summary>
        /// Fails explicitly when the font asset has no atlas texture for nonempty text.
        /// </summary>
        [Fact]
        public void EmitText_WhenFontAtlasIsMissing_ThrowsInvalidOperationException() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            Entity parent = new Entity(core);
            FontAsset font = new FontAsset(new FontInfo("fixture", 10, 3f), null,
                new Dictionary<char, FontChar> {
                    ['A'] = new FontChar(new float4(0f, 0f, 0.25f, 0.5f), 2f, 5f, 0f, 0f)
                }, 10f, 16, 16);
            TextComponent drawable = new TextComponent { Text = "A", Font = font };
            parent.InitComponents();
            parent.AddComponent(drawable);
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 4);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 20f, 20f));

            Assert.Throws<InvalidOperationException>(() => new Batch2DDrawableEmitter(writer, renderer)
                .EmitText(drawable, new Batch2DRun(Batch2DVariant.Textured, null, 0, 0, 20, 20)));
        }

        /// <summary>
        /// Creates a headless core so the real text component exposes its world transform to the emitter.
        /// </summary>
        /// <param name="renderer">Renderer that owns the test core's runtime textures.</param>
        /// <returns>An initialized core with the supplied 2D renderer.</returns>
        static Core CreateCore(Batch2DTestRenderManager renderer) {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, renderer, null, new PlatformInfo("test", "test-version"));
            return core;
        }
    }
}
