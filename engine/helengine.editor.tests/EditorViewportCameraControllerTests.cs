using helengine;
using helengine.editor.tests.testing;
using helengine.ui;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies viewport camera movement paths that are driven by direct mouse input.
    /// </summary>
    public class EditorViewportCameraControllerTests : IDisposable {
        /// <summary>F scales perspective navigation to a container's children without changing orthographic speeds.</summary>
        [Theory]
        [InlineData(CameraProjectionMode.Perspective)]
        [InlineData(CameraProjectionMode.Orthographic)]
        public void FocusSelection_MenuHierarchyUpdatesPerspectiveSpeedsOnly(CameraProjectionMode mode) {
            EditorEntity cameraEntity = new EditorEntity(CoreValue, InteractionServices);
            EditorViewportCameraComponent camera = new EditorViewportCameraComponent {
                Viewport = new float4(100, 100, 800, 600), ProjectionMode = mode
            };
            cameraEntity.AddComponent(camera);
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);
            EditorEntity root = new EditorEntity(CoreValue, InteractionServices);
            root.AddChild(CreateSpriteEntity(new int2(2000, 1200)));
            InteractionServices.Selection.SetSelectedEntity(root);
            EditorViewportSelectionFramingService framing = new EditorViewportSelectionFramingService();
            framing.FocusSelection(camera, controller, root);
            if (mode == CameraProjectionMode.Perspective) {
                Assert.True(controller.MoveSpeed >= 8f);
                Assert.True(controller.WheelZoomSpeed >= 50);
            } else {
                Assert.Equal(EditorViewportCameraController.DefaultMoveSpeed, controller.MoveSpeed);
                Assert.Equal(EditorViewportCameraController.DefaultWheelZoomSpeed, controller.WheelZoomSpeed);
            }
            float moveSpeed = controller.MoveSpeed;
            double zoomSpeed = controller.WheelZoomSpeed;
            CompleteInputFrame(InputValue, CreateMouseState(150, 150, 0));
            AdvanceInput(InputValue, CreateMouseState(150, 150, 120));
            float3 previousPosition = cameraEntity.Position;
            controller.Update();
            Assert.Equal(moveSpeed, controller.MoveSpeed);
            Assert.Equal(zoomSpeed, controller.WheelZoomSpeed);
            if (mode == CameraProjectionMode.Perspective) {
                Assert.InRange(previousPosition.Z - cameraEntity.Position.Z, (float)zoomSpeed - 0.01f, (float)zoomSpeed + 0.01f);
            } else {
                Assert.Equal(previousPosition, cameraEntity.Position);
            }
        }

        /// <summary>Rejects an unrepresentable camera position before changing its orientation, position, or orbit state.</summary>
        [Fact]
        public void SetViewPose_WhenPositionWouldOverflow_PreservesPreviousCameraState() {
            EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);
            controller.SetViewPose(new float3(1f, 2f, 3f), float4.Identity, 10.0);
            float3 originalPosition = cameraEntity.Position;
            float4 originalOrientation = cameraEntity.Orientation;
            float3 originalPivot = controller.GetOrbitTarget();
            float4 orientation = float4.Identity;
            float3 axis = new float3(1f, 0f, 0f);
            float4.CreateFromAxisAngle(ref axis, 0.5f, out orientation);

            Assert.Throws<ArgumentOutOfRangeException>(() => controller.SetViewPose(float3.Zero, orientation, 1e100));

            Assert.Equal(originalPosition, cameraEntity.Position);
            Assert.Equal(originalOrientation, cameraEntity.Orientation);
            Assert.Equal(originalPivot, controller.GetOrbitTarget());
        }

        readonly helengine.editor.EditorSessionInteractionServices InteractionServices = new helengine.editor.EditorSessionInteractionServices();
        readonly Core CoreValue;
        readonly TestInputBackend InputValue;

        public EditorViewportCameraControllerTests() {
            CoreValue = new Core(new CoreInitializationOptions {
                ContentStreamSource = new FakeContentStreamSource()
            });
            InputValue = new TestInputBackend();
            CoreValue.Initialize(null, new TestRenderManager2D(), InputValue, new PlatformInfo("test", "test-version"));
            CoreValue.SessionInteractionGraph = InteractionServices;
        }
        /// <summary>
        /// Clears static viewport input blockers after each camera-controller test.
        /// </summary>
        public void Dispose() {
            InteractionServices.Selection.ClearSelection();
            CoreValue.Dispose();
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
        /// Ensures the editor's normal core update loop invokes one registered viewport controller while right-click navigation is active.
        /// </summary>
        [Fact]
        public void Update_WhenRegisteredWithEditorCoreAndRightMouseNavigationIsActive_MovesCamera() {
            TestInputBackend input = new TestInputBackend();
            EditorCore editorCore = new EditorCore(new Project {
                Name = "Viewport Camera Controller Tests",
                Path = AppContext.BaseDirectory
            });
            editorCore.Initialize(
                null,
                new TestRenderManager2D(),
                input,
                new PlatformInfo("test", "test-version"),
                new CoreInitializationOptions {
                    ContentStreamSource = new FakeContentStreamSource()
                });
            editorCore.SessionInteractionServices = InteractionServices;
            editorCore.SessionInteractionGraph = InteractionServices;
            editorCore.InputSystem.SetMouseClientBounds(new int2(500, 400));
            EditorEntity cameraEntity = CreateCameraEntity(editorCore, out CameraComponent camera);
            CreateController(cameraEntity, camera, editorCore);
            cameraEntity.InitializeHierarchy();

            try {
                input.SetMouseState(CreateMouseState(150, 150, 0));
                editorCore.Update();
                input.SetKeyboardState(new KeyboardState(Keys.W));
                input.SetMouseState(CreateMouseState(150, 150, 0, ButtonState.Pressed, ButtonState.Released));

                editorCore.Update();

                Assert.True(cameraEntity.Position.Z < 0f);
            } finally {
                editorCore.Dispose();
            }
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
        /// Ensures switching projection modes preserves the orbit pivot, camera orientation, and apparent scale.
        /// </summary>
        [Fact]
        public void SetProjectionMode_RoundTrip_PreservesPivotOrientationAndScale() {
            EditorEntity cameraEntity = CreateEditorCameraEntity(out EditorViewportCameraComponent camera);
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);
            float3 pivot = new float3(3f, -2f, 5f);
            float4 orientation;
            float4.CreateFromYawPitchRoll(0.35f, -0.2f, 0f, out orientation);
            const double distance = 18.0;
            controller.SetViewPose(pivot, orientation, distance);
            double initialScale = CameraProjectionUtils.GetWorldUnitsPerPixel(camera, distance, camera.Viewport.W);

            controller.SetProjectionMode(CameraProjectionMode.Orthographic);
            double orthographicScale = CameraProjectionUtils.GetWorldUnitsPerPixel(camera, distance, camera.Viewport.W);
            float3 orthographicPosition = cameraEntity.Position;
            controller.SetProjectionMode(CameraProjectionMode.Perspective);
            double restoredScale = CameraProjectionUtils.GetWorldUnitsPerPixel(camera, distance, camera.Viewport.W);

            Assert.Equal(pivot, controller.GetOrbitTarget());
            Assert.Equal(orientation, cameraEntity.Orientation);
            Assert.Equal(initialScale, orthographicScale, 5);
            Assert.Equal(initialScale, restoredScale, 5);
            Assert.Equal(orthographicPosition, cameraEntity.Position);
        }

        /// <summary>
        /// Ensures 2D pan tracks pointer pixels at the current zoom without multiplying by selection size, fly speed, or viewport aspect.
        /// </summary>
        [Theory]
        [InlineData(1000, 1000, 20, 0, false, 1)]
        [InlineData(1600, 900, 1200, 2000, false, 1)]
        [InlineData(600, 1200, 1200, 2000, false, 1)]
        [InlineData(1600, 900, 20, 8, false, 1)]
        [InlineData(1600, 900, 20, 2000, true, 1)]
        [InlineData(1600, 900, 20, 2000, false, 2)]
        public void Update_WhenOrthographicMiddleMousePans_UsesWorldUnitsPerPixel(int width, int height, float span, int selectionSize, bool manualSpeed, double panRatio) {
            TestInputBackend input = InitializeCore();
            EditorEntity cameraEntity = CreateEditorCameraEntity(out EditorViewportCameraComponent camera);
            cameraEntity.Position = new float3(0f, 0f, 10f);
            camera.Viewport = new float4(0f, 0f, width, height);
            camera.ProjectionMode = CameraProjectionMode.Orthographic;
            camera.OrthographicVerticalSpan = span;
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);
            controller.PanSpeed = EditorViewportCameraController.DefaultPanSpeed * panRatio;
            if (selectionSize > 0) {
                InteractionServices.Selection.SetSelectedEntity(CreateSpriteEntity(new int2(selectionSize, selectionSize)));
            }
            if (manualSpeed) {
                controller.SpeedMode = EditorViewportCameraSpeedMode.ManualOverride;
                controller.ManualSpeedOverride = 100;
            }
            controller.SetOrbitTarget(float3.Zero);

            CompleteInputFrame(input, CreateMouseState(200, 150, 0));
            AdvanceInput(input, CreateMouseState(200, 150, 0, ButtonState.Released, ButtonState.Pressed));
            CompleteControllerFrame(input, controller);
            AdvanceInput(input, CreateMouseState(210, 160, 0, ButtonState.Released, ButtonState.Pressed));
            controller.Update();

            double worldUnitsPerPixel = span / height;
            Assert.Equal(-10 * panRatio, cameraEntity.Position.X / worldUnitsPerPixel, 3);
            Assert.Equal(10 * panRatio, cameraEntity.Position.Y / worldUnitsPerPixel, 3);
            if (selectionSize == 0) {
                Assert.Equal(cameraEntity.Position.X, controller.GetOrbitTarget().X, 4);
                Assert.Equal(cameraEntity.Position.Y, controller.GetOrbitTarget().Y, 4);
            } else {
                Assert.Equal(float3.Zero, InteractionServices.Selection.SelectedEntity.Position);
            }
            Assert.Equal(10f, cameraEntity.Position.Z);
        }

        /// <summary>Measures actual projected pixel movement when panning 2D content in perspective, including off-axis selection and rolled cameras.</summary>
        [Theory]
        [InlineData(100, 0, false, 0, false)]
        [InlineData(100, 2000, false, 0, false)]
        [InlineData(5000, 2000, false, 20000, false)]
        [InlineData(100, 2000, true, 0, false)]
        [InlineData(5000, 2000, true, 20000, false)]
        [InlineData(100, 2000, false, 0, true)]
        public void Update_WhenPerspectiveMiddleMousePans_ContentTracksPointerPixels(float depth, int selectionSize, bool manualSpeed, float selectionOffset, bool rolled) {
            TestInputBackend input = InitializeCore();
            EditorEntity cameraEntity = CreateEditorCameraEntity(out EditorViewportCameraComponent camera);
            cameraEntity.Position = new float3(0, 0, depth);
            camera.Viewport = new float4(0, 0, 1600, 900);
            if (rolled) {
                float4.CreateFromAxisAngle(new float3(0, 0, 1), 0.7f, out float4 orientation);
                cameraEntity.Orientation = orientation;
            }
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);
            controller.SetOrbitTarget(float3.Zero);
            if (selectionSize > 0) {
                Entity selected = CreateSpriteEntity(new int2(selectionSize, selectionSize));
                selected.Position = new float3(selectionOffset, 0, 0);
                InteractionServices.Selection.SetSelectedEntity(selected);
            }
            if (manualSpeed) {
                controller.SpeedMode = EditorViewportCameraSpeedMode.ManualOverride;
                controller.ManualSpeedOverride = 100;
            }
            CompleteInputFrame(input, CreateMouseState(200, 150, 0));
            AdvanceInput(input, CreateMouseState(200, 150, 0, ButtonState.Released, ButtonState.Pressed));
            CompleteControllerFrame(input, controller);
            float2 before = ProjectToViewport(camera, float3.Zero);
            AdvanceInput(input, CreateMouseState(210, 160, 0, ButtonState.Released, ButtonState.Pressed));
            CompleteControllerFrame(input, controller);
            float2 after = ProjectToViewport(camera, float3.Zero);
            Assert.Equal(10, after.X - before.X, 2);
            Assert.Equal(10, after.Y - before.Y, 2);
            AdvanceInput(input, CreateMouseState(211, 161, 0, ButtonState.Released, ButtonState.Pressed));
            controller.Update();
            float2 next = ProjectToViewport(camera, float3.Zero);
            Assert.Equal(1, next.X - after.X, 2);
            Assert.Equal(1, next.Y - after.Y, 2);
        }

        /// <summary>Projects a fixed world point through the same view and projection matrices used by the renderer.</summary>
        /// <param name="camera">Camera after the current input frame.</param>
        /// <param name="point">Stationary scene point whose apparent movement is measured.</param>
        /// <returns>Pixel position in the camera viewport.</returns>
        static float2 ProjectToViewport(CameraComponent camera, float3 point) {
            float3 position = camera.Parent.Position;
            float3 target = position + float4.RotateVector(new float3(0, 0, -1), camera.Parent.Orientation);
            float3 up = float4.RotateVector(new float3(0, 1, 0), camera.Parent.Orientation);
            float4x4.CreateLookAt(ref position, ref target, ref up, out float4x4 view);
            float4x4 projection = CameraProjectionUtils.CreateProjection(camera, camera.Viewport.Z / camera.Viewport.W);
            float4x4.Multiply(ref view, ref projection, out float4x4 matrix);
            double clipX = point.X * matrix.M11 + point.Y * matrix.M21 + point.Z * matrix.M31 + matrix.M41;
            double clipY = point.X * matrix.M12 + point.Y * matrix.M22 + point.Z * matrix.M32 + matrix.M42;
            double clipW = point.X * matrix.M14 + point.Y * matrix.M24 + point.Z * matrix.M34 + matrix.M44;
            return new float2((float)(camera.Viewport.X + (clipX / clipW + 1) * camera.Viewport.Z / 2),
                (float)(camera.Viewport.Y + (1 - clipY / clipW) * camera.Viewport.W / 2));
        }

        /// <summary>
        /// Ensures orthographic wheel zoom changes the visible span while preserving camera pose and orbit pivot.
        /// </summary>
        [Fact]
        public void Update_WhenOrthographicWheelScrolls_ChangesSpanWithoutMovingCamera() {
            TestInputBackend input = InitializeCore();
            EditorEntity cameraEntity = CreateEditorCameraEntity(out EditorViewportCameraComponent camera);
            cameraEntity.Position = new float3(0f, 0f, 10f);
            camera.ProjectionMode = CameraProjectionMode.Orthographic;
            camera.OrthographicVerticalSpan = 20f;
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);
            controller.SetOrbitTarget(float3.Zero);
            float3 initialPosition = cameraEntity.Position;

            CompleteInputFrame(input, CreateMouseState(150, 150, 0));
            AdvanceInput(input, CreateMouseState(150, 150, 120));
            controller.Update();

            Assert.True(camera.OrthographicVerticalSpan < 20f);
            Assert.Equal(initialPosition, cameraEntity.Position);
            Assert.Equal(float3.Zero, controller.GetOrbitTarget());
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
                InteractionServices.InputCapture.SetBlocker(blockerOwner, new int2(120, 120), new int2(80, 80));
                CompleteInputFrame(input, CreateMouseState(150, 150, 0));
                AdvanceInput(input, CreateMouseState(150, 150, 120));

                controller.Update();

                Assert.Equal(float3.Zero, cameraEntity.Position);
            } finally {
                InteractionServices.InputCapture.ClearBlocker(blockerOwner);
            }
        }

        /// <summary>
        /// Ensures auto speed mode scales movement, pan, and zoom upward for large selections.
        /// </summary>
        [Fact]
        public void UpdateEffectiveSpeeds_WhenAutoModeAndLargeSelection_IncreasesMovementPanAndZoom() {
            EditorViewportSelectionFramingService selectionBounds = new EditorViewportSelectionFramingService();
            EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);
            Entity selectedViewportEntity = CreateFixedViewportEntity(new int2(2000, 1200));
            InteractionServices.Selection.SetSelectedEntity(selectedViewportEntity);

            controller.UpdateEffectiveSpeedsForTest(selectionBounds);

            Assert.True(controller.MoveSpeed >= 8f);
            Assert.True(controller.PanSpeed >= 0.5);
            Assert.True(controller.WheelZoomSpeed >= 50.0);
        }

        /// <summary>
        /// Ensures auto speed mode scales movement, pan, and zoom downward for tiny selections.
        /// </summary>
        [Fact]
        public void UpdateEffectiveSpeeds_WhenAutoModeAndTinySelection_DecreasesMovementPanAndZoom() {
            EditorViewportSelectionFramingService selectionBounds = new EditorViewportSelectionFramingService();
            EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);
            Entity spriteEntity = CreateSpriteEntity(new int2(8, 8));
            InteractionServices.Selection.SetSelectedEntity(spriteEntity);

            controller.UpdateEffectiveSpeedsForTest(selectionBounds);

            Assert.True(controller.MoveSpeed < EditorViewportCameraController.DefaultMoveSpeed);
            Assert.True(controller.PanSpeed < EditorViewportCameraController.DefaultPanSpeed);
            Assert.True(controller.WheelZoomSpeed < EditorViewportCameraController.DefaultWheelZoomSpeed);
        }

        /// <summary>
        /// Ensures auto speed mode does not slow unit-size mesh navigation below a usable fraction of the configured baseline.
        /// </summary>
        [Fact]
        public void UpdateEffectiveSpeeds_WhenAutoModeAndUnitCubeSelection_KeepsUsableMovementPanAndZoomFloor() {
            InitializeCore();
            EditorViewportSelectionFramingService selectionBounds = new EditorViewportSelectionFramingService();
            EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);
            Entity meshEntity = CreateMeshEntity(new float3(0f, 0f, 0f), new float3(1f, 1f, 1f), float3.One);
            InteractionServices.Selection.SetSelectedEntity(meshEntity);

            controller.UpdateEffectiveSpeedsForTest(selectionBounds);

            double expectedMinimumMoveSpeed = EditorViewportCameraController.DefaultMoveSpeed * 0.5;
            double expectedMinimumPanSpeed = EditorViewportCameraController.DefaultPanSpeed * 0.5;
            double expectedMinimumWheelZoomSpeed = EditorViewportCameraController.DefaultWheelZoomSpeed * 0.5;

            Assert.True(controller.MoveSpeed >= expectedMinimumMoveSpeed);
            Assert.True(controller.PanSpeed >= expectedMinimumPanSpeed);
            Assert.True(controller.WheelZoomSpeed >= expectedMinimumWheelZoomSpeed);
        }

        /// <summary>
        /// Ensures manual speed mode ignores selection size and uses the authored override value instead.
        /// </summary>
        [Fact]
        public void UpdateEffectiveSpeeds_WhenManualMode_IgnoresSelectionExtent() {
            EditorViewportSelectionFramingService selectionBounds = new EditorViewportSelectionFramingService();
            EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);
            controller.SpeedMode = EditorViewportCameraSpeedMode.ManualOverride;
            controller.ManualSpeedOverride = 12.5;
            InteractionServices.Selection.SetSelectedEntity(CreateFixedViewportEntity(new int2(40000, 20000)));

            controller.UpdateEffectiveSpeedsForTest(selectionBounds);

            Assert.Equal(12.5f, controller.MoveSpeed);
            Assert.Equal(12.5 * (EditorViewportCameraController.DefaultPanSpeed / EditorViewportCameraController.DefaultMoveSpeed), controller.PanSpeed, 4);
            Assert.Equal(12.5 * (EditorViewportCameraController.DefaultWheelZoomSpeed / EditorViewportCameraController.DefaultMoveSpeed), controller.WheelZoomSpeed, 4);
        }

        /// <summary>
        /// Ensures unsupported selections fall back to the current default viewport camera speed values.
        /// </summary>
        [Fact]
        public void UpdateEffectiveSpeeds_WhenNoSupportedSelection_FallsBackToDefaults() {
            EditorViewportSelectionFramingService selectionBounds = new EditorViewportSelectionFramingService();
            EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
            EditorViewportCameraController controller = CreateController(cameraEntity, camera);
            Entity unsupportedEntity = new Entity(CoreValue);
            unsupportedEntity.InitComponents();
            unsupportedEntity.InitChildren();
            InteractionServices.Selection.SetSelectedEntity(unsupportedEntity);

            controller.UpdateEffectiveSpeedsForTest(selectionBounds);

            Assert.Equal(EditorViewportCameraController.DefaultMoveSpeed, controller.MoveSpeed);
            Assert.Equal(EditorViewportCameraController.DefaultPanSpeed, controller.PanSpeed);
            Assert.Equal(EditorViewportCameraController.DefaultWheelZoomSpeed, controller.WheelZoomSpeed);
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
            EditorEntity selectedEntity = new EditorEntity(CoreValue, InteractionServices);
            InteractionServices.Selection.SetSelectedEntity(selectedEntity);

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
            EditorEntity selectedEntity = new EditorEntity(CoreValue, InteractionServices);
            InteractionServices.Selection.SetSelectedEntity(selectedEntity);

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
            Assert.True(CoreValue.InputSystem.IsPointerWrapEnabled);

            AdvanceInput(input, CreateMouseState(150, 150, 0, ButtonState.Released, ButtonState.Released));
            CompleteControllerFrame(input, controller);
            Assert.False(CoreValue.InputSystem.IsPointerWrapEnabled);
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
            EditorEntity selectedEntity = new EditorEntity(CoreValue, InteractionServices);
            InteractionServices.Selection.SetSelectedEntity(selectedEntity);

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
            CoreValue.InputSystem.SetMouseClientBounds(new int2(500, 400));
            return InputValue;
        }

        /// <summary>
        /// Creates a camera entity with a deterministic viewport rectangle.
        /// </summary>
        /// <param name="camera">Receives the camera component attached to the entity.</param>
        /// <returns>Camera entity used by the controller under test.</returns>
        EditorEntity CreateCameraEntity(Core ownerCore, out CameraComponent camera) {
            EditorEntity cameraEntity = new EditorEntity(ownerCore, InteractionServices);
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
        EditorEntity CreateCameraEntity(out CameraComponent camera) {
            return CreateCameraEntity(CoreValue, out camera);
        }

        EditorViewportCameraController CreateController(EditorEntity cameraEntity, CameraComponent camera, Core ownerCore = null) {
            EditorViewportCameraController controller = new EditorViewportCameraController(camera, (ownerCore ?? CoreValue).Input);
            cameraEntity.AddComponent(controller);
            return controller;
        }

        /// <summary>
        /// Creates an editor viewport camera whose optional projection state can be changed by the controller.
        /// </summary>
        /// <param name="camera">Receives the editor viewport camera component.</param>
        /// <returns>Entity owning the camera and its transform.</returns>
        EditorEntity CreateEditorCameraEntity(out EditorViewportCameraComponent camera) {
            EditorEntity cameraEntity = new EditorEntity(CoreValue, InteractionServices);
            camera = new EditorViewportCameraComponent {
                Viewport = new float4(100f, 100f, 300f, 200f)
            };
            cameraEntity.AddComponent(camera);
            return cameraEntity;
        }

        /// <summary>
        /// Creates one fixed-size viewport entity for adaptive speed tests.
        /// </summary>
        /// <param name="viewportSize">Fixed viewport size exposed by the authored viewport component.</param>
        /// <returns>Configured viewport entity.</returns>
        Entity CreateFixedViewportEntity(int2 viewportSize) {
            Entity viewportEntity = new Entity(CoreValue);
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = viewportSize
            });
            return viewportEntity;
        }

        /// <summary>
        /// Creates one sprite entity for adaptive speed tests.
        /// </summary>
        /// <param name="size">Authored sprite size.</param>
        /// <returns>Configured sprite entity.</returns>
        Entity CreateSpriteEntity(int2 size) {
            Entity spriteEntity = new Entity(CoreValue);
            spriteEntity.InitComponents();
            spriteEntity.InitChildren();
            spriteEntity.AddComponent(new SpriteComponent {
                Size = size
            });
            return spriteEntity;
        }

        /// <summary>
        /// Creates one mesh entity with deterministic runtime-model bounds and world scale.
        /// </summary>
        /// <param name="boundsMin">Minimum authored runtime-model bounds.</param>
        /// <param name="boundsMax">Maximum authored runtime-model bounds.</param>
        /// <param name="scale">World scale applied by the mesh entity.</param>
        /// <returns>Configured mesh entity.</returns>
        Entity CreateMeshEntity(float3 boundsMin, float3 boundsMax, float3 scale) {
            TestRuntimeModel runtimeModel = new TestRuntimeModel();
            runtimeModel.SetBounds(boundsMin, boundsMax);

            Entity meshEntity = new Entity(CoreValue);
            meshEntity.InitComponents();
            meshEntity.InitChildren();
            meshEntity.LocalScale = scale;
            meshEntity.AddComponent(new MeshComponent {
                Model = runtimeModel
            });
            return meshEntity;
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
