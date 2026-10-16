using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies validated perspective and orthographic projection generation from camera state.
    /// </summary>
    public class CameraProjectionUtilsTests {
        /// <summary>
        /// Ensures new camera components expose the current renderer defaults as authored clip-plane state.
        /// </summary>
        [Fact]
        public void CameraComponent_WhenConstructed_UsesDefaultClipPlaneDistances() {
            InitializeCore();
            CameraComponent camera = new CameraComponent();

            Assert.Equal(0.1f, camera.NearPlaneDistance);
            Assert.Equal(100f, camera.FarPlaneDistance);
        }

        /// <summary>
        /// Ensures editor viewport cameras begin in perspective mode with a usable orthographic span.
        /// </summary>
        [Fact]
        public void EditorViewportCamera_WhenConstructed_UsesPerspectiveProjection() {
            InitializeCore();
            EditorViewportCameraComponent camera = new EditorViewportCameraComponent();

            Assert.Equal(CameraProjectionMode.Perspective, camera.ProjectionMode);
            Assert.True(camera.OrthographicVerticalSpan >= CameraProjectionUtils.MinimumOrthographicVerticalSpan);
        }

        /// <summary>
        /// Ensures the shared projection helper uses the authored clip-plane values instead of hardcoded renderer constants.
        /// </summary>
        [Fact]
        public void CreatePerspectiveProjection_WhenCameraUsesCustomClipPlanes_UsesAuthoredNearAndFarDistances() {
            InitializeCore();
            CameraComponent camera = new CameraComponent {
                NearPlaneDistance = 0.25f,
                FarPlaneDistance = 640f
            };

            float4x4 projection = CameraProjectionUtils.CreatePerspectiveProjection(camera, (float)(Math.PI / 4.0), 16f / 9f);
            float expectedM33 = camera.FarPlaneDistance / (camera.NearPlaneDistance - camera.FarPlaneDistance);
            float expectedM43 = camera.NearPlaneDistance * expectedM33;

            Assert.Equal(expectedM33, projection.M33, 5);
            Assert.Equal(expectedM43, projection.M43, 5);
        }

        /// <summary>
        /// Ensures invalid clip-plane values are clamped into a legal perspective range before projection creation.
        /// </summary>
        [Fact]
        public void CreatePerspectiveProjection_WhenCameraUsesInvalidClipPlanes_ClampsToLegalDistances() {
            InitializeCore();
            CameraComponent camera = new CameraComponent {
                NearPlaneDistance = -4f,
                FarPlaneDistance = 0.001f
            };

            float4x4 projection = CameraProjectionUtils.CreatePerspectiveProjection(camera, (float)(Math.PI / 4.0), 1.0f);
            float expectedNear = 0.01f;
            float expectedFar = 0.02f;
            float expectedM33 = expectedFar / (expectedNear - expectedFar);
            float expectedM43 = expectedNear * expectedM33;

            Assert.Equal(expectedM33, projection.M33, 5);
            Assert.Equal(expectedM43, projection.M43, 5);
        }

        /// <summary>
        /// Ensures ordinary cameras retain their existing perspective projection exactly.
        /// </summary>
        [Fact]
        public void CreateProjection_OrdinaryCamera_MatchesExistingPerspective() {
            InitializeCore();
            CameraComponent camera = new CameraComponent {
                FieldOfView = (float)(Math.PI / 3.0),
                NearPlaneDistance = 0.4f,
                FarPlaneDistance = 420f
            };
            const float aspectRatio = 16f / 9f;

            float4x4 actual = CameraProjectionUtils.CreateProjection(camera, aspectRatio);
            float4x4 expected = CameraProjectionUtils.CreatePerspectiveProjection(camera, aspectRatio);

            AssertMatrixEqual(expected, actual);
        }

        /// <summary>
        /// Ensures orthographic projection covers the requested vertical and horizontal extents and camera depth range.
        /// </summary>
        [Fact]
        public void CreateProjection_Orthographic_MapsVisibleExtentsAndClipPlanes() {
            InitializeCore();
            CameraComponent camera = CreateOrthographicCamera(20f);
            camera.NearPlaneDistance = 1f;
            camera.FarPlaneDistance = 101f;
            const float aspectRatio = 2f;

            float4x4 projection = CameraProjectionUtils.CreateProjection(camera, aspectRatio);
            float top = (10f * projection.M22) + projection.M42;
            float bottom = (-10f * projection.M22) + projection.M42;
            float right = (20f * projection.M11) + projection.M41;
            float left = (-20f * projection.M11) + projection.M41;
            float nearDepth = (-camera.NearPlaneDistance * projection.M33) + projection.M43;
            float farDepth = (-camera.FarPlaneDistance * projection.M33) + projection.M43;

            Assert.Equal(1f, top, 5);
            Assert.Equal(-1f, bottom, 5);
            Assert.Equal(1f, right, 5);
            Assert.Equal(-1f, left, 5);
            Assert.Equal(0f, nearDepth, 5);
            Assert.Equal(1f, farDepth, 5);
        }

        /// <summary>
        /// Ensures orthographic pixel scale depends on the visible span instead of camera distance.
        /// </summary>
        [Fact]
        public void WorldUnitsPerPixel_Orthographic_IsIndependentOfDistance() {
            InitializeCore();
            CameraComponent camera = CreateOrthographicCamera(20f);

            double nearScale = CameraProjectionUtils.GetWorldUnitsPerPixel(camera, 1.0, 1000.0);
            double farScale = CameraProjectionUtils.GetWorldUnitsPerPixel(camera, 100.0, 1000.0);

            Assert.Equal(0.02, nearScale, 6);
            Assert.Equal(nearScale, farScale, 8);
        }

        /// <summary>
        /// Ensures perspective pixel scale uses the camera's authored field of view.
        /// </summary>
        [Fact]
        public void WorldUnitsPerPixel_Perspective_UsesCameraFieldOfView() {
            InitializeCore();
            CameraComponent camera = new CameraComponent { FieldOfView = (float)(Math.PI / 2.0) };

            double scale = CameraProjectionUtils.GetWorldUnitsPerPixel(camera, 10.0, 100.0);

            Assert.Equal(0.2, scale, 5);
        }

        /// <summary>
        /// Ensures projection creation rejects zero, non-finite, or negative viewport aspect ratios.
        /// </summary>
        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void CreateProjection_WhenAspectRatioIsInvalid_Throws(float aspectRatio) {
            InitializeCore();
            CameraComponent camera = new CameraComponent();

            Assert.Throws<ArgumentOutOfRangeException>(() => CameraProjectionUtils.CreateProjection(camera, aspectRatio));
        }

        /// <summary>
        /// Ensures the editor camera rejects non-finite and degenerate orthographic spans.
        /// </summary>
        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void OrthographicSpan_WhenInvalid_Throws(float span) {
            InitializeCore();
            EditorViewportCameraComponent camera = CreateOrthographicCamera(20f);

            Assert.Throws<ArgumentOutOfRangeException>(() => SetOrthographicSpan(camera, span));
        }

        /// <summary>
        /// Ensures world-units-per-pixel rejects unusable distances and viewport heights.
        /// </summary>
        [Theory]
        [InlineData(0.0, 100.0)]
        [InlineData(1.0, 0.0)]
        [InlineData(-1.0, 100.0)]
        [InlineData(1.0, -100.0)]
        [InlineData(double.NaN, 100.0)]
        [InlineData(1.0, double.PositiveInfinity)]
        public void WorldUnitsPerPixel_WhenDimensionsAreInvalid_Throws(double distance, double viewportHeight) {
            InitializeCore();
            CameraComponent camera = new CameraComponent();

            Assert.Throws<ArgumentOutOfRangeException>(() => CameraProjectionUtils.GetWorldUnitsPerPixel(camera, distance, viewportHeight));
        }

        /// <summary>
        /// Resolves the editor-only camera type so these tests can expose the missing projection behavior before its API exists.
        /// </summary>
        /// <param name="span">Orthographic vertical span to configure.</param>
        /// <returns>An editor viewport camera configured for orthographic projection.</returns>
        EditorViewportCameraComponent CreateOrthographicCamera(float span) {
            EditorViewportCameraComponent camera = new EditorViewportCameraComponent {
                ProjectionMode = CameraProjectionMode.Orthographic,
                OrthographicVerticalSpan = span
            };
            return camera;
        }

        /// <summary>
        /// Invokes the new projection API while preserving its exception type for behavior assertions.
        /// </summary>
        /// <param name="camera">Camera whose projection is requested.</param>
        /// <param name="aspectRatio">Viewport width-to-height ratio.</param>
        /// <returns>The generated projection matrix.</returns>
        float4x4 InvokeProjection(ICamera camera, float aspectRatio) {
            return CameraProjectionUtils.CreateProjection(camera, aspectRatio);
        }

        /// <summary>
        /// Invokes the new world-units-per-pixel API while preserving its exception type for behavior assertions.
        /// </summary>
        /// <param name="camera">Camera that defines the projected scale.</param>
        /// <param name="distance">Distance from the camera to the projected point.</param>
        /// <param name="viewportHeight">Viewport height in pixels.</param>
        /// <returns>World-space distance represented by one vertical pixel.</returns>
        double InvokeWorldUnitsPerPixel(ICamera camera, double distance, double viewportHeight) {
            return CameraProjectionUtils.GetWorldUnitsPerPixel(camera, distance, viewportHeight);
        }

        /// <summary>
        /// Sets the editor camera's orthographic span through reflection so the first test run can verify the absent API.
        /// </summary>
        /// <param name="camera">Editor viewport camera.</param>
        /// <param name="span">Requested full vertical span.</param>
        void SetOrthographicSpan(EditorViewportCameraComponent camera, float span) {
            camera.OrthographicVerticalSpan = span;
        }

        /// <summary>
        /// Compares every projection-matrix element at the precision used by the existing projection tests.
        /// </summary>
        /// <param name="expected">Expected projection.</param>
        /// <param name="actual">Generated projection.</param>
        void AssertMatrixEqual(float4x4 expected, float4x4 actual) {
            Assert.Equal(expected.M11, actual.M11, 5);
            Assert.Equal(expected.M12, actual.M12, 5);
            Assert.Equal(expected.M13, actual.M13, 5);
            Assert.Equal(expected.M14, actual.M14, 5);
            Assert.Equal(expected.M21, actual.M21, 5);
            Assert.Equal(expected.M22, actual.M22, 5);
            Assert.Equal(expected.M23, actual.M23, 5);
            Assert.Equal(expected.M24, actual.M24, 5);
            Assert.Equal(expected.M31, actual.M31, 5);
            Assert.Equal(expected.M32, actual.M32, 5);
            Assert.Equal(expected.M33, actual.M33, 5);
            Assert.Equal(expected.M34, actual.M34, 5);
            Assert.Equal(expected.M41, actual.M41, 5);
            Assert.Equal(expected.M42, actual.M42, 5);
            Assert.Equal(expected.M43, actual.M43, 5);
            Assert.Equal(expected.M44, actual.M44, 5);
        }

        /// <summary>
        /// Initializes a core instance so camera components can allocate render queues during these tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4,
                ContentStreamSource = new FakeContentStreamSource()
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }
    }
}
