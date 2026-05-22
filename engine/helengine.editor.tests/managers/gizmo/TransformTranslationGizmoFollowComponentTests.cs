using helengine;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies translation-gizmo follow behavior for the reusable snap-preview grid.
    /// </summary>
    public class TransformTranslationGizmoFollowComponentTests : IDisposable {
        /// <summary>
        /// Tolerance used for floating-point comparisons.
        /// </summary>
        const float FloatTolerance = 0.001f;
        /// <summary>
        /// Camera created for the current test so shared tool state can be cleaned up.
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
        /// Ensures holding control shows the reusable snap-preview grid and sizes it from the active translation snap value.
        /// </summary>
        [Fact]
        public void Update_WhenControlSnapIsActive_ShowsSnapPreviewWithSnapSizedWorldScale() {
            TestInputBackend input = InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Translate);

            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            RuntimeMaterial highlightMaterial = new TestRuntimeMaterial();
            EditorEntity previewEntity = CreatePreviewEntity(CreateGridPreviewTestMaterial());
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial, normalMaterial, previewEntity);
            gizmoRoot.AddComponent(new TransformTranslationGizmoFollowComponent(sceneCamera, gizmoRoot, normalMaterial, highlightMaterial, previewEntity));

            EditorEntity selectedEntity = new EditorEntity();
            EditorSelectionService.SetSelectedEntity(selectedEntity);
            EditorGizmoHoverService.SetHoveredHandle(gizmoRoot.Children[0]);

            input.SetKeyboardState(new KeyboardState(Keys.LeftControl));
            input.EarlyUpdate();

            UpdateFollowComponent(gizmoRoot);

            float expectedSnapScale = (float)TransformGizmoSnapSettingsService.GetSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1);
            Assert.True(previewEntity.Enabled);
            Assert.InRange(Math.Abs(previewEntity.Scale.X - expectedSnapScale), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(previewEntity.Scale.Y - expectedSnapScale), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(previewEntity.Scale.Z - expectedSnapScale), 0f, FloatTolerance);
        }

        /// <summary>
        /// Ensures the snap-preview grid stays hidden when no snap modifier is active.
        /// </summary>
        [Fact]
        public void Update_WhenNoSnapModifierIsActive_HidesSnapPreview() {
            TestInputBackend input = InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Translate);

            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            RuntimeMaterial highlightMaterial = new TestRuntimeMaterial();
            EditorEntity previewEntity = CreatePreviewEntity(new TestRuntimeMaterial());
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial, normalMaterial, previewEntity);
            gizmoRoot.AddComponent(new TransformTranslationGizmoFollowComponent(sceneCamera, gizmoRoot, normalMaterial, highlightMaterial, previewEntity));

            EditorEntity selectedEntity = new EditorEntity();
            EditorSelectionService.SetSelectedEntity(selectedEntity);
            EditorGizmoHoverService.SetHoveredHandle(gizmoRoot.Children[0]);

            input.SetKeyboardState(new KeyboardState());
            input.EarlyUpdate();

            UpdateFollowComponent(gizmoRoot);

            Assert.False(previewEntity.Enabled);
        }

        /// <summary>
        /// Ensures viewport-owned 2D selections anchor the translation gizmo at the presented bounds center so the gizmo appears on the visible element instead of the authored top-left origin.
        /// </summary>
        [Fact]
        public void Update_WhenViewportOwnedSpriteIsSelected_PositionsTranslationGizmoAtPresentedBoundsCenter() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Translate);

            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            RuntimeMaterial highlightMaterial = new TestRuntimeMaterial();
            EditorEntity previewEntity = CreatePreviewEntity(new TestRuntimeMaterial());
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial, normalMaterial, previewEntity);
            gizmoRoot.AddComponent(new TransformTranslationGizmoFollowComponent(sceneCamera, gizmoRoot, normalMaterial, highlightMaterial, previewEntity));

            Entity selectedEntity = CreateViewportOwnedSpriteEntity(new float3(100f, 200f, 35f), new int2(64, 32));
            EditorSelectionService.SetSelectedEntity(selectedEntity);

            UpdateFollowComponent(gizmoRoot);

            AssertVectorEquals(new float3(132f, -216f, 35f), gizmoRoot.Position);
        }

        /// <summary>
        /// Ensures drag-time updates preserve the current handle facing even when the translated entity crosses into a different snapped yaw sector.
        /// </summary>
        [Fact]
        public void Update_WhileTranslationDragIsActive_PreservesHandleOrientationUntilDragEnds() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Translate);

            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            RuntimeMaterial highlightMaterial = new TestRuntimeMaterial();
            EditorEntity previewEntity = CreatePreviewEntity(new TestRuntimeMaterial());
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial, normalMaterial, previewEntity);
            gizmoRoot.AddComponent(new TransformTranslationGizmoFollowComponent(sceneCamera, gizmoRoot, normalMaterial, highlightMaterial, previewEntity));

            EditorEntity selectedEntity = new EditorEntity();
            selectedEntity.Position = new float3(0f, 0f, 0f);
            EditorSelectionService.SetSelectedEntity(selectedEntity);
            UpdateFollowComponent(gizmoRoot);

            EditorEntity xHandle = (EditorEntity)gizmoRoot.Children[0];
            float4 initialOrientation = xHandle.Orientation;
            float3 initialLocalOffset = xHandle.Position - gizmoRoot.Position;

            EditorGizmoDragService.BeginDrag(sceneCamera, selectedEntity);
            selectedEntity.Position = new float3(-8f, 0f, 0f);
            UpdateFollowComponent(gizmoRoot);

            float4 dragOrientation = xHandle.Orientation;
            float3 dragLocalOffset = xHandle.Position - gizmoRoot.Position;

            EditorGizmoDragService.EndDrag(sceneCamera);
            UpdateFollowComponent(gizmoRoot);

            AssertVectorEquals(selectedEntity.Position, gizmoRoot.Position);
            AssertQuaternionEquals(initialOrientation, dragOrientation);
            AssertVectorEquals(initialLocalOffset, dragLocalOffset);
            Assert.False(AreQuaternionsEqual(initialOrientation, xHandle.Orientation));
        }

        /// <summary>
        /// Ensures plane handles use the dedicated plane materials while axis handles keep the axis materials.
        /// </summary>
        [Fact]
        public void Update_WhenPlaneHandleIsHovered_UsesPlaneHighlightMaterialWithoutChangingAxisMaterials() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Translate);

            RuntimeMaterial axisNormalMaterial = new TestRuntimeMaterial();
            RuntimeMaterial axisHighlightMaterial = new TestRuntimeMaterial();
            RuntimeMaterial planeNormalMaterial = new TestRuntimeMaterial();
            RuntimeMaterial planeHighlightMaterial = new TestRuntimeMaterial();
            EditorEntity previewEntity = CreatePreviewEntity(new TestRuntimeMaterial());
            EditorEntity gizmoRoot = CreateGizmoRoot(axisNormalMaterial, planeNormalMaterial, previewEntity);
            gizmoRoot.AddComponent(new TransformTranslationGizmoFollowComponent(
                sceneCamera,
                gizmoRoot,
                axisNormalMaterial,
                axisHighlightMaterial,
                planeNormalMaterial,
                planeHighlightMaterial,
                previewEntity));

            EditorEntity selectedEntity = new EditorEntity();
            EditorSelectionService.SetSelectedEntity(selectedEntity);
            EditorEntity planeHandle = (EditorEntity)gizmoRoot.Children[1];
            EditorGizmoHoverService.SetHoveredHandle(planeHandle);

            UpdateFollowComponent(gizmoRoot);

            EditorEntity axisHandle = (EditorEntity)gizmoRoot.Children[0];
            MeshComponent axisMesh = FindMeshComponent((Entity)axisHandle.Children[0]);
            MeshComponent planeMesh = FindMeshComponent(planeHandle);

            Assert.Same(axisNormalMaterial, axisMesh.Material);
            Assert.Same(planeHighlightMaterial, planeMesh.Material);
        }

        /// <summary>
        /// Ensures single-axis snap previews enable focused fading while plane-handle previews keep the full grid visible.
        /// </summary>
        [Fact]
        public void Update_WhenSingleAxisSnapPreviewIsShown_ConfiguresFocusedFadeAndPlanePreviewClearsIt() {
            TestInputBackend input = InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Translate);

            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            RuntimeMaterial highlightMaterial = new TestRuntimeMaterial();
            RuntimeMaterial previewMaterial = CreateGridPreviewTestMaterial();
            EditorEntity previewEntity = CreatePreviewEntity(previewMaterial);
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial, normalMaterial, previewEntity);
            gizmoRoot.AddComponent(new TransformTranslationGizmoFollowComponent(sceneCamera, gizmoRoot, normalMaterial, highlightMaterial, previewEntity));

            EditorEntity selectedEntity = new EditorEntity();
            EditorSelectionService.SetSelectedEntity(selectedEntity);
            input.SetKeyboardState(new KeyboardState(Keys.LeftControl));
            input.EarlyUpdate();

            EditorGizmoHoverService.SetHoveredHandle(gizmoRoot.Children[0]);
            UpdateFollowComponent(gizmoRoot);

            ShaderRuntimeMaterial previewShaderMaterial = Assert.IsAssignableFrom<ShaderRuntimeMaterial>(previewMaterial);
            byte[] axisPreviewParameters = previewShaderMaterial.Properties.GetConstantBufferData(0);
            Assert.NotNull(axisPreviewParameters);
            Assert.Equal(1f, ReadSingle(axisPreviewParameters, 0));
            Assert.True(ReadSingle(axisPreviewParameters, 4) > 0f);

            EditorGizmoHoverService.SetHoveredHandle(gizmoRoot.Children[1]);
            UpdateFollowComponent(gizmoRoot);

            byte[] planePreviewParameters = previewShaderMaterial.Properties.GetConstantBufferData(0);
            Assert.NotNull(planePreviewParameters);
            Assert.Equal(0f, ReadSingle(planePreviewParameters, 0));
        }

        /// <summary>
        /// Ensures plane handles keep their authored placement ratio within the gizmo when camera-distance scaling changes.
        /// </summary>
        [Fact]
        public void Update_WhenCameraDistanceChanges_RepositionsPlaneHandleWithTheScaledGizmo() {
            InitializeCore();
            CameraComponent sceneCamera = CreateSceneCamera(new float3(0f, 2f, -8f));
            EditorViewportToolService.SetToolMode(sceneCamera, EditorViewportToolMode.Translate);

            RuntimeMaterial normalMaterial = new TestRuntimeMaterial();
            RuntimeMaterial highlightMaterial = new TestRuntimeMaterial();
            EditorEntity previewEntity = CreatePreviewEntity(new TestRuntimeMaterial());
            EditorEntity gizmoRoot = CreateGizmoRoot(normalMaterial, normalMaterial, previewEntity);
            gizmoRoot.AddComponent(new TransformTranslationGizmoFollowComponent(sceneCamera, gizmoRoot, normalMaterial, highlightMaterial, previewEntity));

            EditorEntity selectedEntity = new EditorEntity();
            EditorSelectionService.SetSelectedEntity(selectedEntity);

            UpdateFollowComponent(gizmoRoot);

            EditorEntity planeHandle = (EditorEntity)gizmoRoot.Children[1];
            float initialRootScale = gizmoRoot.Scale.X;
            float3 initialPlaneLocalPosition = planeHandle.LocalPosition;

            sceneCamera.Parent.Position = new float3(0f, 2f, -20f);
            UpdateFollowComponent(gizmoRoot);

            float scaleRatio = gizmoRoot.Scale.X / initialRootScale;
            float3 expectedPlaneLocalPosition = initialPlaneLocalPosition * scaleRatio;

            Assert.True(gizmoRoot.Scale.X > initialRootScale);
            AssertVectorEquals(expectedPlaneLocalPosition, planeHandle.LocalPosition);
        }

        /// <summary>
        /// Initializes a fresh core with a configurable input system for entity-based tests.
        /// </summary>
        /// <returns>Input manager used by the current test.</returns>
        TestInputBackend InitializeCore() {
            Core core = new Core();
            var input = new TestInputBackend();
            core.Initialize(null, null, input, new PlatformInfo("test", "test-version"));
            return input;
        }

        /// <summary>
        /// Creates a scene camera entity with the supplied world position.
        /// </summary>
        /// <param name="cameraPosition">World-space camera position.</param>
        /// <returns>Configured scene camera component.</returns>
        CameraComponent CreateSceneCamera(float3 cameraPosition) {
            var cameraEntity = new EditorEntity {
                InternalEntity = true,
                Position = cameraPosition
            };

            var sceneCamera = new CameraComponent {
                Viewport = new float4(0f, 0f, 1280f, 720f)
            };
            cameraEntity.AddComponent(sceneCamera);
            Core.Instance.ObjectManager.Cameras.Clear();
            CameraUnderTest = sceneCamera;
            return sceneCamera;
        }

        /// <summary>
        /// Creates a translation gizmo root with one axis handle and one reusable preview child.
        /// </summary>
        /// <param name="axisMaterial">Material assigned to the axis handle meshes.</param>
        /// <param name="planeMaterial">Material assigned to the plane handle mesh.</param>
        /// <param name="previewEntity">Reusable preview entity owned by the gizmo root.</param>
        /// <returns>Configured translation gizmo root.</returns>
        EditorEntity CreateGizmoRoot(RuntimeMaterial axisMaterial, RuntimeMaterial planeMaterial, EditorEntity previewEntity) {
            var gizmoRoot = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneGizmo,
                Name = "Transform Translation Gizmo"
            };

            gizmoRoot.AddChild(CreateAxisEntity("Transform Gizmo X", CreateXAxisOrientation(), axisMaterial));
            gizmoRoot.AddChild(CreatePlaneEntity("Transform Gizmo XZ Plane", planeMaterial));
            gizmoRoot.AddChild(previewEntity);
            return gizmoRoot;
        }

        /// <summary>
        /// Creates one axis entity with a shaft mesh and a tip mesh.
        /// </summary>
        /// <param name="name">Axis entity name.</param>
        /// <param name="orientation">Axis orientation relative to the gizmo root.</param>
        /// <param name="material">Material assigned to the axis meshes.</param>
        /// <returns>Configured axis entity.</returns>
        EditorEntity CreateAxisEntity(string name, float4 orientation, RuntimeMaterial material) {
            var axisEntity = new EditorEntity {
                Name = name,
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneGizmo,
                Enabled = false,
                Scale = float3.Zero,
                Orientation = orientation
            };
            axisEntity.AddComponent(new TransformGizmoHandleComponent(new float3(0f, 1f, 0f)));

            var shaftEntity = new EditorEntity {
                Name = string.Concat(name, " Shaft"),
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneGizmo,
                Enabled = false,
                Scale = float3.Zero
            };
            MeshComponent shaftMesh = new MeshComponent();
            shaftMesh.Model = new TestRuntimeModel();
            shaftMesh.Material = material;
            shaftEntity.AddComponent(shaftMesh);
            axisEntity.AddChild(shaftEntity);

            var tipEntity = new EditorEntity {
                Name = string.Concat(name, " Tip"),
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneGizmo,
                Enabled = false,
                Scale = float3.Zero,
                Position = new float3(0f, TransformTranslationGizmoFactory.ShaftLength, 0f)
            };
            MeshComponent tipMesh = new MeshComponent();
            tipMesh.Model = new TestRuntimeModel();
            tipMesh.Material = material;
            tipEntity.AddComponent(tipMesh);
            axisEntity.AddChild(tipEntity);

            return axisEntity;
        }

        /// <summary>
        /// Creates one plane handle entity with a direct mesh component.
        /// </summary>
        /// <param name="name">Plane handle entity name.</param>
        /// <param name="material">Material assigned to the plane handle mesh.</param>
        /// <returns>Configured plane handle entity.</returns>
        EditorEntity CreatePlaneEntity(string name, RuntimeMaterial material) {
            var planeEntity = new EditorEntity {
                Name = name,
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneGizmo,
                Enabled = false,
                Scale = float3.Zero,
                Position = new float3(TransformTranslationGizmoFactory.PlaneInset, 0f, TransformTranslationGizmoFactory.PlaneInset),
                Orientation = CreateXzPlaneOrientation()
            };
            planeEntity.AddComponent(new TransformGizmoHandleComponent(new float3(1f, 0f, 0f), new float3(0f, 1f, 0f)));

            MeshComponent planeMesh = new MeshComponent();
            planeMesh.Model = new TestRuntimeModel();
            planeMesh.Material = material;
            planeEntity.AddComponent(planeMesh);
            return planeEntity;
        }

        /// <summary>
        /// Creates the reusable preview entity attached to the translation gizmo root.
        /// </summary>
        /// <param name="material">Material assigned to the preview mesh.</param>
        /// <returns>Configured preview entity.</returns>
        EditorEntity CreatePreviewEntity(RuntimeMaterial material) {
            var previewEntity = new EditorEntity {
                Name = "Transform Gizmo Snap Preview",
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneGizmo,
                Enabled = false
            };
            MeshComponent previewMesh = new MeshComponent();
            previewMesh.Model = new TestRuntimeModel();
            previewMesh.Material = material;
            previewEntity.AddComponent(previewMesh);
            return previewEntity;
        }

        /// <summary>
        /// Creates a preview material with one constant-buffer binding so tests can inspect snap-preview shader parameters.
        /// </summary>
        /// <returns>Runtime material exposing a <c>PreviewParams</c> constant buffer.</returns>
        RuntimeMaterial CreateGridPreviewTestMaterial() {
            var material = new TestRuntimeMaterial();
            material.SetLayout(new MaterialLayout(
                "EditorTransformGizmoGridPreview",
                "EditorTransformGizmoGridPreview.vs",
                "EditorTransformGizmoGridPreview.ps",
                "default",
                new MaterialRenderState(),
                Array.Empty<MaterialLayoutBinding>(),
                new[] {
                    new MaterialLayoutBinding("PreviewParams", ShaderResourceType.ConstantBuffer, 0, 1, 16)
                },
                Array.Empty<MaterialLayoutBinding>()));
            return material;
        }

        /// <summary>
        /// Finds the first mesh component attached directly to the supplied entity.
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

            throw new InvalidOperationException("Expected a mesh component on the translation gizmo entity.");
        }

        /// <summary>
        /// Updates the translation follow component attached to the supplied gizmo root.
        /// </summary>
        /// <param name="gizmoRoot">Translation gizmo root to update.</param>
        void UpdateFollowComponent(EditorEntity gizmoRoot) {
            if (gizmoRoot == null) {
                throw new ArgumentNullException(nameof(gizmoRoot));
            }

            for (int componentIndex = 0; componentIndex < gizmoRoot.Components.Count; componentIndex++) {
                if (gizmoRoot.Components[componentIndex] is TransformTranslationGizmoFollowComponent followComponent) {
                    followComponent.Update();
                    return;
                }
            }

            throw new InvalidOperationException("Expected a translation-gizmo follow component on the gizmo root.");
        }

        /// <summary>
        /// Asserts that two vectors match within the standard floating-point tolerance for gizmo tests.
        /// </summary>
        /// <param name="expected">Expected vector value.</param>
        /// <param name="actual">Actual vector value.</param>
        void AssertVectorEquals(float3 expected, float3 actual) {
            Assert.InRange(Math.Abs(expected.X - actual.X), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(expected.Y - actual.Y), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(expected.Z - actual.Z), 0f, FloatTolerance);
        }

        /// <summary>
        /// Asserts that two quaternions match within the standard floating-point tolerance for gizmo tests.
        /// </summary>
        /// <param name="expected">Expected quaternion value.</param>
        /// <param name="actual">Actual quaternion value.</param>
        void AssertQuaternionEquals(float4 expected, float4 actual) {
            Assert.InRange(Math.Abs(expected.X - actual.X), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(expected.Y - actual.Y), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(expected.Z - actual.Z), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(expected.W - actual.W), 0f, FloatTolerance);
        }

        /// <summary>
        /// Determines whether two quaternions match within the standard floating-point tolerance for gizmo tests.
        /// </summary>
        /// <param name="left">First quaternion.</param>
        /// <param name="right">Second quaternion.</param>
        /// <returns>True when all components are within tolerance; otherwise false.</returns>
        bool AreQuaternionsEqual(float4 left, float4 right) {
            return Math.Abs(left.X - right.X) <= FloatTolerance &&
                   Math.Abs(left.Y - right.Y) <= FloatTolerance &&
                   Math.Abs(left.Z - right.Z) <= FloatTolerance &&
                   Math.Abs(left.W - right.W) <= FloatTolerance;
        }

        /// <summary>
        /// Reads one single-precision value from packed constant-buffer bytes.
        /// </summary>
        /// <param name="data">Packed constant-buffer payload.</param>
        /// <param name="offset">Byte offset of the value to read.</param>
        /// <returns>Decoded single-precision value.</returns>
        float ReadSingle(byte[] data, int offset) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            return BitConverter.ToSingle(data, offset);
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
        /// Creates the quaternion that rotates a +Z plane mesh into the XZ plane.
        /// </summary>
        /// <returns>Quaternion rotating the plane into XZ orientation.</returns>
        float4 CreateXzPlaneOrientation() {
            float3 xAxis = new float3(1f, 0f, 0f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref xAxis, (float)(Math.PI * 0.5), out orientation);
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

