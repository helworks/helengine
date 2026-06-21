using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.scene {
    /// <summary>
    /// Verifies the internal viewport grid created for empty editor scenes.
    /// </summary>
    public class EditorViewportGridFactoryTests {
        /// <summary>
        /// Ensures the editor layer-mask definitions reserve one dedicated layer for the viewport grid.
        /// </summary>
        [Fact]
        public void EditorLayerMasks_DefinesSceneGridLayer() {
            FieldInfo field = typeof(EditorLayerMasks).GetField("SceneGrid", BindingFlags.Public | BindingFlags.Static);

            Assert.NotNull(field);
            Assert.Equal(typeof(ushort), field.FieldType);
            Assert.Equal((ushort)0b0001000000000000, Assert.IsType<ushort>(field.GetValue(null)));
        }

        /// <summary>
        /// Ensures the viewport grid is created as an internal scene entity with one mesh component.
        /// </summary>
        [Fact]
        public void Create_CreatesInternalSceneGridEntity() {
            InitializeCore();
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);

            EditorEntity gridEntity = EditorViewportGridFactory.Create(renderManager3D);

            MeshComponent meshComponent = Assert.IsType<MeshComponent>(Assert.Single(gridEntity.Components, component => component is MeshComponent));
            Assert.Equal("Viewport Grid", gridEntity.Name);
            Assert.True(gridEntity.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneGrid, gridEntity.LayerMask);
            Assert.Equal(new float3(0f, -0.001f, 0f), gridEntity.LocalPosition);
            Assert.Single(renderManager3D.BuiltModelAssets);
            Assert.Single(renderManager3D.BuiltMaterialAssets);
            Assert.NotNull(meshComponent.Model);
            Assert.NotNull(Assert.Single(meshComponent.Materials));
        }

        /// <summary>
        /// Ensures the viewport grid uses one centered 10x10 plane aligned to the world XZ plane.
        /// </summary>
        [Fact]
        public void Create_BuildsTenByTenPlaneAlignedToWorldXz() {
            InitializeCore();
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);

            EditorEntity gridEntity = EditorViewportGridFactory.Create(renderManager3D);

            ModelAsset modelAsset = Assert.Single(renderManager3D.BuiltModelAssets);
            Assert.Contains(modelAsset.Positions, position => position.Equals(new float3(-5f, -5f, 0f)));
            Assert.Contains(modelAsset.Positions, position => position.Equals(new float3(5f, 5f, 0f)));

            float3 rotatedNormal = float4.RotateVector(new float3(0f, 0f, 1f), gridEntity.LocalOrientation);
            AssertFloat3Equal(new float3(0f, -1f, 0f), rotatedNormal, 0.0001f);
        }

        /// <summary>
        /// Ensures the viewport grid renders after default scene meshes so its transparent material can still use scene depth.
        /// </summary>
        [Fact]
        public void Create_AssignsRenderOrderAfterDefaultSceneGeometry() {
            InitializeCore();
            TestRenderManager3D renderManager3D = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);

            EditorEntity gridEntity = EditorViewportGridFactory.Create(renderManager3D);
            MeshComponent gridMeshComponent = Assert.IsType<MeshComponent>(Assert.Single(gridEntity.Components, component => component is MeshComponent));
            MeshComponent defaultSceneMeshComponent = new MeshComponent();

            Assert.True(gridMeshComponent.RenderOrder3D > defaultSceneMeshComponent.RenderOrder3D);
        }
 
        /// <summary>
        /// Initializes the global core instance used by the current test.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Verifies two vectors are equal within one absolute tolerance per component.
        /// </summary>
        /// <param name="expected">Expected vector value.</param>
        /// <param name="actual">Actual vector value.</param>
        /// <param name="tolerance">Maximum allowed absolute difference per component.</param>
        void AssertFloat3Equal(float3 expected, float3 actual, float tolerance) {
            Assert.InRange(Math.Abs(actual.X - expected.X), 0f, tolerance);
            Assert.InRange(Math.Abs(actual.Y - expected.Y), 0f, tolerance);
            Assert.InRange(Math.Abs(actual.Z - expected.Z), 0f, tolerance);
        }
    }
}
