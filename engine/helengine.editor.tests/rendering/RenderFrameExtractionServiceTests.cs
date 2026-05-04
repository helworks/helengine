using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies shared render-frame extraction and backend capability contracts.
    /// </summary>
    public class RenderFrameExtractionServiceTests {
        /// <summary>
        /// Ensures the extraction service returns one frame entry for one active camera and exposes backend capability metadata through the render manager contract.
        /// </summary>
        [Fact]
        public void Extract_WhenOneCameraExists_ReturnsFrameForThatCamera() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
            TestRenderManager3D renderManager = new TestRenderManager3D();
            RenderFrameExtractionService extractionService = new RenderFrameExtractionService();
            CameraComponent camera = new CameraComponent();

            RenderFrameExtractionResult result = extractionService.Extract(
                new[] { camera },
                Array.Empty<IDrawable3D>(),
                Array.Empty<LightComponent>(),
                renderManager.GetCapabilityProfile());

            RenderFrame frame = Assert.Single(result.Frames);
            Assert.Same(camera, frame.Camera);
            Assert.Empty(frame.DrawableSubmissions);
            Assert.Empty(frame.LightSubmissions);
            Assert.Empty(frame.ShadowCasterSubmissions);
            Assert.True(result.BackendCapabilities.SupportsForwardRendering);
        }
    }
}
