using helengine;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies rotation-gizmo follow visibility, scale, and highlight behavior.
    /// </summary>
    public class TransformRotationGizmoFollowComponentTests : IDisposable {
        /// <summary>
        /// Camera created for the current test so static tool and drag state can be cleaned up.
        /// </summary>
        CameraComponent CameraUnderTest;

        /// <summary>
        /// Clears shared editor state after each test.
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
        /// Ensures the follow component enables the rings, positions the root at the selection, and highlights the hovered axis.
        /// </summary>
        [Fact]
        public void Update_WhenRotateToolIsActive_ShowsAndHighlightsRotationRings() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Rotate);

            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            RuntimeMaterial highlightMaterial = new TestRuntimeMaterial();
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial);
            EditorEntity hoveredRing = (EditorEntity)gizmoRoot.Children[1];
            gizmoRoot.AddComponent(new TransformRotationGizmoFollowComponent(sceneCamera, gizmoRoot, normalMaterial, highlightMaterial));

            EditorEntity selectedEntity = new EditorEntity();
            selectedEntity.Position = new float3(3f, 4f, 5f);
            EditorSelectionService.SetSelectedEntity(selectedEntity);
            EditorGizmoHoverService.SetHoveredHandle(hoveredRing);

            UpdateFollowComponent(gizmoRoot);

            Assert.Equal(selectedEntity.Position, gizmoRoot.Position);
            Assert.True(gizmoRoot.Scale.X > 0f);
            Assert.Equal(gizmoRoot.Scale.X, gizmoRoot.Scale.Y);
            Assert.Equal(gizmoRoot.Scale.X, gizmoRoot.Scale.Z);

            for (int childIndex = 0; childIndex < gizmoRoot.Children.Count; childIndex++) {
                Assert.True(gizmoRoot.Children[childIndex].Enabled);
            }

            Assert.Same(normalMaterial, FindMeshComponent(gizmoRoot.Children[0]).Material);
            Assert.Same(highlightMaterial, FindMeshComponent(gizmoRoot.Children[1]).Material);
            Assert.Same(normalMaterial, FindMeshComponent(gizmoRoot.Children[2]).Material);
        }

        /// <summary>
        /// Ensures the follow component hides the rotation rings when the viewport is not in rotate mode.
        /// </summary>
        [Fact]
        public void Update_WhenRotateToolIsInactive_HidesRotationRings() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Translate);

            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial);
            for (int childIndex = 0; childIndex < gizmoRoot.Children.Count; childIndex++) {
                gizmoRoot.Children[childIndex].Enabled = true;
            }
            gizmoRoot.AddComponent(new TransformRotationGizmoFollowComponent(sceneCamera, gizmoRoot, normalMaterial, new TestRuntimeMaterial()));

            EditorEntity selectedEntity = new EditorEntity();
            EditorSelectionService.SetSelectedEntity(selectedEntity);

            UpdateFollowComponent(gizmoRoot);

            for (int childIndex = 0; childIndex < gizmoRoot.Children.Count; childIndex++) {
                Assert.False(gizmoRoot.Children[childIndex].Enabled);
            }
        }

        /// <summary>
        /// Ensures drag-time updates keep the existing gizmo scale even when camera distance changes.
        /// </summary>
        [Fact]
        public void Update_WhileDragging_PreservesExistingScaleUntilDragEnds() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Rotate);

            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial);
            gizmoRoot.AddComponent(new TransformRotationGizmoFollowComponent(sceneCamera, gizmoRoot, normalMaterial, new TestRuntimeMaterial()));

            EditorEntity selectedEntity = new EditorEntity();
            selectedEntity.Position = new float3(0f, 0f, 0f);
            EditorSelectionService.SetSelectedEntity(selectedEntity);

            UpdateFollowComponent(gizmoRoot);
            float initialScale = gizmoRoot.Scale.X;

            sceneCamera.Parent.Position = new float3(0f, 2f, -20f);
            EditorGizmoDragService.BeginDrag(sceneCamera, selectedEntity);
            UpdateFollowComponent(gizmoRoot);
            float dragScale = gizmoRoot.Scale.X;

            EditorGizmoDragService.EndDrag(sceneCamera);
            UpdateFollowComponent(gizmoRoot);
            float releasedScale = gizmoRoot.Scale.X;

            Assert.Equal(initialScale, dragScale);
            Assert.True(releasedScale > initialScale);
        }

        /// <summary>
        /// Initializes a fresh core with an object manager for entity-based tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, null, null);
        }

        /// <summary>
        /// Creates a scene camera entity with the supplied world position.
        /// </summary>
        /// <param name="cameraPosition">World-space camera position.</param>
        /// <returns>Configured scene camera component.</returns>
        CameraComponent CreateSceneCamera(float3 cameraPosition) {
            EditorEntity cameraEntity = new EditorEntity();
            cameraEntity.InternalEntity = true;
            cameraEntity.Position = cameraPosition;

            CameraComponent sceneCamera = new CameraComponent();
            sceneCamera.Viewport = new float4(0f, 0f, 1280f, 720f);
            cameraEntity.AddComponent(sceneCamera);
            Core.Instance.ObjectManager.Cameras.Clear();
            CameraUnderTest = sceneCamera;
            return sceneCamera;
        }

        /// <summary>
        /// Creates a rotation gizmo root with three ring children for follow-component tests.
        /// </summary>
        /// <param name="material">Material assigned to the ring meshes.</param>
        /// <returns>Configured rotation gizmo root entity.</returns>
        EditorEntity CreateGizmoRoot(RuntimeMaterial material) {
            EditorEntity gizmoRoot = new EditorEntity();
            gizmoRoot.InternalEntity = true;
            gizmoRoot.LayerMask = EditorLayerMasks.SceneGizmo;
            gizmoRoot.Name = "Transform Rotation Gizmo";

            gizmoRoot.AddChild(CreateRingEntity("Transform Rotation Gizmo X", CreateXAxisOrientation(), material));
            gizmoRoot.AddChild(CreateRingEntity("Transform Rotation Gizmo Y", float4.Identity, material));
            gizmoRoot.AddChild(CreateRingEntity("Transform Rotation Gizmo Z", CreateZAxisOrientation(), material));
            return gizmoRoot;
        }

        /// <summary>
        /// Creates one ring entity with a handle descriptor and mesh.
        /// </summary>
        /// <param name="name">Ring entity name.</param>
        /// <param name="orientation">Ring orientation relative to the gizmo root.</param>
        /// <param name="material">Material assigned to the ring mesh.</param>
        /// <returns>Configured ring entity.</returns>
        EditorEntity CreateRingEntity(string name, float4 orientation, RuntimeMaterial material) {
            EditorEntity ringEntity = new EditorEntity();
            ringEntity.Name = name;
            ringEntity.InternalEntity = true;
            ringEntity.LayerMask = EditorLayerMasks.SceneGizmo;
            ringEntity.Enabled = false;
            ringEntity.Scale = float3.Zero;
            ringEntity.Orientation = orientation;
            ringEntity.AddComponent(new TransformGizmoHandleComponent(new float3(0f, 1f, 0f)));

            MeshComponent meshComponent = new MeshComponent();
            meshComponent.Model = new TestRuntimeModel();
            meshComponent.Material = material;
            ringEntity.AddComponent(meshComponent);
            return ringEntity;
        }

        /// <summary>
        /// Updates the follow component attached to a gizmo root.
        /// </summary>
        /// <param name="gizmoRoot">Gizmo root to update.</param>
        void UpdateFollowComponent(EditorEntity gizmoRoot) {
            if (gizmoRoot == null) {
                throw new ArgumentNullException(nameof(gizmoRoot));
            }

            for (int componentIndex = 0; componentIndex < gizmoRoot.Components.Count; componentIndex++) {
                if (gizmoRoot.Components[componentIndex] is TransformRotationGizmoFollowComponent followComponent) {
                    followComponent.Update();
                    return;
                }
            }

            throw new InvalidOperationException("Expected a rotation-gizmo follow component on the gizmo root.");
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

            throw new InvalidOperationException("Expected a mesh component on the ring entity.");
        }

        /// <summary>
        /// Creates the quaternion that maps a local Y-normal ring into an X-normal ring.
        /// </summary>
        /// <returns>Quaternion rotating +Y to +X.</returns>
        float4 CreateXAxisOrientation() {
            float3 zAxis = new float3(0f, 0f, 1f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref zAxis, (float)(-Math.PI * 0.5), out orientation);
            return orientation;
        }

        /// <summary>
        /// Creates the quaternion that maps a local Y-normal ring into a Z-normal ring.
        /// </summary>
        /// <returns>Quaternion rotating +Y to +Z.</returns>
        float4 CreateZAxisOrientation() {
            float3 xAxis = new float3(1f, 0f, 0f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref xAxis, (float)(Math.PI * 0.5), out orientation);
            return orientation;
        }
    }
}
