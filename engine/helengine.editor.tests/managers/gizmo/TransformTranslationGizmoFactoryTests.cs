using helengine;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies translation-gizmo entity creation and plane-handle geometry.
    /// </summary>
    public class TransformTranslationGizmoFactoryTests {
        /// <summary>
        /// Expected plane side length after increasing the translation plane handles by 30%.
        /// </summary>
        const float ExpectedPlaneSize = 0.325f;

        /// <summary>
        /// Ensures the translation plane meshes are created with the authored plane size.
        /// </summary>
        [Fact]
        public void Create_WhenCalled_BuildsTranslationPlaneMeshesUsingTheConfiguredPlaneSize() {
            InitializeCore();
            TestDirectX11RenderManager3D render3D = TestDirectX11RenderManager3D.Create();
            CameraComponent sceneCamera = CreateSceneCamera();

            TransformTranslationGizmoFactory.Create(
                render3D,
                sceneCamera,
                new TestRuntimeMaterial(),
                new TestRuntimeMaterial(),
                new TestRuntimeMaterial(),
                new TestRuntimeMaterial());

            Assert.Equal(10, render3D.BuiltModelAssets.Count);

            AssertPlaneMeshUsesConfiguredSize(render3D.BuiltModelAssets[6]);
            AssertPlaneMeshUsesConfiguredSize(render3D.BuiltModelAssets[7]);
            AssertPlaneMeshUsesConfiguredSize(render3D.BuiltModelAssets[8]);
        }

        /// <summary>
        /// Initializes a fresh core with an object manager for entity-based factory tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Creates a scene camera entity used by translation-gizmo factory tests.
        /// </summary>
        /// <returns>Configured scene camera component.</returns>
        CameraComponent CreateSceneCamera() {
            EditorEntity cameraEntity = new EditorEntity();
            cameraEntity.InternalEntity = true;
            cameraEntity.Position = new float3(0f, 2f, -8f);

            CameraComponent sceneCamera = new CameraComponent();
            sceneCamera.Viewport = new float4(0f, 0f, 1280f, 720f);
            cameraEntity.AddComponent(sceneCamera);
            return sceneCamera;
        }

        /// <summary>
        /// Asserts that one generated translation plane mesh spans the configured plane size on both in-plane axes.
        /// </summary>
        /// <param name="planeMesh">Generated raw plane mesh asset.</param>
        void AssertPlaneMeshUsesConfiguredSize(ModelAsset planeMesh) {
            Assert.NotNull(planeMesh);
            Assert.NotNull(planeMesh.Positions);

            float maxX = float.MinValue;
            float maxY = float.MinValue;
            for (int positionIndex = 0; positionIndex < planeMesh.Positions.Length; positionIndex++) {
                float3 position = planeMesh.Positions[positionIndex];
                if (position.X > maxX) {
                    maxX = position.X;
                }
                if (position.Y > maxY) {
                    maxY = position.Y;
                }
            }

            Assert.Equal(ExpectedPlaneSize, maxX);
            Assert.Equal(ExpectedPlaneSize, maxY);
        }
    }
}
