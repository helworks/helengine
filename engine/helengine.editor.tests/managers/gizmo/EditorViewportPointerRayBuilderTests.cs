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
        /// Initializes a minimal core for viewport ray tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, null, new TestInputBackend());
        }

        /// <summary>
        /// Creates a scene camera with a stable viewport and identity transform.
        /// </summary>
        /// <returns>Configured scene camera component.</returns>
        CameraComponent CreateSceneCamera() {
            EditorEntity cameraEntity = new EditorEntity {
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
    }
}

