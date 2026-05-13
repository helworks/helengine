using helengine;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies pointer hit testing chooses the interactable that is visually on top.
    /// </summary>
    public class InputSystemTests {
        /// <summary>
        /// Ensures overlapping interactables route hover to the higher render-order element instead of the one registered later.
        /// </summary>
        [Fact]
        public void Update_WhenHigherRenderOrderInteractableOverlapsLowerOrder_UsesTheHigherOrderInteractable() {
            TestInputBackend input = InitializeCore();
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

            Assert.Same(frontInteractable, Core.Instance.PointerInteractionSystem.Hovering);
            Assert.Equal(1, frontHoverCount);
            Assert.Equal(0, backHoverCount);
        }

        /// <summary>
        /// Ensures removing a live interactable from the scene also removes it from pointer hit testing.
        /// </summary>
        [Fact]
        public void Update_WhenInteractableEntityIsDisposed_RemovesItFromHitTesting() {
            TestInputBackend input = InitializeCore();
            CreateUiCamera(320, 240);

            EditorEntity entity = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.EditorUi,
                Position = new float3(24f, 24f, 0f)
            };

            SpriteComponent sprite = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Size = new int2(80, 40),
                RenderOrder2D = 220
            };
            entity.AddComponent(sprite);

            InteractableComponent interactable = new InteractableComponent {
                Size = new int2(80, 40)
            };
            entity.AddComponent(interactable);

            entity.Dispose();

            input.SetMouseState(new MouseState(40, 40, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            input.EarlyUpdate();
            input.Update();

            Assert.DoesNotContain(interactable, Core.Instance.ObjectManager.Interactables);
            Assert.Null(Core.Instance.PointerInteractionSystem.Hovering);
        }

        /// <summary>
        /// Ensures a newly visible interactable under a stationary pointer still receives one hover event.
        /// This matches modal workflows where the mouse does not move after new UI appears.
        /// </summary>
        [Fact]
        public void Update_WhenInteractableAppearsUnderStationaryPointer_RaisesHoverOnTheNewInteractable() {
            TestInputBackend input = InitializeCore();
            CreateUiCamera(320, 240);

            MouseState pointerState = new MouseState(40, 40, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            input.SetMouseState(pointerState);
            input.EarlyUpdate();
            input.Update();

            InteractableComponent interactable = CreateInteractableEntity(new float3(24f, 24f, 0f), new int2(80, 40), 220);
            int hoverCount = 0;
            interactable.CursorEvent += (pos, delta, state) => {
                if (state == PointerInteraction.Hover) {
                    hoverCount++;
                }
            };

            input.SetMouseState(pointerState);
            input.EarlyUpdate();
            input.Update();

            Assert.Same(interactable, Core.Instance.PointerInteractionSystem.Hovering);
            Assert.Equal(1, hoverCount);
        }

        /// <summary>
        /// Ensures the first click on a newly visible interactable under a stationary pointer raises hover before press.
        /// This preserves click activation for controls that require both hover and press state during release.
        /// </summary>
        [Fact]
        public void Update_WhenPressTargetsNewlyVisibleInteractable_RaisesHoverBeforePress() {
            TestInputBackend input = InitializeCore();
            CreateUiCamera(320, 240);

            MouseState releasedState = new MouseState(40, 40, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            MouseState pressedState = new MouseState(40, 40, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            List<PointerInteraction> interactions = new List<PointerInteraction>();

            input.SetMouseState(releasedState);
            input.EarlyUpdate();
            input.Update();

            InteractableComponent interactable = CreateInteractableEntity(new float3(24f, 24f, 0f), new int2(80, 40), 220);
            interactable.CursorEvent += (pos, delta, state) => interactions.Add(state);

            input.SetMouseState(pressedState);
            input.EarlyUpdate();
            input.Update();

            input.SetMouseState(releasedState);
            input.EarlyUpdate();
            input.Update();

            Assert.Equal(new[] { PointerInteraction.Hover, PointerInteraction.Press, PointerInteraction.Release }, interactions);
        }

        /// <summary>
        /// Ensures pointer movement over an inactive window still refreshes hover state even though clicks remain suppressed.
        /// This matches editor controls such as close buttons and menu items that should react visually before activation.
        /// </summary>
        [Fact]
        public void Update_WhenWindowIsInactiveAndPointerMovesOverInteractable_RaisesHover() {
            TestInputBackend input = InitializeCore();
            CreateUiCamera(320, 240);
            input.IsForegroundActive = false;

            InteractableComponent interactable = CreateInteractableEntity(new float3(24f, 24f, 0f), new int2(80, 40), 220);
            int hoverCount = 0;
            interactable.CursorEvent += (pos, delta, state) => {
                if (state == PointerInteraction.Hover) {
                    hoverCount++;
                }
            };

            input.SetMouseState(new MouseState(4, 4, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            input.EarlyUpdate();
            input.Update();

            input.SetMouseState(new MouseState(40, 40, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            input.EarlyUpdate();
            input.Update();

            Assert.Same(interactable, Core.Instance.PointerInteractionSystem.Hovering);
            Assert.Equal(1, hoverCount);
        }

        /// <summary>
        /// Ensures the input system exposes the hovered interactable cursor so native hosts can map it to platform cursors.
        /// </summary>
        [Fact]
        public void Update_WhenHoveringInteractableWithTextCursor_ExposesTextHoverCursor() {
            TestInputBackend input = InitializeCore();
            CreateUiCamera(320, 240);

            InteractableComponent interactable = CreateInteractableEntity(new float3(24f, 24f, 0f), new int2(80, 40), 220);
            interactable.HoverCursor = PointerCursorKind.Text;

            input.SetMouseState(new MouseState(40, 40, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            input.EarlyUpdate();
            input.Update();

            Assert.Same(interactable, Core.Instance.PointerInteractionSystem.Hovering);
            Assert.Equal(PointerCursorKind.Text, input.HoverCursor);
        }

        /// <summary>
        /// Ensures one later camera on a different editor UI layer does not prevent hit testing against visible controls rendered by an earlier shared UI camera.
        /// </summary>
        [Fact]
        public void Update_WhenTopmostCameraUsesAnotherLayer_StillHitsVisibleInteractableOnSharedUiCamera() {
            TestInputBackend input = InitializeCore();
            CreateUiCamera(320, 240, EditorLayerMasks.EditorUi, EditorUiCameraDrawOrders.SharedUi);
            CreateUiCamera(320, 240, EditorLayerMasks.PropertiesPanelContent, EditorUiCameraDrawOrders.PanelContent);

            InteractableComponent interactable = CreateInteractableEntity(
                new float3(24f, 24f, 0f),
                new int2(80, 40),
                220,
                EditorLayerMasks.EditorUi);

            input.SetMouseState(new MouseState(40, 40, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            input.EarlyUpdate();
            input.Update();

            Assert.Same(interactable, Core.Instance.PointerInteractionSystem.Hovering);
        }

        /// <summary>
        /// Ensures runtime-style input capture exposes keyboard transitions without requiring an explicit activation call first.
        /// </summary>
        [Fact]
        public void EarlyUpdate_WhenKeyboardStateChangesWithoutExplicitActivation_StillCapturesThePressedKey() {
            TestInputBackend input = InitializeCore();

            input.SetKeyboardState(new KeyboardState());
            input.EarlyUpdate();
            input.Update();

            input.SetKeyboardState(new KeyboardState(Keys.Enter));
            input.EarlyUpdate();

            Assert.True(Core.Instance.InputSystem.WasKeyPressed(Keys.Enter));
        }

        /// <summary>
        /// Ensures inactive hosts suppress keyboard transitions by default.
        /// </summary>
        [Fact]
        public void EarlyUpdate_WhenWindowIsInactiveAndBackgroundInputIsDisabled_DoesNotCaptureKeyboardTransitions() {
            TestInputBackend input = InitializeCore();
            input.IsForegroundActive = false;

            input.SetKeyboardState(new KeyboardState());
            input.EarlyUpdate();
            input.Update();

            input.SetKeyboardState(new KeyboardState(Keys.Enter));
            input.EarlyUpdate();

            Assert.False(Core.Instance.InputSystem.WasKeyPressed(Keys.Enter));
        }

#if DESKTOP_PLATFORM
        /// <summary>
        /// Ensures enabling background input allows inactive hosts to report keyboard and mouse-button input.
        /// </summary>
        [Fact]
        public void EarlyUpdate_WhenBackgroundInputIsEnabled_CapturesInactiveKeyboardAndMouseButtonInput() {
            TestInputBackend input = InitializeCore();
            input.IsForegroundActive = false;
            Core.Instance.InputSystem.SetBackgroundInputEnabled(true);

            input.SetKeyboardState(new KeyboardState());
            input.SetMouseState(new MouseState(40, 40, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            input.EarlyUpdate();
            input.Update();

            input.SetKeyboardState(new KeyboardState(Keys.Enter));
            input.SetMouseState(new MouseState(40, 40, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            input.EarlyUpdate();

            Assert.True(Core.Instance.InputSystem.WasKeyPressed(Keys.Enter));
            Assert.Equal(ButtonState.Pressed, Core.Instance.InputSystem.CurrentFrame.Mouse.LeftButton);
        }
#endif

        /// <summary>
        /// Ensures the shared input layer leaves edge positions unchanged when pointer wrapping is disabled.
        /// </summary>
        [Fact]
        public void EarlyUpdate_WhenPointerWrapIsDisabled_KeepsTheRawEdgePosition() {
            TestInputBackend input = InitializeCore();
            Core.Instance.InputSystem.SetMouseClientBounds(new int2(320, 240));
            Core.Instance.InputSystem.SetPointerWrapEnabled(false);

            CaptureInputFrame(input, CreateReleasedMouseState(319, 120));

            Assert.Equal(new int2(319, 120), input.GetMousePosition());
        }

        /// <summary>
        /// Ensures the shared input layer wraps the pointer from the right client edge to the left interior edge when wrapping is enabled.
        /// </summary>
        [Fact]
        public void EarlyUpdate_WhenPointerWrapIsEnabledAtRightEdge_WrapsToTheLeftInteriorEdge() {
            TestInputBackend input = InitializeCore();
            Core.Instance.InputSystem.SetMouseClientBounds(new int2(320, 240));
            Core.Instance.InputSystem.SetPointerWrapEnabled(true);

            CaptureInputFrame(input, CreateReleasedMouseState(319, 120));

            Assert.Equal(new int2(1, 120), input.GetMousePosition());
        }

        /// <summary>
        /// Ensures the shared input layer wraps both axes when the pointer reaches a client corner during an active wrapped interaction.
        /// </summary>
        [Fact]
        public void EarlyUpdate_WhenPointerWrapIsEnabledAtBottomRightCorner_WrapsBothAxes() {
            TestInputBackend input = InitializeCore();
            Core.Instance.InputSystem.SetMouseClientBounds(new int2(320, 240));
            Core.Instance.InputSystem.SetPointerWrapEnabled(true);

            CaptureInputFrame(input, CreateReleasedMouseState(319, 239));

            Assert.Equal(new int2(1, 1), input.GetMousePosition());
        }

        /// <summary>
        /// Ensures the first delta after a pointer wrap preserves the local movement without including the full teleport distance.
        /// </summary>
        [Fact]
        public void EarlyUpdate_WhenPointerWrapOccurs_PreservesTheLocalDeltaWithoutReportingTheTeleportDistance() {
            TestInputBackend input = InitializeCore();
            Core.Instance.InputSystem.SetMouseClientBounds(new int2(320, 240));
            Core.Instance.InputSystem.SetPointerWrapEnabled(true);

            CaptureInputFrame(input, CreateReleasedMouseState(318, 120));
            CaptureInputFrame(input, CreateReleasedMouseState(319, 120));

            Assert.Equal(new int2(1, 120), input.GetMousePosition());
            Assert.Equal(new int2(1, 0), input.GetMouseDelta());
        }

        /// <summary>
        /// Initializes a core instance with the minimum services required for input-routing tests.
        /// </summary>
        /// <returns>Input manager used by the current test.</returns>
        TestInputBackend InitializeCore() {
            Core core = new Core();
            TestInputBackend input = new TestInputBackend();
            core.Initialize(null, new TestRenderManager2D(), input, new PlatformInfo("test", "test-version"));
            return input;
        }

        /// <summary>
        /// Creates the UI camera used to evaluate 2D interactables under the pointer.
        /// </summary>
        /// <param name="width">Viewport width in pixels.</param>
        /// <param name="height">Viewport height in pixels.</param>
        void CreateUiCamera(int width, int height) {
            CreateUiCamera(width, height, EditorLayerMasks.EditorUi, 255);
        }

        /// <summary>
        /// Creates one UI camera using the supplied layer mask and draw order.
        /// </summary>
        /// <param name="width">Viewport width in pixels.</param>
        /// <param name="height">Viewport height in pixels.</param>
        /// <param name="layerMask">Layer mask rendered by the camera.</param>
        /// <param name="drawOrder">Draw order assigned to the camera.</param>
        void CreateUiCamera(int width, int height, ushort layerMask, byte drawOrder) {
            EditorEntity cameraEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = layerMask
            };

            CameraComponent camera = new CameraComponent {
                LayerMask = layerMask,
                CameraDrawOrder = drawOrder,
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
            return CreateInteractableEntity(position, size, renderOrder, EditorLayerMasks.EditorUi);
        }

        /// <summary>
        /// Creates one visible interactable entity with the supplied layout, render order, and layer mask.
        /// </summary>
        /// <param name="position">Top-left position in window coordinates.</param>
        /// <param name="size">Interactable size in pixels.</param>
        /// <param name="renderOrder">Render order assigned to the visible surface.</param>
        /// <param name="layerMask">Layer mask assigned to the entity.</param>
        /// <returns>Interactable component used for pointer routing.</returns>
        InteractableComponent CreateInteractableEntity(float3 position, int2 size, byte renderOrder, ushort layerMask) {
            EditorEntity entity = new EditorEntity {
                InternalEntity = true,
                LayerMask = layerMask,
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

        /// <summary>
        /// Captures and completes one input frame for tests that only inspect cached mouse state.
        /// </summary>
        /// <param name="input">Input manager receiving the frame state.</param>
        /// <param name="state">Mouse state to capture for the frame.</param>
        void CaptureInputFrame(TestInputBackend input, MouseState state) {
            input.SetMouseState(state);
            input.EarlyUpdate();
            input.Update();
        }

        /// <summary>
        /// Creates one released-button mouse state at the supplied pointer coordinates.
        /// </summary>
        /// <param name="x">Pointer X coordinate in window pixels.</param>
        /// <param name="y">Pointer Y coordinate in window pixels.</param>
        /// <returns>Mouse state with all buttons released.</returns>
        MouseState CreateReleasedMouseState(int x, int y) {
            return new MouseState(
                x,
                y,
                0,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released);
        }
    }
}

