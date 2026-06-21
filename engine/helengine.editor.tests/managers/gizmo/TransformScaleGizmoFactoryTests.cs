using helengine;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies scale-gizmo entity creation and box-tip layout.
    /// </summary>
    public class TransformScaleGizmoFactoryTests : IDisposable {
        /// <summary>
        /// Tolerance used for axis-tip position comparisons after quaternion-based layout.
        /// </summary>
        const double PositionTolerance = 0.0001;
        /// <summary>
        /// Camera created for the current test so static tool state can be cleaned up.
        /// </summary>
        CameraComponent CameraUnderTest;

        /// <summary>
        /// Clears static editor state that is shared across tests.
        /// </summary>
        public void Dispose() {
            EditorSelectionService.ClearSelection();
            EditorGizmoHoverService.ClearHoveredHandle();

            if (CameraUnderTest != null) {
                EditorViewportToolService.ClearToolMode(CameraUnderTest);
                EditorGizmoDragService.EndDrag(CameraUnderTest);
            }
        }

        /// <summary>
        /// Ensures the scale factory creates three axis handles and three plane handles using box tips.
        /// </summary>
        [Fact]
        public void Create_CreatesAxisAndPlaneHandlesUsingBoxTips() {
            InitializeCore();
            TestRenderManager3D render3D = new TestRenderManager3D();
            CameraComponent sceneCamera = CreateSceneCamera();
            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            RuntimeMaterial highlightMaterial = new TestRuntimeMaterial();

            EditorEntity gizmoRoot = TransformScaleGizmoFactory.Create(render3D, sceneCamera, normalMaterial, highlightMaterial);

            Assert.Equal("Transform Scale Gizmo", gizmoRoot.Name);
            Assert.True(gizmoRoot.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneGizmo, gizmoRoot.LayerMask);
            Assert.Equal(6, gizmoRoot.Children.Count);
            Assert.Equal(9, render3D.BuiltModelAssets.Count);

            AssertAxisHandle((EditorEntity)gizmoRoot.Children[0], "Transform Scale Gizmo X", new float3(TransformScaleGizmoFactory.ShaftLength, 0f, 0f), normalMaterial);
            AssertAxisHandle((EditorEntity)gizmoRoot.Children[1], "Transform Scale Gizmo Y", new float3(0f, TransformScaleGizmoFactory.ShaftLength, 0f), normalMaterial);
            AssertAxisHandle((EditorEntity)gizmoRoot.Children[2], "Transform Scale Gizmo Z", new float3(0f, 0f, TransformScaleGizmoFactory.ShaftLength), normalMaterial);
            AssertPlaneHandle((EditorEntity)gizmoRoot.Children[3], "Transform Scale Gizmo XY Plane", normalMaterial);
            AssertPlaneHandle((EditorEntity)gizmoRoot.Children[4], "Transform Scale Gizmo XZ Plane", normalMaterial);
            AssertPlaneHandle((EditorEntity)gizmoRoot.Children[5], "Transform Scale Gizmo YZ Plane", normalMaterial);
        }

        /// <summary>
        /// Initializes a fresh core with an object manager for entity-based tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Creates a scene camera entity used by scale-gizmo tests.
        /// </summary>
        /// <returns>Configured scene camera component.</returns>
        CameraComponent CreateSceneCamera() {
            EditorEntity cameraEntity = new EditorEntity();
            cameraEntity.InternalEntity = true;
            cameraEntity.Position = new float3(0f, 2f, -8f);

            CameraComponent sceneCamera = new CameraComponent();
            sceneCamera.Viewport = new float4(0f, 0f, 1280f, 720f);
            cameraEntity.AddComponent(sceneCamera);
            CameraUnderTest = sceneCamera;
            return sceneCamera;
        }

        /// <summary>
        /// Validates one axis handle created by the scale factory.
        /// </summary>
        /// <param name="axisEntity">Axis handle entity to inspect.</param>
        /// <param name="expectedName">Expected handle name.</param>
        /// <param name="expectedTipPosition">Expected world position of the box tip base.</param>
        /// <param name="expectedMaterial">Expected base material assigned to the meshes.</param>
        void AssertAxisHandle(
            EditorEntity axisEntity,
            string expectedName,
            float3 expectedTipPosition,
            RuntimeMaterial expectedMaterial) {
            Assert.Equal(expectedName, axisEntity.Name);
            Assert.True(axisEntity.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneGizmo, axisEntity.LayerMask);
            Assert.False(axisEntity.Enabled);
            Assert.Equal(2, axisEntity.Children.Count);

            TransformGizmoHandleComponent handleComponent = FindHandleComponent(axisEntity);
            Assert.NotNull(handleComponent);
            Assert.Equal(TransformGizmoHandleConstraintType.Axis, handleComponent.ConstraintType);

            MeshComponent shaftMesh = FindMeshComponent(axisEntity.Children[0]);
            MeshComponent tipMesh = FindMeshComponent(axisEntity.Children[1]);
            Assert.Same(expectedMaterial, Assert.Single(shaftMesh.Materials));
            Assert.Same(expectedMaterial, Assert.Single(tipMesh.Materials));
            AssertVectorClose(expectedTipPosition, axisEntity.Children[1].Position);
        }

        /// <summary>
        /// Validates one plane handle created by the scale factory.
        /// </summary>
        /// <param name="planeEntity">Plane handle entity to inspect.</param>
        /// <param name="expectedName">Expected handle name.</param>
        /// <param name="expectedMaterial">Expected base material assigned to the plane mesh.</param>
        void AssertPlaneHandle(EditorEntity planeEntity, string expectedName, RuntimeMaterial expectedMaterial) {
            Assert.Equal(expectedName, planeEntity.Name);
            Assert.True(planeEntity.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneGizmo, planeEntity.LayerMask);
            Assert.False(planeEntity.Enabled);

            TransformGizmoHandleComponent handleComponent = FindHandleComponent(planeEntity);
            Assert.NotNull(handleComponent);
            Assert.Equal(TransformGizmoHandleConstraintType.Plane, handleComponent.ConstraintType);

            MeshComponent planeMesh = FindMeshComponent(planeEntity);
            Assert.Same(expectedMaterial, Assert.Single(planeMesh.Materials));
        }

        /// <summary>
        /// Finds the mesh component attached directly to an entity.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>Attached mesh component.</returns>
        MeshComponent FindMeshComponent(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is MeshComponent meshComponent) {
                    return meshComponent;
                }
            }

            throw new InvalidOperationException("Expected a mesh component on the scale gizmo entity.");
        }

        /// <summary>
        /// Finds the transform-gizmo handle component attached directly to an entity.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>Attached transform-gizmo handle component.</returns>
        TransformGizmoHandleComponent FindHandleComponent(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is TransformGizmoHandleComponent handleComponent) {
                    return handleComponent;
                }
            }

            throw new InvalidOperationException("Expected a transform-gizmo handle component on the scale gizmo entity.");
        }

        /// <summary>
        /// Asserts that two vectors are equal within a small floating-point tolerance.
        /// </summary>
        /// <param name="expected">Expected vector value.</param>
        /// <param name="actual">Actual vector value.</param>
        void AssertVectorClose(float3 expected, float3 actual) {
            Assert.InRange(Math.Abs(expected.X - actual.X), 0.0, PositionTolerance);
            Assert.InRange(Math.Abs(expected.Y - actual.Y), 0.0, PositionTolerance);
            Assert.InRange(Math.Abs(expected.Z - actual.Z), 0.0, PositionTolerance);
        }
    }
}
