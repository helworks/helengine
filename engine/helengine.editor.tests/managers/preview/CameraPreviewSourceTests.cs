using helengine.editor;
using helengine.editor.tests.testing;
using helengine.ui;
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

            EditorCore core = new EditorCore(new Project {
                Name = "Camera Preview",
                Path = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"), new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
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
            Assert.True(((TestRenderTarget)initialRenderTarget).WasDisposed);
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

        /// <summary>
        /// Creates a deterministic font asset used by camera preview tests that need text rendering.
        /// </summary>
        /// <returns>Font asset with basic glyph coverage for the baked demo menu labels.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['B'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['D'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['L'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['M'] = new FontChar(new float4(0f, 0f, 11f, 12f), 0f, 11f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['k'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }
    }
}

