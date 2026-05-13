using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies validated perspective-projection generation from camera clip-plane state.
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
        /// Initializes a core instance so camera components can allocate render queues during these tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }
    }
}
