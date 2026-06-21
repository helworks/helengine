using helengine;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies scale-gizmo follow visibility, scale, and highlight behavior.
    /// </summary>
    public class TransformScaleGizmoFollowComponentTests : IDisposable {
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
        /// Ensures the follow component enables the handles, positions the root at the selection, and highlights the hovered handle.
        /// </summary>
        [Fact]
        public void Update_WhenScaleToolIsActive_ShowsAndHighlightsScaleHandles() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Scale);

            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            RuntimeMaterial highlightMaterial = new TestRuntimeMaterial();
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial);
            EditorEntity hoveredHandle = (EditorEntity)gizmoRoot.Children[0];
            gizmoRoot.AddComponent(new TransformScaleGizmoFollowComponent(sceneCamera, gizmoRoot, normalMaterial, highlightMaterial));

            EditorEntity selectedEntity = new EditorEntity();
            selectedEntity.Position = new float3(3f, 4f, 5f);
            EditorSelectionService.SetSelectedEntity(selectedEntity);
            EditorGizmoHoverService.SetHoveredHandle(hoveredHandle);

            UpdateFollowComponent(gizmoRoot);

            Assert.Equal(selectedEntity.Position, gizmoRoot.Position);
            Assert.True(gizmoRoot.Scale.X > 0f);
            Assert.Equal(gizmoRoot.Scale.X, gizmoRoot.Scale.Y);
            Assert.Equal(gizmoRoot.Scale.X, gizmoRoot.Scale.Z);

            for (int childIndex = 0; childIndex < gizmoRoot.Children.Count; childIndex++) {
                Assert.True(gizmoRoot.Children[childIndex].Enabled);
            }

            Assert.Same(highlightMaterial, Assert.Single(FindMeshComponent(gizmoRoot.Children[0].Children[0]).Materials));
            Assert.Same(normalMaterial, Assert.Single(FindMeshComponent(gizmoRoot.Children[1].Children[0]).Materials));
            Assert.Same(normalMaterial, Assert.Single(FindMeshComponent(gizmoRoot.Children[3]).Materials));
        }

        /// <summary>
        /// Ensures the follow component hides the scale handles when the viewport is not in scale mode.
        /// </summary>
        [Fact]
        public void Update_WhenScaleToolIsInactive_HidesScaleHandles() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Translate);

            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial);
            for (int childIndex = 0; childIndex < gizmoRoot.Children.Count; childIndex++) {
                gizmoRoot.Children[childIndex].Enabled = true;
            }
            gizmoRoot.AddComponent(new TransformScaleGizmoFollowComponent(sceneCamera, gizmoRoot, normalMaterial, new TestRuntimeMaterial()));

            EditorEntity selectedEntity = new EditorEntity();
            EditorSelectionService.SetSelectedEntity(selectedEntity);

            UpdateFollowComponent(gizmoRoot);

            for (int childIndex = 0; childIndex < gizmoRoot.Children.Count; childIndex++) {
                Assert.False(gizmoRoot.Children[childIndex].Enabled);
            }
        }

        /// <summary>
        /// Ensures viewport-owned 2D selections anchor the scale gizmo at the presented bounds center so the handles appear on the visible element.
        /// </summary>
        [Fact]
        public void Update_WhenViewportOwnedSpriteIsSelected_PositionsScaleGizmoAtPresentedBoundsCenter() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Scale);

            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial);
            gizmoRoot.AddComponent(new TransformScaleGizmoFollowComponent(sceneCamera, gizmoRoot, normalMaterial, new TestRuntimeMaterial()));

            Entity selectedEntity = CreateViewportOwnedSpriteEntity(new float3(100f, 200f, 35f), new int2(64, 32));
            EditorSelectionService.SetSelectedEntity(selectedEntity);

            UpdateFollowComponent(gizmoRoot);

            Assert.Equal(new float3(132f, -216f, 35f), gizmoRoot.Position);
        }

        /// <summary>
        /// Ensures drag-time updates keep the existing gizmo scale even when camera distance changes.
        /// </summary>
        [Fact]
        public void Update_WhileDragging_PreservesExistingScaleUntilDragEnds() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Scale);

            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial);
            gizmoRoot.AddComponent(new TransformScaleGizmoFollowComponent(sceneCamera, gizmoRoot, normalMaterial, new TestRuntimeMaterial()));

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
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
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
        /// Creates a scale gizmo root with axis and plane children for follow-component tests.
        /// </summary>
        /// <param name="material">Material assigned to the handle meshes.</param>
        /// <returns>Configured scale gizmo root entity.</returns>
        EditorEntity CreateGizmoRoot(RuntimeMaterial material) {
            EditorEntity gizmoRoot = new EditorEntity();
            gizmoRoot.InternalEntity = true;
            gizmoRoot.LayerMask = EditorLayerMasks.SceneGizmo;
            gizmoRoot.Name = "Transform Scale Gizmo";

            gizmoRoot.AddChild(CreateAxisEntity("Transform Scale Gizmo X", CreateXAxisOrientation(), material));
            gizmoRoot.AddChild(CreateAxisEntity("Transform Scale Gizmo Y", float4.Identity, material));
            gizmoRoot.AddChild(CreateAxisEntity("Transform Scale Gizmo Z", CreateZAxisOrientation(), material));
            gizmoRoot.AddChild(CreatePlaneEntity("Transform Scale Gizmo XY Plane", float4.Identity, material));
            gizmoRoot.AddChild(CreatePlaneEntity("Transform Scale Gizmo XZ Plane", CreateXzPlaneOrientation(), material));
            gizmoRoot.AddChild(CreatePlaneEntity("Transform Scale Gizmo YZ Plane", CreateYzPlaneOrientation(), material));
            return gizmoRoot;
        }

        /// <summary>
        /// Creates one axis entity with a shaft child and a tip child.
        /// </summary>
        /// <param name="name">Axis entity name.</param>
        /// <param name="orientation">Axis orientation relative to the gizmo root.</param>
        /// <param name="material">Material assigned to the axis meshes.</param>
        /// <returns>Configured axis entity.</returns>
        EditorEntity CreateAxisEntity(string name, float4 orientation, RuntimeMaterial material) {
            EditorEntity axisEntity = new EditorEntity();
            axisEntity.Name = name;
            axisEntity.InternalEntity = true;
            axisEntity.LayerMask = EditorLayerMasks.SceneGizmo;
            axisEntity.Enabled = false;
            axisEntity.Scale = float3.Zero;
            axisEntity.Orientation = orientation;
            axisEntity.AddComponent(new TransformGizmoHandleComponent(new float3(0f, 1f, 0f)));

            EditorEntity shaftEntity = new EditorEntity();
            shaftEntity.Name = string.Concat(name, " Shaft");
            shaftEntity.InternalEntity = true;
            shaftEntity.LayerMask = EditorLayerMasks.SceneGizmo;
            shaftEntity.Enabled = false;
            shaftEntity.Scale = float3.Zero;
            MeshComponent shaftMesh = new MeshComponent();
            shaftMesh.Model = new TestRuntimeModel();
            shaftMesh.Materials = new RuntimeMaterial[] { material };
            shaftEntity.AddComponent(shaftMesh);
            axisEntity.AddChild(shaftEntity);

            EditorEntity tipEntity = new EditorEntity();
            tipEntity.Name = string.Concat(name, " Tip");
            tipEntity.InternalEntity = true;
            tipEntity.LayerMask = EditorLayerMasks.SceneGizmo;
            tipEntity.Enabled = false;
            tipEntity.Scale = float3.Zero;
            tipEntity.Position = new float3(0f, TransformScaleGizmoFactory.ShaftLength, 0f);
            MeshComponent tipMesh = new MeshComponent();
            tipMesh.Model = new TestRuntimeModel();
            tipMesh.Materials = new RuntimeMaterial[] { material };
            tipEntity.AddComponent(tipMesh);
            axisEntity.AddChild(tipEntity);
            return axisEntity;
        }

        /// <summary>
        /// Creates one plane handle entity with a single plane mesh.
        /// </summary>
        /// <param name="name">Plane-handle entity name.</param>
        /// <param name="orientation">Plane orientation relative to the gizmo root.</param>
        /// <param name="material">Material assigned to the plane mesh.</param>
        /// <returns>Configured plane-handle entity.</returns>
        EditorEntity CreatePlaneEntity(string name, float4 orientation, RuntimeMaterial material) {
            EditorEntity planeEntity = new EditorEntity();
            planeEntity.Name = name;
            planeEntity.InternalEntity = true;
            planeEntity.LayerMask = EditorLayerMasks.SceneGizmo;
            planeEntity.Enabled = false;
            planeEntity.Scale = float3.Zero;
            planeEntity.Orientation = orientation;
            planeEntity.AddComponent(new TransformGizmoHandleComponent(new float3(1f, 0f, 0f), new float3(0f, 1f, 0f)));

            MeshComponent planeMesh = new MeshComponent();
            planeMesh.Model = new TestRuntimeModel();
            planeMesh.Materials = new RuntimeMaterial[] { material };
            planeEntity.AddComponent(planeMesh);
            return planeEntity;
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
                if (gizmoRoot.Components[componentIndex] is TransformScaleGizmoFollowComponent followComponent) {
                    followComponent.Update();
                    return;
                }
            }

            throw new InvalidOperationException("Expected a scale-gizmo follow component on the gizmo root.");
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
        /// Creates the quaternion that maps +Y geometry into +X direction.
        /// </summary>
        /// <returns>Quaternion rotating +Y to +X.</returns>
        float4 CreateXAxisOrientation() {
            float3 zAxis = new float3(0f, 0f, 1f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref zAxis, (float)(-Math.PI * 0.5), out orientation);
            return orientation;
        }

        /// <summary>
        /// Creates the quaternion that maps +Y geometry into +Z direction.
        /// </summary>
        /// <returns>Quaternion rotating +Y to +Z.</returns>
        float4 CreateZAxisOrientation() {
            float3 xAxis = new float3(1f, 0f, 0f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref xAxis, (float)(Math.PI * 0.5), out orientation);
            return orientation;
        }

        /// <summary>
        /// Creates the plane rotation that maps local XY plane to world XZ plane.
        /// </summary>
        /// <returns>Quaternion rotating local +Y to world +Z.</returns>
        float4 CreateXzPlaneOrientation() {
            float3 xAxis = new float3(1f, 0f, 0f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref xAxis, (float)(Math.PI * 0.5), out orientation);
            return orientation;
        }

        /// <summary>
        /// Creates the plane rotation that maps local XY plane to world YZ plane.
        /// </summary>
        /// <returns>Quaternion rotating local +X to world +Z.</returns>
        float4 CreateYzPlaneOrientation() {
            float3 yAxis = new float3(0f, 1f, 0f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref yAxis, (float)(-Math.PI * 0.5), out orientation);
            return orientation;
        }

        /// <summary>
        /// Creates one viewport-owned sprite entity for presented-space gizmo alignment tests.
        /// </summary>
        /// <param name="localPosition">Stored viewport-local position of the authored sprite entity.</param>
        /// <param name="size">Authored sprite size.</param>
        /// <returns>Viewport-owned authored sprite entity.</returns>
        Entity CreateViewportOwnedSpriteEntity(float3 localPosition, int2 size) {
            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = new int2(1280, 720)
            });

            Entity selectedEntity = new Entity {
                LocalPosition = localPosition
            };
            selectedEntity.InitComponents();
            selectedEntity.InitChildren();
            selectedEntity.AddComponent(new SpriteComponent {
                Size = size
            });
            viewportEntity.AddChild(selectedEntity);
            return selectedEntity;
        }
    }
}
