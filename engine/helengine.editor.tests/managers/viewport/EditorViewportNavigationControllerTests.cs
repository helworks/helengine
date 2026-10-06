using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.viewport {
    /// <summary>
    /// Verifies standard-view targets, camera transitions, projection selection, and viewport-local controller state.
    /// </summary>
    public sealed class EditorViewportNavigationControllerTests : IDisposable {
        readonly Core CoreValue;
        readonly TestInputBackend InputValue;

        /// <summary>
        /// Initializes core services used by viewport navigation tests.
        /// </summary>
        public EditorViewportNavigationControllerTests() {
            CoreValue = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            InputValue = new TestInputBackend();
            CoreValue.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), InputValue, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Disposes core services after a navigation test.
        /// </summary>
        public void Dispose() {
            CoreValue.Dispose();
        }

        /// <summary>
        /// Ensures each signed cube direction resolves to its world-axis camera offset and only faces switch to orthographic mode.
        /// </summary>
        /// <param name="x">Target offset along world X.</param>
        /// <param name="y">Target offset along world Y.</param>
        /// <param name="z">Target offset along world Z.</param>
        [Theory]
        [InlineData(-1, -1, -1)]
        [InlineData(-1, -1, 0)]
        [InlineData(-1, -1, 1)]
        [InlineData(-1, 0, -1)]
        [InlineData(-1, 0, 0)]
        [InlineData(-1, 0, 1)]
        [InlineData(-1, 1, -1)]
        [InlineData(-1, 1, 0)]
        [InlineData(-1, 1, 1)]
        [InlineData(0, -1, -1)]
        [InlineData(0, -1, 0)]
        [InlineData(0, -1, 1)]
        [InlineData(0, 0, -1)]
        [InlineData(0, 0, 1)]
        [InlineData(0, 1, -1)]
        [InlineData(0, 1, 0)]
        [InlineData(0, 1, 1)]
        [InlineData(1, -1, -1)]
        [InlineData(1, -1, 0)]
        [InlineData(1, -1, 1)]
        [InlineData(1, 0, -1)]
        [InlineData(1, 0, 0)]
        [InlineData(1, 0, 1)]
        [InlineData(1, 1, -1)]
        [InlineData(1, 1, 0)]
        [InlineData(1, 1, 1)]
        public void SelectTarget_AnyOfTwentySixDirections_AlignsCameraAndChangesProjectionOnlyForFaces(int x, int y, int z) {
            EditorViewportNavigationController navigationController = CreateNavigationStack(out EditorEntity cameraEntity, out EditorViewportCameraComponent camera, out EditorViewportCameraController cameraController);
            float3 pivot = new float3(7f, -2f, 4f);
            const double distance = 16.0;
            cameraController.SetViewPose(pivot, float4.Identity, distance);

            navigationController.SelectTarget(new EditorViewportNavigationTarget(x, y, z));
            navigationController.Advance(EditorViewportNavigationController.TransitionDurationSeconds);

            float3 expectedCameraOffset = Normalize(new float3(x, y, z)) * (float)distance;
            AssertVectorApproximately(expectedCameraOffset, cameraEntity.Position - pivot, 0.0001f);
            AssertVectorApproximately(Normalize(new float3(-x, -y, -z)), float4.RotateVector(new float3(0f, 0f, -1f), cameraEntity.Orientation), 0.0001f);
            Assert.Equal((x != 0 && y == 0 && z == 0) || (x == 0 && y != 0 && z == 0) || (x == 0 && y == 0 && z != 0), camera.ProjectionMode == CameraProjectionMode.Orthographic);
        }

        /// <summary>
        /// Ensures top and bottom views use deterministic world-front camera-up directions.
        /// </summary>
        [Theory]
        [InlineData(0, 1, 0, 0f, 0f, -1f)]
        [InlineData(0, -1, 0, 0f, 0f, 1f)]
        public void SelectTarget_TopAndBottom_UseStableCameraUpBasis(int x, int y, int z, float expectedUpX, float expectedUpY, float expectedUpZ) {
            EditorViewportNavigationController navigationController = CreateNavigationStack(out EditorEntity cameraEntity, out EditorViewportCameraComponent camera, out EditorViewportCameraController cameraController);
            cameraController.SetViewPose(float3.Zero, float4.Identity, 12.0);

            navigationController.SelectTarget(new EditorViewportNavigationTarget(x, y, z));
            navigationController.Advance(EditorViewportNavigationController.TransitionDurationSeconds);

            float3 cameraUp = float4.RotateVector(new float3(0f, 1f, 0f), cameraEntity.Orientation);
            AssertVectorApproximately(new float3(expectedUpX, expectedUpY, expectedUpZ), cameraUp, 0.0001f);
            Assert.Equal(CameraProjectionMode.Orthographic, camera.ProjectionMode);
        }

        /// <summary>
        /// Ensures a standard transition lasts exactly the configured 200 milliseconds and reaches its target.
        /// </summary>
        [Fact]
        public void Advance_AtTransitionDuration_CompletesExactlyAtTargetPose() {
            EditorViewportNavigationController navigationController = CreateNavigationStack(out EditorEntity cameraEntity, out EditorViewportCameraComponent camera, out EditorViewportCameraController cameraController);
            cameraController.SetViewPose(new float3(3f, 5f, -1f), float4.Identity, 14.0);

            navigationController.SelectTarget(new EditorViewportNavigationTarget(1, 0, 0));
            navigationController.Advance(0.1);

            Assert.True(navigationController.IsTransitioning);
            navigationController.Advance(0.1);

            Assert.False(navigationController.IsTransitioning);
            AssertVectorApproximately(new float3(1f, 0f, 0f), Normalize(cameraEntity.Position - cameraController.GetOrbitTarget()), 0.0001f);
            Assert.Equal(CameraProjectionMode.Orthographic, camera.ProjectionMode);
        }

        /// <summary>
        /// Ensures interpolation follows the shortest quaternion arc between equivalent near-pole representations.
        /// </summary>
        [Fact]
        public void Advance_WhenTargetCrossesQuaternionSignBoundary_UsesShortestArc() {
            EditorViewportNavigationController navigationController = CreateNavigationStack(out EditorEntity cameraEntity, out EditorViewportCameraComponent camera, out EditorViewportCameraController cameraController);
            float4 initialOrientation;
            float4.CreateFromYawPitchRoll((float)(-170.0 * Math.PI / 180.0), 0f, 0f, out initialOrientation);
            cameraController.SetViewPose(float3.Zero, initialOrientation, 10.0);

            navigationController.SelectTarget(new EditorViewportNavigationTarget(0, 0, -1));
            navigationController.Advance(EditorViewportNavigationController.TransitionDurationSeconds * 0.5);

            float3 halfwayForward = float4.RotateVector(new float3(0f, 0f, -1f), cameraEntity.Orientation);
            Assert.True(halfwayForward.Z > 0.9f);
            Assert.Equal(CameraProjectionMode.Orthographic, camera.ProjectionMode);
            Assert.True(navigationController.IsTransitioning);
        }

        /// <summary>
        /// Ensures a second target begins its transition from the currently displayed intermediate camera pose.
        /// </summary>
        [Fact]
        public void SelectTarget_DuringTransition_StartsFromDisplayedPose() {
            EditorViewportNavigationController navigationController = CreateNavigationStack(out EditorEntity cameraEntity, out EditorViewportCameraComponent camera, out EditorViewportCameraController cameraController);
            cameraController.SetViewPose(new float3(2f, 3f, 4f), float4.Identity, 20.0);

            navigationController.SelectTarget(new EditorViewportNavigationTarget(1, 0, 0));
            navigationController.Advance(0.05);
            float4 displayedOrientation = cameraEntity.Orientation;
            float3 displayedOffset = cameraEntity.Position - new float3(2f, 3f, 4f);
            navigationController.SelectTarget(new EditorViewportNavigationTarget(0, 1, 0));
            navigationController.Advance(0.0);

            Assert.True(navigationController.IsTransitioning);
            AssertQuaternionOrientationApproximately(displayedOrientation, cameraEntity.Orientation, 0.0001f);
            AssertVectorApproximately(displayedOffset, cameraEntity.Position - new float3(2f, 3f, 4f), 0.0001f);
            Assert.Equal(CameraProjectionMode.Orthographic, camera.ProjectionMode);
        }

        /// <summary>
        /// Ensures pose changes performed outside the navigation controller cancel its transition without overwriting the new pose.
        /// </summary>
        [Fact]
        public void Advance_WhenExternalNavigationChangesPose_CancelsTransitionAndPreservesExternalPose() {
            EditorViewportNavigationController navigationController = CreateNavigationStack(out EditorEntity cameraEntity, out EditorViewportCameraComponent camera, out EditorViewportCameraController cameraController);
            cameraController.SetViewPose(float3.Zero, float4.Identity, 10.0);

            navigationController.SelectTarget(new EditorViewportNavigationTarget(0, 0, -1));
            navigationController.Advance(0.05);
            cameraEntity.Position += new float3(4f, 0f, 0f);
            float3 externalPosition = cameraEntity.Position;
            navigationController.Advance(0.01);

            Assert.False(navigationController.IsTransitioning);
            Assert.Equal(externalPosition, cameraEntity.Position);
            Assert.Equal(CameraProjectionMode.Orthographic, camera.ProjectionMode);
        }

        /// <summary>
        /// Ensures diagonal selection and orbit preserve projection, while projection toggles preserve displayed scale.
        /// </summary>
        [Fact]
        public void OrbitAndProjectionToggle_PreserveProjectionRulesAndApparentScale() {
            EditorViewportNavigationController navigationController = CreateNavigationStack(out EditorEntity cameraEntity, out EditorViewportCameraComponent camera, out EditorViewportCameraController cameraController);
            cameraController.SetViewPose(new float3(1f, 2f, 3f), float4.Identity, 8.0);
            navigationController.SelectTarget(new EditorViewportNavigationTarget(1, 0, 1));
            navigationController.Advance(EditorViewportNavigationController.TransitionDurationSeconds);
            float3 pivot = cameraController.GetOrbitTarget();
            float4 initialOrbitOrientation = cameraEntity.Orientation;
            double perspectiveScale = CameraProjectionUtils.GetWorldUnitsPerPixel(camera, 8.0, camera.Viewport.W);

            navigationController.Orbit(new float2(0.1f, 0.05f));

            Assert.Equal(CameraProjectionMode.Perspective, camera.ProjectionMode);
            Assert.Equal(pivot, cameraController.GetOrbitTarget());
            Assert.NotEqual(initialOrbitOrientation, cameraEntity.Orientation);
            navigationController.ToggleProjection();
            double orthographicScale = CameraProjectionUtils.GetWorldUnitsPerPixel(camera, 8.0, camera.Viewport.W);

            Assert.Equal(CameraProjectionMode.Orthographic, camera.ProjectionMode);
            Assert.Equal(perspectiveScale, orthographicScale, 5);
            Assert.Equal(pivot, cameraController.GetOrbitTarget());
        }

        /// <summary>
        /// Ensures one navigation controller cannot mutate another viewport's pose or transition state.
        /// </summary>
        [Fact]
        public void SelectTarget_TwoControllers_KeepViewportStateIndependent() {
            EditorViewportNavigationController firstNavigationController = CreateNavigationStack(out EditorEntity firstCameraEntity, out EditorViewportCameraComponent firstCamera, out EditorViewportCameraController firstCameraController);
            EditorViewportNavigationController secondNavigationController = CreateNavigationStack(out EditorEntity secondCameraEntity, out EditorViewportCameraComponent secondCamera, out EditorViewportCameraController secondCameraController);
            secondCameraEntity.Position = new float3(0f, 0f, 23f);
            float3 secondCameraPosition = secondCameraEntity.Position;
            float4 secondCameraOrientation = secondCameraEntity.Orientation;

            firstNavigationController.SelectTarget(new EditorViewportNavigationTarget(1, 0, 0));
            firstNavigationController.Advance(EditorViewportNavigationController.TransitionDurationSeconds);

            Assert.False(firstNavigationController.IsTransitioning);
            Assert.False(secondNavigationController.IsTransitioning);
            Assert.Equal(CameraProjectionMode.Orthographic, firstCamera.ProjectionMode);
            Assert.Equal(CameraProjectionMode.Perspective, secondCamera.ProjectionMode);
            Assert.Equal(secondCameraPosition, secondCameraEntity.Position);
            Assert.Equal(secondCameraOrientation, secondCameraEntity.Orientation);
        }

        /// <summary>
        /// Ensures target construction rejects directions outside the cube or with no active axis.
        /// </summary>
        [Fact]
        public void NavigationTarget_WhenDirectionIsZeroOrOutsideUnitCube_Throws() {
            Assert.Throws<ArgumentException>(() => new EditorViewportNavigationTarget(0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EditorViewportNavigationTarget(2, 0, 0));
        }

        /// <summary>
        /// Creates an editor camera, its existing navigation component, and one navigation-cube controller.
        /// </summary>
        /// <returns>The camera entity, camera component, camera controller, and navigation controller.</returns>
        EditorViewportNavigationController CreateNavigationStack(out EditorEntity cameraEntity, out EditorViewportCameraComponent camera, out EditorViewportCameraController cameraController) {
            EditorSessionInteractionServices interactionServices = new EditorSessionInteractionServices();
            cameraEntity = new EditorEntity(CoreValue, interactionServices) {
                InternalEntity = true,
                Position = new float3(0f, 0f, 10f),
                Orientation = float4.Identity
            };
            camera = new EditorViewportCameraComponent {
                Viewport = new float4(0f, 0f, 800f, 600f)
            };
            cameraEntity.AddComponent(camera);
            cameraController = new EditorViewportCameraController(camera, CoreValue.Input);
            cameraEntity.AddComponent(cameraController);
            EditorViewportNavigationController navigationController = new EditorViewportNavigationController(cameraController);
            return navigationController;
        }

        /// <summary>
        /// Normalizes one non-zero direction using double-precision magnitude math.
        /// </summary>
        /// <param name="value">Vector to normalize.</param>
        /// <returns>Unit vector with the same direction.</returns>
        static float3 Normalize(float3 value) {
            double length = Math.Sqrt((value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z));
            return new float3((float)(value.X / length), (float)(value.Y / length), (float)(value.Z / length));
        }

        /// <summary>
        /// Asserts that two vectors differ by no more than the requested per-component tolerance.
        /// </summary>
        /// <param name="expected">Expected vector.</param>
        /// <param name="actual">Actual vector.</param>
        /// <param name="tolerance">Maximum absolute difference per component.</param>
        static void AssertVectorApproximately(float3 expected, float3 actual, float tolerance) {
            Assert.InRange(Math.Abs(expected.X - actual.X), 0.0, tolerance);
            Assert.InRange(Math.Abs(expected.Y - actual.Y), 0.0, tolerance);
            Assert.InRange(Math.Abs(expected.Z - actual.Z), 0.0, tolerance);
        }

        /// <summary>
        /// Asserts quaternion orientation equality while accepting the equivalent negated quaternion representation.
        /// </summary>
        /// <param name="expected">Expected orientation.</param>
        /// <param name="actual">Actual orientation.</param>
        /// <param name="tolerance">Maximum absolute difference for equivalent quaternion components.</param>
        static void AssertQuaternionOrientationApproximately(float4 expected, float4 actual, float tolerance) {
            float4 negatedExpected = new float4(-expected.X, -expected.Y, -expected.Z, -expected.W);
            double directError = QuaternionComponentError(expected, actual);
            double negatedError = QuaternionComponentError(negatedExpected, actual);
            Assert.InRange(Math.Min(directError, negatedError), 0.0, tolerance);
        }

        /// <summary>
        /// Computes the maximum component error between two quaternions.
        /// </summary>
        /// <param name="left">First orientation.</param>
        /// <param name="right">Second orientation.</param>
        /// <returns>Maximum absolute component difference.</returns>
        static double QuaternionComponentError(float4 left, float4 right) {
            return Math.Max(
                Math.Max(Math.Abs(left.X - right.X), Math.Abs(left.Y - right.Y)),
                Math.Max(Math.Abs(left.Z - right.Z), Math.Abs(left.W - right.W)));
        }
    }
}
