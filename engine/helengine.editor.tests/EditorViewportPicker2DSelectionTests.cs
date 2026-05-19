using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies direct 2D-first viewport selection behavior for scene view picking.
    /// </summary>
    public sealed class EditorViewportPicker2DSelectionTests : IDisposable {
        /// <summary>
        /// Initializes the core services required by the viewport 2D selection tests.
        /// </summary>
        public EditorViewportPicker2DSelectionTests() {
            Core core = new Core();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Disposes the active core instance after each test.
        /// </summary>
        public void Dispose() {
            Core.Instance?.Dispose();
        }

        /// <summary>
        /// Ensures direct viewport selection resolves the underlying 2D scene entity under the pointer.
        /// </summary>
        [Fact]
        public void ResolveSelectableEntityAtPointer_WhenSelectableScene2DExists_ReturnsTheUnderlying2DEntity() {
            CameraComponent sceneCamera = CreateSceneCamera(new float4(0f, 0f, 320f, 180f));
            InteractableComponent interactable = CreateSceneInteractableEntity(new float3(20f, 30f, 0f), new int2(100, 60), 4);

            Entity selectedEntity = EditorViewportDirect2DPresentationService.ResolveSelectableEntityAtPointer(
                sceneCamera,
                sceneCamera.Viewport,
                new int2(60, 50));

            Assert.Same(interactable.Parent, selectedEntity);
        }

        /// <summary>
        /// Ensures direct viewport selection returns null when no selectable 2D scene entity lies under the pointer so the picker can fall back to 3D.
        /// </summary>
        [Fact]
        public void ResolveSelectableEntityAtPointer_WhenNoSelectableScene2DExists_ReturnsNull() {
            CameraComponent sceneCamera = CreateSceneCamera(new float4(0f, 0f, 320f, 180f));
            CreateSceneInteractableEntity(new float3(20f, 30f, 0f), new int2(100, 60), 4);

            Entity selectedEntity = EditorViewportDirect2DPresentationService.ResolveSelectableEntityAtPointer(
                sceneCamera,
                sceneCamera.Viewport,
                new int2(250, 150));

            Assert.Null(selectedEntity);
        }

        /// <summary>
        /// Creates one active scene camera with the supplied viewport rectangle.
        /// </summary>
        /// <param name="viewport">Viewport rectangle used by direct scene selection.</param>
        /// <returns>Configured scene camera component.</returns>
        CameraComponent CreateSceneCamera(float4 viewport) {
            Entity cameraEntity = new Entity {
                LayerMask = EditorLayerMasks.SceneObjects
            };
            cameraEntity.InitComponents();
            cameraEntity.InitChildren();

            CameraComponent camera = new CameraComponent {
                LayerMask = EditorLayerMasks.SceneObjects,
                CameraDrawOrder = 255,
                Viewport = viewport
            };
            cameraEntity.AddComponent(camera);
            return camera;
        }

        /// <summary>
        /// Creates one selectable scene 2D entity with a visible sprite and interactable bounds.
        /// </summary>
        /// <param name="position">Top-left entity position in window-space coordinates.</param>
        /// <param name="size">Interactable size in pixels.</param>
        /// <param name="renderOrder">2D render order assigned to the visible sprite.</param>
        /// <returns>Interactable component registered for hit resolution.</returns>
        InteractableComponent CreateSceneInteractableEntity(float3 position, int2 size, byte renderOrder) {
            Entity entity = new Entity {
                LayerMask = EditorLayerMasks.SceneObjects,
                Position = position
            };
            entity.InitComponents();
            entity.InitChildren();

            SpriteComponent sprite = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Size = size,
                RenderOrder2D = renderOrder
            };
            entity.AddComponent(sprite);

            InteractableComponent interactable = new InteractableComponent {
                Size = size
            };
            entity.AddComponent(interactable);
            return interactable;
        }
    }
}
