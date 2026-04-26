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
        }

        /// <summary>
        /// Ensures scrolling upward over the viewport moves the camera forward without requiring a mouse button hold.
        /// </summary>
        [Fact]
        public void Update_WhenWheelScrollsUpInsideViewport_MovesCameraForward() {
            TestInputManager input = InitializeCore();
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
            TestInputManager input = InitializeCore();
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
            TestInputManager input = InitializeCore();
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
            TestInputManager input = InitializeCore();
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
        /// Initializes core services with configurable input for camera-controller tests.
        /// </summary>
        /// <returns>Input manager used by the current test.</returns>
        TestInputManager InitializeCore() {
            EditorInputCaptureService.Reset();
            Core core = new Core();
            TestInputManager input = new TestInputManager();
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
        void AdvanceInput(TestInputManager input, MouseState mouseState) {
            input.SetMouseState(mouseState);
            input.EarlyUpdate();
        }

        /// <summary>
        /// Captures and completes one input frame so the next capture reports deltas against it.
        /// </summary>
        /// <param name="input">Input manager receiving the mouse state.</param>
        /// <param name="mouseState">Mouse state to capture as the previous frame.</param>
        void CompleteInputFrame(TestInputManager input, MouseState mouseState) {
            input.SetMouseState(mouseState);
            input.EarlyUpdate();
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
    }
}
