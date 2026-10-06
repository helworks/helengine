using helengine.editor;
using helengine.editor.tests.testing;
using helengine.ui;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies live camera preview behavior.
    /// </summary>
public class CameraPreviewSourceTests : IDisposable {
        EditorSessionInteractionServices InteractionServices => GeneratedAssetGraph.InteractionServices;
        /// <summary>
        /// Temporary content root used by the camera preview tests.
        /// </summary>
        readonly string TempRootPath;
        readonly Core CoreValue;
        readonly TestGeneratedAssetGraph GeneratedAssetGraph;

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
            CoreValue = core;
            GeneratedAssetGraph = new TestGeneratedAssetGraph(core);
        }

        /// <summary>
        /// Deletes temporary test content after each test.
        /// </summary>
        public void Dispose() {
            GeneratedAssetGraph.Dispose();
            CoreValue.Dispose();
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
            EditorSceneCameraSuppressionService.AttachAndSuppress(cameraEntity, GeneratedAssetGraph.ObjectManager);

            CameraPreviewSource source = new CameraPreviewSource(cameraEntity, liveCamera, CoreValue.RenderManager3D, GeneratedAssetGraph.RendererResources);
            source.Update();

            Assert.Equal(new float3(3f, 4f, -9f), source.PreviewCamera.Parent.Position);
            Assert.Equal(EditorUiCameraDrawOrders.SharedUi - 1, source.PreviewCamera.CameraDrawOrder);
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
            CameraPreviewSource source = new CameraPreviewSource(cameraEntity, liveCamera, CoreValue.RenderManager3D, GeneratedAssetGraph.RendererResources);
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
        /// Ensures camera previews redraw at panel resolution while retaining their authored logical canvas.
        /// </summary>
        [Fact]
        public void Resize_WhenSuppressedSceneCameraUsesAuthoredViewport_PreservesLogicalCanvasAtPanelResolution() {
            EditorEntity cameraEntity = CreateCameraEntity(new float4(0f, 0f, 1280f, 720f));
            CameraComponent liveCamera = Assert.IsType<CameraComponent>(Assert.Single(cameraEntity.Components, component => component is CameraComponent));
            EditorSceneCameraSuppressionService.AttachAndSuppress(cameraEntity, GeneratedAssetGraph.ObjectManager);

            CameraPreviewSource source = new CameraPreviewSource(cameraEntity, liveCamera, CoreValue.RenderManager3D, GeneratedAssetGraph.RendererResources);
            source.Resize(new int2(320, 180));

            TestRenderTarget resizedRenderTarget = Assert.IsType<TestRenderTarget>(source.RenderTarget);
            Assert.Equal(320, resizedRenderTarget.Width);
            Assert.Equal(180, resizedRenderTarget.Height);
            Assert.Equal(new float4(0f, 0f, 320f, 180f), source.PreviewCamera.Viewport);
            Assert.Equal(new int2(1280, 720), Assert.IsType<CameraPreviewComponent>(source.PreviewCamera).LogicalViewportSize);

            source.Resize(new int2(640, 400));
            Assert.True(resizedRenderTarget.WasDisposed);
            Assert.Equal(640, source.RenderTarget.Width);
            Assert.Equal(400, source.RenderTarget.Height);
            Assert.Equal(new int2(1280, 720), Assert.IsType<CameraPreviewComponent>(source.PreviewCamera).LogicalViewportSize);
        }

        /// <summary>
        /// Creates one editor entity with a live camera that can be converted into a preview source.
        /// </summary>
        /// <returns>Editor entity with one camera component.</returns>
        EditorEntity CreateCameraEntity() {
            return CreateCameraEntity(new float4(0f, 0f, 128f, 72f));
        }

        /// <summary>Changing the logical canvas refreshes projection without reallocating an unchanged panel texture.</summary>
        [Fact]
        public void Update_WhenCanvasSizeChanges_RefreshesTargetAndReusesItOnUnchangedFrames() {
            EditorEntity cameraEntity = CreateCameraEntity();
            CameraComponent camera = Assert.IsType<CameraComponent>(Assert.Single(cameraEntity.Components, component => component is CameraComponent));
            EditorSceneCanvasProfileState profile = new EditorSceneCanvasProfileState();
            using CameraPreviewSource source = new CameraPreviewSource(cameraEntity, camera, CoreValue.RenderManager3D, profile, GeneratedAssetGraph.RendererResources);
            source.Resize(new int2(320, 180));
            TestRenderTarget previousTarget = Assert.IsType<TestRenderTarget>(source.RenderTarget);
            profile.SetCanvasWidth(640);
            profile.SetCanvasHeight(360);

            source.Update();

            Assert.False(previousTarget.WasDisposed);
            Assert.Same(previousTarget, source.RenderTarget);
            Assert.Equal(320, source.RenderTarget.Width);
            Assert.Equal(180, source.RenderTarget.Height);
            Assert.Equal(new float4(0, 0, 320, 180), source.PreviewCamera.Viewport);
            Assert.Equal(new int2(640, 360), Assert.IsType<CameraPreviewComponent>(source.PreviewCamera).LogicalViewportSize);
            RenderTarget updatedTarget = source.RenderTarget;
            source.Update();
            Assert.Same(updatedTarget, source.RenderTarget);
        }

        /// <summary>Authored text stays on the world-preview path so a fixed screen-space copy cannot hide camera movement.</summary>
        /// <param name="hasViewport">Whether text belongs to an authored viewport bound to the selected camera.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Update_WithSceneText_UsesWorldPreviewAcrossEditsAndResizes(bool hasViewport) {
            EditorEntity cameraEntity = CreateCameraEntity();
            CameraComponent camera = Assert.IsType<CameraComponent>(Assert.Single(cameraEntity.Components, component => component is CameraComponent));
            EditorEntity root = new EditorEntity(CoreValue, InteractionServices);
            if (hasViewport) {
                root.AddComponent(new ViewportComponent {
                    BindingMode = ViewportComponent.ExplicitCameraBindingMode, BoundCameraComponent = camera
                });
            }
            EditorEntity labelEntity = new EditorEntity(CoreValue, InteractionServices) { LayerMask = EditorLayerMasks.SceneObjects };
            root.AddChild(labelEntity);
            TextComponent text = new TextComponent { Font = CreateFont(), Text = "A", Size = new int2(64, 32) };
            labelEntity.AddComponent(text);
            EditorEntity syncHost = new EditorEntity(CoreValue, InteractionServices) { InternalEntity = true };
            EditorWorldSpace2DPreviewSyncComponent sync = new EditorWorldSpace2DPreviewSyncComponent(GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources);
            syncHost.AddComponent(sync);
            sync.Update();
            EditorSceneCameraSuppressionService.AttachAndSuppress(cameraEntity, GeneratedAssetGraph.ObjectManager);
            using CameraPreviewSource source = new CameraPreviewSource(cameraEntity, camera, CoreValue.RenderManager3D, GeneratedAssetGraph.RendererResources);
            source.Resize(new int2(320, 180));
            source.Update();

            Assert.Contains(source.PreviewCamera, CoreValue.ObjectManager.Cameras);
            Assert.Equal(0, source.PreviewCamera.RenderQueue2D.Count);
            Assert.Equal(1, source.PreviewCamera.RenderQueue3D.Count);
            text.Text = "B";
            text.FontScale = 2f;
            cameraEntity.Position = new float3(10, 20, -30);
            source.Resize(new int2(640, 360));
            source.Update();
            Assert.Equal(cameraEntity.Position, source.PreviewCamera.Parent.Position);
            Assert.Equal(0, source.PreviewCamera.RenderQueue2D.Count);
            Assert.Equal(1, source.PreviewCamera.RenderQueue3D.Count);

            EditorEntity secondLabelEntity = new EditorEntity(CoreValue, InteractionServices) { LayerMask = EditorLayerMasks.SceneObjects };
            root.AddChild(secondLabelEntity);
            secondLabelEntity.AddComponent(new TextComponent { Font = text.Font, Text = "C", Size = new int2(64, 32) });
            sync.Update();
            source.Update();
            Assert.Equal(0, source.PreviewCamera.RenderQueue2D.Count);
            Assert.Equal(2, source.PreviewCamera.RenderQueue3D.Count);
            root.RemoveChild(secondLabelEntity);
            secondLabelEntity.Dispose();
            sync.Update();
            source.Update();
            Assert.Equal(1, source.PreviewCamera.RenderQueue3D.Count);
        }

        /// <summary>The live preview follows authored projection changes without requiring a panel resize or reselection.</summary>
        [Fact]
        public void Update_WhenCameraProjectionChanges_MirrorsProjectionState() {
            EditorEntity cameraEntity = CreateCameraEntity();
            CameraComponent camera = Assert.IsType<CameraComponent>(Assert.Single(cameraEntity.Components, component => component is CameraComponent));
            using CameraPreviewSource source = new CameraPreviewSource(cameraEntity, camera, CoreValue.RenderManager3D, GeneratedAssetGraph.RendererResources);
            camera.FieldOfView = 0.9f;
            camera.NearPlaneDistance = 0.25f;
            camera.FarPlaneDistance = 750f;

            source.Update();

            Assert.Equal(camera.FieldOfView, source.PreviewCamera.FieldOfView);
            Assert.Equal(camera.NearPlaneDistance, source.PreviewCamera.NearPlaneDistance);
            Assert.Equal(camera.FarPlaneDistance, source.PreviewCamera.FarPlaneDistance);
        }

        /// <summary>
        /// Creates one editor entity with a live camera that can be converted into a preview source.
        /// </summary>
        /// <param name="viewport">Viewport assigned to the created camera.</param>
        /// <returns>Editor entity with one camera component.</returns>
        EditorEntity CreateCameraEntity(float4 viewport) {
            EditorEntity cameraEntity = new EditorEntity(CoreValue, InteractionServices);
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
