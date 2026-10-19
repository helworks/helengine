using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>Protects sprite-local pivots and rotations through the full affine entity hierarchy.</summary>
    public sealed class SpriteQuadGeometryTests {
        /// <summary>Verifies a centered sprite rotates without moving its authored pivot.</summary>
        [Fact]
        public void ResolveSpriteQuad_CenteredQuarterTurn_KeepsCenterAndUvCorners() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            Entity entity = new Entity(core) { LocalPosition = new float3(100f, 80f, 0f) };
            entity.InitComponents();
            SpriteComponent sprite = new SpriteComponent {
                Size = new int2(20, 10), Pivot = new float2(0.5f, 0.5f),
                RotationRadians = (float)(Math.PI / 2d)
            };
            entity.AddComponent(sprite);

            SpriteQuad2D quad = Batch2DGeometry.ResolveSpriteQuad(sprite);

            AssertPoint(105f, 70f, quad.TopLeft);
            AssertPoint(105f, 90f, quad.TopRight);
            AssertPoint(95f, 90f, quad.BottomRight);
            AssertPoint(95f, 70f, quad.BottomLeft);
            Batch2DGeometry.CreateSpriteQuad(quad, new float4(.2f, .3f, .4f, .5f),
                new float4(1f, 1f, 1f, 1f), out Batch2DVertex topLeft, out Batch2DVertex topRight,
                out Batch2DVertex bottomRight, out Batch2DVertex bottomLeft);
            Assert.Equal(.2f, topLeft.TexLocal.X);
            Assert.Equal(.3f, topRight.TexLocal.Y);
            Assert.Equal((double)(.6f),(double)(bottomRight.TexLocal.X), 5);
            Assert.Equal((double)(.8f),(double)(bottomLeft.TexLocal.Y), 5);
        }

        /// <summary>Ensures nonuniform mirrored ancestor scale is applied after local sprite rotation, retaining shear.</summary>
        [Fact]
        public void ResolveSpriteQuad_NonuniformMirroredParent_PreservesAffineCorners() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            Entity parent = new Entity(core) { LocalScale = new float3(-2f, 3f, 1f) };
            parent.InitChildren();
            Entity child = new Entity(core) { LocalPosition = new float3(10f, 20f, 0f) };
            parent.AddChild(child);
            child.InitComponents();
            SpriteComponent sprite = new SpriteComponent {
                Size = new int2(20, 10), RotationRadians = (float)(Math.PI / 4d)
            };
            child.AddComponent(sprite);

            SpriteQuad2D quad = Batch2DGeometry.ResolveSpriteQuad(sprite);

            double diagonal = Math.Sqrt(0.5d);
            AssertPoint(-20f, 60f, quad.TopLeft);
            AssertPoint((float)(-20d - 40d * diagonal), (float)(60d + 60d * diagonal), quad.TopRight);
            AssertPoint((float)(-20d + 20d * diagonal), (float)(60d + 30d * diagonal), quad.BottomLeft);
            AssertPoint((float)(-20d - 20d * diagonal), (float)(60d + 90d * diagonal), quad.BottomRight);
        }

        /// <summary>Retains legacy top-left anchoring and texture-size fallback when no pivot or angle is authored.</summary>
        [Fact]
        public void ResolveSpriteQuad_Defaults_UseTopLeftAndTextureSize() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            Entity entity = new Entity(core) { LocalPosition = new float3(7f, 11f, 0f) };
            entity.InitComponents();
            SpriteComponent sprite = new SpriteComponent { Texture = renderer.CreateTexture(32, 16) };
            entity.AddComponent(sprite);

            SpriteQuad2D quad = Batch2DGeometry.ResolveSpriteQuad(sprite);

            AssertPoint(7f, 11f, quad.TopLeft);
            AssertPoint(39f, 27f, quad.BottomRight);
        }

        /// <summary>Checks individual coordinates with tolerance for single-precision matrix storage.</summary>
        /// <param name="x">Expected horizontal pixel position.</param>
        /// <param name="y">Expected vertical pixel position.</param>
        /// <param name="point">Resolved sprite corner.</param>
        static void AssertPoint(float x, float y, float2 point) {
            Assert.Equal((double)(x),(double)(point.X), 4);
            Assert.Equal((double)(y),(double)(point.Y), 4);
        }

        /// <summary>Creates a headless core for entity lifecycle and texture ownership.</summary>
        /// <param name="renderer">Test renderer supplying textures.</param>
        /// <returns>An initialized core.</returns>
        static Core CreateCore(Batch2DTestRenderManager renderer) {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, renderer, null, new PlatformInfo("test", "test-version"));
            return core;
        }
    }
}
