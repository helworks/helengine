using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-only viewport selection framing behavior for scene-view focus operations.
    /// </summary>
    public sealed class EditorViewportSelectionFramingServiceTests : IDisposable {
        /// <summary>
        /// Initializes the core services required by editor viewport framing tests.
        /// </summary>
        public EditorViewportSelectionFramingServiceTests() {
            Core core = new Core();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Disposes the active core instance after each framing test.
        /// </summary>
        public void Dispose() {
            EditorSelectionService.ClearSelection();
            Core.Instance?.Dispose();
        }

        /// <summary>
        /// Ensures focusing a selected viewport frames all authored viewport corners inside the active scene camera.
        /// </summary>
        [Fact]
        public void FocusSelection_WhenViewportEntityIsSelected_FramesEntireViewport() {
            CameraComponent camera = CreateSceneCamera();
            EditorViewportCameraController controller = CreateCameraController(camera, out EditorEntity cameraEntity);
            Entity viewportEntity = CreateViewportEntity(new int2(1280, 720));
            EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();

            service.FocusSelection(camera, controller, viewportEntity);

            Assert.Equal(new float3(640f, 360f, 0f), controller.GetOrbitTarget());
            AssertPointIsVisible(cameraEntity, camera, new float3(0f, 0f, 0f));
            AssertPointIsVisible(cameraEntity, camera, new float3(1280f, 0f, 0f));
            AssertPointIsVisible(cameraEntity, camera, new float3(0f, 720f, 0f));
            AssertPointIsVisible(cameraEntity, camera, new float3(1280f, 720f, 0f));
        }

        /// <summary>
        /// Ensures focusing a very large selected viewport expands the far clip plane when the default editor distance is insufficient.
        /// </summary>
        [Fact]
        public void FocusSelection_WhenViewportIsHuge_ExpandsFarPlaneToFitSelection() {
            CameraComponent camera = CreateSceneCamera();
            EditorViewportCameraController controller = CreateCameraController(camera, out EditorEntity cameraEntity);
            Entity viewportEntity = CreateViewportEntity(new int2(40000, 20000));
            EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();

            service.FocusSelection(camera, controller, viewportEntity);

            Assert.True(camera.FarPlaneDistance > 5000f);
            AssertPointIsVisible(cameraEntity, camera, new float3(0f, 0f, 0f));
            AssertPointIsVisible(cameraEntity, camera, new float3(40000f, 20000f, 0f));
        }

        /// <summary>
        /// Ensures focusing a selected mesh uses the mesh bounds center as the orbit target.
        /// </summary>
        [Fact]
        public void FocusSelection_WhenMeshEntityIsSelected_UsesMeshBoundsCenterAsOrbitTarget() {
            CameraComponent camera = CreateSceneCamera();
            EditorViewportCameraController controller = CreateCameraController(camera, out _);
            Entity meshEntity = CreateMeshEntity();
            EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();

            service.FocusSelection(camera, controller, meshEntity);

            Assert.Equal(new float3(21f, 42f, 63f), controller.GetOrbitTarget());
        }

        /// <summary>
        /// Ensures viewport selection extent resolves from the full fixed viewport size.
        /// </summary>
        [Fact]
        public void ResolveSelectionExtent_WhenViewportEntityIsSelected_UsesResolvedViewportSize() {
            Entity viewportEntity = CreateViewportEntity(new int2(1280, 720));
            EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();

            double selectionExtent = service.ResolveSelectionExtentForTest(viewportEntity);

            Assert.Equal(1280.0, selectionExtent);
        }

        /// <summary>
        /// Ensures mesh selection extent resolves from the largest scaled model dimension.
        /// </summary>
        [Fact]
        public void ResolveSelectionExtent_WhenMeshEntityIsSelected_UsesLargestScaledModelDimension() {
            TestRuntimeModel runtimeModel = new TestRuntimeModel();
            runtimeModel.SetBounds(new float3(-1f, -2f, -3f), new float3(3f, 4f, 5f));
            Entity meshEntity = new Entity();
            meshEntity.InitComponents();
            meshEntity.InitChildren();
            meshEntity.LocalScale = new float3(2f, 3f, 4f);
            meshEntity.AddComponent(new MeshComponent {
                Model = runtimeModel
            });
            EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();

            double selectionExtent = service.ResolveSelectionExtentForTest(meshEntity);

            Assert.Equal(32.0, selectionExtent);
        }

        /// <summary>
        /// Ensures sprite selection extent resolves from the largest sprite dimension.
        /// </summary>
        [Fact]
        public void ResolveSelectionExtent_WhenSpriteEntityIsSelected_UsesLargestSpriteDimension() {
            Entity spriteEntity = new Entity();
            spriteEntity.InitComponents();
            spriteEntity.InitChildren();
            spriteEntity.AddComponent(new SpriteComponent {
                Size = new int2(64, 96)
            });
            EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();

            double selectionExtent = service.ResolveSelectionExtentForTest(spriteEntity);

            Assert.Equal(96.0, selectionExtent);
        }

        /// <summary>
        /// Ensures unsupported selections report zero extent so callers can fall back cleanly.
        /// </summary>
        [Fact]
        public void ResolveSelectionExtent_WhenEntityHasNoSupportedBounds_ReturnsZero() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();
            EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();

            double selectionExtent = service.ResolveSelectionExtentForTest(entity);

            Assert.Equal(0.0, selectionExtent);
        }

        /// <summary>
        /// Creates one standard scene camera used by framing tests.
        /// </summary>
        /// <returns>Configured camera component.</returns>
        CameraComponent CreateSceneCamera() {
            CameraComponent camera = new CameraComponent();
            camera.Viewport = new float4(0f, 0f, 1280f, 720f);
            camera.FarPlaneDistance = 5000f;
            return camera;
        }

        /// <summary>
        /// Creates one scene camera controller hosted on an editor entity.
        /// </summary>
        /// <param name="camera">Scene camera rendered by the controller host.</param>
        /// <param name="cameraEntity">Receives the created camera host entity.</param>
        /// <returns>Configured viewport camera controller.</returns>
        EditorViewportCameraController CreateCameraController(CameraComponent camera, out EditorEntity cameraEntity) {
            cameraEntity = new EditorEntity();
            cameraEntity.AddComponent(camera);

            EditorViewportCameraController controller = new EditorViewportCameraController(camera);
            cameraEntity.AddComponent(controller);
            return controller;
        }

        /// <summary>
        /// Creates one authored viewport entity with a fixed viewport size.
        /// </summary>
        /// <param name="viewportSize">Authored viewport size in pixels.</param>
        /// <returns>Configured authored viewport entity.</returns>
        Entity CreateViewportEntity(int2 viewportSize) {
            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = viewportSize
            });
            return viewportEntity;
        }

        /// <summary>
        /// Creates one mesh entity with deterministic model bounds.
        /// </summary>
        /// <returns>Configured mesh entity.</returns>
        Entity CreateMeshEntity() {
            TestRuntimeModel runtimeModel = new TestRuntimeModel();
            runtimeModel.SetBounds(new float3(20f, 40f, 60f), new float3(22f, 44f, 66f));

            Entity meshEntity = new Entity();
            meshEntity.InitComponents();
            meshEntity.InitChildren();
            meshEntity.AddComponent(new MeshComponent {
                Model = runtimeModel
            });
            return meshEntity;
        }

        /// <summary>
        /// Asserts that one world-space point projects inside the active scene camera viewport.
        /// </summary>
        /// <param name="cameraEntity">Entity that owns the scene camera transform.</param>
        /// <param name="camera">Scene camera used to project the point.</param>
        /// <param name="worldPoint">World-space point that should remain visible.</param>
        void AssertPointIsVisible(EditorEntity cameraEntity, CameraComponent camera, float3 worldPoint) {
            double verticalFieldOfView = Math.PI / 4.0;
            float4 viewport = camera.Viewport;
            double aspectRatio = viewport.Z / viewport.W;
            double horizontalFieldOfView = 2.0 * Math.Atan(Math.Tan(verticalFieldOfView * 0.5) * aspectRatio);
            float3 relativePoint = worldPoint - cameraEntity.Position;
            float4 inverseOrientation = float4.Inverse(cameraEntity.Orientation);
            float3 viewPoint = float4.RotateVector(relativePoint, inverseOrientation);
            double depth = Math.Max(-(double)viewPoint.Z, 0.0001);
            double halfHorizontal = Math.Abs(viewPoint.X) / depth;
            double halfVertical = Math.Abs(viewPoint.Y) / depth;

            Assert.True(viewPoint.Z < 0f);
            Assert.True(halfHorizontal <= Math.Tan(horizontalFieldOfView * 0.5) + 0.0001);
            Assert.True(halfVertical <= Math.Tan(verticalFieldOfView * 0.5) + 0.0001);
        }
    }
}
