using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies viewport pointer rays continue moving after the pointer leaves the scene viewport.
    /// </summary>
    public class EditorViewportPointerRayBuilderTests {
        /// <summary>
        /// Ensures pointer rays are not clamped to the viewport edge after the pointer exits the viewport during an active drag.
        /// </summary>
        [Fact]
        public void TryBuildPerspectiveCameraRay_WhenPointerLeavesViewport_ContinuesPastViewportEdge() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera();

            bool edgeRayBuilt = EditorViewportPointerRayBuilder.TryBuildPerspectiveCameraRay(
                sceneCamera,
                new int2(99, 50),
                out float3 edgeRayOrigin,
                out float3 edgeRayDirection);
            bool outsideRayBuilt = EditorViewportPointerRayBuilder.TryBuildPerspectiveCameraRay(
                sceneCamera,
                new int2(140, 50),
                out float3 outsideRayOrigin,
                out float3 outsideRayDirection);

            Assert.True(edgeRayBuilt);
            Assert.True(outsideRayBuilt);
            Assert.Equal(edgeRayOrigin, outsideRayOrigin);
            Assert.True(outsideRayDirection.X > edgeRayDirection.X);
            Assert.Equal(edgeRayDirection.Y, outsideRayDirection.Y);
            Assert.Equal(edgeRayDirection.Z < 0f, outsideRayDirection.Z < 0f);
        }

        /// <summary>
        /// Ensures orthographic pointer rays stay parallel while their origins move across the view plane.
        /// </summary>
        [Fact]
        public void TryBuildCameraRay_OrthographicUsesParallelDirectionsAndScreenDependentOrigins() {
            InitializeCore();
            EditorViewportCameraComponent sceneCamera = CreateEditorSceneCamera(new float4(0f, 0f, 100f, 100f));
            sceneCamera.ProjectionMode = CameraProjectionMode.Orthographic;
            sceneCamera.OrthographicVerticalSpan = 20f;

            bool centerBuilt = EditorViewportPointerRayBuilder.TryBuildCameraRay(sceneCamera, new int2(50, 50), out float3 centerOrigin, out float3 centerDirection);
            bool rightBuilt = EditorViewportPointerRayBuilder.TryBuildCameraRay(sceneCamera, new int2(75, 50), out float3 rightOrigin, out float3 rightDirection);

            Assert.True(centerBuilt);
            Assert.True(rightBuilt);
            Assert.Equal(centerDirection, rightDirection);
            Assert.Equal(new float3(5f, 0f, 0f), rightOrigin - centerOrigin);
        }

        /// <summary>
        /// Ensures perspective pointer rays share their camera origin and use the camera's authored field of view.
        /// </summary>
        [Fact]
        public void TryBuildCameraRay_PerspectiveUsesCommonOriginAndCameraFieldOfView() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera();
            sceneCamera.FieldOfView = (float)(Math.PI / 2.0);

            bool centerBuilt = EditorViewportPointerRayBuilder.TryBuildCameraRay(sceneCamera, new int2(50, 50), out float3 centerOrigin, out float3 centerDirection);
            bool rightBuilt = EditorViewportPointerRayBuilder.TryBuildCameraRay(sceneCamera, new int2(100, 50), out float3 rightOrigin, out float3 rightDirection);

            Assert.True(centerBuilt);
            Assert.True(rightBuilt);
            Assert.Equal(centerOrigin, rightOrigin);
            Assert.Equal(Math.Sqrt(0.5), rightDirection.X, 6);
            Assert.Equal(-Math.Sqrt(0.5), rightDirection.Z, 6);
            Assert.Equal(-1f, centerDirection.Z);
        }

        /// <summary>
        /// Ensures pointer coordinates are normalized within a viewport whose origin is not the window origin.
        /// </summary>
        [Fact]
        public void TryBuildCameraRay_WhenViewportIsOffCenter_UsesViewportLocalCenter() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera();
            sceneCamera.Viewport = new float4(20f, 30f, 100f, 80f);

            bool built = EditorViewportPointerRayBuilder.TryBuildCameraRay(sceneCamera, new int2(70, 70), out float3 origin, out float3 direction);

            Assert.True(built);
            Assert.Equal(float3.Zero, origin);
            Assert.Equal(new float3(0f, 0f, -1f), direction);
        }

        /// <summary>
        /// Ensures minimized viewports cannot create invalid pointer rays.
        /// </summary>
        [Fact]
        public void TryBuildCameraRay_WhenViewportHasZeroDimension_ReturnsFalse() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera();
            sceneCamera.Viewport = new float4(0f, 0f, 100f, 0f);

            bool built = EditorViewportPointerRayBuilder.TryBuildCameraRay(sceneCamera, new int2(50, 50), out float3 origin, out float3 direction);

            Assert.False(built);
            Assert.Equal(float3.Zero, origin);
            Assert.Equal(float3.Zero, direction);
        }

        /// <summary>
        /// Ensures the explicitly perspective ray helper also follows a non-default authored field of view.
        /// </summary>
        [Fact]
        public void TryBuildPerspectiveCameraRay_WhenCameraUsesNonDefaultFieldOfView_UsesAuthoredFieldOfView() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera();
            sceneCamera.FieldOfView = (float)(Math.PI / 2.0);

            bool built = EditorViewportPointerRayBuilder.TryBuildPerspectiveCameraRay(
                sceneCamera,
                new int2(100, 50),
                out _,
                out float3 direction);

            Assert.True(built);
            Assert.Equal(Math.Sqrt(0.5), direction.X, 6);
            Assert.Equal(-Math.Sqrt(0.5), direction.Z, 6);
        }

        /// <summary>
        /// Initializes a minimal core for viewport ray tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            core.Initialize(null, null, new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Creates a scene camera with a stable viewport and identity transform.
        /// </summary>
        /// <returns>Configured scene camera component.</returns>
        CameraComponent CreateSceneCamera() {
            EditorEntity cameraEntity = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                InternalEntity = true,
                Position = float3.Zero,
                Orientation = float4.Identity
            };

            CameraComponent sceneCamera = new CameraComponent {
                Viewport = new float4(0f, 0f, 100f, 100f)
            };
            cameraEntity.AddComponent(sceneCamera);
            Core.Instance.ObjectManager.Cameras.Clear();
            return sceneCamera;
        }

        /// <summary>
        /// Creates and attaches an editor viewport camera for projection-aware ray tests.
        /// </summary>
        /// <param name="viewport">Viewport rectangle expressed in window coordinates.</param>
        /// <returns>Attached editor viewport camera.</returns>
        EditorViewportCameraComponent CreateEditorSceneCamera(float4 viewport) {
            EditorEntity cameraEntity = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                InternalEntity = true,
                Position = float3.Zero,
                Orientation = float4.Identity
            };

            EditorViewportCameraComponent sceneCamera = new EditorViewportCameraComponent { Viewport = viewport };
            cameraEntity.AddComponent(sceneCamera);
            Core.Instance.ObjectManager.Cameras.Clear();
            return sceneCamera;
        }

    }
}

