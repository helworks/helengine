using helengine;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies viewport camera movement paths that are driven by direct mouse input.
    /// </summary>
    public class EditorViewportCameraControllerTests : IDisposable {
        /// <summary>
        /// Clears static viewport input blockers after each camera-controller test.
        /// </summary>
        public void Dispose() {
            EditorInputCaptureService.Reset();
            EditorSelectionService.ClearSelection();
        }

        /// <summary>
        /// Ensures scrolling upward over the viewport moves the camera forward without requiring a mouse button hold.
        /// </summary>
        [Fact]
        public void Update_WhenWheelScrollsUpInsideViewport_MovesCameraForward() {
            TestInputBackend input = InitializeCore();
            EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);
            controller.WheelZoomSpeed = 2.0;

            CompleteInputFrame(input, CreateMouseState(150, 150, 0));
            AdvanceInput(input, CreateMouseState(150, 150, 120));

            controller.Update();

            Assert.Equal(0f, cameraEntity.Position.X);
            Assert.Equal(0f, cameraEntity.Position.Y);
            Assert.Equal(-2f, cameraEntity.Position.Z);
        }

        /// <summary>
        /// Ensures scrolling downward over the viewport moves the camera backward along its forward axis.
        /// </summary>
        [Fact]
        public void Update_WhenWheelScrollsDownInsideViewport_MovesCameraBackward() {
            TestInputBackend input = InitializeCore();
            EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);
            controller.WheelZoomSpeed = 2.0;

            CompleteInputFrame(input, CreateMouseState(150, 150, 120));
            AdvanceInput(input, CreateMouseState(150, 150, 0));

            controller.Update();

            Assert.Equal(0f, cameraEntity.Position.X);
            Assert.Equal(0f, cameraEntity.Position.Y);
            Assert.Equal(2f, cameraEntity.Position.Z);
        }

        /// <summary>
        /// Ensures wheel input outside the viewport does not move the camera.
        /// </summary>
        [Fact]
        public void Update_WhenWheelScrollsOutsideViewport_DoesNotMoveCamera() {
            TestInputBackend input = InitializeCore();
            EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);
            controller.WheelZoomSpeed = 2.0;

            CompleteInputFrame(input, CreateMouseState(25, 25, 0));
            AdvanceInput(input, CreateMouseState(25, 25, 120));

            controller.Update();

            Assert.Equal(float3.Zero, cameraEntity.Position);
        }

        /// <summary>
        /// Ensures UI blockers suppress wheel zoom while the pointer is inside a blocked region.
        /// </summary>
        [Fact]
        public void Update_WhenWheelScrollsInsideBlockedViewportRegion_DoesNotMoveCamera() {
            TestInputBackend input = InitializeCore();
            EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);
            object blockerOwner = new object();
            controller.WheelZoomSpeed = 2.0;

            try {
                EditorInputCaptureService.SetBlocker(blockerOwner, new int2(120, 120), new int2(80, 80));
                CompleteInputFrame(input, CreateMouseState(150, 150, 0));
                AdvanceInput(input, CreateMouseState(150, 150, 120));

                controller.Update();

                Assert.Equal(float3.Zero, cameraEntity.Position);
            } finally {
                EditorInputCaptureService.ClearBlocker(blockerOwner);
            }
        }

        /// <summary>
        /// Ensures Alt plus middle mouse orbits around the selected entity while preserving the selected pivot distance.
        /// </summary>
        [Fact]
        public void Update_WhenAltMiddleMouseOrbitsSelectedEntity_PreservesSelectedPivotDistance() {
            TestInputBackend input = InitializeCore();
            EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
            cameraEntity.Position = new float3(0f, 0f, 10f);
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);
            EditorEntity selectedEntity = new EditorEntity();
            EditorSelectionService.SetSelectedEntity(selectedEntity);

            CompleteInputFrame(input, CreateMouseState(150, 150, 0));
            AdvanceInput(input, CreateMouseState(150, 150, 0, ButtonState.Pressed), new KeyboardState(Keys.LeftAlt));
            CompleteControllerFrame(input, controller);
            AdvanceInput(input, CreateMouseState(190, 150, 0, ButtonState.Pressed), new KeyboardState(Keys.LeftAlt));

            CompleteControllerFrame(input, controller);

            Assert.NotEqual(new float3(0f, 0f, 10f), cameraEntity.Position);
            Assert.Equal(10d, Distance(cameraEntity.Position, selectedEntity.Position), 3);
        }

        /// <summary>
        /// Ensures Alt plus middle mouse still orbits when no scene entity is selected by using the stored view target.
        /// </summary>
        [Fact]
        public void Update_WhenAltMiddleMouseOrbitsWithoutSelection_UsesStoredVirtualTarget() {
            TestInputBackend input = InitializeCore();
            EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
            cameraEntity.Position = new float3(0f, 0f, 8f);
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);

            CompleteInputFrame(input, CreateMouseState(150, 150, 0));
            AdvanceInput(input, CreateMouseState(150, 150, 0, ButtonState.Pressed), new KeyboardState(Keys.LeftAlt));
            CompleteControllerFrame(input, controller);
            AdvanceInput(input, CreateMouseState(180, 135, 0, ButtonState.Pressed), new KeyboardState(Keys.LeftAlt));

            CompleteControllerFrame(input, controller);

            Assert.NotEqual(new float3(0f, 0f, 8f), cameraEntity.Position);
        }

        /// <summary>
        /// Ensures orbiting continues after the pointer leaves the viewport when the drag started inside it.
        /// </summary>
        [Fact]
        public void Update_WhenAltMiddleMouseLeavesViewportAfterStartingInside_KeepsOrbiting() {
            TestInputBackend input = InitializeCore();
            EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
            cameraEntity.Position = new float3(0f, 0f, 10f);
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);
            EditorEntity selectedEntity = new EditorEntity();
            EditorSelectionService.SetSelectedEntity(selectedEntity);

            CompleteInputFrame(input, CreateMouseState(150, 150, 0));
            AdvanceInput(input, CreateMouseState(150, 150, 0, ButtonState.Pressed), new KeyboardState(Keys.LeftAlt));
            CompleteControllerFrame(input, controller);
            AdvanceInput(input, CreateMouseState(430, 150, 0, ButtonState.Pressed), new KeyboardState(Keys.LeftAlt));

            CompleteControllerFrame(input, controller);

            Assert.NotEqual(new float3(0f, 0f, 10f), cameraEntity.Position);
            Assert.Equal(10d, Distance(cameraEntity.Position, selectedEntity.Position), 3);
        }

        /// <summary>
        /// Ensures right-mouse freelook keeps rotating after the pointer wraps across a client edge.
        /// </summary>
        [Fact]
        public void Update_WhenRightMouseLookLeavesClientEdgeAfterStartingInside_KeepsRotating() {
            TestInputBackend input = InitializeCore();
            EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);

            CompleteInputFrame(input, CreateMouseState(150, 150, 0, ButtonState.Released, ButtonState.Released));
            AdvanceInput(input, CreateMouseState(399, 150, 0, ButtonState.Pressed, ButtonState.Released));
            CompleteControllerFrame(input, controller);
            AdvanceInput(input, CreateMouseState(500, 150, 0, ButtonState.Pressed, ButtonState.Released));

            CompleteControllerFrame(input, controller);

            Assert.NotEqual(float4.Identity, cameraEntity.Orientation);
        }

        /// <summary>
        /// Ensures middle-mouse pan keeps moving after the pointer wraps across a client edge.
        /// </summary>
        [Fact]
        public void Update_WhenMiddleMousePanLeavesClientEdgeAfterStartingInside_KeepsPanning() {
            TestInputBackend input = InitializeCore();
            EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);

            CompleteInputFrame(input, CreateMouseState(150, 150, 0, ButtonState.Released, ButtonState.Released));
            AdvanceInput(input, CreateMouseState(399, 150, 0, ButtonState.Released, ButtonState.Pressed));
            CompleteControllerFrame(input, controller);
            AdvanceInput(input, CreateMouseState(500, 150, 0, ButtonState.Released, ButtonState.Pressed));

            CompleteControllerFrame(input, controller);

            Assert.NotEqual(float3.Zero, cameraEntity.Position);
        }

        /// <summary>
        /// Ensures camera navigation enables pointer wrapping only for the active drag lifetime.
        /// </summary>
        [Fact]
        public void Update_WhenRightMouseLookBeginsAndEndsInsideViewport_TogglesPointerWrapForTheDragLifetime() {
            TestInputBackend input = InitializeCore();
            EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);

            CompleteInputFrame(input, CreateMouseState(150, 150, 0, ButtonState.Released, ButtonState.Released));
            AdvanceInput(input, CreateMouseState(150, 150, 0, ButtonState.Pressed, ButtonState.Released));
            CompleteControllerFrame(input, controller);
            Assert.True(Core.Instance.InputSystem.IsPointerWrapEnabled);

            AdvanceInput(input, CreateMouseState(150, 150, 0, ButtonState.Released, ButtonState.Released));
            CompleteControllerFrame(input, controller);
            Assert.False(Core.Instance.InputSystem.IsPointerWrapEnabled);
        }

        /// <summary>
        /// Ensures wheel zoom updates orbit distance so the next orbit keeps the new selected-target distance.
        /// </summary>
        [Fact]
        public void Update_WhenWheelZoomChangesDistance_OrbitKeepsTheUpdatedSelectedTargetDistance() {
            TestInputBackend input = InitializeCore();
            EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
            cameraEntity.Position = new float3(0f, 0f, 10f);
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);
            EditorEntity selectedEntity = new EditorEntity();
            EditorSelectionService.SetSelectedEntity(selectedEntity);

            CompleteInputFrame(input, CreateMouseState(150, 150, 0));
            AdvanceInput(input, CreateMouseState(150, 150, 120));
            CompleteControllerFrame(input, controller);
            AdvanceInput(input, CreateMouseState(150, 150, 120, ButtonState.Pressed), new KeyboardState(Keys.LeftAlt));
            CompleteControllerFrame(input, controller);
            AdvanceInput(input, CreateMouseState(180, 150, 120, ButtonState.Pressed), new KeyboardState(Keys.LeftAlt));

            CompleteControllerFrame(input, controller);

            Assert.NotEqual(new float3(0f, 0f, 9f), cameraEntity.Position);
            Assert.Equal(9d, Distance(cameraEntity.Position, selectedEntity.Position), 3);
        }

        /// <summary>
        /// Initializes core services with configurable input for camera-controller tests.
        /// </summary>
        /// <returns>Input manager used by the current test.</returns>
        TestInputBackend InitializeCore() {
            EditorInputCaptureService.Reset();
            Core core = new Core();
            TestInputBackend input = new TestInputBackend();
            core.InputSystem.SetMouseClientBounds(new int2(500, 400));
            core.Initialize(null, new TestRenderManager2D(), input);
            return input;
        }

        /// <summary>
        /// Creates a camera entity with a deterministic viewport rectangle.
        /// </summary>
        /// <param name="camera">Receives the camera component attached to the entity.</param>
        /// <returns>Camera entity used by the controller under test.</returns>
        EditorEntity CreateCameraEntity(out CameraComponent camera) {
            EditorEntity cameraEntity = new EditorEntity();
            camera = new CameraComponent {
                Viewport = new float4(100f, 100f, 300f, 200f)
            };
            cameraEntity.AddComponent(camera);
            return cameraEntity;
        }

        /// <summary>
        /// Creates and attaches a viewport camera controller to the supplied camera entity.
        /// </summary>
        /// <param name="cameraEntity">Entity that should own the controller.</param>
        /// <param name="camera">Camera component managed by the controller.</param>
        /// <returns>Controller attached to the camera entity.</returns>
        EditorViewportCameraController CreateController(EditorEntity cameraEntity, CameraComponent camera) {
            EditorViewportCameraController controller = new EditorViewportCameraController(camera);
            cameraEntity.AddComponent(controller);
            return controller;
        }

        /// <summary>
        /// Captures one input frame with the supplied mouse state.
        /// </summary>
        /// <param name="input">Input manager receiving the mouse state.</param>
        /// <param name="mouseState">Mouse state to expose for the next frame.</param>
        void AdvanceInput(TestInputBackend input, MouseState mouseState) {
            AdvanceInput(input, mouseState, new KeyboardState());
        }

        /// <summary>
        /// Captures one input frame with the supplied mouse and keyboard state.
        /// </summary>
        /// <param name="input">Input manager receiving the current frame state.</param>
        /// <param name="mouseState">Mouse state to expose for the next frame.</param>
        /// <param name="keyboardState">Keyboard state to expose for the next frame.</param>
        void AdvanceInput(TestInputBackend input, MouseState mouseState, KeyboardState keyboardState) {
            input.SetKeyboardState(keyboardState);
            input.SetMouseState(mouseState);
            input.EarlyUpdate();
        }

        /// <summary>
        /// Captures and completes one input frame so the next capture reports deltas against it.
        /// </summary>
        /// <param name="input">Input manager receiving the mouse state.</param>
        /// <param name="mouseState">Mouse state to capture as the previous frame.</param>
        void CompleteInputFrame(TestInputBackend input, MouseState mouseState) {
            input.SetMouseState(mouseState);
            input.EarlyUpdate();
            input.Update();
        }

        /// <summary>
        /// Executes one controller frame and then finalizes input so the next frame captures fresh deltas.
        /// </summary>
        /// <param name="input">Input manager supplying the current frame state.</param>
        /// <param name="controller">Viewport camera controller under test.</param>
        void CompleteControllerFrame(TestInputBackend input, EditorViewportCameraController controller) {
            controller.Update();
            input.Update();
        }

        /// <summary>
        /// Creates one mouse state with released buttons and a specific wheel value.
        /// </summary>
        /// <param name="x">Pointer X coordinate in window pixels.</param>
        /// <param name="y">Pointer Y coordinate in window pixels.</param>
        /// <param name="scrollWheel">Absolute scroll wheel value for the frame.</param>
        /// <returns>Mouse state used by the controller tests.</returns>
        MouseState CreateMouseState(int x, int y, int scrollWheel) {
            return new MouseState(
                x,
                y,
                scrollWheel,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released);
        }

        /// <summary>
        /// Creates one mouse state with configurable right and middle button states.
        /// </summary>
        /// <param name="x">Pointer X coordinate in window pixels.</param>
        /// <param name="y">Pointer Y coordinate in window pixels.</param>
        /// <param name="scrollWheel">Absolute scroll wheel value for the frame.</param>
        /// <param name="rightButton">Right mouse button state for the frame.</param>
        /// <param name="middleButton">Middle mouse button state for the frame.</param>
        /// <returns>Mouse state used by the controller tests.</returns>
        MouseState CreateMouseState(int x, int y, int scrollWheel, ButtonState rightButton, ButtonState middleButton) {
            return new MouseState(
                x,
                y,
                scrollWheel,
                ButtonState.Released,
                middleButton,
                rightButton,
                ButtonState.Released,
                ButtonState.Released);
        }

        /// <summary>
        /// Creates one mouse state with a configurable middle-button state and released states for the other buttons.
        /// </summary>
        /// <param name="x">Pointer X coordinate in window pixels.</param>
        /// <param name="y">Pointer Y coordinate in window pixels.</param>
        /// <param name="scrollWheel">Absolute scroll wheel value for the frame.</param>
        /// <param name="middleButton">Middle mouse button state for the frame.</param>
        /// <returns>Mouse state used by the controller tests.</returns>
        MouseState CreateMouseState(int x, int y, int scrollWheel, ButtonState middleButton) {
            return CreateMouseState(x, y, scrollWheel, ButtonState.Released, middleButton);
        }

        /// <summary>
        /// Computes the distance between two world positions.
        /// </summary>
        /// <param name="left">First position.</param>
        /// <param name="right">Second position.</param>
        /// <returns>Distance in world units.</returns>
        double Distance(float3 left, float3 right) {
            double deltaX = left.X - right.X;
            double deltaY = left.Y - right.Y;
            double deltaZ = left.Z - right.Z;
            return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
        }
    }
}

