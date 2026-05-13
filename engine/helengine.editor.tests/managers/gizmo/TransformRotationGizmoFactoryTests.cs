using helengine;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies rotation-gizmo entity creation and ring metadata.
    /// </summary>
    public class TransformRotationGizmoFactoryTests : IDisposable {
        /// <summary>
        /// Tolerance used for axis-direction comparisons.
        /// </summary>
        const double DirectionTolerance = 0.0001;
        /// <summary>
        /// Camera created for the current test so its tool state can be cleaned up.
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
        /// Ensures the rotation factory creates three hollow-ring handles aligned to the world axes.
        /// </summary>
        [Fact]
        public void Create_CreatesThreeTubeRingHandlesAlignedToWorldAxes() {
            InitializeCore();
            TestRenderManager3D render3D = new TestRenderManager3D();
            CameraComponent sceneCamera = CreateSceneCamera();
            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            RuntimeMaterial highlightMaterial = new TestRuntimeMaterial();
            RuntimeMaterial previewMaterial = new TestRuntimeMaterial();

            EditorEntity gizmoRoot = TransformRotationGizmoFactory.Create(render3D, sceneCamera, normalMaterial, highlightMaterial, previewMaterial);

            Assert.Equal("Transform Rotation Gizmo", gizmoRoot.Name);
            Assert.True(gizmoRoot.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneGizmo, gizmoRoot.LayerMask);
            Assert.Equal(4, gizmoRoot.Children.Count);
            Assert.Equal(3, render3D.BuiltModelAssets.Count);

            AssertRingHandle((EditorEntity)gizmoRoot.Children[0], "Transform Rotation Gizmo X", new float3(1f, 0f, 0f), normalMaterial);
            AssertRingHandle((EditorEntity)gizmoRoot.Children[1], "Transform Rotation Gizmo Y", new float3(0f, 1f, 0f), normalMaterial);
            AssertRingHandle((EditorEntity)gizmoRoot.Children[2], "Transform Rotation Gizmo Z", new float3(0f, 0f, 1f), normalMaterial);
            AssertSnapPreview((EditorEntity)gizmoRoot.Children[3], previewMaterial);
        }

        /// <summary>
        /// Ensures the rotation factory tags each generated ring mesh with the expected axis-color UV marker.
        /// </summary>
        [Fact]
        public void Create_WritesAxisMarkersToGeneratedRingAssets() {
            InitializeCore();
            TestRenderManager3D render3D = new TestRenderManager3D();
            CameraComponent sceneCamera = CreateSceneCamera();

            TransformRotationGizmoFactory.Create(
                render3D,
                sceneCamera,
                new TestRuntimeMaterial(),
                new TestRuntimeMaterial(),
                new TestRuntimeMaterial());

            AssertUniformMarker(render3D.BuiltModelAssets[0], new float2(0.1f, 0.1f));
            AssertUniformMarker(render3D.BuiltModelAssets[1], new float2(0.9f, 0.1f));
            AssertUniformMarker(render3D.BuiltModelAssets[2], new float2(0.1f, 0.9f));
        }

        /// <summary>
        /// Validates the reusable rotation snap-preview entity created by the factory.
        /// </summary>
        /// <param name="previewEntity">Preview entity to inspect.</param>
        /// <param name="expectedMaterial">Expected preview material assigned to the preview mesh.</param>
        void AssertSnapPreview(EditorEntity previewEntity, RuntimeMaterial expectedMaterial) {
            Assert.Equal("Transform Rotation Gizmo Snap Preview", previewEntity.Name);
            Assert.True(previewEntity.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneGizmo, previewEntity.LayerMask);
            Assert.False(previewEntity.Enabled);

            MeshComponent meshComponent = FindMeshComponent(previewEntity);
            Assert.NotNull(meshComponent);
            Assert.Same(expectedMaterial, meshComponent.Material);
            Assert.Null(meshComponent.Model);
        }

        /// <summary>
        /// Initializes a fresh core with an object manager for entity-based tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Creates a scene camera entity used by rotation-gizmo tests.
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
        /// Validates one rotation ring handle created by the factory.
        /// </summary>
        /// <param name="ringEntity">Ring handle entity to inspect.</param>
        /// <param name="expectedName">Expected entity name.</param>
        /// <param name="expectedAxis">Expected world-space rotation axis.</param>
        /// <param name="expectedMaterial">Expected base material assigned to the ring mesh.</param>
        void AssertRingHandle(
            EditorEntity ringEntity,
            string expectedName,
            float3 expectedAxis,
            RuntimeMaterial expectedMaterial) {
            Assert.Equal(expectedName, ringEntity.Name);
            Assert.True(ringEntity.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneGizmo, ringEntity.LayerMask);
            Assert.False(ringEntity.Enabled);

            TransformGizmoHandleComponent handleComponent = FindHandleComponent(ringEntity);
            Assert.NotNull(handleComponent);
            Assert.Equal(TransformGizmoHandleConstraintType.Axis, handleComponent.ConstraintType);

            float3 actualAxis = Normalize(float4.RotateVector(handleComponent.LocalPrimaryDirection, ringEntity.Orientation));
            AssertVectorClose(expectedAxis, actualAxis);

            MeshComponent meshComponent = FindMeshComponent(ringEntity);
            Assert.NotNull(meshComponent);
            Assert.Same(expectedMaterial, meshComponent.Material);
            Assert.NotNull(meshComponent.Model);
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

            throw new InvalidOperationException("Expected a mesh component on the rotation ring entity.");
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

            throw new InvalidOperationException("Expected a transform-gizmo handle component on the rotation ring entity.");
        }

        /// <summary>
        /// Asserts that every vertex UV in a model asset matches the expected uniform marker.
        /// </summary>
        /// <param name="modelAsset">Model asset to inspect.</param>
        /// <param name="expectedMarker">Expected UV marker value.</param>
        void AssertUniformMarker(ModelAsset modelAsset, float2 expectedMarker) {
            if (modelAsset == null) {
                throw new ArgumentNullException(nameof(modelAsset));
            }

            for (int texCoordIndex = 0; texCoordIndex < modelAsset.TexCoords.Length; texCoordIndex++) {
                Assert.Equal(expectedMarker.X, modelAsset.TexCoords[texCoordIndex].X);
                Assert.Equal(expectedMarker.Y, modelAsset.TexCoords[texCoordIndex].Y);
            }
        }

        /// <summary>
        /// Normalizes a direction vector for test comparisons.
        /// </summary>
        /// <param name="value">Vector to normalize.</param>
        /// <returns>Normalized direction vector.</returns>
        float3 Normalize(float3 value) {
            double lengthSquared =
                (value.X * value.X) +
                (value.Y * value.Y) +
                (value.Z * value.Z);
            if (lengthSquared <= 0.0) {
                throw new InvalidOperationException("Direction vector must be non-zero.");
            }

            double inverseLength = 1.0 / Math.Sqrt(lengthSquared);
            return new float3(
                (float)(value.X * inverseLength),
                (float)(value.Y * inverseLength),
                (float)(value.Z * inverseLength));
        }

        /// <summary>
        /// Asserts that two direction vectors are equal within a small tolerance.
        /// </summary>
        /// <param name="expected">Expected direction.</param>
        /// <param name="actual">Actual direction.</param>
        void AssertVectorClose(float3 expected, float3 actual) {
            Assert.InRange(Math.Abs(expected.X - actual.X), 0.0, DirectionTolerance);
            Assert.InRange(Math.Abs(expected.Y - actual.Y), 0.0, DirectionTolerance);
            Assert.InRange(Math.Abs(expected.Z - actual.Z), 0.0, DirectionTolerance);
        }
    }
}
