using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.viewport {
    /// <summary>
    /// Verifies navigation-cube click, orbit, scaling, capture, and focus-loss behavior through session input ownership.
    /// </summary>
    public sealed class EditorViewportNavigationCubeInteractionControllerTests : IDisposable {
        readonly Core CoreValue;
        readonly EditorSessionInteractionServices InteractionServicesValue;
        readonly EditorEntity CameraEntityValue;
        readonly EditorViewportCameraComponent CameraValue;
        readonly EditorViewportCameraController CameraControllerValue;
        readonly EditorViewportNavigationController NavigationControllerValue;
        readonly EditorViewportNavigationCubeInteractionController InteractionControllerValue;
        readonly int2 ViewportPosition = new int2(100, 50);
        readonly int2 ContentSize = new int2(800, 600);
        readonly int2 CubeCenter;

        /// <summary>
        /// Initializes one session-owned camera and navigation-cube interaction controller.
        /// </summary>
        public EditorViewportNavigationCubeInteractionControllerTests() {
            CoreValue = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            CoreValue.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
            InteractionServicesValue = new EditorSessionInteractionServices();
            CameraEntityValue = new EditorEntity(CoreValue, InteractionServicesValue) {
                InternalEntity = true,
                Position = new float3(0f, 0f, 10f),
                Orientation = float4.Identity
            };
            CameraValue = new EditorViewportCameraComponent { Viewport = new float4(100f, 50f, 800f, 600f) };
            CameraEntityValue.AddComponent(CameraValue);
            CameraControllerValue = new EditorViewportCameraController(CameraValue, CoreValue.Input);
            CameraEntityValue.AddComponent(CameraControllerValue);
            CameraControllerValue.SetViewPose(float3.Zero, float4.Identity, 10.0);
            NavigationControllerValue = new EditorViewportNavigationController(CameraControllerValue);
            InteractionControllerValue = new EditorViewportNavigationCubeInteractionController(
                NavigationControllerValue,
                InteractionServicesValue.InputCapture);
            InteractionControllerValue.Resize(ViewportPosition, ContentSize, 1f);
            int2 cubePosition = InteractionControllerValue.CubeScreenPosition;
            CubeCenter = new int2(cubePosition.X + 48, cubePosition.Y + 48);
        }

        /// <summary>
        /// Releases pointer blockers and editor session state created by one interaction test.
        /// </summary>
        public void Dispose() {
            InteractionControllerValue.Dispose();
            InteractionServicesValue.Dispose();
            CoreValue.Dispose();
        }

        /// <summary>
        /// Ensures movement below four logical pixels remains a click and selects the face without scene input.
        /// </summary>
        [Fact]
        public void UpdatePointer_MovementBelowFourLogicalPixels_ClicksTargetAndBlocksSceneAndGizmos() {
            int2 facePointer = Add(CubeCenter, new int2(20, 0));
            UpdatePointer(facePointer, true, true, true);
            Assert.True(InteractionControllerValue.IsPointerCaptured);
            Assert.True(InteractionServicesValue.InputCapture.IsPointerBlocked(Add(ViewportPosition, new int2(20, 20))));

            int2 releasePosition = Add(facePointer, new int2(3, 0));
            UpdatePointer(releasePosition, false, true, true);
            Assert.False(InteractionControllerValue.IsOrbiting);
            UpdatePointer(releasePosition, false, false, true);

            Assert.False(InteractionControllerValue.IsPointerCaptured);
            Assert.Equal(CameraProjectionMode.Orthographic, CameraValue.ProjectionMode);
            Assert.Equal(float4.Identity, CameraEntityValue.Orientation);
            Assert.False(InteractionServicesValue.InputCapture.IsPointerBlocked(Add(ViewportPosition, new int2(20, 20))));
        }

        /// <summary>Ensures a center press released outside the center hotspot does not toggle projection.</summary>
        [Fact]
        public void UpdatePointer_CenterPressReleasedOutsideCube_DoesNotToggleProjection() {
            int2 center = GetCubeCenter();
            float4 originalOrientation = CameraEntityValue.Orientation;
            float3 originalPivot = CameraControllerValue.GetOrbitTarget();
            UpdatePointer(center, true, true, true);

            UpdatePointer(Add(ViewportPosition, new int2(30, 30)), false, false, true);

            Assert.Equal(CameraProjectionMode.Perspective, CameraValue.ProjectionMode);
            Assert.Equal(originalOrientation, CameraEntityValue.Orientation);
            Assert.Equal(originalPivot, CameraControllerValue.GetOrbitTarget());
            Assert.False(InteractionControllerValue.IsPointerCaptured);
        }

        /// <summary>Ensures a face press released in the center hotspot is neither a face selection nor a toggle.</summary>
        [Fact]
        public void UpdatePointer_FacePressReleasedInCenter_DoesNotChangeNavigation() {
            int2 facePointer = Add(GetCubeCenter(), new int2(20, 0));
            int2 center = GetCubeCenter();
            float4 originalOrientation = CameraEntityValue.Orientation;
            float3 originalPivot = CameraControllerValue.GetOrbitTarget();
            UpdatePointer(facePointer, true, true, true);

            UpdatePointer(center, false, false, true);

            Assert.Equal(CameraProjectionMode.Perspective, CameraValue.ProjectionMode);
            Assert.Equal(originalOrientation, CameraEntityValue.Orientation);
            Assert.Equal(originalPivot, CameraControllerValue.GetOrbitTarget());
        }

        /// <summary>
        /// Ensures movement at the four-pixel boundary starts an orbit and capture remains active outside the cube.
        /// </summary>
        [Fact]
        public void UpdatePointer_AtFourLogicalPixels_OrbitsAndCapturesUntilReleaseOutsideCube() {
            UpdatePointer(CubeCenter, true, true, true);
            UpdatePointer(Add(CubeCenter, new int2(4, 0)), false, true, true);

            Assert.True(InteractionControllerValue.IsOrbiting);
            Assert.NotEqual(float4.Identity, CameraEntityValue.Orientation);
            Assert.Equal(CameraProjectionMode.Perspective, CameraValue.ProjectionMode);
            Assert.True(InteractionServicesValue.InputCapture.IsPointerBlocked(Add(ViewportPosition, new int2(20, 20))));

            int2 releaseOutsideCube = Add(ViewportPosition, new int2(24, 24));
            UpdatePointer(releaseOutsideCube, false, false, true);

            Assert.False(InteractionControllerValue.IsPointerCaptured);
            Assert.Equal(CameraProjectionMode.Perspective, CameraValue.ProjectionMode);
            Assert.False(InteractionServicesValue.InputCapture.IsPointerBlocked(releaseOutsideCube));
            Assert.True(InteractionControllerValue.IsVisible);
        }

        /// <summary>
        /// Ensures a projection-control click preserves orientation and apparent scale at the orbit pivot.
        /// </summary>
        [Fact]
        public void UpdatePointer_ProjectionControlClick_TogglesModeWithoutChangingPoseOrScale() {
            int2 controlPosition = InteractionControllerValue.ProjectionControlScreenPosition;
            int2 controlSize = InteractionControllerValue.ProjectionControlScreenSize;
            int2 controlCenter = new int2(controlPosition.X + (controlSize.X / 2), controlPosition.Y + (controlSize.Y / 2));
            float4 originalOrientation = CameraEntityValue.Orientation;
            float3 originalPosition = CameraEntityValue.Position;
            double perspectiveScale = CameraProjectionUtils.GetWorldUnitsPerPixel(CameraValue, 10.0, CameraValue.Viewport.W);

            UpdatePointer(controlCenter, true, true, true);
            UpdatePointer(controlCenter, false, false, true);

            double orthographicScale = CameraProjectionUtils.GetWorldUnitsPerPixel(CameraValue, 10.0, CameraValue.Viewport.W);
            Assert.Equal(CameraProjectionMode.Orthographic, CameraValue.ProjectionMode);
            Assert.Equal(originalOrientation, CameraEntityValue.Orientation);
            Assert.Equal(originalPosition, CameraEntityValue.Position);
            Assert.Equal(perspectiveScale, orthographicScale, 5);
        }

        /// <summary>
        /// Ensures the cube center toggles projection in both directions without changing the current view pose.
        /// </summary>
        /// <param name="uiScale">UI scale used to derive the cube center in device pixels.</param>
        /// <param name="isDiagonal">True when the camera begins at a diagonal orientation.</param>
        [Theory]
        [InlineData(1f, false)]
        [InlineData(2f, false)]
        [InlineData(1f, true)]
        [InlineData(2f, true)]
        public void UpdatePointer_CenterClick_TogglesBothProjectionModesWithoutChangingPose(float uiScale, bool isDiagonal) {
            if (isDiagonal) {
                NavigationControllerValue.SelectTarget(new EditorViewportNavigationTarget(1, 1, 1));
                NavigationControllerValue.Advance(1.0);
            }
            InteractionControllerValue.Resize(ViewportPosition, ContentSize, uiScale);
            int2 center = GetCubeCenter();
            float4 originalOrientation = CameraEntityValue.Orientation;
            float3 originalPosition = CameraEntityValue.Position;
            float3 originalPivot = CameraControllerValue.GetOrbitTarget();

            UpdatePointer(center, true, true, true);
            UpdatePointer(center, false, false, true);

            Assert.Equal(CameraProjectionMode.Orthographic, CameraValue.ProjectionMode);
            Assert.Equal(originalOrientation, CameraEntityValue.Orientation);
            Assert.Equal(originalPosition, CameraEntityValue.Position);
            Assert.Equal(originalPivot, CameraControllerValue.GetOrbitTarget());

            UpdatePointer(center, true, true, true);
            UpdatePointer(center, false, false, true);

            Assert.Equal(CameraProjectionMode.Perspective, CameraValue.ProjectionMode);
            Assert.Equal(originalOrientation, CameraEntityValue.Orientation);
            Assert.Equal(originalPosition, CameraEntityValue.Position);
            Assert.Equal(originalPivot, CameraControllerValue.GetOrbitTarget());
        }

        /// <summary>
        /// Ensures device bounds honor UI scale and the projection row hides when the content area cannot fit it.
        /// </summary>
        [Fact]
        public void Resize_ScaledAndConstrainedContent_UsesScaledCubeAndSuppressesProjectionRow() {
            InteractionControllerValue.Resize(new int2(40, 60), new int2(1200, 900), 2f);

            Assert.True(InteractionControllerValue.IsVisible);
            Assert.True(InteractionControllerValue.IsProjectionControlVisible);
            Assert.Equal(new int2(192, 192), InteractionControllerValue.CubeScreenSize);
            Assert.Equal(new int2(144, 48), InteractionControllerValue.ProjectionControlScreenSize);
            Assert.Equal(new int2(40 + 1200 - 16 - 192, 60 + 16), InteractionControllerValue.CubeScreenPosition);

            InteractionControllerValue.Resize(ViewportPosition, new int2(300, 130), 1f);
            Assert.True(InteractionControllerValue.IsVisible);
            Assert.False(InteractionControllerValue.IsProjectionControlVisible);
            Assert.False(InteractionServicesValue.InputCapture.IsPointerBlocked(
                Add(InteractionControllerValue.ProjectionControlScreenPosition, new int2(2, 2))));

            InteractionControllerValue.Resize(ViewportPosition, new int2(111, 111), 1f);
            Assert.False(InteractionControllerValue.IsVisible);
            Assert.False(InteractionControllerValue.IsProjectionControlVisible);
        }

        /// <summary>
        /// Ensures repeated identical layout updates do not interrupt a captured pointer gesture.
        /// </summary>
        [Fact]
        public void Resize_WhenLayoutIsUnchanged_PreservesActivePointerCapture() {
            UpdatePointer(CubeCenter, true, true, true);
            Assert.True(InteractionControllerValue.IsPointerCaptured);

            InteractionControllerValue.Resize(ViewportPosition, ContentSize, 1f);
            InteractionControllerValue.Resize(ViewportPosition, ContentSize, 1f);

            Assert.True(InteractionControllerValue.IsPointerCaptured);
            Assert.True(InteractionServicesValue.InputCapture.IsPointerBlocked(Add(ViewportPosition, new int2(20, 20))));
        }

        /// <summary>
        /// Ensures foreground focus loss cancels a held cube gesture without clicking or leaving viewport capture behind.
        /// </summary>
        [Fact]
        public void UpdatePointer_ForegroundFocusLost_CancelsPendingGestureAndCapture() {
            UpdatePointer(CubeCenter, true, true, true);
            int2 scenePoint = Add(ViewportPosition, new int2(20, 20));
            Assert.True(InteractionServicesValue.InputCapture.IsPointerBlocked(scenePoint));

            UpdatePointer(scenePoint, false, true, false);

            Assert.False(InteractionControllerValue.IsPointerCaptured);
            Assert.False(InteractionServicesValue.InputCapture.IsPointerBlocked(scenePoint));
            Assert.Equal(CameraProjectionMode.Perspective, CameraValue.ProjectionMode);
            Assert.Equal(float4.Identity, CameraEntityValue.Orientation);
        }

        /// <summary>
        /// Ensures disposal during a held gesture clears both stationary and full-viewport input blockers.
        /// </summary>
        [Fact]
        public void Dispose_DuringCapturedGesture_RemovesAllCubeBlockers() {
            UpdatePointer(CubeCenter, true, true, true);
            Assert.True(InteractionServicesValue.InputCapture.IsPointerBlocked(CubeCenter));
            Assert.True(InteractionServicesValue.InputCapture.IsPointerBlocked(Add(ViewportPosition, new int2(20, 20))));

            InteractionControllerValue.Dispose();

            Assert.False(InteractionServicesValue.InputCapture.IsPointerBlocked(CubeCenter));
            Assert.False(InteractionServicesValue.InputCapture.IsPointerBlocked(Add(ViewportPosition, new int2(20, 20))));
            Assert.Throws<ObjectDisposedException>(() => InteractionControllerValue.Resize(ViewportPosition, ContentSize, 1f));
        }

        /// <summary>
        /// Advances the controller with one deterministic input sample.
        /// </summary>
        /// <param name="pointerPosition">Window-space pointer position.</param>
        /// <param name="wasPrimaryPressed">True when the primary button went down during the frame.</param>
        /// <param name="isPrimaryPressed">True while the primary button remains down.</param>
        /// <param name="hasForegroundFocus">True while the host window retains foreground focus.</param>
        /// <summary>
        /// Adds two integer points by component for APIs that intentionally do not define point arithmetic operators.
        /// </summary>
        /// <param name="left">First point.</param>
        /// <param name="right">Second point or displacement.</param>
        /// <returns>Component-wise sum.</returns>
        static int2 Add(int2 left, int2 right) {
            return new int2(left.X + right.X, left.Y + right.Y);
        }

        /// <summary>Returns the center of the current device-pixel cube bounds.</summary>
        /// <returns>Window-space cube center.</returns>
        int2 GetCubeCenter() {
            int2 cubePosition = InteractionControllerValue.CubeScreenPosition;
            int2 cubeSize = InteractionControllerValue.CubeScreenSize;
            return new int2(cubePosition.X + (cubeSize.X / 2), cubePosition.Y + (cubeSize.Y / 2));
        }

        void UpdatePointer(int2 pointerPosition, bool wasPrimaryPressed, bool isPrimaryPressed, bool hasForegroundFocus) {
            InteractionControllerValue.UpdatePointer(
                pointerPosition,
                wasPrimaryPressed,
                isPrimaryPressed,
                hasForegroundFocus,
                CameraEntityValue.Orientation,
                0.016);
        }
    }
}
