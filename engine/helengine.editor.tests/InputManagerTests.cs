using helengine;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies pointer hit testing chooses the interactable that is visually on top.
    /// </summary>
    public class InputManagerTests {
        /// <summary>
        /// Ensures overlapping interactables route hover to the higher render-order element instead of the one registered later.
        /// </summary>
        [Fact]
        public void Update_WhenHigherRenderOrderInteractableOverlapsLowerOrder_UsesTheHigherOrderInteractable() {
            TestInputManager input = InitializeCore();
            CreateUiCamera(320, 240);

            InteractableComponent frontInteractable = CreateInteractableEntity(new float3(24f, 24f, 0f), new int2(80, 40), 220);
            InteractableComponent backInteractable = CreateInteractableEntity(new float3(24f, 24f, 0f), new int2(80, 40), 110);
            int frontHoverCount = 0;
            int backHoverCount = 0;

            frontInteractable.CursorEvent += (pos, delta, state) => {
                if (state == PointerInteraction.Hover) {
                    frontHoverCount++;
                }
            };
            backInteractable.CursorEvent += (pos, delta, state) => {
                if (state == PointerInteraction.Hover) {
                    backHoverCount++;
                }
            };

            input.SetMouseState(new MouseState(40, 40, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            input.EarlyUpdate();
            input.Update();

            Assert.Same(frontInteractable, input.Hovering);
            Assert.Equal(1, frontHoverCount);
            Assert.Equal(0, backHoverCount);
        }

        /// <summary>
        /// Initializes a core instance with the minimum services required for input-routing tests.
        /// </summary>
        /// <returns>Input manager used by the current test.</returns>
        TestInputManager InitializeCore() {
            Core core = new Core();
            TestInputManager input = new TestInputManager();
            core.Initialize(null, new TestRenderManager2D(), input);
            return input;
        }

        /// <summary>
        /// Creates the UI camera used to evaluate 2D interactables under the pointer.
        /// </summary>
        /// <param name="width">Viewport width in pixels.</param>
        /// <param name="height">Viewport height in pixels.</param>
        void CreateUiCamera(int width, int height) {
            EditorEntity cameraEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.EditorUi
            };

            CameraComponent camera = new CameraComponent {
                LayerMask = EditorLayerMasks.EditorUi,
                CameraDrawOrder = 255,
                Viewport = new float4(0f, 0f, width, height)
            };
            cameraEntity.AddComponent(camera);
        }

        /// <summary>
        /// Creates one visible interactable entity with the supplied layout and render order.
        /// </summary>
        /// <param name="position">Top-left position in window coordinates.</param>
        /// <param name="size">Interactable size in pixels.</param>
        /// <param name="renderOrder">Render order assigned to the visible surface.</param>
        /// <returns>Interactable component used for pointer routing.</returns>
        InteractableComponent CreateInteractableEntity(float3 position, int2 size, byte renderOrder) {
            EditorEntity entity = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.EditorUi,
                Position = position
            };

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
