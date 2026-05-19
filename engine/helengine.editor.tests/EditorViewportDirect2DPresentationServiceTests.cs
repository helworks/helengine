using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-side world-presentation coordinate conversions for authored 2D content.
    /// </summary>
    public sealed class EditorViewportDirect2DPresentationServiceTests : IDisposable {
        /// <summary>
        /// Initializes the core services required by direct 2D presentation tests.
        /// </summary>
        public EditorViewportDirect2DPresentationServiceTests() {
            Core core = new Core();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Clears shared editor preview state and disposes the active core instance after each test.
        /// </summary>
        public void Dispose() {
            EditorWorldSpace2DPreviewRegistry.Clear();
            EditorWorldSpace2DPreviewMeshResources.ResetForTests();
            Core.Instance?.Dispose();
        }

        /// <summary>
        /// Ensures viewport-owned entities round-trip between stored world space and presented world space when reference-canvas fit is active.
        /// </summary>
        [Fact]
        public void ResolveStoredWorldPositionFromPresented_WhenViewportUsesReferenceCanvasFit_RestoresStoredWorldPosition() {
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            renderManager3D.AddWindow(IntPtr.Zero, 1600, 1200);

            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.ScreenBindingMode
            });
            viewportEntity.AddComponent(new ReferenceCanvasFitComponent {
                ReferenceWidth = 1280,
                ReferenceHeight = 720
            });

            Entity sourceEntity = new Entity {
                LocalPosition = new float3(100f, 200f, 35f)
            };
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            viewportEntity.AddChild(sourceEntity);
            sourceEntity.AddComponent(new SpriteComponent {
                Size = new int2(64, 64),
                Texture = TextureUtils.PixelTexture
            });

            Core.Instance.Update();

            float3 presentedWorldPosition = EditorViewportDirect2DPresentationService.ResolvePresentedWorldPosition(sourceEntity);
            float3 restoredWorldPosition = EditorViewportDirect2DPresentationService.ResolveStoredWorldPositionFromPresented(sourceEntity, presentedWorldPosition);

            AssertFloat3ApproximatelyEqual(sourceEntity.Position, restoredWorldPosition, 0.001f);
        }

        /// <summary>
        /// Ensures viewport-owned supported 2D content resolves a presented anchor at the visible bounds center instead of the authored top-left origin.
        /// </summary>
        [Fact]
        public void ResolvePresentedWorldAnchorPosition_WhenViewportOwnedSpriteIsSelected_ReturnsPresentedBoundsCenter() {
            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = new int2(1280, 720)
            });

            Entity sourceEntity = new Entity {
                LocalPosition = new float3(100f, 200f, 35f)
            };
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            viewportEntity.AddChild(sourceEntity);
            sourceEntity.AddComponent(new SpriteComponent {
                Size = new int2(64, 32),
                Texture = TextureUtils.PixelTexture
            });

            float3 presentedAnchorPosition = EditorViewportDirect2DPresentationService.ResolvePresentedWorldAnchorPosition(sourceEntity);

            AssertFloat3ApproximatelyEqual(new float3(132f, -216f, 35f), presentedAnchorPosition, 0.001f);
        }

        /// <summary>
        /// Ensures anchor-based writeback preserves the authored stored origin while scene-view gizmos operate from the visible bounds center.
        /// </summary>
        [Fact]
        public void ResolveStoredWorldPositionFromPresentedAnchor_WhenViewportOwnedSpriteUsesPresentedCenter_RestoresStoredWorldPosition() {
            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = new int2(1280, 720)
            });

            Entity sourceEntity = new Entity {
                LocalPosition = new float3(100f, 200f, 35f)
            };
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            viewportEntity.AddChild(sourceEntity);
            sourceEntity.AddComponent(new SpriteComponent {
                Size = new int2(64, 32),
                Texture = TextureUtils.PixelTexture
            });

            float3 presentedAnchorPosition = EditorViewportDirect2DPresentationService.ResolvePresentedWorldAnchorPosition(sourceEntity);
            float3 restoredWorldPosition = EditorViewportDirect2DPresentationService.ResolveStoredWorldPositionFromPresentedAnchor(sourceEntity, presentedAnchorPosition);

            AssertFloat3ApproximatelyEqual(sourceEntity.Position, restoredWorldPosition, 0.001f);
        }

        /// <summary>
        /// Verifies two vectors are equal within one small tolerance on every axis.
        /// </summary>
        /// <param name="expected">Expected vector.</param>
        /// <param name="actual">Actual vector.</param>
        /// <param name="tolerance">Inclusive tolerance applied per component.</param>
        static void AssertFloat3ApproximatelyEqual(float3 expected, float3 actual, float tolerance) {
            Assert.InRange(actual.X, expected.X - tolerance, expected.X + tolerance);
            Assert.InRange(actual.Y, expected.Y - tolerance, expected.Y + tolerance);
            Assert.InRange(actual.Z, expected.Z - tolerance, expected.Z + tolerance);
        }
    }
}
