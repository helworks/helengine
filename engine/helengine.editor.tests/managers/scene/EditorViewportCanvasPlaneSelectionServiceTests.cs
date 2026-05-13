using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.scene {
    /// <summary>
    /// Verifies viewport canvas-plane clicks resolve back into the shared 2D interactable selection path.
    /// </summary>
    public class EditorViewportCanvasPlaneSelectionServiceTests {
        /// <summary>
        /// Ensures a pointer that hits the world-space plane selects the matching 2D scene entity on the simulated canvas.
        /// </summary>
        [Fact]
        public void ResolveSelectableEntityAtPointer_WhenPointerHitsPlaneInteractable_ReturnsSceneEntity() {
            InitializeCore();
            EditorEntity cameraEntity = CreateViewportCameraEntity();
            CameraComponent sceneCamera = FindCameraComponent(cameraEntity);
            EditorViewportCanvasPlanePreviewComponent previewComponent = CreatePreviewComponent(cameraEntity, sceneCamera, 200, 200, 100);
            EditorEntity expectedEntity = CreateInteractableEntity(new float3(80f, 80f, 0f), new int2(40, 40), 3);

            Entity selectedEntity = EditorViewportCanvasPlaneSelectionService.ResolveSelectableEntityAtPointer(
                previewComponent,
                cameraEntity,
                sceneCamera.Viewport,
                new int2(50, 50));

            Assert.Same(expectedEntity, selectedEntity);
        }

        /// <summary>
        /// Ensures a pointer ray that misses the plane bounds does not resolve a scene selection.
        /// </summary>
        [Fact]
        public void ResolveSelectableEntityAtPointer_WhenPointerMissesPlaneBounds_ReturnsNull() {
            InitializeCore();
            EditorEntity cameraEntity = CreateViewportCameraEntity();
            CameraComponent sceneCamera = FindCameraComponent(cameraEntity);
            EditorViewportCanvasPlanePreviewComponent previewComponent = CreatePreviewComponent(cameraEntity, sceneCamera, 200, 200, 100);
            CreateInteractableEntity(new float3(80f, 80f, 0f), new int2(40, 40), 3);

            Entity selectedEntity = EditorViewportCanvasPlaneSelectionService.ResolveSelectableEntityAtPointer(
                previewComponent,
                cameraEntity,
                sceneCamera.Viewport,
                new int2(0, 0));

            Assert.Null(selectedEntity);
        }

        /// <summary>
        /// Ensures the viewport-to-canvas mapping treats the top of the plane as canvas Y zero so visual hits match the rendered preview.
        /// </summary>
        [Fact]
        public void ResolveSelectableEntityAtPointer_WhenPointerHitsTopOfPlane_SelectsTopCanvasEntity() {
            InitializeCore();
            EditorEntity cameraEntity = CreateViewportCameraEntity();
            CameraComponent sceneCamera = FindCameraComponent(cameraEntity);
            EditorViewportCanvasPlanePreviewComponent previewComponent = CreatePreviewComponent(cameraEntity, sceneCamera, 200, 200, 100);
            EditorEntity expectedEntity = CreateInteractableEntity(new float3(0f, 0f, 0f), new int2(40, 40), 3);

            Entity selectedEntity = EditorViewportCanvasPlaneSelectionService.ResolveSelectableEntityAtPointer(
                previewComponent,
                cameraEntity,
                sceneCamera.Viewport,
                new int2(40, 40));

            Assert.Same(expectedEntity, selectedEntity);
        }

        /// <summary>
        /// Initializes the lightweight editor core services required by canvas-plane selection tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Creates one viewport camera entity aimed at the center of a two-unit canvas plane.
        /// </summary>
        /// <returns>Viewport camera entity used to evaluate plane-hit mapping.</returns>
        EditorEntity CreateViewportCameraEntity() {
            var cameraEntity = new EditorEntity {
                Position = new float3(1f, 1f, 10f),
                Orientation = float4.Identity
            };
            var sceneCamera = new CameraComponent {
                LayerMask = EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGrid | EditorLayerMasks.SceneCameraVisuals | EditorLayerMasks.SceneCanvasPlane,
                Viewport = new float4(0f, 0f, 100f, 100f)
            };
            cameraEntity.AddComponent(sceneCamera);
            return cameraEntity;
        }

        /// <summary>
        /// Finds the camera component attached to one editor entity.
        /// </summary>
        /// <param name="entity">Entity whose components should be searched.</param>
        /// <returns>Camera component registered on the entity.</returns>
        CameraComponent FindCameraComponent(EditorEntity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is CameraComponent cameraComponent) {
                    return cameraComponent;
                }
            }

            throw new InvalidOperationException("Viewport camera entity must contain a camera component.");
        }

        /// <summary>
        /// Creates one preview component configured for the supplied canvas size and pixel scale.
        /// </summary>
        /// <param name="cameraEntity">Viewport camera entity that owns the preview component.</param>
        /// <param name="sceneCamera">Viewport camera component used by the preview component.</param>
        /// <param name="canvasWidth">Simulated canvas width in pixels.</param>
        /// <param name="canvasHeight">Simulated canvas height in pixels.</param>
        /// <param name="pixelsPerWorldUnit">Canvas scale expressed as pixels per world unit.</param>
        /// <returns>Initialized preview component attached to the viewport camera.</returns>
        EditorViewportCanvasPlanePreviewComponent CreatePreviewComponent(
            EditorEntity cameraEntity,
            CameraComponent sceneCamera,
            int canvasWidth,
            int canvasHeight,
            int pixelsPerWorldUnit) {
            var sceneCanvasProfileState = new EditorSceneCanvasProfileState();
            sceneCanvasProfileState.SetCanvasWidth(canvasWidth);
            sceneCanvasProfileState.SetCanvasHeight(canvasHeight);
            var settings = new EditorViewportCanvasPreviewSettings {
                PixelsPerWorldUnit = pixelsPerWorldUnit
            };
            var previewComponent = new EditorViewportCanvasPlanePreviewComponent(sceneCamera, sceneCanvasProfileState, settings, Core.Instance.RenderManager3D);
            cameraEntity.AddComponent(previewComponent);
            previewComponent.Update();
            return previewComponent;
        }

        /// <summary>
        /// Creates one visible 2D interactable entity on the simulated canvas.
        /// </summary>
        /// <param name="position">Top-left canvas position in pixels.</param>
        /// <param name="size">Interactable size in pixels.</param>
        /// <param name="renderOrder">2D render order assigned to the visible sprite.</param>
        /// <returns>Created scene entity that should be returned by the selection bridge.</returns>
        EditorEntity CreateInteractableEntity(float3 position, int2 size, byte renderOrder) {
            var entity = new EditorEntity {
                LayerMask = EditorLayerMasks.SceneObjects,
                Position = position
            };
            var sprite = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Size = size,
                RenderOrder2D = renderOrder
            };
            entity.AddComponent(sprite);
            var interactable = new InteractableComponent {
                Size = size
            };
            entity.AddComponent(interactable);
            return entity;
        }
    }
}
