using helengine;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies rotation-gizmo follow visibility, scale, highlight behavior, and snap-preview state.
    /// </summary>
    public class TransformRotationGizmoFollowComponentTests : IDisposable {
        /// <summary>
        /// Tolerance used for floating-point comparisons.
        /// </summary>
        const float FloatTolerance = 0.001f;
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
            TestInputManager input = InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Rotate);

            TestRenderManager3D render3D = new TestRenderManager3D();
            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            RuntimeMaterial highlightMaterial = new TestRuntimeMaterial();
            EditorEntity previewEntity = CreatePreviewEntity(new TestRuntimeMaterial());
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial, previewEntity);
            EditorEntity hoveredRing = (EditorEntity)gizmoRoot.Children[1];
            gizmoRoot.AddComponent(new TransformRotationGizmoFollowComponent(sceneCamera, render3D, gizmoRoot, normalMaterial, highlightMaterial, previewEntity));

            EditorEntity selectedEntity = new EditorEntity();
            selectedEntity.Position = new float3(3f, 4f, 5f);
            EditorSelectionService.SetSelectedEntity(selectedEntity);
            EditorGizmoHoverService.SetHoveredHandle(hoveredRing);
            input.SetKeyboardState(new KeyboardState());
            input.EarlyUpdate();

            UpdateFollowComponent(gizmoRoot);

            Assert.Equal(selectedEntity.Position, gizmoRoot.Position);
            Assert.True(gizmoRoot.Scale.X > 0f);
            Assert.Equal(gizmoRoot.Scale.X, gizmoRoot.Scale.Y);
            Assert.Equal(gizmoRoot.Scale.X, gizmoRoot.Scale.Z);

            for (int childIndex = 0; childIndex < 3; childIndex++) {
                Assert.True(gizmoRoot.Children[childIndex].Enabled);
            }

            Assert.Same(normalMaterial, FindMeshComponent(gizmoRoot.Children[0]).Material);
            Assert.Same(highlightMaterial, FindMeshComponent(gizmoRoot.Children[1]).Material);
            Assert.Same(normalMaterial, FindMeshComponent(gizmoRoot.Children[2]).Material);
            Assert.False(previewEntity.Enabled);
        }

        /// <summary>
        /// Ensures the follow component hides the rotation rings when the viewport is not in rotate mode.
        /// </summary>
        [Fact]
        public void Update_WhenRotateToolIsInactive_HidesRotationRings() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Translate);

            TestRenderManager3D render3D = new TestRenderManager3D();
            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            EditorEntity previewEntity = CreatePreviewEntity(new TestRuntimeMaterial());
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial, previewEntity);
            for (int childIndex = 0; childIndex < 3; childIndex++) {
                gizmoRoot.Children[childIndex].Enabled = true;
            }
            previewEntity.Enabled = true;
            gizmoRoot.AddComponent(new TransformRotationGizmoFollowComponent(sceneCamera, render3D, gizmoRoot, normalMaterial, new TestRuntimeMaterial(), previewEntity));

            EditorEntity selectedEntity = new EditorEntity();
            EditorSelectionService.SetSelectedEntity(selectedEntity);

            UpdateFollowComponent(gizmoRoot);

            for (int childIndex = 0; childIndex < 3; childIndex++) {
                Assert.False(gizmoRoot.Children[childIndex].Enabled);
            }

            Assert.False(previewEntity.Enabled);
        }

        /// <summary>
        /// Ensures drag-time updates keep the existing gizmo scale even when camera distance changes.
        /// </summary>
        [Fact]
        public void Update_WhileDragging_PreservesExistingScaleUntilDragEnds() {
            TestInputManager input = InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Rotate);

            TestRenderManager3D render3D = new TestRenderManager3D();
            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            EditorEntity previewEntity = CreatePreviewEntity(new TestRuntimeMaterial());
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial, previewEntity);
            gizmoRoot.AddComponent(new TransformRotationGizmoFollowComponent(sceneCamera, render3D, gizmoRoot, normalMaterial, new TestRuntimeMaterial(), previewEntity));

            EditorEntity selectedEntity = new EditorEntity();
            selectedEntity.Position = new float3(0f, 0f, 0f);
            EditorSelectionService.SetSelectedEntity(selectedEntity);
            input.SetKeyboardState(new KeyboardState());
            input.EarlyUpdate();

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
        /// Ensures holding control shows the reusable rotation preview, aligns it to the hovered ring, and caches the built preview model.
        /// </summary>
        [Fact]
        public void Update_WhenControlSnapIsActive_ShowsPreviewAndBuildsOneCachedModel() {
            TestInputManager input = InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Rotate);

            TestRenderManager3D render3D = new TestRenderManager3D();
            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            RuntimeMaterial highlightMaterial = new TestRuntimeMaterial();
            EditorEntity previewEntity = CreatePreviewEntity(new TestRuntimeMaterial());
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial, previewEntity);
            EditorEntity hoveredRing = (EditorEntity)gizmoRoot.Children[1];
            gizmoRoot.AddComponent(new TransformRotationGizmoFollowComponent(sceneCamera, render3D, gizmoRoot, normalMaterial, highlightMaterial, previewEntity));

            EditorSelectionService.SetSelectedEntity(new EditorEntity());
            EditorGizmoHoverService.SetHoveredHandle(hoveredRing);
            input.SetKeyboardState(new KeyboardState(Keys.LeftControl));
            input.EarlyUpdate();

            UpdateFollowComponent(gizmoRoot);
            UpdateFollowComponent(gizmoRoot);

            Assert.True(previewEntity.Enabled);
            Assert.NotNull(FindMeshComponent(previewEntity).Model);
            Assert.Single(render3D.BuiltModelAssets);

            ModelAsset previewModelAsset = render3D.BuiltModelAssets[0];
            double expectedSnapValue = TransformGizmoSnapSettingsService.GetSnapValue(EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap1);
            for (int texCoordIndex = 0; texCoordIndex < previewModelAsset.TexCoords.Length; texCoordIndex++) {
                Assert.InRange(Math.Abs(previewModelAsset.TexCoords[texCoordIndex].X - (float)expectedSnapValue), 0f, FloatTolerance);
            }

            float3 previewNormal = float4.RotateVector(new float3(0f, 0f, 1f), previewEntity.Orientation);
            Assert.InRange(Math.Abs(previewNormal.X - 0f), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(previewNormal.Y - 1f), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(previewNormal.Z - 0f), 0f, FloatTolerance);
        }

        /// <summary>
        /// Ensures the rotation preview stays hidden when no snap modifier is active.
        /// </summary>
        [Fact]
        public void Update_WhenNoSnapModifierIsActive_HidesPreview() {
            TestInputManager input = InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Rotate);

            TestRenderManager3D render3D = new TestRenderManager3D();
            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            RuntimeMaterial highlightMaterial = new TestRuntimeMaterial();
            EditorEntity previewEntity = CreatePreviewEntity(new TestRuntimeMaterial());
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial, previewEntity);
            gizmoRoot.AddComponent(new TransformRotationGizmoFollowComponent(sceneCamera, render3D, gizmoRoot, normalMaterial, highlightMaterial, previewEntity));

            EditorSelectionService.SetSelectedEntity(new EditorEntity());
            EditorGizmoHoverService.SetHoveredHandle(gizmoRoot.Children[0]);
            input.SetKeyboardState(new KeyboardState());
            input.EarlyUpdate();

            UpdateFollowComponent(gizmoRoot);

            Assert.False(previewEntity.Enabled);
            Assert.Empty(render3D.BuiltModelAssets);
        }

        /// <summary>
        /// Initializes a fresh core with a configurable input manager for entity-based tests.
        /// </summary>
        /// <returns>Input manager used by the current test.</returns>
        TestInputManager InitializeCore() {
            Core core = new Core();
            var input = new TestInputManager();
            core.Initialize(null, null, input);
            return input;
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
        /// Creates a rotation gizmo root with three ring children and one preview child for follow-component tests.
        /// </summary>
        /// <param name="material">Material assigned to the ring meshes.</param>
        /// <param name="previewEntity">Reusable preview entity attached as the final child.</param>
        /// <returns>Configured rotation gizmo root entity.</returns>
        EditorEntity CreateGizmoRoot(RuntimeMaterial material, EditorEntity previewEntity) {
            EditorEntity gizmoRoot = new EditorEntity();
            gizmoRoot.InternalEntity = true;
            gizmoRoot.LayerMask = EditorLayerMasks.SceneGizmo;
            gizmoRoot.Name = "Transform Rotation Gizmo";

            gizmoRoot.AddChild(CreateRingEntity("Transform Rotation Gizmo X", CreateXAxisOrientation(), material));
            gizmoRoot.AddChild(CreateRingEntity("Transform Rotation Gizmo Y", float4.Identity, material));
            gizmoRoot.AddChild(CreateRingEntity("Transform Rotation Gizmo Z", CreateZAxisOrientation(), material));
            gizmoRoot.AddChild(previewEntity);
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
        /// Creates the reusable preview entity attached to the rotation gizmo root.
        /// </summary>
        /// <param name="material">Material assigned to the preview mesh.</param>
        /// <returns>Configured preview entity.</returns>
        EditorEntity CreatePreviewEntity(RuntimeMaterial material) {
            var previewEntity = new EditorEntity {
                Name = "Transform Rotation Gizmo Snap Preview",
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneGizmo,
                Enabled = false
            };
            MeshComponent previewMesh = new MeshComponent();
            previewMesh.Material = material;
            previewEntity.AddComponent(previewMesh);
            return previewEntity;
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
