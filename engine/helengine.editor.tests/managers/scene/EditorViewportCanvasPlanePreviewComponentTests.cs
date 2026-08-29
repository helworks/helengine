using helengine.directx11;
using helengine.editor.tests.testing;
using helengine.vulkan;
using Xunit;

namespace helengine.editor.tests.managers.scene {
    /// <summary>
    /// Verifies the offscreen preview component that renders the simulated 2D canvas onto a world-space plane.
    /// </summary>
    public class EditorViewportCanvasPlanePreviewComponentTests : IDisposable {
        TestGeneratedAssetGraph GeneratedAssetGraph;
        /// <summary>
        /// Ensures attaching the preview component creates a default-sized render target and correctly sized plane.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenAttached_CreatesDefaultSizedRenderTargetAndPlane() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera();
            EditorSceneCanvasProfileState sceneCanvasProfileState = new EditorSceneCanvasProfileState();
            EditorViewportCanvasPreviewSettings settings = new EditorViewportCanvasPreviewSettings();
            EditorEntity cameraEntity = Assert.IsType<EditorEntity>(sceneCamera.Parent);
            var component = new EditorViewportCanvasPlanePreviewComponent(sceneCamera, sceneCanvasProfileState, settings, Core.Instance.RenderManager3D, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources);

            cameraEntity.AddComponent(component);
            component.Update();

            TestRenderTarget renderTarget = Assert.IsType<TestRenderTarget>(component.PreviewRenderTarget);
            Assert.Equal(1280, renderTarget.Width);
            Assert.Equal(720, renderTarget.Height);
            Assert.Equal(new float3(6.4f, 3.6f, 0f), component.PlaneEntity.LocalPosition);
            Assert.Equal(new float3(12.8f, 7.2f, 1f), component.PlaneEntity.LocalScale);
            Assert.Equal(new float4(0f, 0f, 1280f, 720f), component.PreviewCamera.Viewport);
        }

        /// <summary>
        /// Ensures changing the preview settings rebuilds the render target and updates plane placement and scale.
        /// </summary>
        [Fact]
        public void Update_WhenCanvasSettingsChange_RebuildsRenderTargetAndPlaneScale() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera();
            EditorSceneCanvasProfileState sceneCanvasProfileState = new EditorSceneCanvasProfileState();
            EditorViewportCanvasPreviewSettings settings = new EditorViewportCanvasPreviewSettings();
            EditorEntity cameraEntity = Assert.IsType<EditorEntity>(sceneCamera.Parent);
            var component = new EditorViewportCanvasPlanePreviewComponent(sceneCamera, sceneCanvasProfileState, settings, Core.Instance.RenderManager3D, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources);

            cameraEntity.AddComponent(component);
            component.Update();
            RenderTarget initialRenderTarget = component.PreviewRenderTarget;

            sceneCanvasProfileState.SetCanvasWidth(1920);
            sceneCanvasProfileState.SetCanvasHeight(1080);
            settings.PixelsPerWorldUnit = 200;
            component.Update();

            Assert.NotSame(initialRenderTarget, component.PreviewRenderTarget);
            Assert.True(((TestRenderTarget)initialRenderTarget).WasDisposed);
            Assert.Equal(new float3(4.8f, 2.7f, 0f), component.PlaneEntity.LocalPosition);
            Assert.Equal(new float3(9.6f, 5.4f, 1f), component.PlaneEntity.LocalScale);
            Assert.Equal(new float4(0f, 0f, 1920f, 1080f), component.PreviewCamera.Viewport);
        }

        /// <summary>
        /// Ensures authored 2D scene drawables render only through the offscreen preview camera once the viewport plane preview is active.
        /// </summary>
        [Fact]
        public void Update_WhenViewportPreviewIsActive_RoutesAuthoredSceneDrawablesOnlyToPreviewCamera() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera();
            EditorSceneCanvasProfileState sceneCanvasProfileState = new EditorSceneCanvasProfileState();
            EditorViewportCanvasPreviewSettings settings = new EditorViewportCanvasPreviewSettings();
            EditorEntity cameraEntity = Assert.IsType<EditorEntity>(sceneCamera.Parent);
            var component = new EditorViewportCanvasPlanePreviewComponent(sceneCamera, sceneCanvasProfileState, settings, Core.Instance.RenderManager3D, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources);
            CreateSceneRoundedRectEntity();

            cameraEntity.AddComponent(component);
            component.Update();

            Assert.Equal(0, sceneCamera.RenderQueue2D.Count);
            Assert.Equal(1, component.PreviewCamera.RenderQueue2D.Count);
        }

        /// <summary>
        /// Initializes the core services required by canvas-plane preview component tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
            GeneratedAssetGraph = new TestGeneratedAssetGraph(core);
            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            shaderBackendRegistry.Register(new VulkanShaderBackend());
        }

        public void Dispose() {
            GeneratedAssetGraph.Dispose();
            Core.Instance?.Dispose();
        }

        /// <summary>
        /// Creates one scene camera entity that can host the canvas-plane preview component.
        /// </summary>
        /// <returns>Scene camera component used by the preview component.</returns>
        CameraComponent CreateSceneCamera() {
            EditorEntity cameraEntity = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices());
            var sceneCamera = new CameraComponent {
                LayerMask = EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGrid | EditorLayerMasks.SceneCameraVisuals | EditorLayerMasks.SceneCanvasPlane,
                Viewport = new float4(0f, 0f, 640f, 360f)
            };
            cameraEntity.AddComponent(sceneCamera);
            return sceneCamera;
        }

        /// <summary>
        /// Creates one authored scene entity containing a rounded rectangle drawable.
        /// </summary>
        /// <returns>Scene entity registered on the authored scene-object layer.</returns>
        EditorEntity CreateSceneRoundedRectEntity() {
            var sceneEntity = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                LayerMask = EditorLayerMasks.SceneObjects,
                IsSceneOwned = true
            };
            RoundedRectComponent roundedRect = new RoundedRectComponent();
            sceneEntity.AddComponent(roundedRect);
            sceneEntity.InitializeHierarchy();
            if (!GeneratedAssetGraph.ObjectManager.Drawables2D.Contains(roundedRect)) {
                GeneratedAssetGraph.ObjectManager.RegisterForRender2D(roundedRect);
            }
            return sceneEntity;
        }
    }
}
