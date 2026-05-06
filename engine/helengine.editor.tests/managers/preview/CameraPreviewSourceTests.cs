using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies live camera preview behavior.
    /// </summary>
    public class CameraPreviewSourceTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the camera preview tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the camera preview tests.
        /// </summary>
        public CameraPreviewSourceTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-camera-preview-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary test content after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the source mirrors authored camera state when suppression metadata exists.
        /// </summary>
        [Fact]
        public void Update_WhenSuppressionStateExists_MirrorsAuthoredCameraState() {
            EditorEntity cameraEntity = CreateCameraEntity();
            CameraComponent liveCamera = Assert.IsType<CameraComponent>(Assert.Single(cameraEntity.Components, component => component is CameraComponent));
            EditorSceneCameraSuppressionService.AttachAndSuppress(cameraEntity);

            CameraPreviewSource source = new CameraPreviewSource(cameraEntity, liveCamera, Core.Instance.RenderManager3D);
            source.Update();

            Assert.Equal(new float3(3f, 4f, -9f), source.PreviewCamera.Parent.Position);
            Assert.Equal(7, source.PreviewCamera.CameraDrawOrder);
            Assert.Equal(EditorLayerMasks.SceneObjects, source.PreviewCamera.LayerMask);
            Assert.Equal(new CameraClearSettings(true, new float4(0.2f, 0.3f, 0.4f, 1f), true, 1f, false, 0), source.PreviewCamera.ClearSettings);
        }

        /// <summary>
        /// Ensures resizing the source rebuilds the render target with the requested dimensions.
        /// </summary>
        [Fact]
        public void Resize_WhenPanelSizeChanges_RebuildsTheRenderTarget() {
            EditorEntity cameraEntity = CreateCameraEntity();
            CameraComponent liveCamera = Assert.IsType<CameraComponent>(Assert.Single(cameraEntity.Components, component => component is CameraComponent));
            CameraPreviewSource source = new CameraPreviewSource(cameraEntity, liveCamera, Core.Instance.RenderManager3D);
            RenderTarget initialRenderTarget = source.RenderTarget;

            source.Resize(new int2(320, 180));

            TestRenderTarget resizedRenderTarget = Assert.IsType<TestRenderTarget>(source.RenderTarget);
            Assert.NotSame(initialRenderTarget, source.RenderTarget);
            Assert.True(((TestRenderTarget)initialRenderTarget).IsDisposed);
            Assert.Equal(320, resizedRenderTarget.Width);
            Assert.Equal(180, resizedRenderTarget.Height);
            Assert.Equal(new float4(0f, 0f, 320f, 180f), source.PreviewCamera.Viewport);
        }

        /// <summary>
        /// Ensures suppressed authored scene cameras preserve their authored viewport dimensions when previewed.
        /// </summary>
        [Fact]
        public void Resize_WhenSuppressedSceneCameraUsesAuthoredViewport_PreservesAuthoredViewportDimensions() {
            EditorEntity cameraEntity = CreateCameraEntity(new float4(0f, 0f, 1280f, 720f));
            CameraComponent liveCamera = Assert.IsType<CameraComponent>(Assert.Single(cameraEntity.Components, component => component is CameraComponent));
            EditorSceneCameraSuppressionService.AttachAndSuppress(cameraEntity);

            CameraPreviewSource source = new CameraPreviewSource(cameraEntity, liveCamera, Core.Instance.RenderManager3D);
            source.Resize(new int2(320, 180));

            TestRenderTarget resizedRenderTarget = Assert.IsType<TestRenderTarget>(source.RenderTarget);
            Assert.Equal(1280, resizedRenderTarget.Width);
            Assert.Equal(720, resizedRenderTarget.Height);
            Assert.Equal(new float4(0f, 0f, 1280f, 720f), source.PreviewCamera.Viewport);
        }

        /// <summary>
        /// Creates one editor entity with a live camera that can be converted into a preview source.
        /// </summary>
        /// <returns>Editor entity with one camera component.</returns>
        EditorEntity CreateCameraEntity() {
            return CreateCameraEntity(new float4(0f, 0f, 128f, 72f));
        }

        /// <summary>
        /// Creates one editor entity with a live camera that can be converted into a preview source.
        /// </summary>
        /// <param name="viewport">Viewport assigned to the created camera.</param>
        /// <returns>Editor entity with one camera component.</returns>
        EditorEntity CreateCameraEntity(float4 viewport) {
            EditorEntity cameraEntity = new EditorEntity();
            cameraEntity.Position = new float3(3f, 4f, -9f);
            float4 orientation;
            float4.CreateFromYawPitchRoll(0.25f, -0.15f, 0f, out orientation);
            cameraEntity.Orientation = orientation;

            CameraComponent camera = new CameraComponent {
                CameraDrawOrder = 7,
                LayerMask = EditorLayerMasks.SceneObjects,
                Viewport = viewport,
                ClearSettings = new CameraClearSettings(true, new float4(0.2f, 0.3f, 0.4f, 1f), true, 1f, false, 0)
            };
            cameraEntity.AddComponent(camera);

            return cameraEntity;
        }
    }
}
