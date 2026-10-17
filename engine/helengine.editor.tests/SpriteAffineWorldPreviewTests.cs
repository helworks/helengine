using helengine.editor.tests.testing;

namespace helengine.editor.tests {
    public sealed class SpriteAffineWorldPreviewTests {
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void PreviewCornersPreservePivotLocalRotationAndAncestorScale(bool viewport, bool cancelScale) {
            using Core core = new(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            TestRenderManager3D renderer = new();
            core.Initialize(renderer, new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test"));
            using TestGeneratedAssetGraph graph = new(core);
            Entity ancestor = new(core);
            ancestor.InitComponents(); ancestor.InitChildren();
            ancestor.LocalScale = new float3(2f, 0.75f, 1f);
            ancestor.LocalPosition = new float3(20f, 30f, 7f);
            if (viewport) { ancestor.AddComponent(new ViewportComponent { FixedSize = new int2(640, 480) }); }
            Entity source = new(core);
            source.InitComponents(); source.InitChildren();
            ancestor.AddChild(source);
            source.LocalPosition = new float3(40f, 50f, 3f);
            SpriteComponent sprite = new() { Texture = core.RenderManager2D.PixelTexture, Size = new int2(80, 40), Pivot = new float2(0.5f, 0.25f), RotationRadians = 0.7f };
            if (cancelScale) {
                // The decomposed Scale becomes one, but intermediate rotation keeps the matrix sheared.
                source.LocalScale = new float3(0.5f, 4f / 3f, 1f);
                float3 axis = float3.Normalize(new float3(0.3f, 0.4f, 1f));
                float4.CreateFromAxisAngle(ref axis, 0.6f, out float4 orientation);
                source.LocalOrientation = orientation;
                sprite.Pivot = new float2(0f, 0f);
                sprite.RotationRadians = 0f;
                Assert.Equal(new float3(1f, 1f, 1f), source.Scale);
            }
            source.AddComponent(sprite);
            EditorEntity proxy = new(core, graph.InteractionServices);
            EditorSpriteWorldPreviewComponent preview = new(source, sprite, graph.ShaderLibrary, graph.RendererResources);
            proxy.AddComponent(preview);
            preview.SynchronizeFromSource();
            ModelAsset mesh = renderer.BuiltModelAssets.Last();
            SpriteQuad2D local = Batch2DGeometry.ResolveSpriteLocalQuad(sprite);
            float2[] corners = [local.TopLeft, local.TopRight, local.BottomRight, local.BottomLeft];
            for (int index = 0; index < 4; index++) {
                float4x4 matrix = source.WorldTransformMatrix;
                float2 corner = corners[index];
                float3 expected = new(corner.X * matrix.M11 + corner.Y * matrix.M21 + matrix.M41,
                    corner.X * matrix.M12 + corner.Y * matrix.M22 + matrix.M42,
                    corner.X * matrix.M13 + corner.Y * matrix.M23 + matrix.M43);
                expected = helengine.editor.EditorViewportDirect2DPresentationService.TransformPresentedWorldPoint(source, expected);
                float3 actual = mesh.Positions[index] * proxy.Scale;
                actual = proxy.Position + float4.RotateVector(actual, proxy.Orientation);
                Assert.InRange((actual - expected).Length(), 0f, 0.001f);
            }
            int buildCount = renderer.BuiltModelAssets.Count;
            preview.SynchronizeFromSource();
            Assert.Equal(buildCount, renderer.BuiltModelAssets.Count);
            proxy.RemoveComponent(preview);
            Assert.NotEmpty(renderer.ReleasedModels);
        }
    }
}
