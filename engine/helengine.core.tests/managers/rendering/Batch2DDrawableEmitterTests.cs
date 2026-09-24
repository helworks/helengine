using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>
    /// Verifies sprite and rounded-shape components expand into shared batch vertices.
    /// </summary>
    public sealed class Batch2DDrawableEmitterTests {
        /// <summary>
        /// Confirms sprite source coordinates, parent scale and rotation, and byte tint are snapshotted into vertices.
        /// </summary>
        [Fact]
        public void EmitSprite_WhenTransformed_EmitsDestinationAndSourceCoordinates() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            Entity parent = new Entity(core) {
                Position = new float3(10f, 20f, 7f),
                Scale = new float3(2f, 3f, 1f),
                Orientation = new float4(0f, 0f, MathF.Sqrt(0.5f), MathF.Sqrt(0.5f))
            };
            RuntimeTexture texture = renderer.CreateTexture(80, 60);
            SpriteComponent drawable = new SpriteComponent {
                Texture = texture,
                Size = new int2(30, 40),
                SourceRect = new float4(0.25f, 0.5f, 0.5f, 0.25f),
                Color = new byte4(255, 128, 0, 64)
            };
            parent.InitComponents();
            parent.AddComponent(drawable);
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 8);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 200f, 200f));

            RuntimeTexture incomingTexture = renderer.CreateTexture(1, 1);
            new Batch2DDrawableEmitter(writer, renderer).EmitSprite(drawable,
                new Batch2DRun(Batch2DVariant.RoundedShape, incomingTexture, 3, 4, 190, 180));
            writer.Flush();

            Assert.Single(sink.Runs);
            Assert.Same(texture, sink.Runs[0].Texture);
            Assert.Equal(Batch2DVariant.Textured, sink.Runs[0].Variant);
            Assert.Equal(3, sink.Runs[0].ScissorX);
            Assert.Equal(4, sink.Runs[0].ScissorY);
            Assert.Equal(190, sink.Runs[0].ScissorWidth);
            Assert.Equal(180, sink.Runs[0].ScissorHeight);
            Assert.Equal(-20f, sink.Vertices[0][0].Position.X, 4);
            Assert.Equal(110f, sink.Vertices[0][0].Position.Y, 4);
            Assert.Equal(0f, sink.Vertices[0][0].Position.Z);
            Assert.Equal(0.25f, sink.Vertices[0][0].TexLocal.X);
            Assert.Equal(0.5f, sink.Vertices[0][0].TexLocal.Y);
            Assert.Equal(0.75f, sink.Vertices[0][2].TexLocal.X);
            Assert.Equal(0.75f, sink.Vertices[0][2].TexLocal.Y);
            Assert.Equal(128f / 255f, sink.Vertices[0][0].Color.Y, 5);
            Assert.Equal(64f / 255f, sink.Vertices[0][0].Color.W, 5);
        }

        /// <summary>
        /// Confirms empty, disabled, and texture-sized fallback behavior does not create invalid geometry.
        /// </summary>
        [Fact]
        public void EmitSprite_WhenEmptyOrDisabledSkipsAndNonpositiveSizeUsesTextureBounds() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            Entity parent = new Entity(core);
            RuntimeTexture texture = renderer.CreateTexture(12, 9);
            SpriteComponent drawable = new SpriteComponent { Texture = texture, Size = new int2(0, 0) };
            parent.InitComponents();
            parent.AddComponent(drawable);
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 8);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 100f, 100f));
            Batch2DDrawableEmitter emitter = new Batch2DDrawableEmitter(writer, renderer);
            Batch2DRun run = new Batch2DRun(Batch2DVariant.Textured, texture, 0, 0, 100, 100);

            emitter.EmitSprite(drawable, run);
            drawable.Texture = null;
            emitter.EmitSprite(drawable, run);
            drawable.Texture = texture;
            drawable.Size = new int2(-1, 0);
            emitter.EmitSprite(drawable, run);
            parent.Enabled = false;
            emitter.EmitSprite(drawable, run);
            writer.Flush();

            Assert.Equal(2, sink.Vertices.Sum(vertices => vertices.Length) / 4);
            Assert.Equal(12f, sink.Vertices[0][1].Position.X);
            Assert.Equal(9f, sink.Vertices[0][2].Position.Y);
        }

        /// <summary>
        /// Confirms rounded fills preserve each corner bit, straight alpha, local bounds, border clamping, and rotation.
        /// </summary>
        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
        [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
        [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)]
        [InlineData(12)] [InlineData(13)] [InlineData(14)] [InlineData(15)]
        public void EmitRoundedRect_WhenCornerMaskChanges_DecodesMaskIntoVertexAttributes(int mask) {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            Entity parent = new Entity(core) { Position = new float3(4f, 6f, 2f) };
            RoundedRectComponent drawable = new RoundedRectComponent {
                Size = new int2(20, 10),
                Corners = (RoundedRectCorners)mask,
                Radius = 9f,
                BorderThickness = 8f,
                FillColor = new byte4(10, 20, 30, 128),
                BorderColor = new byte4(40, 50, 60, 64),
                Rotation = MathF.PI / 2f
            };
            parent.InitComponents();
            parent.AddComponent(drawable);
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 8);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 100f, 100f));

            new Batch2DDrawableEmitter(writer, renderer).EmitRoundedRect(drawable,
                new Batch2DRun(Batch2DVariant.RoundedShape, null, 0, 0, 100, 100));
            writer.Flush();

            Batch2DVertex first = sink.Vertices[0][0];
            Assert.Equal(10f, first.Shape.X);
            Assert.Equal(5f, first.Shape.Y);
            Assert.Equal(5f, first.Shape.Z);
            Assert.Equal(5f, first.Shape.W);
            Assert.Equal(mask & 1, (int)first.Corners.X);
            Assert.Equal((mask >> 1) & 1, (int)first.Corners.Y);
            Assert.Equal((mask >> 2) & 1, (int)first.Corners.Z);
            Assert.Equal((mask >> 3) & 1, (int)first.Corners.W);
            Assert.Equal(128f / 255f, first.Color.W, 5);
            Assert.Equal(64f / 255f, first.BorderColor.W, 5);
            Assert.Equal(0f, first.TexLocal.X);
            Assert.Equal(0f, first.TexLocal.Y);
            Assert.Equal(9f, first.Position.X, 4);
            Assert.Equal(21f, first.Position.Y, 4);
            Assert.Equal(0f, first.Position.Z);
            Assert.Equal(-10f, first.TexLocal.Z);
            Assert.Equal(-5f, first.TexLocal.W);
            Assert.Null(sink.Runs[0].Texture);
            Assert.Equal(Batch2DVariant.RoundedShape, sink.Runs[0].Variant);
        }

        /// <summary>
        /// Creates a headless core so drawable components resolve their actual world transforms.
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
